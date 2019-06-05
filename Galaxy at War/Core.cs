using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BattleTech;
using Harmony;
using Newtonsoft.Json;
using static Logger;
using Random = System.Random;
using BattleTech.UI;
using fastJSON;
using HBS;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

public static class Core
{
    #region Init

    public static void Init(string modDir, string settings)
    {
        var harmony = HarmonyInstance.Create("com.Same.BattleTech.GalaxyAtWar");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        // read settings
        try
        {
            Settings = JsonConvert.DeserializeObject<ModSettings>(settings);
            Settings.modDirectory = modDir;

        }
        catch (Exception)
        {
            Settings = new ModSettings();
        }

        using (var writer = new StreamWriter("Mods\\GalaxyAtWar\\settings.json"))
            writer.Write(JSON.ToNiceJSON(Settings, new JSONParameters{UsingGlobalTypes = true}));
        // blank the logfile
        Clear();
        // PrintObjectFields(Settings, "Settings");
    }

    // logs out all the settings and their values at runtime
    internal static void PrintObjectFields(object obj, string name)
    {
        LogDebug($"[START {name}]");

        var settingsFields = typeof(ModSettings)
            .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        foreach (var field in settingsFields)
        {
            if (field.GetValue(obj) is IEnumerable &&
                !(field.GetValue(obj) is string))
            {
                LogDebug(field.Name);
                foreach (var item in (IEnumerable) field.GetValue(obj))
                {
                    LogDebug("\t" + item);
                }
            }
            else
            {
                LogDebug($"{field.Name,-30}: {field.GetValue(obj)}");
            }
        }

        LogDebug($"[END {name}]");
    }

    #endregion

    internal static ModSettings Settings;
    public static WarStatus WarStatus;
    public static StarSystem SelectedSystem;
    public static List<StarSystem> ChangedSystems = new List<StarSystem>();

    [HarmonyPatch(typeof(SimGameState), "OnDayPassed")]
    public static class SimGameState_OnDayPassed_Patch
    {
        static void Prefix(SimGameState __instance, int timeLapse)
        {
            SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

            // already have a save?

            if (WarStatus == null && sim.CompanyTags.Any(x => x.StartsWith("GalaxyAtWarSave{")))
            {
                LogDebug(">>> Loading");
                SaveHandling.DeserializeWar();
            }

            // first time setup if not
            if (WarStatus == null)
            {
                LogDebug(">>> First-time initialization");
                WarStatus = new WarStatus();
                InitializeAllWarStatus();
            }

            //Is this area needed? When would this be true? 
            if (WarStatus.systems.Count == 0)
            {
                LogDebug("NO SYSTEMS BUT NOT NULL");
                try
                {
                    InitializeAllWarStatus();
                }
                catch (Exception ex)
                {
                    LogDebug("WarStatus.systems.Count == 0");
                    Error(ex);
                }
            }

            if (sim.DayRemainingInQuarter % Settings.WarFrequency != 0)
                 return;

            LogDebug(">>> PROC");

            // Proc effects
            foreach (var warFaction in WarStatus.warFactionTracker)
            {
                try
                {
                    warFaction.attackTargets.Clear();
                    warFaction.defenseTargets.Clear();
                }
                catch // silent drop
                {
                }
            }

            LogDebug("Calculations and System Push");
            Random rand = new Random();
            foreach (var systemStatus in WarStatus.systems)
            {
                CalculateAttackTargets(systemStatus.starSystem);
                CalculateDefenseTargets(systemStatus.starSystem);
                RefreshNeighbors(systemStatus.starSystem);
                RefreshContracts(systemStatus.starSystem);
                LogDebug("Pushing");
                //Add resources from neighboring systems.
                foreach (var neighbor in systemStatus.neighborSystems.Keys)
                {
                    var PushFactor = Settings.APRPush * rand.Next(1, Settings.APRPushRandomizer + 1);
                    systemStatus.influenceTracker[neighbor] += systemStatus.neighborSystems[neighbor] * PushFactor;
                }
            }

            LogDebug("Finished System Push");

            //Attack!
            LogDebug("Attacking Fool");
            foreach (var warFaction in WarStatus.warFactionTracker)
            {
                DivideAttackResources(warFaction);
                AllocateAttackResources(warFaction);
            }

            foreach (var warFaction in WarStatus.warFactionTracker)
            {
                AllocateDefensiveResources(warFaction);
            }

            UpdateInfluenceFromAttacks(sim);

            //foreach (StarSystem starSystem in ChangedSystems)
            //    PostChangeSystemUpdate(starSystem);
            //ChangedSystems.Clear();

            //Increase War Escalation or decay defenses.
            foreach (var warfaction in WarStatus.warFactionTracker)
            {
                if (!warfaction.GainedSystem)
                    warfaction.DaysSinceSystemAttacked += 1;
                else
                {
                    warfaction.DaysSinceSystemAttacked = 0;
                    warfaction.GainedSystem = false;
                }

                if (!warfaction.LostSystem)
                    warfaction.DaysSinceSystemLost += 1;
                else
                {
                    warfaction.DaysSinceSystemLost = 0;
                    warfaction.LostSystem = false;
                }
            }

            //Comstar report on ongoing war.
            if (sim.DayRemainingInQuarter == 30)
            {
                var ReportString = MonthlyWarReport();
                Console.Write(String.Format("0, -10", ReportString));
                GameInstance game = LazySingletonBehavior<UnityGameInstance>.Instance.Game;
                SimGameInterruptManager interruptQueue = (SimGameInterruptManager)AccessTools
                    .Field(typeof(SimGameState), "interruptQueue").GetValue(game.Simulation);
                interruptQueue.QueueGenericPopup_NonImmediate("Comstar Bulletin: Galaxy at War", ReportString, true, null);
                //interruptQueue.QueueGenericPopup_NonImmediate("Comstar Bulletin: Galaxy at War", sim.CurSystem.Name + " taken!" + newowner.ToString() +
                //                                                                                 " conquered from " + oldowner.ToString(), true, null);
                sim.StopPlayMode();
            }
            //GameInstance game = LazySingletonBehavior<UnityGameInstance>.Instance.Game;
            //SimGameInterruptManager interruptQueue = (SimGameInterruptManager)AccessTools.Field(typeof(SimGameState), "interruptQueue").GetValue(game.Simulation);
            //interruptQueue.QueueGenericPopup_NonImmediate("Comstar Bulletin: Galaxy at War", " poop " + " from " + "heaven", true, null);

            //if ((__instance.DayRemainingInQuarter - num <= 0))
            //{
            //    foreach (KeyValuePair<string, string> changes in Fields.thisMonthChanges)
            //    {
            //        StarSystem changedSystem = __instance.StarSystems.Find(x => x.Name.Equals(changes.Key));
            //        if (!Helper.GetFactionName(changedSystem.Owner, __instance.DataManager).Equals(changes.Value))
            //        {
            //            War war = Helper.getWar(changedSystem.Owner);
            //            if (war != null)
            //            {
            //                if (war.attackers.ContainsKey(changedSystem.Owner))
            //                {
            //                    war.monthlyEvents.Add("<color=" + Fields.settings.attackercolor + ">" + Helper.GetFactionName(changedSystem.Owner, __instance.DataManager) + "</color>" + " took " + "<color=" + Fields.settings.planetcolor + ">" + changes.Key + "</color>" + " from " + "<color=" + Fields.settings.defendercolor + ">" + changes.Value + "</color>");
            //                }
            //                else
            //                {
            //                    war.monthlyEvents.Add("<color=" + Fields.settings.defendercolor + ">" + Helper.GetFactionName(changedSystem.Owner, __instance.DataManager) + "</color>" + " took " + "<color=" + Fields.settings.planetcolor + ">" + changes.Key + "</color>" + " from " + "<color=" + Fields.settings.attackercolor + ">" + changes.Value + "</color>");
            //                }
            //            }
            //        }
            //    }

            //Log("===================================================");
            //Log("TESTING ZONE");
            //Log("===================================================");
            ////TESTING ZONE
            //foreach (WarFaction WF in WarStatus.warFactionTracker)
            //{
            //    Log("----------------------------------------------");
            //    Log(WF.faction.ToString());
            //    try
            //    {
            //        //Log("\tAttacked By :");
            //        //foreach (Faction fac in DLT.AttackedBy)
            //        //    Log("\t\t" + fac.ToString());
            //        //Log("\tOwner :" + DLT.);
            //        Log("\tAttack Resources :" + WF.AttackResources.ToString());
            //        Log("\tDefensive Resources :" + WF.DefensiveResources.ToString());
            //        //Log("\tDeath List:");
            //        //foreach (Faction faction in DLT.deathList.Keys)
            //        //{
            //        //    Log("\t\t" + faction.ToString() + ": " + DLT.deathList[faction]);
            //        //}
            //    }
            //    catch (Exception e)
            //    {
            //        Error(e);
            //    }
            //}

            //SaveHandling.SerializeWar();
            LogDebug(">>> DONE PROC");
        }

        public static void InitializeAllWarStatus()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;

            //initialize all WarFactions, DeathListTrackers, and SystemStatuses
            foreach (var faction in Settings.IncludedFactions)
            {
                new WarFaction(faction, Settings.AttackResourceMap[faction], Settings.DefensiveResourceMap[faction]);
                new DeathListTracker(faction);
            }

            foreach (var system in sim.StarSystems)
            {
                var warFaction = WarStatus.warFactionTracker.Find(x => x.faction == system.Owner);
                if (Settings.DefensiveFactions.Contains(warFaction.faction) && Settings.DefendersUseARforDR)
                {
                    warFaction.DefensiveResources += GetTotalAttackResources(system);
                    warFaction.AttackResources = 0;
                }
                else
                    warFaction.AttackResources += GetTotalAttackResources(system);

                warFaction.DefensiveResources += GetTotalDefensiveResources(system);
                new SystemStatus(sim, system.Name, system.Owner);
            }
        }
    }

    //public static void InitializeSystems(SimGameState sim)
    //{
    //    LogDebug(">>> Initialize systems");
    //    foreach (var starSystem in sim.StarSystems)
    //    {
    //        LogDebug("Add system " + starSystem.Name);
    //        WarStatus.systems.Add(new SystemStatus(sim, starSystem.Name));
    //    }

    //    foreach (var starSystem in WarStatus.systems)
    //    {
    //        //starSystem.FindNeighbors();
    //        starSystem.CalculateSystemInfluence();
    //    }
    //}

    public static void InitializeAllResources()
    {
        foreach (var system in WarStatus.systems)
        {
            var warFaction = WarStatus.warFactionTracker.Find(x => x.faction == system.owner);
            warFaction.AttackResources += GetTotalAttackResources(system.starSystem);
            warFaction.DefensiveResources += GetTotalDefensiveResources(system.starSystem);
        }
    }

    public static void CalculateAttackTargets(StarSystem starSystem)
    {
        LogDebug("Calculate Potential Attack Targets");
        SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
        LogDebug("Can Attack:");
        if (starSystem == null)
        {
            LogDebug("PPPPPPPPPOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOO");
            return;
        }

        foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
        {
            var warFac = WarStatus.warFactionTracker.Find(x => x.faction == starSystem.Owner);
            if (warFac == null)
            {
                LogDebug("Didn't find warFaction for " + starSystem.Owner);
                return;
            }

            if (neighborSystem.Owner != starSystem.Owner && !warFac.attackTargets.ContainsKey(neighborSystem.Owner))
            {
                var tempList = new List<StarSystem> {neighborSystem};
                warFac.attackTargets.Add(neighborSystem.Owner, tempList);
                LogDebug("\t" + neighborSystem.Name + ": " + neighborSystem.Owner);
            }
            else if ((neighborSystem.Owner != starSystem.Owner) && warFac.attackTargets.ContainsKey(neighborSystem.Owner) &&
                     !warFac.attackTargets[neighborSystem.Owner].Contains(neighborSystem))
            {
                warFac.attackTargets[neighborSystem.Owner].Add(neighborSystem);
                LogDebug("\t" + neighborSystem.Name + ": " + neighborSystem.Owner);
            }
        }

        //using (var writer = new StreamWriter("Mods\\GalaxyAtWar\\" + SaveHandling.fileName))
        //    writer.Write(JsonConvert.SerializeObject(Globals.attackTargets));
    }

    public static void CalculateDefenseTargets(StarSystem starSystem)
    {
        LogDebug("Calculate Potential Defendable Systems");
        LogDebug(starSystem.Name);
        LogDebug("Needs defense:");
        SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

        foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
        {
            var warFac = WarStatus.warFactionTracker.Find(x => x.faction == starSystem.Owner);
            if (warFac == null)
            {
                LogDebug("Didn't find warFaction for " + starSystem.Owner);
                return;
            }

            if ((neighborSystem.Owner != starSystem.Owner) && !warFac.defenseTargets.Contains(starSystem))
            {
                warFac.defenseTargets.Add(starSystem);
                LogDebug("\t" + starSystem.Name + ": " + starSystem.Owner);
            }
        }
    }

    public static void DivideAttackResources(WarFaction warFaction)
    {
        LogDebug("Attacking");
        var deathList = WarStatus.deathListTracker.Find(x => x.faction == warFaction.faction);
        var warFAR = warFaction.warFactionAttackResources;
        warFAR.Clear();
        var tempTargets = new Dictionary<Faction, float>();
        foreach (Faction fact in warFaction.attackTargets.Keys)
        {
            tempTargets.Add(fact, deathList.deathList[fact]);
        }

        var total = tempTargets.Values.Sum();

        Random rand = new Random();
        float attackResources = 0f;
        var i = warFaction.AttackResources;
        while (i > 0)
        {
            if (i >= 1)
            {
                attackResources += rand.Next(1, Settings.APRPushRandomizer + 1);
                i--;
            }
            else
            {
                attackResources = i * rand.Next(1, Settings.APRPushRandomizer + 1);
                i = 0;
            }
        }

        attackResources = attackResources * (1 + warFaction.DaysSinceSystemAttacked * Settings.ResourceAdjustmentPerCycle / 100);
       

        foreach (Faction Rfact in tempTargets.Keys)
        {
            warFAR.Add(Rfact, tempTargets[Rfact] * attackResources / total);
        }
    }

    public static void AllocateAttackResources(WarFaction warFaction)
    {
        if (warFaction.warFactionAttackResources.Keys.Count() == 0)
            return;
        var random = new Random();
        var warFAR = warFaction.warFactionAttackResources;

       
        //Go through the different resources allocated from attacking faction to spend against each targetFaction
        foreach (var targetFaction in warFAR.Keys)
        {
            var targetFAR = warFAR[targetFaction];

            while (targetFAR > 0.0)
            {
                var rand = random.Next(0, warFaction.attackTargets[targetFaction].Count());
                var system = WarStatus.systems.Find(f => f.name == warFaction.attackTargets[targetFaction][rand].Name);
                var maxValueList = system.influenceTracker.Values.OrderByDescending(x => x).ToList();
                var PmaxValue = maxValueList[1];
                var ITValue = system.influenceTracker[warFaction.faction];
                float bonusAR = 0f;

                if (ITValue > PmaxValue)
                    bonusAR = (ITValue - PmaxValue) * 0.15f;

                
                if (targetFAR > 1 + bonusAR)
                {
                    system.influenceTracker[warFaction.faction] += 1 + bonusAR;
                    targetFAR -= 1 + bonusAR;
                }
                else
                {
                    system.influenceTracker[warFaction.faction] += targetFAR;
                    targetFAR = 0;
                }
            }
        }
    }

    public static void AllocateDefensiveResources(WarFaction warFaction)
    {
        if (warFaction.defenseTargets.Count() == 0)
            return;
        var faction = warFaction.faction;
        if (WarStatus.warFactionTracker.Find(x => x.faction == faction) == null)
            return;

        Random random = new Random();
        float defensiveResources = 0f;
        var i = warFaction.DefensiveResources;
        while (i > 0)
        {
            if (i >= 1)
            {
                defensiveResources += random.Next(1, Settings.APRPushRandomizer + 1);
                i--;
            }
            else
            {
                defensiveResources = i * random.Next(1, Settings.APRPushRandomizer + 1);
                i = 0;
            }
        }

        defensiveResources = defensiveResources * (100 * Settings.GlobalDefenseFactor -
                                         Settings.ResourceAdjustmentPerCycle * warFaction.DaysSinceSystemLost) / 100;

        while (defensiveResources > 0.0)
        {
            float highest = 0f;
            Faction highestFaction = faction;
            var rand = random.Next(0, warFaction.defenseTargets.Count());
            var system = warFaction.defenseTargets[rand].Name;
            var systemStatus = WarStatus.systems.Find(x => x.name == system);

            foreach (Faction tempfaction in systemStatus.influenceTracker.Keys)
            {
                if (systemStatus.influenceTracker[tempfaction] > highest)

                {
                    highest = systemStatus.influenceTracker[tempfaction];
                    highestFaction = tempfaction;
                }
            }

            if (highestFaction == faction)
            {
                if (defensiveResources > 0)
                {
                    systemStatus.influenceTracker[faction] += 1;
                    defensiveResources -= 1;
                }
                else
                {
                    systemStatus.influenceTracker[faction] += defensiveResources;
                    defensiveResources = 0;
                }
            }
            else
            {
                var totalInfluence = systemStatus.influenceTracker.Values.Sum();
                var diffRes = systemStatus.influenceTracker[highestFaction]/totalInfluence - systemStatus.influenceTracker[faction]/totalInfluence;
                var bonusDefense = (diffRes * totalInfluence - (Settings.TakeoverThreshold/100) * totalInfluence) / (Settings.TakeoverThreshold / 100 + 1);
                if (diffRes > Settings.TakeoverThreshold)
                    if (defensiveResources >= bonusDefense)
                    {
                        systemStatus.influenceTracker[faction] += bonusDefense;
                        defensiveResources -= bonusDefense;
                    }
                    else
                    {
                        systemStatus.influenceTracker[faction] += Math.Min(defensiveResources, 5);
                        defensiveResources -= Math.Min(defensiveResources, 5);
                    }
                else
                {
                    systemStatus.influenceTracker[faction] += Math.Min(defensiveResources, 5);
                    defensiveResources -= Math.Min(defensiveResources, 5);
                }
            }
        }
    }

    public static void ChangeSystemOwnership(SimGameState sim, StarSystem system, Faction faction, bool ForceFlip)
    {
        if (faction != system.Owner || ForceFlip)
        {
            Faction OldFaction = system.Owner;
            if (system.Def.Tags.Contains(Settings.FactionTags[OldFaction]))
                system.Def.Tags.Remove(Settings.FactionTags[OldFaction]);
            system.Def.Tags.Add(Settings.FactionTags[faction]);
            system.Def.SystemShopItems.Add(Settings.FactionShops[faction]);
            if (system.Def.FactionShopItems != null)
            {
                Traverse.Create(system.Def).Property("FactionShopOwner").SetValue(faction);
                if (system.Def.FactionShopItems.Contains(Settings.FactionShopItems[system.Def.Owner]))
                    system.Def.FactionShopItems.Remove(Settings.FactionShopItems[system.Def.Owner]);
                system.Def.FactionShopItems.Add(Settings.FactionShopItems[faction]);
            }

            var systemStatus = WarStatus.systems.Find(x => x.name == system.Name);
            var oldOwner = systemStatus.owner;
            systemStatus.owner = faction;

            Traverse.Create(system.Def).Property("Owner").SetValue(faction);
            //Change the Kill List for the factions.
            var TotalAR = GetTotalAttackResources(system);
            var TotalDR = GetTotalDefensiveResources(system);
            var SystemValue = TotalAR + TotalDR;
            var KillListDelta = Math.Max(10, SystemValue);

            var factionTracker = WarStatus.deathListTracker.Find(x => x.faction == OldFaction);
            if (factionTracker.deathList[faction] < 50)
                factionTracker.deathList[faction] = 50;

            factionTracker.deathList[faction] += KillListDelta;
            //Allies are upset that their friend is being beaten up.

            foreach (var ally in sim.FactionsDict[OldFaction].Allies)
            {
                if (!Settings.IncludedFactions.Contains(ally) || faction == ally)
                    continue;

                var factionAlly = WarStatus.deathListTracker.Find(x => x.faction == ally);
                factionAlly.deathList[faction] += KillListDelta / 2;
            }

            //Enemies of the target faction are happy with the faction doing the beating.

            foreach (var enemy in sim.FactionsDict[OldFaction].Enemies)
            {
                if (!Settings.IncludedFactions.Contains(enemy) || enemy == faction)
                    continue;
                var factionEnemy = WarStatus.deathListTracker.Find(x => x.faction == enemy);
                factionEnemy.deathList[faction] -= KillListDelta / 2;
            }

            factionTracker.AttackedBy.Add(faction);

            WarFaction WFWinner = WarStatus.warFactionTracker.Find(x => x.faction == faction);
            WFWinner.GainedSystem = true;
            WFWinner.MonthlySystemsChanged += 1;
            WFWinner.TotalSystemsChanged += 1;
            WFWinner.AttackResources += TotalAR;
            WFWinner.DefensiveResources += TotalDR;
            WarFaction WFLoser = WarStatus.warFactionTracker.Find(x => x.faction == OldFaction);
            WFLoser.LostSystem = true;
            WFLoser.MonthlySystemsChanged -= 1;
            WFLoser.TotalSystemsChanged -= 1;
            WFLoser.AttackResources -= TotalAR;
            WFLoser.DefensiveResources -= TotalDR;

            ChangedSystems.Add(system);

        }
    }

    private static void UpdateInfluenceFromAttacks(SimGameState sim)
    {
        var tempRTFactions = WarStatus.deathListTracker;
        foreach (var deathListTracker in tempRTFactions)
        {
            deathListTracker.AttackedBy.Clear();
        }

        LogDebug($"Updating influence for {WarStatus.systems.Count().ToString()} systems");
        foreach (var systemStatus in WarStatus.systems)
        {
            var tempDict = new Dictionary<Faction, float>();
            var totalInfluence = systemStatus.influenceTracker.Values.Sum();
            var highest = 0f;
            var highestfaction = systemStatus.owner;
            foreach (var kvp in systemStatus.influenceTracker)
            {
                tempDict.Add(kvp.Key, kvp.Value / totalInfluence * 100);
                if (kvp.Value > highest)
                {
                    highest = kvp.Value;
                    highestfaction = kvp.Key;
                }
            }

            systemStatus.influenceTracker = tempDict;
            var diffStatus = systemStatus.influenceTracker[highestfaction] - systemStatus.influenceTracker[systemStatus.owner];

            if (highestfaction != systemStatus.owner && (diffStatus > Settings.TakeoverThreshold))
            {
                var previousOwner = systemStatus.owner;
                var starSystem = systemStatus.starSystem;

                if (starSystem != null)
                {
                    ChangeSystemOwnership(sim, starSystem, highestfaction, false);
                }

                LogDebug(">>> Ownership changed to " + highestfaction);
                if (highestfaction == Faction.NoFaction || highestfaction == Faction.Locals)
                {
                    LogDebug("\tNoFaction or Locals, continuing");
                    continue;
                }

                // BUG NRE on deserialization
                var WarFactionWinner = WarStatus.warFactionTracker.Find(x => x.faction == highestfaction);
                if (WarFactionWinner != null)
                    WarFactionWinner.DaysSinceSystemAttacked = 0;

                try
                {
                    var WarFactionLoser = WarStatus.warFactionTracker.Find(x => x.faction == previousOwner);
                    if (WarFactionLoser != null)
                        WarFactionLoser.DaysSinceSystemLost = 0;
                }
                catch (Exception ex)
                {
                    Error(ex);
                }
            }
        }

        foreach (var deathListTracker in tempRTFactions)
        {
            AdjustDeathList(deathListTracker, sim);
        }
    }

    public static string MonthlyWarReport()
    {
        string summaryString = "";
        string summaryString2 = "";
        string combinedString = "";

        foreach (Faction faction in Settings.IncludedFactions)
        {
            WarFaction warFaction = WarStatus.warFactionTracker.Find(x => x.faction == faction);
            combinedString = combinedString + "<b><u>" + Settings.FactionNames[faction] + "</b></u>\n";
            summaryString = "Monthly Change in Systems: " + warFaction.MonthlySystemsChanged + "\n";
            summaryString2 = "Overall Change in Systems: " + warFaction.TotalSystemsChanged + "\n\n";

            combinedString = combinedString + summaryString + summaryString2;
            warFaction.MonthlySystemsChanged = 0;
        }
        char[] trim = { '\n' };
        combinedString = combinedString.TrimEnd(trim);
        return combinedString;
    }

    public static void RefreshNeighbors(StarSystem starSystem)
    {
        SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
        var neighborSystems = WarStatus.systems.Find(x => x.name == starSystem.Name).neighborSystems;
        neighborSystems.Clear();
        var neighbors = sim.Starmap.GetAvailableNeighborSystem(starSystem);
        foreach (var neighborSystem in neighbors)
        {
            if (neighborSystems.ContainsKey(neighborSystem.Owner))
                neighborSystems[neighborSystem.Owner] += 1;
            else
                neighborSystems.Add(neighborSystem.Owner, 1);
        }
    }

    public static void RefreshContracts(StarSystem starSystem)
    {
        var ContractEmployers = starSystem.Def.ContractEmployers;
        var ContractTargets = starSystem.Def.ContractTargets;
        SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

        var owner = starSystem.Owner;
        ContractEmployers.Clear();
        ContractTargets.Clear();
        ContractEmployers.Add(owner);
        ContractTargets.Add(owner);

        var neighborSystems = WarStatus.systems.Find(x => x.owner == owner).neighborSystems;
        foreach (var systemNeighbor in neighborSystems.Keys)
        {
            if (!ContractEmployers.Contains(systemNeighbor) && !Settings.DefensiveFactions.Contains(systemNeighbor))
                ContractEmployers.Add(systemNeighbor);

            if (!ContractTargets.Contains(systemNeighbor) && !Settings.DefensiveFactions.Contains(systemNeighbor))
                ContractTargets.Add(systemNeighbor);
        }

        if (ContractTargets.Count() == 1)
        {
            ContractTargets.Clear();
            foreach (Faction EF in sim.FactionsDict[owner].Enemies)
                ContractTargets.Add(EF);
        }
        if (ContractTargets.Count() == 0)
        {
            foreach (Faction EF in Settings.DefensiveFactions)
                ContractTargets.Add(EF);
        }
    }

    public static void AdjustDeathList(DeathListTracker deathListTracker, SimGameState sim)
    {
        var deathList = deathListTracker.deathList;
        var KL_List = new List<Faction>(deathList.Keys);

        var deathListFaction = deathListTracker.faction;
        foreach (Faction faction in KL_List)
        {
            //Factions go towards peace over time if not attacked.But there is diminishing returns further from 50.
            if (!deathListTracker.AttackedBy.Contains(faction))
            {
                if (deathList[faction] > 50)
                    deathList[faction] -= 1 - (deathList[faction] - 50) / 50;
                else if (deathList[faction] <= 50)
                    deathList[faction] -= 1 - (50 - deathList[faction]) / 50;
            }

            //Ceiling and floor for faction enmity. 
            if (deathList[faction] > 99)
                deathList[faction] = 99;

            if (deathList[faction] < 1)
                deathList[faction] = 1;

            //Defensive Only factions are always neutral
            if (Settings.DefensiveFactions.Contains(faction))
                deathList[faction] = 50;

            if (deathList[faction] > 75)
            {
                if (!sim.FactionsDict[deathListFaction].Enemies.Contains(faction))
                {
                    var enemies = new List<Faction>(sim.FactionsDict[deathListFaction].Enemies);
                    enemies.Add(faction);
                    Traverse.Create(sim.FactionsDict[deathListFaction]).Property("Enemies").SetValue(enemies.ToArray());
                }

                if (sim.FactionsDict[deathListFaction].Allies.Contains(faction))
                {
                    var allies = new List<Faction>(sim.FactionsDict[deathListFaction].Allies);
                    allies.Remove(faction);
                    Traverse.Create(sim.FactionsDict[deathListFaction]).Property("Allies").SetValue(allies.ToArray());
                }
            }

            if (deathList[faction] <= 75 && deathList[faction] > 25)
            {
                if (sim.FactionsDict[deathListFaction].Enemies.Contains(faction))
                {
                    var enemies = new List<Faction>(sim.FactionsDict[deathListFaction].Enemies);
                    enemies.Remove(faction);
                    Traverse.Create(sim.FactionsDict[deathListFaction]).Property("Enemies").SetValue(enemies.ToArray());
                }

                if (sim.FactionsDict[deathListFaction].Allies.Contains(faction))
                {
                    var allies = new List<Faction>(sim.FactionsDict[deathListFaction].Allies);
                    allies.Remove(faction);
                    Traverse.Create(sim.FactionsDict[deathListFaction]).Property("Allies").SetValue(allies.ToArray());
                }
            }

            if (deathList[faction] <= 25)
            {
                if (!sim.FactionsDict[deathListFaction].Allies.Contains(faction))
                {
                    var allies = new List<Faction>(sim.FactionsDict[deathListFaction].Allies);
                    allies.Add(faction);
                    Traverse.Create(sim.FactionsDict[deathListFaction]).Property("Allies").SetValue(allies.ToArray());
                }

                if (sim.FactionsDict[deathListFaction].Enemies.Contains(faction))
                {
                    var enemies = new List<Faction>(sim.FactionsDict[deathListFaction].Enemies);
                    enemies.Remove(faction);
                    Traverse.Create(sim.FactionsDict[deathListFaction]).Property("Enemies").SetValue(enemies.ToArray());
                }
            }
        }
    }

    public static int GetTotalAttackResources(StarSystem system)
    {
        int result = 0;
        if (system.Tags.Contains("planet_industry_poor"))
            result += Settings.planet_industry_poor;
        if (system.Tags.Contains("planet_industry_mining"))
            result += Settings.planet_industry_mining;
        if (system.Tags.Contains("planet_industry_rich"))
            result += Settings.planet_industry_rich;
        if (system.Tags.Contains("planet_industry_manufacturing"))
            result += Settings.planet_industry_manufacturing;
        if (system.Tags.Contains("planet_industry_research"))
            result += Settings.planet_industry_research;
        if (system.Tags.Contains("planet_other_starleague"))
            result += Settings.planet_other_starleague;
        return result;
    }

    public static int GetTotalDefensiveResources(StarSystem system)
    {
        int result = 0;
        if (system.Tags.Contains("planet_industry_agriculture"))
            result += Settings.planet_industry_agriculture;
        if (system.Tags.Contains("planet_industry_aquaculture"))
            result += Settings.planet_industry_aquaculture;
        if (system.Tags.Contains("planet_other_capital"))
            result += Settings.planet_other_capital;
        if (system.Tags.Contains("planet_other_megacity"))
            result += Settings.planet_other_megacity;
        if (system.Tags.Contains("planet_pop_large"))
            result += Settings.planet_pop_large;
        if (system.Tags.Contains("planet_pop_medium"))
            result += Settings.planet_pop_medium;
        if (system.Tags.Contains("planet_pop_none"))
            result += Settings.planet_pop_none;
        if (system.Tags.Contains("planet_pop_small"))
            result += Settings.planet_pop_small;
        if (system.Tags.Contains("planet_other_hub"))
            result += Settings.planet_other_hub;
        if (system.Tags.Contains("planet_other_comstar"))
            result += Settings.planet_other_comstar;
        return result;
    }

    //public static void RefreshResources(SimGameState sim)
    //{
    //    // no point iterating over a KVP if you aren't using the values
    //    //LogDebug($"Object size: {JsonConvert.SerializeObject(Core.WarStatus).Length / 1024}kb");
    //    foreach (var faction in sim.FactionsDict.Select(x => x.Key))
    //    {
    //        if (Settings.ResourceMap.ContainsKey(faction))
    //        {
    //            // initialize resources from the ResourceMap
    //            if (WarStatus.warFactionTracker.Find(x => x.faction == faction) == null)
    //            {
    //                var StartingResources = Settings.ResourceMap[faction];
    //                var DefensiveStartingResources = Settings.DefensiveResourceMap[faction];
    //                WarStatus.warFactionTracker.Add(new WarFaction(faction, StartingResources, DefensiveStartingResources));
    //            }
    //            else
    //            {
    //                WarFaction warFaction = WarStatus.warFactionTracker.Find(x => x.faction == faction);
    //                if (warFaction != null)
    //                {
    //                    warFaction.AttackResources = Settings.ResourceMap[faction];
    //                    warFaction.DefensiveResources = Settings.DefensiveResourceMap[faction];
    //                }
    //            }
    //        }
    //    }

    //    var random = new Random();
    //    foreach (var system in sim.StarSystems)
    //    {
    //        var resources = GetTotalResources(system);
    //        var DefensiveResources = GetTotalDefensiveResources(system);
    //        var owner = system.Owner;
    //        try
    //        {
    //            var faction = WarStatus.warFactionTracker.Where(x => x != null).FirstOrDefault(x => x.faction == owner);
    //            if (faction != null)
    //            {
    //                faction.AttackResources += resources;
    //                faction.DefensiveResources += DefensiveResources;
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            Error(ex);
    //        }
    //    }

    //    foreach (var faction in WarStatus.warFactionTracker)
    //    {
    //        if (Settings.DefensiveFactions.Contains(faction.faction) && Settings.DefendersUseARforDR)
    //        {
    //            faction.DefensiveResources += faction.AttackResources;
    //            faction.AttackResources = 0;
    //        }

    //        float tempnum = 0f;
    //        int i = 0;
    //        do
    //        {
    //            tempnum += random.Next(1, Settings.ResourceRandomizer + 1);
    //            i++;
    //        } while (i < faction.AttackResources);

    //        faction.AttackResources = tempnum * (100f + faction.DaysSinceSystemAttacked * Settings.ResourceAdjustmentPerCycle) / 100f;

    //        tempnum = 0f;
    //        i = 0;
    //        do
    //        {
    //            tempnum += random.Next(1, Settings.ResourceRandomizer + 1);
    //            i++;
    //        } while (i < faction.DefensiveResources);

    //        faction.DefensiveResources = tempnum * (100f * Settings.GlobalDefenseFactor
    //                                                - faction.DaysSinceSystemLost * Settings.ResourceAdjustmentPerCycle) / 100f;
    //    }
    //}

    //public static void PostChangeSystemUpdate(StarSystem starSystem)
    //{
    //    SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
    //    var system = WarStatus.systems.Find(x => x.name == starSystem.Name);

    //    var ContractEmployers = system.starSystem.Def.ContractEmployers;
    //    var ContractTargets = system.starSystem.Def.ContractTargets;

    //    foreach (var systemNeighbor in sim.Starmap.GetAvailableNeighborSystem(starSystem))
    //    {
    //        if (!ContractEmployers.Contains(systemNeighbor.Owner) && !Settings.DefensiveFactions.Contains(systemNeighbor.Owner))
    //            ContractEmployers.Add(systemNeighbor.Owner);

    //        if (!ContractTargets.Contains(systemNeighbor.Owner) && !Settings.DefensiveFactions.Contains(systemNeighbor.Owner))
    //            ContractTargets.Add(systemNeighbor.Owner);

    //        var farContractEmployers = systemNeighbor.Def.ContractEmployers;
    //        var farContractTargets = systemNeighbor.Def.ContractTargets;

    //        foreach (var farsystemNeighbor in sim.Starmap.GetAvailableNeighborSystem(systemNeighbor))
    //        {
    //            if (!farContractEmployers.Contains(farsystemNeighbor.Owner) && !Settings.DefensiveFactions.Contains(farsystemNeighbor.Owner))
    //                ContractEmployers.Add(farsystemNeighbor.Owner);

    //            if (!ContractTargets.Contains(farsystemNeighbor.Owner) && !Settings.DefensiveFactions.Contains(farsystemNeighbor.Owner))
    //                ContractTargets.Add(farsystemNeighbor.Owner);
    //        }
    //    }
    //}

    [HarmonyPatch(typeof(Contract), "CompleteContract")]
    public static class CompleteContract_Patch
    {
        public static void Postfix(Contract __instance, MissionResult result, bool isGoodFaithEffort)
        {
            var teamfaction = __instance.Override.employerTeam.faction;
            var enemyfaction = __instance.Override.targetTeam.faction;
            var difficulty = __instance.Difficulty;
            var system = __instance.TargetSystem;
            var warsystem = WarStatus.systems.Find(x => x.name == system);

            if (result == MissionResult.Victory)
            {
                warsystem.influenceTracker[teamfaction] += difficulty * Settings.DifficultyFactor;
                warsystem.influenceTracker[enemyfaction] -= difficulty * Settings.DifficultyFactor;
            }
            else if (result == MissionResult.Defeat || (result != MissionResult.Victory && !isGoodFaithEffort))
            {
                warsystem.influenceTracker[teamfaction] -= difficulty * Settings.DifficultyFactor;
                warsystem.influenceTracker[enemyfaction] += difficulty * Settings.DifficultyFactor;
            }

            var sim = __instance.BattleTechGame.Simulation;
            var oldowner = sim.CurSystem.Owner;
            UpdateInfluenceFromAttacks(sim);
            var newowner = sim.CurSystem.Owner;

            //This is a WIP for the pop-up after a system changes due to player interaction.
            if (oldowner != newowner)
            {
                GameInstance game = LazySingletonBehavior<UnityGameInstance>.Instance.Game;
                SimGameInterruptManager interruptQueue = (SimGameInterruptManager) AccessTools
                    .Field(typeof(SimGameState), "interruptQueue").GetValue(game.Simulation);
                interruptQueue.QueueGenericPopup_NonImmediate("Comstar Bulletin: Galaxy at War", sim.CurSystem.Name + " taken!" + newowner.ToString() +
                                                                                                 " conquered from " + oldowner.ToString(), true, null);
                sim.StopPlayMode();
            }
        }
    }

    
}
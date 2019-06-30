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
using Error = BestHTTP.SocketIO.Error;
using UnityEngine.Scripting;
using System.Runtime.InteropServices;
using Localize;

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
    public static readonly Random Random = new Random();
    public static Faction teamfaction;
    public static Faction enemyfaction;
    public static int difficulty;
    public static MissionResult missionResult;
    public static bool isGoodFaithEffort;
    public static List<Faction> FactionEnemyHolder = new List<Faction>();
    public static Dictionary<Faction, List<StarSystem>> attackTargets = new Dictionary<Faction, List<StarSystem>>();
    public static List<StarSystem> defenseTargets = new List<StarSystem>();

    [HarmonyPatch(typeof(SimGameState), "OnDayPassed")]
    public static class SimGameState_OnDayPassed_Patch
    {
        static void Prefix(SimGameState __instance, int timeLapse)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            WarStatus.CurSystem = sim.CurSystem.Name;
            if (Core.WarStatus.HotBox.Contains(sim.CurSystem.Name))
            {
                WarStatus.EscalationDays--;

                if (WarStatus.EscalationDays == 0)
                {
                    Galaxy_at_War.HotSpots.CompleteEscalation();
                }
                if (WarStatus.EscalationOrder != null)
                {
                    WarStatus.EscalationOrder.PayCost(1);
                    TaskManagementElement taskManagementElement4 = null;
                    TaskTimelineWidget timelineWidget = (TaskTimelineWidget)AccessTools.Field(typeof(SGRoomManager), "timelineWidget").GetValue(__instance.RoomManager);
                    Dictionary<WorkOrderEntry, TaskManagementElement> ActiveItems = 
                        (Dictionary<WorkOrderEntry, TaskManagementElement>)AccessTools.Field(typeof(TaskTimelineWidget), "ActiveItems").GetValue(timelineWidget);
                    if (ActiveItems.TryGetValue(WarStatus.EscalationOrder, out taskManagementElement4))
                    {
                        taskManagementElement4.UpdateItem(0);
                    }
                }
            }
            if (!Core.WarStatus.StartGameInitialized)
            {
                Galaxy_at_War.HotSpots.ProcessHotSpots();
                var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                Core.WarStatus.StartGameInitialized = true;
            }
        }

        public static void Postfix(SimGameState  __instance)
        {
            if (!WarStatus.GaW_Event_PopUp)
            {
                GaW_Notification();
                WarStatus.GaW_Event_PopUp = true;
            }
            if (__instance.DayRemainingInQuarter % Settings.WarFrequency == 0)
            {
                //LogDebug(">>> PROC");
                if (__instance.DayRemainingInQuarter != 30)
                    WarTick(false, false);
                else
                    WarTick(true, true);

                SaveHandling.SerializeWar();
                LogDebug(">>> DONE PROC");
            }

            //Comstar report on ongoing war.
            if (__instance.DayRemainingInQuarter == 30)
            {
                //var ReportString = MonthlyWarReport();
                //WarSummary(ReportString);
                //SimGameInterruptManager interruptQueue = (SimGameInterruptManager) AccessTools
                //    .Field(typeof(SimGameState), "interruptQueue").GetValue(__instance);
                //interruptQueue.QueueGenericPopup_NonImmediate("Comstar Bulletin: Galaxy at War", ReportString, true, null);
                //__instance.StopPlayMode();
                if (!WarStatus.HotBoxTravelling)
                {
                    var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                    __instance.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                }
            }
        }
    }


    internal static void WarTick(bool UseFullSet, bool CheckForSystemChange)
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        WarStatus.PrioritySystems.Clear();

        int SystemSubsetSize = WarStatus.systems.Count;
        if (Settings.UseSubsetOfSystems && !UseFullSet)
            SystemSubsetSize = (int)(SystemSubsetSize * Settings.SubSetFraction);
        var SystemSubset = WarStatus.systems.OrderBy(x => Guid.NewGuid()).Take(SystemSubsetSize);

        LogDebug("Calculations and System Push");

        foreach (var systemStatus in SystemSubset)
        {
            systemStatus.PriorityAttack = false;
            systemStatus.PriorityDefense = false;
            if (WarStatus.InitializeAtStart || WarStatus.SystemChangedOwners.Contains(systemStatus.name))
            {
                if (!WarStatus.InitializeAtStart)
                    WarStatus.SystemChangedOwners.Remove(systemStatus.name);
                systemStatus.CurrentlyAttackedBy.Clear();
                CalculateAttackAndDefenseTargets(systemStatus.starSystem);
                RefreshContracts(systemStatus.starSystem);
            }
            if (systemStatus.Contended || Core.WarStatus.HotBox.Contains(systemStatus.name)) continue;

            //Add resources from neighboring systems.
            if (systemStatus.neighborSystems.Count != 0)
            {
                foreach (var neighbor in systemStatus.neighborSystems.Keys)
                {
                    if (!Settings.ImmuneToWar.Contains(neighbor))
                    {
                        var PushFactor = Settings.APRPush * Random.Next(1, Settings.APRPushRandomizer + 1);
                        systemStatus.influenceTracker[neighbor] += systemStatus.neighborSystems[neighbor] * PushFactor;
                    }
                }
            }
        }
        WarStatus.InitializeAtStart = false;
        //Attack!
        //LogDebug("Attacking Fool");
        foreach (var warFaction in WarStatus.warFactionTracker)
        {
            DivideAttackResources(warFaction);
            AllocateAttackResources(warFaction);
        }
        foreach (var warFaction in WarStatus.warFactionTracker)
        {
            AllocateDefensiveResources(warFaction);
        }
            UpdateInfluenceFromAttacks(sim, CheckForSystemChange);
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
        if (WarStatus.StartGameInitialized)
        {
            Galaxy_at_War.HotSpots.ProcessHotSpots();
            StarmapMod.SetupRelationPanel();
        }



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
    }

    public static void CalculateAttackAndDefenseTargets(StarSystem starSystem)
    {
        SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
        var warSystem = WarStatus.systems.Find(x => x.name == starSystem.Name);
        var OwnerNeighborSystems = warSystem.neighborSystems;
        OwnerNeighborSystems.Clear();
        if (starSystem == null || sim.Starmap.GetAvailableNeighborSystem(starSystem).Count == 0)
            return;

        foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
        {
            if (neighborSystem.Owner != starSystem.Owner && !Settings.ImmuneToWar.Contains(neighborSystem.Owner))
            {
                var warFac = WarStatus.warFactionTracker.Find(x => x.faction == starSystem.Owner);
                if (warFac == null)
                    return;

                if (!warFac.attackTargets.ContainsKey(neighborSystem.Owner))
                {
                    var tempList = new List<string> { neighborSystem.Name };
                    warFac.attackTargets.Add(neighborSystem.Owner, tempList);
                }
                else if (warFac.attackTargets.ContainsKey(neighborSystem.Owner) 
                    && !warFac.attackTargets[neighborSystem.Owner].Contains(neighborSystem.Name))
                {
                    warFac.attackTargets[neighborSystem.Owner].Add(neighborSystem.Name);
                }
                if (!warFac.defenseTargets.Contains(starSystem.Name))
                {
                    warFac.defenseTargets.Add(starSystem.Name);
                }
            }
            RefreshNeighbors(OwnerNeighborSystems, neighborSystem);
        }
    }

    public static void RefreshNeighbors(Dictionary<Faction, int> starSystem, StarSystem neighborSystem)
    {

        if (starSystem.ContainsKey(neighborSystem.Owner))
            starSystem[neighborSystem.Owner] += 1;
        else
            starSystem.Add(neighborSystem.Owner, 1);
    }

    //public static void CalculateDefenseTargets(StarSystem starSystem)
    //{
    //    SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

    //    foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
    //    {
    //        var warFac = WarStatus.warFactionTracker.Find(x => x.faction == starSystem.Owner);
    //        if (warFac == null)
    //        {
    //            return;
    //        }

    //        if ((neighborSystem.Owner != starSystem.Owner) && !warFac.defenseTargets.Contains(starSystem))
    //        {
    //            warFac.defenseTargets.Add(starSystem);
    //        }
    //    }
    //}

    public static void DivideAttackResources(WarFaction warFaction)
    {
        //Log("Attacking");
        var deathList = WarStatus.deathListTracker.Find(x => x.faction == warFaction.faction);
        var warFAR = warFaction.warFactionAttackResources;
        warFAR.Clear();
        var tempTargets = new Dictionary<Faction, float>();
        foreach (Faction fact in warFaction.attackTargets.Keys)
        {
            tempTargets.Add(fact, deathList.deathList[fact]);
        }

        var total = tempTargets.Values.Sum();
        float attackResources = 0f;;
        float i = warFaction.AttackResources * Settings.ResourceScale;
        while (i > 0)
        {
            if (i >= 1)
            {
                attackResources += Random.Next(1, Settings.APRPushRandomizer + 1);
                i--;
            }
            else
            {
                attackResources = i * Random.Next(1, Settings.APRPushRandomizer + 1);
                i = 0;
            }
        }

        attackResources = attackResources * (1 + warFaction.DaysSinceSystemAttacked * Settings.AResourceAdjustmentPerCycle / 100);
        foreach (Faction Rfact in tempTargets.Keys)
        {
            warFAR.Add(Rfact, tempTargets[Rfact] * attackResources / total);
        }
    }

    public static void AllocateAttackResources(WarFaction warFaction)
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        var FactionRep = sim.GetRawReputation(warFaction.faction);
        int maxContracts = Galaxy_at_War.HotSpots.ProcessReputation(FactionRep);
        if (warFaction.warFactionAttackResources.Keys.Count == 0)
            return;
        var warFAR = warFaction.warFactionAttackResources;
        //Go through the different resources allocated from attacking faction to spend against each targetFaction
        foreach (var targetFaction in warFAR.Keys)
        {
            var targetFAR = warFAR[targetFaction];
            var factionDLT = Core.WarStatus.deathListTracker.Find(x => x.faction == warFaction.faction);
            while (targetFAR > 0.0)
            {
                var rand = Random.Next(0, warFaction.attackTargets[targetFaction].Count);
                var system = WarStatus.systems.Find(f => f.name == warFaction.attackTargets[targetFaction][rand]);

                //Find most valuable target for attacking for later. Used in HotSpots.
                if (factionDLT.deathList[targetFaction] >= Core.Settings.PriorityHatred && system.DifficultyRating <= maxContracts 
                    && system.DifficultyRating >= maxContracts - 4)
                {
                    system.PriorityAttack = true;
                    if (!system.CurrentlyAttackedBy.Contains(warFaction.faction))
                    {
                        system.CurrentlyAttackedBy.Add(warFaction.faction);
                    }
                    if (!WarStatus.PrioritySystems.Contains(system.starSystem.Name))
                    {
                        WarStatus.PrioritySystems.Add(system.starSystem.Name);
                    }
                }

                //Distribute attacking resources to systems.
                if (system.Contended || Core.WarStatus.HotBox.Contains(system.name))
                {
                    warFaction.attackTargets[targetFaction].Remove(system.starSystem.Name);
                    if (warFaction.attackTargets[targetFaction].Count == 0 || !warFaction.attackTargets.Keys.Contains(targetFaction))
                    {
                        break;
                    }
                    else
                        continue;
                }
                
                var maxValueList = system.influenceTracker.Values.OrderByDescending(x => x).ToList();
                float PmaxValue = 200.0f;
                if (maxValueList.Count > 1)
                    PmaxValue = maxValueList[1];
                var ITValue = system.influenceTracker[warFaction.faction];
                float basicAR = (float)(11 - system.DifficultyRating) / 2;

                float bonusAR = 0f;
                if (ITValue > PmaxValue)
                    bonusAR = (ITValue - PmaxValue) * 0.15f;

                float TotalAR = basicAR + bonusAR;

                if (targetFAR > TotalAR)
                {
                    system.influenceTracker[warFaction.faction] += TotalAR;
                    targetFAR -= TotalAR;
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
        var faction = warFaction.faction;
        if (warFaction.defenseTargets.Count == 0 || WarStatus.warFactionTracker.Find(x => x.faction == faction) == null)
            return;
        
        float defensiveResources = 0f;
        var i = warFaction.DefensiveResources * Settings.ResourceScale;
        while (i > 0)
        {
            if (i >= 1)
            {
                defensiveResources += Random.Next(1, Settings.APRPushRandomizer + 1);
                i--;
            }
            else
            {
                defensiveResources = i * Random.Next(1, Settings.APRPushRandomizer + 1);
                i = 0;
            }
        }

        defensiveResources = defensiveResources * (100 * Settings.GlobalDefenseFactor -
                                                   Settings.DResourceAdjustmentPerCycle * warFaction.DaysSinceSystemLost) / 100;

        while (defensiveResources > 0.0)
        {
            float highest = 0f;
            Faction highestFaction = faction;
            var rand = Random.Next(0, warFaction.defenseTargets.Count);
            var system = warFaction.defenseTargets[rand];
            var systemStatus = WarStatus.systems.Find(x => x.name == system);

            if (systemStatus.Contended || Core.WarStatus.HotBox.Contains(systemStatus.name))
            {
                warFaction.defenseTargets.Remove(systemStatus.starSystem.Name);
                if (warFaction.defenseTargets.Count == 0 || warFaction.defenseTargets == null)
                {
                    break;
                }
                else
                    continue;
            }

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
                var diffRes = systemStatus.influenceTracker[highestFaction] / totalInfluence - systemStatus.influenceTracker[faction] / totalInfluence;
                var bonusDefense = 1 + (diffRes * totalInfluence - (Settings.TakeoverThreshold / 100) * totalInfluence) / (Settings.TakeoverThreshold / 100 + 1);
                if (100 * diffRes > Settings.TakeoverThreshold)
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
            
            WarFaction WFWinner = WarStatus.warFactionTracker.Find(x => x.faction == faction);
            WFWinner.GainedSystem = true;
            WFWinner.MonthlySystemsChanged += 1;
            WFWinner.TotalSystemsChanged += 1;
            if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(WFWinner.faction))
            {
                WFWinner.DefensiveResources += TotalAR;
                WFWinner.DefensiveResources += TotalDR;
            }
            else
            {
                WFWinner.AttackResources += TotalAR;
                WFWinner.DefensiveResources += TotalDR;
            }
            WFWinner.AttackResources += TotalAR;
            WFWinner.DefensiveResources += TotalDR;
            WarFaction WFLoser = WarStatus.warFactionTracker.Find(x => x.faction == OldFaction);
            WFLoser.LostSystem = true;
            WFLoser.MonthlySystemsChanged -= 1;
            WFLoser.TotalSystemsChanged -= 1;
            RemoveAndFlagSystems(WFLoser, system);
            if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(WFWinner.faction))
            {
                WFLoser.DefensiveResources -= TotalAR;
                WFLoser.DefensiveResources -= TotalDR;
            }
            else
            {
                WFLoser.AttackResources -= TotalAR;
                WFLoser.DefensiveResources -= TotalDR;
            }
            if (!WarStatus.SystemChangedOwners.Contains(system.Name))
                WarStatus.SystemChangedOwners.Add(system.Name);
            foreach (var neighbor in sim.Starmap.GetAvailableNeighborSystem(system))
            {
                if (!WarStatus.SystemChangedOwners.Contains(neighbor.Name))
                    WarStatus.SystemChangedOwners.Add(neighbor.Name);
            }
        }
    }

    public static void ChangeDeathlistFromAggression(StarSystem system, Faction faction, Faction OldFaction)
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        var TotalAR = GetTotalAttackResources(system);
        var TotalDR = GetTotalDefensiveResources(system);
        var SystemValue = TotalAR + TotalDR;
        var KillListDelta = Math.Max(10, SystemValue);
        var factionTracker = WarStatus.deathListTracker.Find(x => x.faction == OldFaction);
        if (factionTracker.deathList[faction] < 50)
            factionTracker.deathList[faction] = 50;
        factionTracker.deathList[faction] += KillListDelta;
        //Allies are upset that their friend is being beaten up.
        if (!Settings.DefensiveFactions.Contains(OldFaction))
        {
            foreach (var ally in sim.FactionsDict[OldFaction].Allies)
            {
                if (!Settings.IncludedFactions.Contains(ally) || faction == ally)
                    continue;

                var factionAlly = WarStatus.deathListTracker.Find(x => x.faction == ally);
                factionAlly.deathList[faction] += KillListDelta / 2;
            }
        }
        //Enemies of the target faction are happy with the faction doing the beating.
        if (!Settings.DefensiveFactions.Contains(OldFaction))
        {
            foreach (var enemy in sim.FactionsDict[OldFaction].Enemies)
            {
                if (!Settings.IncludedFactions.Contains(enemy) || enemy == faction)
                    continue;
                var factionEnemy = WarStatus.deathListTracker.Find(x => x.faction == enemy);
                factionEnemy.deathList[faction] -= KillListDelta / 2;
            }
        }
        factionTracker.AttackedBy.Add(faction);
    }

    private static void RemoveAndFlagSystems(WarFaction OldOwner, StarSystem system)
    {
        OldOwner.defenseTargets.Remove(system.Name);
        if (!WarStatus.SystemChangedOwners.Contains(system.Name))
            WarStatus.SystemChangedOwners.Add(system.Name);
        foreach (var neighborsystem in UnityGameInstance.BattleTechGame.Simulation.Starmap.GetAvailableNeighborSystem(system))
        {
            var WFAT = WarStatus.warFactionTracker.Find(x => x.faction == neighborsystem.Owner).attackTargets;
            if (WFAT.Keys.Contains(OldOwner.faction) && !WFAT[OldOwner.faction].Contains(system.Name))
                WFAT[OldOwner.faction].Remove(system.Name);
        }
    }

    private static void UpdateInfluenceFromAttacks(SimGameState sim, bool CheckForSystemChange)
    {
        var tempRTFactions = WarStatus.deathListTracker;
        foreach (var deathListTracker in tempRTFactions)
        {
            deathListTracker.AttackedBy.Clear();
        }

        if (CheckForSystemChange)
            WarStatus.LostSystems.Clear();

        LogDebug($"Updating influence for {WarStatus.systems.Count.ToString()} systems");
        foreach (var systemStatus in Core.WarStatus.systems)
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
            var starSystem = systemStatus.starSystem;
            

            if (highestfaction != systemStatus.owner && (diffStatus > Settings.TakeoverThreshold && !Core.WarStatus.HotBox.Contains(systemStatus.name)
                && !Settings.DefensiveFactions.Contains(highestfaction) && !Settings.ImmuneToWar.Contains(starSystem.Owner)))
            {
                if (!systemStatus.Contended)
                {
                    systemStatus.Contended = true;
                    ChangeDeathlistFromAggression(starSystem, highestfaction, starSystem.Owner);
                }
                else if (CheckForSystemChange)
                { 
                    ChangeSystemOwnership(sim, starSystem, highestfaction, false);
                    systemStatus.Contended = false;
                    WarStatus.LostSystems.Add(starSystem.Name);
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

        char[] trim = {'\n'};
        combinedString = combinedString.TrimEnd(trim);
        return combinedString;
    }

    public static void RefreshContracts(StarSystem starSystem)
    {
        if (WarStatus.HotBox.Contains(starSystem.Name))
            return;
        var ContractEmployers = starSystem.Def.ContractEmployers;
        var ContractTargets = starSystem.Def.ContractTargets;
        SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
        var owner = starSystem.Owner;
        ContractEmployers.Clear();
        ContractTargets.Clear();
        ContractEmployers.Add(owner);
        foreach (Faction EF in Settings.DefensiveFactions)
        {
            if (Settings.ImmuneToWar.Contains(EF))
                continue;
            ContractTargets.Add(EF);
        }
        if (!ContractTargets.Contains(owner))
            ContractTargets.Add(owner);
        var neighborSystems = WarStatus.systems.Find(x => x.name == starSystem.Name).neighborSystems;
        foreach (var systemNeighbor in neighborSystems.Keys)
        {
            if (Settings.ImmuneToWar.Contains(systemNeighbor))
                continue;
            if (!ContractEmployers.Contains(systemNeighbor) && !Settings.DefensiveFactions.Contains(systemNeighbor))
                ContractEmployers.Add(systemNeighbor);

            if (!ContractTargets.Contains(systemNeighbor) && !Settings.DefensiveFactions.Contains(systemNeighbor))
                ContractTargets.Add(systemNeighbor);
        }
        if (ContractEmployers.Count == 1)
            ContractEmployers.Add(Faction.AuriganPirates);
    }

    [HarmonyPatch(typeof(SimGameState), "GenerateContractParticipants")]
    public static class SimGameState_GenerateContractParticipants_Patch
    {
        public static void Prefix(FactionDef employer, StarSystemDef system)
        {
            FactionEnemyHolder.Clear();
            var NewEnemies = system.ContractTargets;
            FactionEnemyHolder = employer.Enemies.ToList();
            var NewFactionEnemies = FactionEnemyHolder;
            foreach (var Enemy in NewEnemies)
            {
                if (!NewFactionEnemies.Contains(Enemy) && !employer.Allies.Contains(Enemy) && Enemy != employer.Faction)
                    NewFactionEnemies.Add(Enemy);
            }
            Traverse.Create(employer).Property("Enemies").SetValue(NewFactionEnemies.ToArray());
        }

        public static void Postfix(FactionDef employer)
        {
            Traverse.Create(employer).Property("Enemies").SetValue(FactionEnemyHolder.ToArray());
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

    public static float GetTotalAttackResources(StarSystem system)
    {
        float result = 0;
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

    public static float GetTotalDefensiveResources(StarSystem system)
    {
        float result = 0;
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

    [
    HarmonyPatch(typeof(Contract), "CompleteContract")]
    public static class CompleteContract_Patch
    {
        public static void Postfix(Contract __instance, MissionResult result, bool isGoodFaithEffort)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            teamfaction = __instance.Override.employerTeam.faction;
            enemyfaction = __instance.Override.targetTeam.faction;
            difficulty = __instance.Difficulty;
            missionResult = result;
        }

        [HarmonyPatch(typeof(SimGameState), "ResolveCompleteContract")]
        public static class SimGameState_ResolveCompleteContract_Patch
        {
            public static void Postfix(SimGameState __instance)
            {
                var warsystem = WarStatus.systems.Find(x => x.name == __instance.CurSystem.Name);

                if (missionResult == MissionResult.Victory && teamfaction != Faction.AuriganPirates
                    && enemyfaction != Faction.AuriganPirates)
                {
                    warsystem.influenceTracker[teamfaction] += difficulty * Settings.DifficultyFactor;
                    warsystem.influenceTracker[enemyfaction] -= difficulty * Settings.DifficultyFactor;
                }
               
                var tempIT = new Dictionary<Faction, float>(warsystem.influenceTracker);
                var highKey = tempIT.OrderByDescending(x => x.Value).Select(x => x.Key).First();
                var highValue = tempIT.OrderByDescending(x => x.Value).Select(x => x.Value).First();
                tempIT.Remove(highKey);
                var secondValue = tempIT.OrderByDescending(x => x.Value).Select(x => x.Value).First();
                var oldOwner = warsystem.owner;

                if (highKey == teamfaction && highValue - secondValue > Settings.TakeoverThreshold && !Settings.DefensiveFactions.Contains(teamfaction)
                    && warsystem.starSystem.Owner != Faction.ComStar)
                {
                    ChangeSystemOwnership(__instance, warsystem.starSystem, teamfaction, false);

                    //This is a WIP for the pop-up after a system changes due to player interaction.

                    GameInstance game = LazySingletonBehavior<UnityGameInstance>.Instance.Game;
                    SimGameInterruptManager interruptQueue = (SimGameInterruptManager)AccessTools
                        .Field(typeof(SimGameState), "interruptQueue").GetValue(game.Simulation);
                    interruptQueue.QueueGenericPopup_NonImmediate("ComStar Bulletin: Galaxy at War", __instance.CurSystem.Name + " taken! "
                        + Settings.FactionNames[teamfaction] +" conquered from " + Settings.FactionNames[oldOwner], true, null);
                    var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                    __instance.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                    __instance.StopPlayMode();
                }
            }
        }
    }
    public static void SystemDifficulty()
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        var TotalSystems = WarStatus.systems.Count;
        var DifficultyCutoff = TotalSystems / 10;
        int i = 0;
        foreach (var system in WarStatus.systems.OrderBy(x => x.TotalResources))
        {
            var simSystem2 = sim.StarSystems.Find(x => x.Name == system.name);
            if (i <= DifficultyCutoff)
            { 
                system.DifficultyRating = 1;
                var SimSystem = sim.StarSystems.Find(x => x.Name == system.name);
                List<int> difficultyList = new List<int> { 1, 1 };
                Traverse.Create(SimSystem.Def).Field("DifficultyList").SetValue(difficultyList);
                Traverse.Create(SimSystem.Def).Field("DefaultDifficulty").SetValue(1);
            }
            if (i <= DifficultyCutoff * 2 && i > DifficultyCutoff)
            {
                system.DifficultyRating = 2;
                var SimSystem = sim.StarSystems.Find(x => x.Name == system.name);
                List<int> difficultyList = new List<int> { 2, 2 };
                Traverse.Create(SimSystem.Def).Field("DifficultyList").SetValue(difficultyList);
                Traverse.Create(SimSystem.Def).Field("DefaultDifficulty").SetValue(2);
            }
            if (i <= DifficultyCutoff * 3 && i > 2* DifficultyCutoff)
            {
                system.DifficultyRating = 3;
                var SimSystem = sim.StarSystems.Find(x => x.Name == system.name);
                List<int> difficultyList = new List<int> { 3, 3 };
                Traverse.Create(SimSystem.Def).Field("DifficultyList").SetValue(difficultyList);
                Traverse.Create(SimSystem.Def).Field("DefaultDifficulty").SetValue(3);
            }
            if (i <= DifficultyCutoff * 4 && i > 3 * DifficultyCutoff)
            {
                system.DifficultyRating = 4;
                var SimSystem = sim.StarSystems.Find(x => x.Name == system.name);
                List<int> difficultyList = new List<int> { 4, 4 };
                Traverse.Create(SimSystem.Def).Field("DifficultyList").SetValue(difficultyList);
                Traverse.Create(SimSystem.Def).Field("DefaultDifficulty").SetValue(4);
            }
            if (i <= DifficultyCutoff * 5 && i > 4 * DifficultyCutoff)
            {
                system.DifficultyRating = 5;
                var SimSystem = sim.StarSystems.Find(x => x.Name == system.name);
                List<int> difficultyList = new List<int> { 5, 5 };
                Traverse.Create(SimSystem.Def).Field("DifficultyList").SetValue(difficultyList);
                Traverse.Create(SimSystem.Def).Field("DefaultDifficulty").SetValue(5);
            }
            if (i <= DifficultyCutoff * 6 && i > 5 * DifficultyCutoff)
            {
                system.DifficultyRating = 6;
                var SimSystem = sim.StarSystems.Find(x => x.Name == system.name);
                List<int> difficultyList = new List<int> { 6, 6 };
                Traverse.Create(SimSystem.Def).Field("DifficultyList").SetValue(difficultyList);
                Traverse.Create(SimSystem.Def).Field("DefaultDifficulty").SetValue(6);
            }
            if (i <= DifficultyCutoff * 7 && i > 6 * DifficultyCutoff)
            {
                system.DifficultyRating = 7;
                var SimSystem = sim.StarSystems.Find(x => x.Name == system.name);
                List<int> difficultyList = new List<int> { 7, 7 };
                Traverse.Create(SimSystem.Def).Field("DifficultyList").SetValue(difficultyList);
                Traverse.Create(SimSystem.Def).Field("DefaultDifficulty").SetValue(7);
            }
            if (i <= DifficultyCutoff * 8 && i > 7 * DifficultyCutoff)
            {
                system.DifficultyRating = 8;
                var SimSystem = sim.StarSystems.Find(x => x.Name == system.name);
                List<int> difficultyList = new List<int> { 8, 8 };
                Traverse.Create(SimSystem.Def).Field("DifficultyList").SetValue(difficultyList);
                Traverse.Create(SimSystem.Def).Field("DefaultDifficulty").SetValue(8);
            }
            if (i <= DifficultyCutoff * 9 && i > 8 * DifficultyCutoff)
            {
                system.DifficultyRating = 9;
                var SimSystem = sim.StarSystems.Find(x => x.Name == system.name);
                List<int> difficultyList = new List<int> { 9, 9 };
                Traverse.Create(SimSystem.Def).Field("DifficultyList").SetValue(difficultyList);
                Traverse.Create(SimSystem.Def).Field("DefaultDifficulty").SetValue(9);
            }
            if (i > 9 * DifficultyCutoff)
            {
                system.DifficultyRating = 10;
                var SimSystem = sim.StarSystems.Find(x => x.Name == system.name);
                List<int> difficultyList = new List<int> { 10, 10 };
                Traverse.Create(SimSystem.Def).Field("DifficultyList").SetValue(difficultyList);
                Traverse.Create(SimSystem.Def).Field("DefaultDifficulty").SetValue(10);
            }
            i++;
        }
    }
    //82 characters per line.
    public static void GaW_Notification()
    {
        SimGameResultAction simGameResultAction = new SimGameResultAction();
        simGameResultAction.Type = SimGameResultAction.ActionType.System_ShowSummaryOverlay;
        simGameResultAction.value = Strings.T("Galaxy at War");
        simGameResultAction.additionalValues = new string[1];
        simGameResultAction.additionalValues[0] = Strings.T("In Galaxy at War, the Great Houses of the Innersphere will not simply wait for a wedding invitation" +
            " to show their disdain for each other. To that end, war will break out as petty bickering turns into all out conflict. Your reputation with the factions" +
            " is key - the more they like you, the more they'll bring you to the front lines and the greater the rewards. Perhaps an enterprising mercenary could make make" +
            " their fortune changing the tides of battle and helping a faction dominate the Inner Sphere.\n\n <b>New features in Galaxy at War:</b>" +
            "\n• Each planet generates Attack Resources and Defensive Resources that they will be constantly " +
            "spending to spread their influence and protect their own systems." +
            "\n• Planetary Resources and Faction Influence can be seen on the Star Map by hovering over any system." +
            "\n• Successfully completing missions will swing the influence towards the Faction granting the contract." +
            "\n• Factions that you have the highest reputation with will offer you travel expenses to go to their most " +
            "valuable offensive or defensive targets." +
            "\n• If you accept a travel contract the Faction will blockade the system for 30 days to allow you the opportunity to swing that system in their favor. A bonus will be granted for every mission you complete within that system during that time." +
            "\n• Sumire will flag the systems in purple on the Star Map that are the most valuable local targets." +
            "\n• Sumire will also highlight systems in yellow that have changed ownership during the previous month." +
            "\n• Hitting Control-R will bring up a summary of the Faction's relationships and their overall war status.");

        SimGameState.ApplyEventAction(simGameResultAction, null);
        UnityGameInstance.BattleTechGame.Simulation.StopPlayMode();
    }

    //internal static void WarSummary(string eventString)
    //{
    //    var simGame = UnityGameInstance.BattleTechGame.Simulation;
    //    var eventDef = new SimGameEventDef(
    //            SimGameEventDef.EventPublishState.PUBLISHED,
    //            SimGameEventDef.SimEventType.UNSELECTABLE,
    //            EventScope.Company,
    //            new DescriptionDef(
    //                "SalvageOperationsEventID",
    //                "Salvage Operations",
    //                eventString,
    //                "uixTxrSpot_YangWorking.png",
    //                0, 0, false, "", "", ""),
    //            new RequirementDef { Scope = EventScope.Company },
    //            new RequirementDef[0],
    //            new SimGameEventObject[0],
    //            null, 1, false);


    //    var eventTracker = new SimGameEventTracker();
    //    eventTracker.Init(new[] { EventScope.Company }, 0, 0, SimGameEventDef.SimEventType.NORMAL, simGame);
    //    simGame.InterruptQueue.QueueEventPopup(eventDef, EventScope.Company, eventTracker);


    //}


}
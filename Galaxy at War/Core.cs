using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BattleTech;
using Harmony;
using Newtonsoft.Json;
using static Logger;
using Random = System.Random;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

public class Core
{
    #region Init

    public static void Init(string modDir, string settings)
    {
        var harmony = HarmonyInstance.Create("ca.gnivler.BattleTech.DevMod");
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

                // TODO if it deserializes something invalid, handle it

                //foreach (var faction in sim.FactionsDict.Keys)
                //    LogPotentialTargets(faction);
            }

            // first time setup if not
            if (WarStatus == null)
            {
                LogDebug(">>> First-time initialization");

                //This generates the initial distribution of Influence amongst the systems.
                WarStatus = new WarStatus();
                InitializeSystems(sim);

                //foreach (var faction in sim.FactionsDict.Keys)
                //    LogPotentialTargets(faction);
            }

            if (WarStatus.systems.Count == 0)
            {
                LogDebug("NO SYSTEMS BUT NOT NULL");
                try
                {
                    InitializeSystems(sim);
                }
                catch (Exception ex)
                {
                    LogDebug("WarStatus.systems.Count == 0");
                    Error(ex);
                }
            }

            // if (sim.DayRemainingInQuarter % Settings.WarFrequency != 0)
            //     return;

            LogDebug(">>> PROC");

            // Proc effects

            //Add resources for adjacent systems
            RefreshResources(__instance);
               
            var rand = new Random();
            foreach (var systemStatus in WarStatus.systems)
            {

                try
                {
                    systemStatus.warFaction.attackTargets.Clear();
                    systemStatus.warFaction.defenseTargets.Clear();
                    systemStatus.neighborSystems.Clear();
                }
                catch // silent drop
                {
                }

                systemStatus.CalculateAttackTargets();
                systemStatus.CalculateDefenseTargets();
                systemStatus.FindNeighbors();

                foreach (var neighbor in systemStatus.neighborSystems)
                {
                    var PushFactor = Settings.APRPush * rand.Next(1, Settings.APRPushRandomizer + 1);
                    if (systemStatus.influenceTracker.ContainsKey(neighbor.Key))
                        systemStatus.influenceTracker[neighbor.Key] += neighbor.Value * PushFactor;
                    else
                        systemStatus.influenceTracker.Add(neighbor.Key, neighbor.Value * PushFactor);
                }
            }

            foreach (var systemStatus in WarStatus.systems)
            {
                if (systemStatus.warFaction?.attackTargets.Count > 0)
                {
                    // attacking
                    foreach (var faction in systemStatus.warFaction?.attackTargets.Keys)
                        AllocateAttackResources(faction);
                }

                if (systemStatus.warFaction?.defenseTargets.Count > 0)
                {
                    // defending
                    foreach (var faction in systemStatus.warFaction?.attackTargets.Keys)
                        AllocateDefensiveResources(faction);
                }
            }

            UpdateInfluenceFromAttacks(sim);

            //Increase War Escalation of decay defenses.
            foreach (var warfaction in WarStatus.warFactionTracker)
            {
                if (Settings.GainedSystem.Contains(warfaction.faction))
                    warfaction.DaysSinceSystemAttacked += 1;

                if (Settings.LostSystem.Contains(warfaction.faction))
                    warfaction.DaysSinceSystemLost += 1;
            }

            Settings.GainedSystem.Clear();
            Settings.LostSystem.Clear();

            SaveHandling.SerializeWar();
            LogDebug(">>> DONE PROC");
        }

        public static void InitializeSystems(SimGameState sim)
        {
            LogDebug(">>> Initialize systems");
            foreach (var starSystem in sim.StarSystems)
            {
                LogDebug("Add system " + starSystem.Name);
                WarStatus.systems.Add(new SystemStatus(sim, starSystem.Name));
            }

            foreach (var starSystem in WarStatus.systems)
            {
                starSystem.FindNeighbors();
                starSystem.CalculateSystemInfluence();
            }
        }

        // TODO refactor to make it compile
        //public static void LogPotentialTargets(Faction faction)
        //{
        //    if (WarStatus.warFactionTracker.First(x=> x.faction == faction).attackTargets.Keys.Contains(faction))
        //    {
        //        LogDebug("------------------------------------------------------");
        //        LogDebug(faction.ToString());
        //        LogDebug("Attack Targets");
        //        foreach (StarSystem attackedSystem in WarStatus.warFactionTracker.First(x=> x.faction == faction).attackTargets[faction])
        //            LogDebug($"{attackedSystem.Name,-30} : {attackedSystem.Owner}");
        //    }
        //    else
        //    {
        //        LogDebug($"No Attack Targets for {faction.ToString()}!");
        //    }
        //
        //    if (WarStatus.warFactionTracker.First(x=> x.faction == faction).defenseTargets.Contains(faction))
        //    {
        //        LogDebug("------------------------------------------------------");
        //        LogDebug(faction.ToString());
        //        LogDebug("Defense Targets");
        //        foreach (StarSystem defensedsystem in WarStatus.warFactionTracker.First(x=> x.faction == faction).defenseTargets[faction])
        //            LogDebug($"{defensedsystem.Name,-30} : {defensedsystem.Owner}");
        //    }
        //    else
        //    {
        //        LogDebug($"No Defense Targets for {faction.ToString()}!");
        //    }
        //}

        public static void DivideAttackResources(SimGameState sim, Faction faction)
        {
            var uniqueFactions = new Dictionary<Faction, float>();
            foreach (var systemStatus in WarStatus.systems)
            {
                foreach (var attackSystem in systemStatus.warFaction?.attackTargets[faction])
                    if (!uniqueFactions.ContainsKey(attackSystem.Owner))
                        uniqueFactions.Add(attackSystem.Owner, 0f);

                RefreshResources(sim);
                var deathList = WarStatus.deathListTracker.Find(x => x.faction == faction).deathList;
                var warFaction = WarStatus.warFactionTracker.Find(x => x.faction == faction);
                var attackResources = warFaction.AttackResources;
                var total = deathList.Values.Sum();

                var tempDict = new Dictionary<Faction, float>();
                foreach (var tempfaction in uniqueFactions.Keys)
                {
                    if (!tempDict.ContainsKey(tempfaction))
                        tempDict.Add(tempfaction, 0);

                    if (deathList.ContainsKey(tempfaction))
                        tempDict[tempfaction] = deathList[tempfaction] * attackResources / total;
                }

                warFaction.warFactionAttackResources = tempDict;
            }
        }

        public static void AllocateAttackResources(Faction faction)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            DivideAttackResources(sim, faction);
            var random = new Random();
            LogDebug("AllocateAttackResources faction: " + faction);

            var attackResources = WarStatus.FindWarFactionResources(faction);
            if (attackResources == null)
            {
                LogDebug("attackResources null " + faction);
                return;
            }

            if (!attackResources.ContainsKey(faction)) return;
            Log(attackResources.ContainsKey(faction).ToString());

            //Go through the different resources allocated from attacking faction to spend against each targetFaction
            foreach (var targetFaction in WarStatus.FindWarFactionResources(faction))
            {
                var attacklist = new List<StarSystem>();
                //Generate the list of all systems being attacked by faction and pulls out the ones that match the targetFaction
                foreach (var systemStatus in WarStatus.systems)
                {
                    foreach (var system in systemStatus.warFaction.attackTargets[faction])

                    {
                        //if (Globals.attackResources[faction].ContainsKey(system.Owner))
                        if (WarStatus.FindWarFactionResources(faction).ContainsKey(system.Owner))
                            attacklist.Add(system);
                    }

                    //Allocate all the resources against the targetFaction to systems controlled by targetFaction.
                    var i = 0;
                    do
                    {
                        var rand = random.Next(0, attacklist.Count);
                        var system = WarStatus.systems.First(f => f.name == attacklist[rand].Name);
                        system.influenceTracker[faction] += 1;
                        i++;
                    } while (i < WarStatus.FindWarFactionResources(faction)[targetFaction.Key]);
                }
            }
        }

        public static void AllocateDefensiveResources(Faction faction)
        {
            var random = new Random();
            if (WarStatus.warFactionTracker.Find(x => x.faction == faction) == null)
                return;

            WarFaction warFaction = WarStatus.warFactionTracker.Find(x => x.faction == faction);
            var DefensiveResources = warFaction.DefensiveResources;

            while (DefensiveResources > 0)
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
                    systemStatus.influenceTracker[faction] += 1;
                    DefensiveResources -= 1;
                }
                else
                {
                    var diffRes = systemStatus.influenceTracker[highestFaction] - systemStatus.influenceTracker[faction];
                    if (diffRes >= DefensiveResources)
                    {
                        systemStatus.influenceTracker[faction] += DefensiveResources;
                        DefensiveResources = 0;
                    }
                    else
                    {
                        systemStatus.influenceTracker[faction] += diffRes;
                        DefensiveResources -= diffRes;
                    }
                }
            }
        }

        public static void ChangeSystemOwnership(SimGameState sim, StarSystem system, Faction faction, bool ForceFlip)
        {
            if (faction != system.Owner || ForceFlip)
            {
                Faction OldFaction = system.Owner;

                system.Def.Tags.Remove(Settings.FactionTags[system.Owner]);
                system.Def.Tags.Add(Settings.FactionTags[faction]);
                system.Def.SystemShopItems.Add(Settings.FactionShops[faction]);
                if (system.Def.FactionShopItems != null)
                {
                    Traverse.Create(system.Def).Property("FactionShopOwner").SetValue(faction);
                    system.Def.FactionShopItems.Remove(Settings.FactionShopItems[system.Def.Owner]);
                    system.Def.FactionShopItems.Add(Settings.FactionShopItems[faction]);
                }

                system.Def.ContractEmployers.Clear();
                system.Def.ContractTargets.Clear();
                List<Faction> ContractEmployers = new List<Faction>();
                ContractEmployers.Add(system.Owner);

                foreach (var systemNeighbor in sim.Starmap.GetAvailableNeighborSystem(system))
                {
                    if (!ContractEmployers.Remove(systemNeighbor.Owner))
                        ContractEmployers.Add(systemNeighbor.Owner);
                }

                Traverse.Create(system.Def).Property("ContractEmployers").SetValue(ContractEmployers);

                if (ContractEmployers.Count() > 1)
                {
                    Traverse.Create(system.Def).Property("ContractTargets").SetValue(ContractEmployers);
                }
                else
                {
                    // TODO, not sure this generates intended result (a list of enemies?)
                    FactionDef FactionEnemies;
                    FactionEnemies = sim.FactionsDict[faction];

                    Traverse.Create(system.Def).Property("ContractTargets").SetValue(FactionEnemies.Enemies.ToList());
                }

                var systemStatus = WarStatus.systems.Find(x => x.name == system.Name);
                var oldOwner = systemStatus.owner;
                systemStatus.owner = faction;
                Traverse.Create(system.Def.Owner).Property("Owner").SetValue(faction);

                //Change the Kill List for the factions.
                var SystemValue = GetTotalResources(system) + GetTotalDefensiveResources(system);
                var KillListDelta = Math.Max(10, SystemValue);
                //WarStatus.factionTracker.Find(x=> x.faction == faction)

                var factionTracker = WarStatus.deathListTracker.Find(x => x.faction == system.Owner);
                if (factionTracker.deathList[faction] < 75)
                    factionTracker.deathList[faction] = 75;

                factionTracker.deathList[faction] += KillListDelta;

                //Allies are upset that their friend is being beaten up.

                foreach (var ally in sim.FactionsDict[OldFaction].Allies)
                {
                    var factionAlly = WarStatus.deathListTracker.Find(x => x.faction == ally);
                    factionAlly.deathList[ally] += KillListDelta / 2;
                }

                //Enemies of the target faction are happy with the faction doing the beating.
                foreach (var enemy in sim.FactionsDict[OldFaction].Enemies)
                {
                    var factionEnemy = WarStatus.deathListTracker.Find(x => x.faction == enemy);
                    factionEnemy.deathList[faction] -= KillListDelta / 2;
                }

                factionTracker.AttackedBy.Add(faction);

                Settings.LostSystem.Add(OldFaction);
                Settings.GainedSystem.Add(faction);
            }
        }

        private static void UpdateInfluenceFromAttacks(SimGameState sim)
        {
            LogDebug($"Updating influence for {WarStatus.systems.Count().ToString()} systems");
            foreach (var systemStatus in WarStatus.systems)
            {
                var tempDict = new Dictionary<Faction, float>();
                var totalInfluence = systemStatus.influenceTracker.Values.Sum();
                var highest = 0f;
                var highestfaction = systemStatus.owner;
                Log($"Attacking status for {systemStatus.name}");
                foreach (var kvp in systemStatus.influenceTracker)
                {
                    tempDict.Add(kvp.Key, kvp.Value / totalInfluence * 100);
                    Log($"{kvp.Key}: {tempDict[kvp.Key]}");
                    if (kvp.Value > highest)
                    {
                        highest = kvp.Value;
                        highestfaction = kvp.Key;
                    }
                }

                systemStatus.influenceTracker = tempDict;
                var diffStatus = systemStatus.influenceTracker[highestfaction] - systemStatus.influenceTracker[systemStatus.owner];
                //Need to add changes to the Kill List. Here is a good spot. 
                if (highestfaction != systemStatus.owner && (diffStatus >= Settings.TakeoverThreshold))
                {
                    var previousOwner = systemStatus.owner;
                    var starSystem = sim.StarSystems.Find(x => x.Name == systemStatus.name);
                    if (starSystem != null)
                        ChangeSystemOwnership(sim, starSystem, highestfaction, true);

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
                        Log("TWO");
                        Error(ex);
                    }
                }
            }

            var tempRTFactions = WarStatus.deathListTracker;
            foreach (var deathListTracker in tempRTFactions)
            {
                Log(deathListTracker.faction.ToString());
                AdjustDeathList(deathListTracker, sim);
                deathListTracker.AttackedBy.Clear();
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

        public static int GetTotalResources(StarSystem system)
        {
            int result = 0;
            if (system.Tags.Contains("planet_industry_poor"))
                result += Settings.planet_industry_poor;
            if (system.Tags.Contains("planet_industry_mining"))
                result += Settings.planet_industry_mining;
            if (system.Tags.Contains("planet_industry_rich"))
                result += Settings.planet_industry_rich;
            if (system.Tags.Contains("planet_other_comstar"))
                result += Settings.planet_other_comstar;
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
            return result;
        }

        public static void RefreshResources(SimGameState sim)
        {
            // no point iterating over a KVP if you aren't using the values
            //LogDebug($"Object size: {JsonConvert.SerializeObject(Core.WarStatus).Length / 1024}kb");
            foreach (var faction in sim.FactionsDict.Select(x => x.Key).Except(Settings.ExcludedFactions))
            {
                //Log(faction.ToString());
                if (Settings.ResourceMap.ContainsKey(faction))
                {
                    // initialize resources from the ResourceMap
                    if (WarStatus.warFactionTracker.Find(x => x.faction == faction) == null)
                    {
                        var StartingResources = Settings.ResourceMap[faction];
                        var DefensiveStartingResources = Settings.DefensiveResourceMap[faction];
                        WarStatus.warFactionTracker.Add(new WarFaction(faction, StartingResources, DefensiveStartingResources));
                    }
                    else
                    {
                        WarFaction warFaction = WarStatus.warFactionTracker.Find(x => x.faction == faction);
                        if (warFaction != null)
                        {
                            warFaction.AttackResources = Settings.ResourceMap[faction];
                            warFaction.DefensiveResources = Settings.DefensiveResourceMap[faction];
                        }
                        else
                            Log($"warFaction {faction} was null");
                    }
                }
            }

            var random = new Random();
            foreach (var system in sim.StarSystems)
            {
                var resources = GetTotalResources(system);
                var DefensiveResources = GetTotalDefensiveResources(system);
                var owner = system.Owner;
                Log($"{system.Name + ":",-20} {owner,-20}, total resources: {resources}, total defensive resources: {DefensiveResources}");
                try
                {
                    var faction = WarStatus.warFactionTracker.Where(x => x != null).FirstOrDefault(x => x.faction == owner);
                    if (faction != null)
                    {
                        faction.AttackResources += resources;
                        faction.DefensiveResources += DefensiveResources;
                    }
                }
                catch (Exception ex)
                {
                    Error(ex);
                }
            }

            foreach (var faction in WarStatus.warFactionTracker)
            {
                float tempnum = 0f;
                int i = 0;
                do
                {
                    tempnum += random.Next(1, Settings.ResourceRandomizer + 1);
                    i++;
                } while (i < faction.AttackResources);

                faction.AttackResources = tempnum * (100f + faction.DaysSinceSystemAttacked * Settings.ResourceAdjustmentPerCycle) / 100f;

                tempnum = 0f;
                i = 0;
                do
                {
                    tempnum += random.Next(1, Settings.ResourceRandomizer + 1);
                    i++;
                } while (i < faction.DefensiveResources);

                faction.DefensiveResources = tempnum * (100f * Settings.GlobalDefenseFactor
                                                        - faction.DaysSinceSystemLost * Settings.ResourceAdjustmentPerCycle) / 100f;

                Log($"Faction: {faction.faction}, Attack Resources: {faction.AttackResources}, " +
                    $"Defensive Resources: {faction.DefensiveResources}");
            }
        }

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
                UpdateInfluenceFromAttacks(sim);
            }
        }
    }
}
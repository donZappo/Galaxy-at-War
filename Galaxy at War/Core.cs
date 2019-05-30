using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BattleTech;
using Harmony;
using HBS.Util;
using Newtonsoft.Json;
using UnityEngine;
using static Logger;
using Random = System.Random;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

namespace GalaxyAtWar
{
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
            PrintObjectFields(Settings, "Settings");
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
                if (sim == null)
                {
                    Log("WTAF");
                    return;
                }

                // already have a save?
                var fileName = $"WarStatus_{sim.InstanceGUID}.json";

                //if (WarStatus == null && sim.CompanyTags.Any(x => x.StartsWith("GalaxyAtWarSave{")))
                if (WarStatus == null && File.Exists("Mods\\GalaxyAtWar\\" + fileName))
                {
                    LogDebug(">>> Loading " + fileName);
                    SaveHandling.DeserializeWar();

                    WarStatus.attackTargets.Clear();
                    WarStatus.defenseTargets.Clear();

                    foreach (var faction in sim.FactionsDict.Keys)
                        LogPotentialTargets(faction);
                }

                //// first time setup if not
                if (WarStatus == null)
                {
                    LogDebug(">>> First-time initialization");

                    //This generates the initial distribution of Influence amongst the systems.
                    WarStatus = new WarStatus();
                    InitializeSystems(sim);

                    foreach (var faction in sim.FactionsDict.Keys)
                        LogPotentialTargets(faction);
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
                if (WarStatus.systems.Count > 0)
                    foreach (var system in WarStatus.systems)
                    {
                        try
                        {
                            //LogDebug("Calculating neighbors for " + system.name);
                            StaticMethods.CalculateNeighbours(sim, system.name);
                        }
                        catch (Exception ex)
                        {
                            LogDebug("CALC NEIGHBORS");
                            Error(ex);
                        }
                    }
                else
                    LogDebug("NO SYSTEMS FOUND");

                //Add resources for adjacent systems
                var rand = new Random();
                try
                {
                    foreach (var system in WarStatus.systems)
                    {
                        //Log($"\n\n{system.name}");
                        foreach (var neighbor in WarStatus.neighborSystems)
                        {
                            var PushFactor = Settings.APRPush * rand.Next(1, Settings.APRPushRandomizer + 1);
                            //Log(neighbor.Key.ToString());
                            //Log("Dictionary:");
                            //foreach (var kvp in system.influenceTracker)
                            //    Log($"\t{kvp.Key}: {kvp.Value}");
                            //Log(system.influenceTracker.ContainsKey(neighbor.Key).ToString());

                            if (system.influenceTracker.ContainsKey(neighbor.Key))
                                system.influenceTracker[neighbor.Key] += neighbor.Value * PushFactor;
                            else
                                system.influenceTracker.Add(neighbor.Key, neighbor.Value * PushFactor);
                        }

                        Log($"\n{system.name} influenceTracker:");
                        //system.influenceTracker.Do(x => Log($"{x.Key.ToString()} {x.Value}"));
                    }
                }

                catch (Exception ex)
                {
                    LogDebug("\n2");
                    Error(ex);
                }

                //Log("Refreshing Resources");
                try
                {
                    RefreshResources(__instance);
                }
                catch (Exception ex)
                {
                    LogDebug("3");
                    Error(ex);
                }

                try
                {
                    LogDebug($"WarStatus.attackTargets {WarStatus.attackTargets.Count}");
                    if (WarStatus.attackTargets.Count > 0)
                    {
                        // attacking
                        foreach (var faction in WarStatus.attackTargets.Keys)
                            AllocateAttackResources(faction);

                        //defending
                        foreach (var faction in WarStatus.attackTargets.Keys)
                            AllocateDefensiveResources(faction);
                    }
                }
                catch (Exception ex)
                {
                    LogDebug("5");
                    Error(ex);
                }

                try
                {
                    UpdateInfluenceFromAttacks(sim);
                }
                catch (Exception ex)
                {
                    LogDebug("6");
                    Error(ex);
                }

                //Increase War Escalation of decay defenses.
                foreach (var warfaction in WarStatus.factionTracker)
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
        }

        public static void InitializeSystems(SimGameState sim)
        {
            LogDebug(">>> Initialize systems");
            foreach (var starSystem in sim.StarSystems)
                WarStatus.systems.Add(new SystemStatus(sim, starSystem.Name));

            foreach (var starSystem in WarStatus.systems)
            {
                StaticMethods.CalculateNeighbours(sim, starSystem.name);
                StaticMethods.DistributeInfluence(starSystem.influenceTracker, starSystem.owner, starSystem.name);
                StaticMethods.CalculateAttackTargets(sim, starSystem.name);
                StaticMethods.CalculateDefenseTargets(sim, starSystem.name);
            }
        }

        public static void LogPotentialTargets(Faction faction)
        {
            if (WarStatus.attackTargets.Keys.Contains(faction))
            {
                Log("------------------------------------------------------");
                Log(faction.ToString());
                Log("Attack Targets");
                foreach (StarSystem attackedSystem in WarStatus.attackTargets[faction])
                    Log($"{attackedSystem.Name,-30} : {attackedSystem.Owner}");
            }
            else
            {
                Log($"No Attack Targets for {faction.ToString()}!");
            }

            if (WarStatus.defenseTargets.Keys.Contains(faction))
            {
                Log("------------------------------------------------------");
                Log(faction.ToString());
                Log("Defense Targets");
                foreach (StarSystem defensedsystem in WarStatus.defenseTargets[faction])
                    Log($"{defensedsystem.Name,-30} : {defensedsystem.Owner}");
            }
            else
            {
                Log($"No Defense Targets for {faction.ToString()}!");
            }
        }

        public static void DivideAttackResources(SimGameState sim, Faction faction)
        {
            Dictionary<Faction, float> uniqueFactions = new Dictionary<Faction, float>();
            foreach (StarSystem attackSystem in WarStatus.attackTargets[faction])
            {
                if (!uniqueFactions.ContainsKey(attackSystem.Owner))
                    uniqueFactions.Add(attackSystem.Owner, 0f);
            }

            RefreshResources(sim);
            var killList = WarStatus.relationTracker.factions.First(f => f.faction == faction).deathList;
            WarFaction warFaction = WarStatus.factionTracker.Find(x => x.faction == faction);
            float resources = warFaction.resources;

            float total = uniqueFactions.Values.Sum();
            foreach (Faction tempfaction in uniqueFactions.Keys)
            {
                uniqueFactions[tempfaction] = killList[tempfaction] * resources / total;
            }

            WarStatus.attackResources.Add(faction, uniqueFactions);
        }

        public static void AllocateAttackResources(Faction faction)
        {
            var random = new Random();
            Log("AllocateAttackResources faction: " + faction);
            try
            {
                if (!WarStatus.attackResources.ContainsKey(faction)) return;
                Log(WarStatus.attackResources.ContainsKey(faction).ToString());

                //Go through the different resources allocated from attacking faction to spend against each targetfaction
                foreach (var targetfaction in WarStatus.attackResources[faction].Keys)
                {
                    var attacklist = new List<StarSystem>();

                    //Generate the list of all systems being attacked by faction and pulls out the ones that match the targetfaction
                    foreach (var system in WarStatus.attackTargets[faction])
                        if (WarStatus.attackResources[faction].ContainsKey(system.Owner))
                            attacklist.Add(system);

                    //Allocate all the resources against the targetfaction to systems controlled by targetfaction.
                    var i = 0;
                    do
                    {
                        var rand = random.Next(0, attacklist.Count);
                        var systemStatus = WarStatus.systems.First(f => f.name == attacklist[rand].Name);
                        systemStatus.influenceTracker[faction] += 1;
                    } while (i < WarStatus.attackResources[faction][targetfaction]);
                }
            }
            catch (Exception ex)
            {
                Error(ex);
                Application.Quit();
            }
        }

        public static void AllocateDefensiveResources(Faction faction)
        {
            var random = new Random();
            if (WarStatus.factionTracker.Find(x => x.faction == faction) == null)
                return;
            WarFaction warFaction = WarStatus.factionTracker.Find(x => x.faction == faction);

            var DefensiveResources = warFaction.DefensiveResources;
            if (!WarStatus.defenseTargets.ContainsKey(faction))
                return;

            var systems = WarStatus.defenseTargets[faction];

            // TODO fix so != 0
            while (DefensiveResources > 0)
            {
                float highest = 0f;
                Faction highestFaction = faction;
                var rand = random.Next(0, systems.Count());
                var systemStatus = WarStatus.systems.First(f => f.name == systems[rand].Name);

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

                //Log($"DefensiveResources: {DefensiveResources}");
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
                    if (!ContractEmployers.Contains(systemNeighbor.Owner))
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
                var factionTracker = WarStatus.relationTracker.factions.Find(x => x.faction == oldOwner);
                if (factionTracker.deathList[faction] < 75)
                    factionTracker.deathList[faction] = 75;

                factionTracker.deathList[faction] += KillListDelta;

                //Allies are upset that their friend is being beaten up.
                foreach (var ally in sim.FactionsDict[OldFaction].Allies)
                {
                    var factionAlly = WarStatus.relationTracker.factions.Find(x => x.faction == ally);
                    factionAlly.deathList[ally] += KillListDelta / 2;
                }

                //Enemies of the target faction are happy with the faction doing the beating.
                foreach (var enemy in sim.FactionsDict[OldFaction].Enemies)
                {
                    var factionEnemy = WarStatus.relationTracker.factions.Find(x => x.faction == enemy);
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
            foreach (var systemstatus in WarStatus.systems)
            {
                var tempDict = new Dictionary<Faction, float>();
                var totalInfluence = systemstatus.influenceTracker.Values.Sum();
                var highest = 0f;
                var highestfaction = systemstatus.owner;
                Log($"Attacking status for {systemstatus.name}");
                foreach (var kvp in systemstatus.influenceTracker)
                {
                    tempDict.Add(kvp.Key, kvp.Value / totalInfluence * 100);
                    Log($"{kvp.Key}: {tempDict[kvp.Key]}");
                    if (kvp.Value > highest)
                    {
                        highest = kvp.Value;
                        highestfaction = kvp.Key;
                    }
                }

                systemstatus.influenceTracker = tempDict;
                var diffStatus = systemstatus.influenceTracker[highestfaction] - systemstatus.influenceTracker[systemstatus.owner];
                //Need to add changes to the Kill List. Here is a good spot. 
                if (highestfaction != systemstatus.owner && (diffStatus >= Settings.TakeoverThreshold))
                {
                    var previousOwner = systemstatus.owner;
                    var starSystem = sim.StarSystems.Find(x => x.Name == systemstatus.name);
                    if (starSystem != null)
                    {
                        ChangeSystemOwnership(sim, starSystem, highestfaction, true);
                    }
                    else
                    {
                        Log("+=======+++== NULL");
                    }

                    LogDebug(">>> Ownership changed to " + highestfaction);
                    if (highestfaction == Faction.NoFaction || highestfaction == Faction.Locals)
                    {
                        LogDebug("\tNoFaction or Locals, continuing");
                        continue;
                    }

                    // BUG NRE on deserialization
                    var WarFactionWinner = WarStatus.factionTracker.Find(x => x.faction == highestfaction);
                    if (WarFactionWinner != null)
                        WarFactionWinner.DaysSinceSystemAttacked = 0;

                    try
                    {
                        var WarFactionLoser = WarStatus.factionTracker.Find(x => x.faction == previousOwner);
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

            var tempRTFactions = WarStatus.relationTracker.factions;
            foreach (var deathListTracker in tempRTFactions)
            {
                Log(deathListTracker.faction.ToString());
                AdjustKillList(deathListTracker, sim);
                deathListTracker.AttackedBy.Clear();
            }
        }

        public static void AdjustKillList(DeathListTracker deathListTracker, SimGameState sim)
        {
            var KillList = deathListTracker.deathList;
            var KL_List = new List<Faction>(KillList.Keys);

            var KillListFaction = deathListTracker.faction;
            foreach (Faction faction in KL_List)
            {
                //Factions go towards peace over time if not attacked.But there is diminishing returns further from 50.
                if (!deathListTracker.AttackedBy.Contains(faction))
                {
                    if (KillList[faction] > 50)
                        KillList[faction] -= 1 - (KillList[faction] - 50) / 50;
                    else if (KillList[faction] <= 50)
                        KillList[faction] -= 1 - (50 - KillList[faction]) / 50;
                }

                //Ceiling and floor for faction enmity. 
                if (KillList[faction] > 99)
                    KillList[faction] = 99;

                if (KillList[faction] < 1)
                    KillList[faction] = 1;

                if (KillList[faction] > 75)
                {
                    if (!sim.FactionsDict[KillListFaction].Enemies.Contains(faction))
                    {
                        var enemies = new List<Faction>(sim.FactionsDict[KillListFaction].Enemies);
                        enemies.Add(faction);
                        Traverse.Create(sim.FactionsDict[KillListFaction]).Property("Enemies").SetValue(enemies.ToArray());
                    }

                    if (sim.FactionsDict[KillListFaction].Allies.Contains(faction))
                    {
                        var allies = new List<Faction>(sim.FactionsDict[KillListFaction].Allies);
                        allies.Remove(faction);
                        Traverse.Create(sim.FactionsDict[KillListFaction]).Property("Allies").SetValue(allies.ToArray());
                    }
                }

                if (KillList[faction] <= 75 && KillList[faction] > 25)
                {
                    if (sim.FactionsDict[KillListFaction].Enemies.Contains(faction))
                    {
                        var enemies = new List<Faction>(sim.FactionsDict[KillListFaction].Enemies);
                        enemies.Remove(faction);
                        Traverse.Create(sim.FactionsDict[KillListFaction]).Property("Enemies").SetValue(enemies.ToArray());
                    }

                    if (sim.FactionsDict[KillListFaction].Allies.Contains(faction))
                    {
                        var allies = new List<Faction>(sim.FactionsDict[KillListFaction].Allies);
                        allies.Remove(faction);
                        Traverse.Create(sim.FactionsDict[KillListFaction]).Property("Allies").SetValue(allies.ToArray());
                    }
                }

                if (KillList[faction] <= 25)
                {
                    if (!sim.FactionsDict[KillListFaction].Allies.Contains(faction))
                    {
                        var allies = new List<Faction>(sim.FactionsDict[KillListFaction].Allies);
                        allies.Add(faction);
                        Traverse.Create(sim.FactionsDict[KillListFaction]).Property("Allies").SetValue(allies.ToArray());
                    }

                    if (sim.FactionsDict[KillListFaction].Enemies.Contains(faction))
                    {
                        var enemies = new List<Faction>(sim.FactionsDict[KillListFaction].Enemies);
                        enemies.Remove(faction);
                        Traverse.Create(sim.FactionsDict[KillListFaction]).Property("Enemies").SetValue(enemies.ToArray());
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
            foreach (var faction in sim.FactionsDict.Select(x => x.Key).Except(Settings.ExcludedFactions))
            {
                //Log(faction.ToString());
                if (Settings.ResourceMap.ContainsKey(faction))
                {
                    // initialize resources from the ResourceMap
                    if (WarStatus.factionTracker.Find(x => x.faction == faction) == null)
                    {
                        var StartingResources = Settings.ResourceMap[faction];
                        var DefensiveStartingResources = Settings.DefensiveResourceMap[faction];
                        WarStatus.factionTracker.Add(new WarFaction(faction, StartingResources, DefensiveStartingResources));
                    }
                    else
                    {
                        WarFaction warFaction = WarStatus.factionTracker.Find(x => x.faction == faction);
                        if (warFaction != null)
                        {
                            warFaction.resources = Settings.ResourceMap[faction];
                            warFaction.DefensiveResources = Settings.DefensiveResourceMap[faction];
                        }
                        else
                            Log($"warFaction {faction} was null");
                    }
                }
            }

            if (sim.Starmap == null)
            {
                LogDebug("wHAAAAAAAAaat");
                return;
            }

            Random random = new Random();
            foreach (var system in sim.StarSystems)
            {
                var resources = GetTotalResources(system);
                var DefensiveResources = GetTotalDefensiveResources(system);
                var owner = system.Owner;
                Log($"{system.Name + ":",-20} {owner,-20}, total resources: {resources}, total defensive resources: {DefensiveResources}");
                try
                {
                    var faction = WarStatus.factionTracker.Where(x => x != null).FirstOrDefault(x => x.faction == owner);
                    if (faction != null)
                    {
                        faction.resources += resources;
                        faction.DefensiveResources += DefensiveResources;
                    }
                }
                catch (Exception ex)
                {
                    Error(ex);
                }
            }

            foreach (var faction in WarStatus.factionTracker)
            {
                float tempnum = 0f;
                int i = 0;
                do
                {
                    tempnum += random.Next(1, Settings.ResourceRandomizer + 1);
                    i++;
                } while (i < faction.resources);

                faction.resources = tempnum * (100f + faction.DaysSinceSystemAttacked * Settings.ResourceAdjustmentPerCycle) / 100f;

                tempnum = 0f;
                i = 0;
                do
                {
                    tempnum += random.Next(1, Settings.ResourceRandomizer + 1);
                    i++;
                } while (i < faction.DefensiveResources);

                faction.DefensiveResources = tempnum * (100f * Settings.GlobalDefenseFactor
                                                        - faction.DaysSinceSystemLost * Settings.ResourceAdjustmentPerCycle) / 100f;

                Log($"Faction: {faction.faction}, Attack Resources: {faction.resources}, " +
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

//try
//{
//    foreach (KeyValuePair<Faction, FactionDef> pair in Sim.FactionsDict)
//    {
//        {
//            if (Fields.factionResources.Find(x => x.faction == pair.Key) == null)
//            {
//                Fields.factionResources.Add(new FactionResources(pair.Key, 0, 0));
//            }
//            else
//            {
//                FactionResources resources = Fields.factionResources.Find(x => x.faction == pair.Key);
//                resources.offence = 0;
//                resources.defence = 0;
//            }
//        }
//    }
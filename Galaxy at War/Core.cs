using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using BattleTech;
using Harmony;
using Newtonsoft.Json;
using UnityEngine;
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
    public static WarProgress WarProgress;

    [HarmonyPatch(typeof(SimGameState), "OnDayPassed")]
    public static class SimGameState_OnDayPassed_Patch
    {
        internal static readonly SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

        static void Prefix(SimGameState __instance, int timeLapse)
        {
            // already have a save?
            var fileName = $"WarStatus_{sim.InstanceGUID}.json";

            if (WarStatus == null && File.Exists("Mods\\GalaxyAtWar\\" + fileName))
            {
                Log(">>> Loading WarStatus.json");
                //WarStatus WarStatus = new WarStatus(false, false);
                SaveHandling.DeserializeWar();

                WarStatus.attackTargets.Clear();
                WarStatus.defenseTargets.Clear();

                //var progress = new WarStatus(false, false);
                WarProgress = new WarProgress();
                foreach (Faction faction in sim.FactionsDict.Keys)
                    WarProgress.PotentialTargets(faction);
            }

            // first time setup if not
            if (WarStatus == null)
            {
                Log(">>> First-time initialization");
                WarStatus = new WarStatus(true);

                WarStatus.attackTargets.Clear();
                WarStatus.defenseTargets.Clear();
                WarProgress = new WarProgress();
                foreach (Faction faction in sim.FactionsDict.Keys)
                    WarProgress.PotentialTargets(faction);
            }

            if (sim.DayRemainingInQuarter % Settings.WarFrequency != 0)
                return;

            Log(">>> PROC");

            // Proc effects
            foreach (var system in WarStatus.systems)
            {
                system.CalculateNeighbours(sim, false);
            }

            RefreshResources(__instance);

            // attacking
            Log($"WarStatus.attackTargets.Keys {WarStatus.attackTargets.Keys.Count}");
            foreach (var faction in WarStatus.attackTargets.Keys)
                AllocateAttackResources(faction);

            UpdateInfluenceFromAttacks();

            SaveHandling.SerializeWar();

            // testing crap
            //var someReturn = WarStatus.RelationTracker.Factions.Find(f => f.Keys.Any(k => k == Faction.Liao));
            //
            //Log("Liao relations");
            //foreach (var kvp in someReturn)
            //{
            //    LogDebug($"{kvp.Key.ToString()} - {kvp.Value}");
            //}
            //
            //var system = WarStatus.Systems.First(p => p.name == "Lindsay");
            //Log("foreach influenceMap PoC!");
            //foreach (var faction in system.InfluenceTracker)
            //{
            //    Log($"{faction.Key}: {faction.Value}");
            //}
            //
            //Log("DOMINANT FACTION: " + system.owner);
            //
            //Log($"=== Neighbours ===");
            //foreach (var neighbour in system.neighbourSystems)
            //{
            //    Log($"{neighbour.Key}: {neighbour.Value}");
            //}
            //
            //Log($"===");
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
        var killList = Enumerable.First(WarStatus.relationTracker.factions, f => f.faction == faction).killList;
        WarFaction warFaction = WarStatus.factionTracker.Find(x => x.faction == faction);
        float resources = warFaction.resources;
        float total = Enumerable.Sum(uniqueFactions.Values);

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
            foreach (var targetfaction in WarStatus.attackResources[faction].Keys)
            {
                var attacklist = new List<StarSystem>();
                foreach (var system in WarStatus.attackTargets[faction])
                    if (WarStatus.attackResources[faction].ContainsKey(system.Owner))
                        attacklist.Add(system);

                var i = 0;
                do
                {
                    var rand = random.Next(0, attacklist.Count);
                    var systemStatus = Enumerable.First(WarStatus.systems, f => f.name == attacklist[rand].Name);
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

    public static void ChangeSystemOwnership(SimGameState sim, StarSystem system, Faction faction, bool ForceFlip)
    {
        if (faction != system.Owner || ForceFlip)
        {
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

            if (Enumerable.Count(ContractEmployers) > 1)
            {
                Traverse.Create(system.Def).Property("ContractTargets").SetValue(ContractEmployers);
            }
            else
            {
                // TODO, not sure this generates intended result (a list of enemies?)
                FactionDef FactionEnemies;
                FactionEnemies = sim.FactionsDict[faction];

                Traverse.Create(system.Def).Property("ContractTargets").SetValue(Enumerable.ToList(FactionEnemies.Enemies));
            }

            Traverse.Create(system.Def).Property("Owner").SetValue(faction);
        }
    }

    private static void UpdateInfluenceFromAttacks()
    {
        Log(">>> UpdateInfluenceFromAttacks");
        // resolve resources back to faction influence
        foreach (var foo in WarStatus.attackTargets)
        {
            LogDebug(foo.Key.ToString());
            foreach (var bar in foo.Value)
            {
                LogDebug($"\t{bar.Name}");
            }
        }

        try
        {
            foreach (var faction in WarStatus.attackTargets.Keys)
            {
                var adjustingFaction = WarStatus.factionTracker.Find(f => f.faction == faction);
                Log("UpdateInfluenceFromAttacks adjustingFaction: " + adjustingFaction.faction);
                if (WarStatus.attackResources.ContainsKey(faction))
                {
                    Log($"adjustingFaction {adjustingFaction} ({adjustingFaction.resources}) + {WarStatus.attackResources[faction].Values.Sum()}");
                    adjustingFaction.resources += WarStatus.attackResources[faction].Values.Sum();
                    Log($"= {adjustingFaction.resources}");
                }
                else
                {
                    Log($"attackResources didn't contain {faction}");
                }
            }
        }
        catch (Exception ex)
        {
            Error(ex);
            Application.Quit();
        }

        // go through every system
        foreach (var system in WarStatus.systems)
        {
            Log($"\n{system.name}\n====================");
            var attackTargets = WarStatus.attackResources;
            // go through every attack target
            foreach (var target in attackTargets)
            {
                var faction = target.Key;
                // TODO verify: I do not trust the math I have written here at all
                // set the system's influence for that faction to increase by the resources
                Log($"influenceDelta for {system}, {faction} is {target.Value[faction]}");
                system.influenceTracker[faction] += target.Value[faction];
                // attempt at converting to percentage
                var totalInfluence = system.influenceTracker.Values.Sum();
                system.influenceTracker[faction] /= totalInfluence * 100;
            }

            foreach (var data in system.influenceTracker)
            {
                Log($"{data.Key,-20}: {data.Value,10}");
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

    public class WarFaction
    {
        public float resources;
        public readonly Faction faction;

        public WarFaction(Faction faction, float resources)
        {
            this.faction = faction;
            this.resources = resources;
        }
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
                    WarStatus.factionTracker.Add(new WarFaction(faction, StartingResources));
                }
                else
                {
                    WarFaction warFaction = WarStatus.factionTracker.Find(x => x.faction == faction);
                    if (warFaction != null)
                        warFaction.resources = Settings.ResourceMap[faction];
                    else
                        Log($"warFaction {faction} was null");
                }
            }
        }

        if (sim.Starmap == null) return;

        foreach (var system in sim.StarSystems)
        {
            var resources = GetTotalResources(system);
            var owner = system.Owner;
            Log($"{system.Name + ":",-20} {owner,-20}, total resources: {resources}");
            try
            {
                var faction = WarStatus.factionTracker.Where(x => x != null).FirstOrDefault(x => x.faction == owner);
                if (faction != null)
                    faction.resources += resources;
            }
            catch (Exception ex)
            {
                Error(ex);
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
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BattleTech;
using FogOfWar;
using Harmony;
using Newtonsoft.Json;
using static Logger;
using Enumerable = System.Linq.Enumerable;

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

    public static void DivideAttackResources(SimGameState sim, Faction faction)
    {
        Dictionary<Faction, float> uniqueFactions = new Dictionary<Faction, float>();

        foreach (StarSystem attackSystem in WarStatus.attackTargets[faction])
        {
            if (!uniqueFactions.ContainsKey(attackSystem.Owner))
                uniqueFactions.Add(attackSystem.Owner, 0f);
        }

        RefreshResources(sim);
        var killList = Enumerable.First(WarStatus.relationTracker.Factions, f => f.faction == faction).killList;
        WarFaction warFaction = WarStatus.factionTracker.Find(x => x.faction == faction);
        float resources = warFaction.resources;
        float total = Enumerable.Sum(uniqueFactions.Values);

        foreach (Faction tempfaction in uniqueFactions.Keys)
        {
            uniqueFactions[tempfaction] = killList[tempfaction] * (float) resources / total;
        }

        WarStatus.attackResources.Add(faction, uniqueFactions);
    }

    public static void AllocateAttackResources(Faction faction)
    {
        Random random = new Random();
        foreach (Faction targetFaction in WarStatus.attackResources[faction].Keys)
        {
            List<StarSystem> attackList = new List<StarSystem>();
            foreach (StarSystem system in WarStatus.attackTargets[faction])
            {
                if (WarStatus.attackResources[faction].ContainsKey(system.Owner))
                    attackList.Add(system);
            }

            // BUG i never increments
            int i = 0;
            do
            {
                int rand = random.Next(0, attackList.Count);
                var systemStatus = WarStatus.systems.First(f => f.name == attackList[rand].Name);
                systemStatus.influenceTracker[faction] += 1;
            } while (i < WarStatus.attackResources[faction][targetFaction]);
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

            foreach (var SystemNeighbor in sim.Starmap.GetAvailableNeighborSystem(system))
            {
                if (!ContractEmployers.Contains(SystemNeighbor.Owner))
                    ContractEmployers.Add(SystemNeighbor.Owner);
            }

            Traverse.Create(system.Def).Property("ContractEmployers").SetValue(ContractEmployers);

            if (Enumerable.Count(ContractEmployers) > 1)
            {
                Traverse.Create(system.Def).Property("ContractTargets").SetValue(ContractEmployers);
            }
            else
            {
                // TODO, not sure this generates intended result (a list of enemies?)
                // should be enemies = sim.FactionsDict[faction].Enemies;
                FactionDef FactionEnemies;
                FactionEnemies = sim.FactionsDict[faction];

                Traverse.Create(system.Def).Property("ContractTargets").SetValue(Enumerable.ToList(FactionEnemies.Enemies));
            }

            Traverse.Create(system.Def).Property("Owner").SetValue(faction);
        }
    }

    [HarmonyPatch(typeof(SimGameState), "OnDayPassed")]
    public static class SimGameState_OnDayPassed_Patch
    {
        internal static SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

        static void Prefix(SimGameState __instance, int timeLapse)
        {
            if (sim.DayRemainingInQuarter % Settings.WarFrequency != 0)
                return;
            RefreshResources(__instance);

            // already have a save?
            if (WarStatus == null && File.Exists("Mods\\GalaxyAtWar\\WarStatus.json"))
            {
                WarStatus = new WarStatus(true);
                WarProgress = new WarProgress();

                WarStatus.attackTargets.Clear();
                WarStatus.defenseTargets.Clear();
                Log("Here");

                foreach (Faction faction in sim.FactionsDict.Keys)
                    WarProgress.PotentialTargets(faction);
                DeserializeWar();
            }

            // first time setup if not
            if (WarStatus == null)
            {
                WarStatus = new WarStatus(true);

                WarStatus.attackTargets.Clear();
                WarStatus.defenseTargets.Clear();
                WarProgress = new WarProgress();
                foreach (Faction faction in sim.FactionsDict.Keys)
                    WarProgress.PotentialTargets(faction);
            }

            // Proc effects

            // attacking
            foreach (var faction in WarStatus.attackTargets.Keys)
            {
                AllocateAttackResources(faction);
            }

            // resolve resources back to faction influence
            foreach (var faction in WarStatus.attackTargets.Keys)
            {
                var adjustingFaction = WarStatus.factionTracker.Find(f => f.faction == faction);
                adjustingFaction.resources += WarStatus.attackResources[faction].Values.Sum();
            }

            // go through every system
            foreach (var system in WarStatus.systems)
            {
                var attackTargets = WarStatus.attackResources;
                // go through every attack target
                foreach (var target in attackTargets)
                {
                    var faction = target.Key;
                    // TODO verify: I do not trust the math I have written here at all
                    // set the system's influence for that faction to increase by the resources
                    system.influenceTracker[faction] += target.Value[faction];
                    // attempt at converting to percentage
                    var totalInfluence = system.influenceTracker.Values.Sum();
                    system.influenceTracker[faction] /= totalInfluence * 100;
                }
            }

            SerializeWar();

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

    public static void RefreshResources(SimGameState Sim)
    {
        // no point iterating over a KVP if you aren't using the values
        foreach (var faction in Enumerable.Except(Enumerable.Select(Sim.FactionsDict, x => x.Key), Settings.ExcludedFactions))
        {
            Log(faction.ToString());
            if (Settings.ResourceMap.ContainsKey(faction.ToString()))
            {
                // initialize resources from the ResourceMap
                if (WarStatus.factionTracker.Find(x => x.faction == faction) == null)
                {
                    int StartingResources = Settings.ResourceMap[faction.ToString()];
                    WarStatus.factionTracker.Add(new WarFaction(faction, StartingResources));
                }
                else
                {
                    WarFaction warFaction = WarStatus.factionTracker.Find(x => x.faction == faction);
                    warFaction.resources = Settings.ResourceMap[faction.ToString()];
                }
            }
        }

        if (Sim.Starmap != null)
        {
            foreach (StarSystem system in Sim.StarSystems)
            {
                int resources = GetTotalResources(system);
                Faction owner = system.Owner;
                try
                {
                    WarFaction factionresources = WarStatus.factionTracker.Find(x => x.faction == owner);
                    factionresources.resources += resources;
                }
                catch (Exception)
                {
                }
            }
        }
    }

    internal static void SerializeWar()
    {
        using (var writer = new StreamWriter("Mods\\GalaxyAtWar\\WarStatus.json"))
            writer.Write(JsonConvert.SerializeObject(WarStatus));
    }

    internal static void DeserializeWar()
    {
        using (var reader = new StreamReader("Mods\\GalaxyAtWar\\WarStatus.json"))
            WarStatus = JsonConvert.DeserializeObject<WarStatus>(reader.ReadToEnd());
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
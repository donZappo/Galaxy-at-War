using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        Dictionary<Faction, float> UniqueFactions = new Dictionary<Faction, float>();
        

        foreach (StarSystem attacksystem in Settings.AttackTargets[faction])
        {
            if (!UniqueFactions.ContainsKey(attacksystem.Owner))
                UniqueFactions.Add(attacksystem.Owner, 0f);
        }

        RefreshResources(sim);
        var killList = Enumerable.First(WarStatus.RelationTracker.Factions, f => f.faction == faction).killList;
        WarFaction warFaction = WarStatus.FactionTracker.Find(x => x.faction == faction);
        int resources = warFaction.resources;
        float total = Enumerable.Sum(UniqueFactions.Values);

        foreach (Faction tempfaction in UniqueFactions.Keys)
        {
            UniqueFactions[tempfaction] = killList[tempfaction] * (float)resources/total;
        }
        Settings.AttackResources.Add(faction, UniqueFactions);
    }

    public static void AllocateAttackResources(Faction faction)
    {
        Random random = new Random();
        foreach (Faction targetfaction in Settings.AttackResources[faction].Keys)
        {
            List<StarSystem> attacklist = new List<StarSystem>();
            foreach (StarSystem system in Settings.AttackTargets[faction])
            {
                if (Settings.AttackResources[faction].ContainsKey(system.Owner))
                    attacklist.Add(system);
            }
            int i = 0;
            do
            {
                int rand = random.Next(0, attacklist.Count);
                //var infTracker = Enumerable.First(systemStatus.InfluenceTracker.Keys, x=> x == Faction.Liao.ToString());
                var systemStatus = Enumerable.First(WarStatus.Systems, f => f.name == "Detroit");
                var resTracker = systemStatus.ResourceTracker.Find(x => x.faction == Faction.Liao);
                var factionResources = resTracker.resources;
                var liao = factionResources["Liao"];
                var facTracker = WarStatus.FactionTracker.Find(f=> f.faction == Faction.Liao);
                var LiaoResources = facTracker.resources;
                
                LiaoResources += 10;
                var systemResources = WarStatus.ResourceTracker.Find(f=> f.faction == Faction.Liao);
                var influence = systemStatus.InfluenceTracker[infTracker];
                systemStatus = WarStatus.Systems. Contains(attacklist[rand].ToString())
                
            } while (i < Settings.AttackResources[faction][targetfaction]);
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

            if (WarStatus == null && File.Exists("Mods\\GalaxyAtWar\\WarStatus.json"))
            {
                WarStatus = new WarStatus(true);
                WarProgress = new WarProgress();

                Settings.AttackTargets.Clear();
                Settings.DefenseTargets.Clear();
                Log("Here");

                foreach (Faction faction in sim.FactionsDict.Keys)
                    WarProgress.PotentialTargets(faction);
                DeserializeWar();
            }

            if (WarStatus == null)
            {
                WarStatus = new WarStatus(true);

                Settings.AttackTargets.Clear();
                Settings.DefenseTargets.Clear();
                WarProgress = new WarProgress();
                foreach (Faction faction in sim.FactionsDict.Keys)
                    WarProgress.PotentialTargets(faction);
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
        public int resources;
        public readonly Faction faction;

        public WarFaction(Faction faction, int resources)
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
                if (WarStatus.FactionTracker.Find(x => x.faction == faction) == null)
                {
                    int StartingResources = Settings.ResourceMap[faction.ToString()];
                    WarStatus.FactionTracker.Add(new WarFaction(faction, StartingResources));
                }
                else
                {
                    WarFaction warFaction = WarStatus.FactionTracker.Find(x => x.faction == faction);
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
                    WarFaction factionresources = WarStatus.FactionTracker.Find(x => x.faction == owner);
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
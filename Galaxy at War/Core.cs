using System;
using System.Collections;
using System.Collections.Generic;
using BattleTech;
using Harmony;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using static Logger;
using System.Linq;

public class Core
{
    #region Init

    internal static ModSettings Settings;

    public static void Init(string modDir, string settings)
    {
        var harmony = HarmonyInstance.Create("ca.gnivler.BattleTech.DevMod");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        // read settings
        try
        {
            Settings = JsonConvert.DeserializeObject<ModSettings>(settings);
            Settings.ModDirectory = modDir;
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

        var settingsFields = typeof(Settings)
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

    public static int GetTotalResources(StarSystem system)
    {
        int result = 0;
        if (system.Tags.Contains("planet_industry_poor"))
            result += ModSettings.planet_industry_poor;
        if (system.Tags.Contains("planet_industry_mining"))
            result += ModSettings.planet_industry_mining;
        if (system.Tags.Contains("planet_industry_rich"))
            result += ModSettings.planet_industry_rich;
        if (system.Tags.Contains("planet_other_comstar"))
            result += ModSettings.planet_other_comstar;
        if (system.Tags.Contains("planet_industry_manufacturing"))
            result += ModSettings.planet_industry_manufacturing;
        if (system.Tags.Contains("planet_industry_research"))
            result += ModSettings.planet_industry_research;
        if (system.Tags.Contains("planet_other_starleague"))
            result += ModSettings.planet_other_starleague;
        return result;
    }
    public class FactionResources
    {
        public int resources;
        public Faction faction;

        public FactionResources(Faction faction, int resources)
        {
            this.faction = faction;
            this.resources = resources;
        }
    }

    public static void RefreshResources(SimGameState Sim)
    {
        foreach (KeyValuePair<Faction, FactionDef> pair in Sim.FactionsDict)
        {
            if (ModSettings.FactionResources.ContainsKey(pair.Key.ToString()))
            {
                if (ModSettings.FactionResourcesHolder.Find(x => x.faction == pair.Key) == null)
                {
                    int StartingResources = ModSettings.FactionResources[pair.Key.ToString()];
                    ModSettings.FactionResourcesHolder.Add(new FactionResources(pair.Key, StartingResources));
                }
                else
                {
                    FactionResources factionresources = ModSettings.FactionResourcesHolder.Find(x => x.faction == pair.Key);
                    factionresources.resources = ModSettings.FactionResources[pair.Key.ToString()];
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
                    FactionResources factionresources = ModSettings.FactionResourcesHolder.Find(x => x.faction == owner);
                    factionresources.resources += resources;
                }
                catch (Exception)
                {

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
}
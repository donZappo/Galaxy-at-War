using System;
using System.Collections;
using System.Collections.Generic;
using BattleTech;
using Harmony;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using static Logger;

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

    [HarmonyPatch(typeof(SimGameState), "OnDayPassed")]
    public class testpatch
    {
        public static void Postfix(SimGameState __instance)
        {
            var temporaryResultTracker = Traverse.Create(__instance).Field("TemporaryResultTracker").GetValue<List<TemporarySimGameResult>>();
            temporaryResultTracker.Add("something");
            //Traverse.Create(__instance).Method("AddOrRemoveTempTags").GetValue(__instance, needTemporarySimGameResult, needBool);
        }
    }

    public static void RefreshResources(SimGameState Sim)
    {
        try
        {
            foreach (KeyValuePair<Faction, FactionDef> pair in Sim.FactionsDict)
            {
                if (!IsExcluded(pair.Key))
                {
                    if (Fields.factionResources.Find(x => x.faction == pair.Key) == null)
                    {
                        Fields.factionResources.Add(new FactionResources(pair.Key, 0, 0));
                    }
                    else
                    {
                        FactionResources resources = Fields.factionResources.Find(x => x.faction == pair.Key);
                        resources.offence = 0;
                        resources.defence = 0;
                    }
                }
            }

            if (Sim.Starmap != null)
            {
                foreach (StarSystem system in Sim.StarSystems)
                {
                    FactionResources resources = Fields.factionResources.Find(x => x.faction == system.Owner);
                    if (resources != null)
                    {
                        if (!IsExcluded(resources.faction))
                        {
                            resources.offence += Mathf.RoundToInt(GetOffenceValue(system) * (1 - (Fields.WarFatique[system.Owner] / 100)));
                            resources.defence += Mathf.RoundToInt(GetDefenceValue(system) * (1 - (Fields.WarFatique[system.Owner] / 100)));
                        }
                    }
                }
            }

            if (Fields.settings.debug)
            {
                foreach (KeyValuePair<Faction, FactionDef> pair in Sim.FactionsDict)
                {
                    if (!IsExcluded(pair.Key))
                    {
                        FactionResources resources = Fields.factionResources.Find(x => x.faction == pair.Key);
                        Logger.LogMonthlyReport(Helper.GetFactionName(pair.Key, Sim.DataManager) + " Exhaustion:" + Fields.WarFatique[pair.Key] + " Attack:" + resources.offence + " Defence:" + resources.defence);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }
}
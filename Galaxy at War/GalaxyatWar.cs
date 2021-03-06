using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BattleTech;
using Harmony;
using Newtonsoft.Json;
using UnityEngine;
using static GalaxyatWar.Globals;
using static GalaxyatWar.Helpers;

// ReSharper disable UnusedType.Global 
// ReSharper disable UnusedMember.Global

namespace GalaxyatWar
{
    public class GalaxyatWar
    {
        public static void Init(string modDir, string settings)
        {
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

            Logger.Clear();
            Logger.LogDebug($"GaW {Assembly.GetExecutingAssembly().GetName().Version.ToString(3)} Starting up...");

            foreach (var value in Settings.GetType().GetFields())
            {
                var v = value.GetValue(Settings);
                Logger.LogDebug($"{value.Name}: {v}");
                if (v is IEnumerable<string> list)
                {
                    if (!list.Any())
                    {
                        Logger.LogDebug("Empty list");
                        continue;
                    }
                    foreach (var item in list)
                    {
                        Logger.LogDebug($"  {item}");
                    }
                }
                else if (v is Dictionary<string, string> dict)
                {
                    if (!dict.Any())
                    {
                        Logger.LogDebug("Empty dictionary");
                        continue;
                    }
                    foreach (var pair in dict.Where(kvp => !string.IsNullOrEmpty(kvp.Key)))
                    {
                        Logger.LogDebug($"  {pair.Key} : {pair.Value}");
                    }
                }

            }

            Logger.LogDebug(new string('=', 80));
            Logger.LogDebug("");

            var harmony = HarmonyInstance.Create("com.Same.BattleTech.GalaxyAtWar");

            var original = AccessTools.Method(typeof(SimGameState), "ResolveCompleteContract");
            var postfix = AccessTools.Method(typeof(HotSpots), "SimGameStateResolveCompleteContractPatch");
            harmony.Patch(original, null, new HarmonyMethod(postfix));
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            CopySettingsToState();
        }
    }
}

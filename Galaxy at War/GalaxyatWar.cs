using System;
using System.Reflection;
using BattleTech;
using Harmony;
using Newtonsoft.Json;

namespace GalaxyatWar
{
    public class GalaxyatWar
    {
        public static void Init(string modDir, string settings)
        {
            // read settings
            try
            {
                Core.Settings = JsonConvert.DeserializeObject<ModSettings>(settings);
                Core.Settings.modDirectory = modDir;
            }
            catch (Exception)
            {
                Core.Settings = new ModSettings();
            }

            var harmony = HarmonyInstance.Create("com.Same.BattleTech.GalaxyAtWar");
            harmony.PatchAll(Assembly.GetExecutingAssembly());


            // blank the logfile
            Logger.Clear();
            
            Core.CopySettingsToState();
        }
    }
}

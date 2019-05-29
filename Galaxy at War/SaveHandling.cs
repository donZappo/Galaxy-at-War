using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text.RegularExpressions;
using BattleTech;
using BattleTech.Save;
using BattleTech.Save.Core;
using BattleTech.Save.Test;
using Harmony;
using Newtonsoft.Json;
using UnityEngine;

public static class SaveHandling
{
    private static readonly SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.Rehydrate))]
    public static class SimGameState_Rehydrate_Patch
    {
        public static void Postfix()
        {
            Logger.Log("Rehydrate Postfix");
            if (UnityGameInstance.BattleTechGame.Simulation == null) return;
            if (!sim.CompanyTags.Any(x => x.StartsWith("GalaxyAtWarSave"))) return;
            Core.WarStatus = DeserializeWar();
        }
    }

    [HarmonyPatch(typeof(SerializableReferenceContainer), "Save")]
    public class SerializableReferenceContainer_Save_Patch
    {
        public static void Postfix()
        {
            if (UnityGameInstance.BattleTechGame.Simulation == null) return;
            Logger.Log("Save Postfix");
            SerializeWar();
        }
    }

    [HarmonyPatch(typeof(SimGameState), "Update")]
    public static class SimGameState_Update_Patch
    {
        public static void Postfix(SimGameState __instance)
        {
            // clear the war state completely
            var hotkeyF10 = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F10);
            if (hotkeyF10)
            {
                foreach (var tag in sim.CompanyTags)
                {
                    if (tag.StartsWith("GalaxyAtWar"))
                    {
                        sim.CompanyTags.Remove(tag);
                        Logger.Log("removed tag");
                    }
                    else
                    {
                        Logger.Log("left " + tag);
                    }
                }

                Core.WarStatus = null;
            }

            //sim.CompanyTags.Where(tag => tag.StartsWith("GalaxyAtWar")).Do(x => sim.CompanyTags.Remove(x));

            var hotkeyD = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.D);
            if (hotkeyD)
                using (var writer = new StreamWriter("Mods\\GalaxyAtWar\\Dump.json"))
                    writer.Write(JsonConvert.SerializeObject(Core.WarStatus));

            var hotkeyL = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.L);
            if (hotkeyL)
            {
                sim.CompanyTags.Where(x => x.Contains("GalaxyAtWar")).Do(Logger.LogDebug);
                
                var tag = sim.CompanyTags.Where(x => x.Contains("GalaxyAtWar")).First();
                if (tag != null)
                {
                    var dupes = Regex.Matches(tag, @"""faction"":8");
                    var num = dupes.Count == 0 ? 42 : dupes.Count;
                    Logger.LogDebug("Dupes " + dupes.Count);
                }
                else
                    Logger.LogDebug("FUCK");
                
                // WTFFFFF
                //Logger.LogDebug(Regex.Matches(sim.CompanyTags.ToList().First(x=>x.Contains("GalaxyAtWar")), @"""faction"":8").Count.ToString());
                
            }

            var hotkeyT = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.T);
            if (hotkeyT)
            {
                var tagLength = sim.CompanyTags.FirstOrDefault(x => x.StartsWith("GalaxyAtWarSave"))?.Length;
                Logger.Log($"GalaxyAtWarSize {tagLength / 1024}kb");
            }
        }
    }

    internal static void SerializeWar()
    {
        sim.CompanyTags.Where(tag => tag.Contains(@"{""systems"":[],""relationTracker"":")).Do(x => sim.CompanyTags.Remove(x));
        sim.CompanyTags.Where(tag => tag.StartsWith("GalaxyAtWar")).Do(x => sim.CompanyTags.Remove(x));
        sim.CompanyTags.Add("GalaxyAtWarSave" + JsonConvert.SerializeObject(Core.WarStatus));
        Logger.Log(">>> Serialization complete");
    }

    internal static WarStatus DeserializeWar()
    {
        Logger.Log(">>> Deserialization");
        return JsonConvert.DeserializeObject<WarStatus>(sim.CompanyTags.First(x => x.StartsWith("GalaxyAtWarSave")).Substring(15));
    }
}
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BattleTech;
using BattleTech.Save.Test;
using Harmony;
using Newtonsoft.Json;
using UnityEngine;

public static class SaveHandling
{
    private static readonly SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
    private static string fileName = $"WarStatus_{sim.InstanceGUID}.json";

    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.Rehydrate))]
    public static class SimGameState_Rehydrate_Patch
    {
        public static void Postfix()
        {
            if (UnityGameInstance.BattleTechGame.Simulation == null) return;
            //if (!sim.CompanyTags.Any(x => x.StartsWith("GalaxyAtWarSave"))) return;
            if (!File.Exists("Mods\\GalaxyAtWar\\" + fileName)) return;
            Logger.LogDebug("Rehydrate Postfix");
            DeserializeWar();
        }
    }

    [HarmonyPatch(typeof(SerializableReferenceContainer), "Save")]
    public class SerializableReferenceContainer_Save_Patch
    {
        public static void Prefix()
        {
            if (UnityGameInstance.BattleTechGame.Simulation == null) return;
            Logger.LogDebug("Save Prefix");
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

                //Core.WarStatus = null;
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
        //sim.CompanyTags.Where(tag => tag.Contains(@"{""systems"":[],""relationTracker"":")).Do(x => sim.CompanyTags.Remove(x));
        //sim.CompanyTags.Where(tag => tag.StartsWith("GalaxyAtWar")).Do(x => sim.CompanyTags.Remove(x));
        //sim.CompanyTags.Add("GalaxyAtWarSave" + JsonConvert.SerializeObject(Core.WarStatus));
        Logger.LogDebug($"Serializing systems: {Core.WarStatus.systems.Count}");
        using (var writer = new StreamWriter("Mods\\GalaxyAtWar\\" + fileName))
            writer.Write(JsonConvert.SerializeObject(Core.WarStatus));
        Logger.LogDebug(">>> Serialization complete");
    }

    internal static void DeserializeWar()
    {
        Logger.LogDebug(">>> Deserialization");
        using (var reader = new StreamReader("Mods\\GalaxyAtWar\\" + fileName))
        {
            Core.WarStatus = JsonConvert.DeserializeObject<WarStatus>(reader.ReadToEnd());
            try
            {
                Logger.LogDebug($"Deserialized systems: {Core.WarStatus.systems.Count}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        //return warStatus;

        //return JsonConvert.DeserializeObject<WarStatus>(sim.CompanyTags.First(x => x.StartsWith("GalaxyAtWarSave")).Substring(15));
    }
}
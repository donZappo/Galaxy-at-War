using System.IO;
using System.Linq;
using BattleTech;
using BattleTech.Save.Test;
using Harmony;
using HBS.Collections;
using Newtonsoft.Json;
using UnityEngine;
using static Logger;

public static class SaveHandling
{
    private static readonly SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

    [HarmonyPatch(typeof(SimGameState), "_OnAttachUXComplete")]
    public static class SimGameState__OnAttachUXComplete_Patch
    {
        public static void Postfix(ref TagSet ___companyTags)
        {
            // initialize WarStatus
            if (!sim.CompanyTags.Any(x => x.StartsWith("GalaxyAtWarSave{")))
            {
                LogDebug("Setting up new WarStatus");
                Core.WarStatus = new WarStatus();
                Core.WarTick();
            }
            else
            {
                LogDebug("_OnAttachUXComplete Postfix");
                DeserializeWar();
            }
        }
    }

    [HarmonyPatch(typeof(SerializableReferenceContainer), "Save")]
    public static class SerializableReferenceContainer_Save_Patch
    {
        public static void Prefix()
        {
            if (UnityGameInstance.BattleTechGame.Simulation == null) return;
            if (Core.WarStatus != null)
                SerializeWar();
        }
    }

    [HarmonyPatch(typeof(SerializableReferenceContainer), "Load")]
    public static class SerializableReferenceContainer_Load_Patch
    {
        public static void Prefix()
        {
            if (UnityGameInstance.BattleTechGame.Simulation == null) return;
            LogDebug("Clearing GaW save tags");
            sim.CompanyTags.Where(tag => tag.StartsWith("GalaxyAtWar")).Do(x => sim.CompanyTags.Remove(x));
        }
    }

    internal static void SerializeWar()
    {
        sim.CompanyTags.Where(tag => tag.StartsWith("GalaxyAtWar")).Do(x => sim.CompanyTags.Remove(x));
        sim.CompanyTags.Add("GalaxyAtWarSave" + JsonConvert.SerializeObject(Core.WarStatus));
        LogDebug($"Serializing object size: {JsonConvert.SerializeObject(Core.WarStatus).Length / 1024}kb");
        LogDebug(">>> Serialization complete");
    }

    internal static void DeserializeWar()
    {
        Core.WarStatus = JsonConvert.DeserializeObject<WarStatus>(sim.CompanyTags.First(x => x.StartsWith("GalaxyAtWarSave{")).Substring(15));
        LogDebug(">>> Deserialization complete");
        LogDebug($"Size after load: {JsonConvert.SerializeObject(Core.WarStatus).Length / 1024}kb");
    }

    [HarmonyPatch(typeof(SimGameState), "Update")]
    public static class SimGameState_Update_Patch
    {
        public static void Postfix(SimGameState __instance)
        {
            // clear the WarStatus completely
            var hotkeyF10 = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F10);
            if (hotkeyF10)
            {
                foreach (var tag in sim.CompanyTags)
                    if (tag.StartsWith("GalaxyAtWar"))
                        sim.CompanyTags.Remove(tag);

                LogDebug("Setting up new WarStatus");
                Core.WarStatus = new WarStatus();
                Core.WarTick();
            }

            var hotkeyD = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.D);
            if (hotkeyD)
                using (var writer = new StreamWriter("Mods\\GalaxyAtWar\\SaveDump.json"))
                    writer.Write(JsonConvert.SerializeObject(Core.WarStatus));

            var hotkeyT = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.T);
            if (hotkeyT)
                sim.CompanyTags.Add(new string('=', 50));

            var hotkeyL = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.L);
            if (hotkeyL)
                sim.CompanyTags.Do(LogDebug);
        }
    }
}
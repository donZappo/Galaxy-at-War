using System.IO;
using System.Runtime.Remoting.Messaging;
using BattleTech;
using BattleTech.Save;
using BattleTech.Save.Core;
using BattleTech.Save.Test;
using Harmony;
using Newtonsoft.Json;
using ObjectDumper;
using UnityEngine;

public static class SaveHandling
{
    private static readonly string FileName = $"WarStatus_{UnityGameInstance.BattleTechGame.Simulation.InstanceGUID}.json";

    //[HarmonyPatch(typeof(GameInstanceSave), "PostDeserialization")]
    //public class GameInstanceSave_PostDeserialization_Patch
    //{
    //    public static void Postfix()
    //    {
    //        if (UnityGameInstance.BattleTechGame.Simulation == null) return;
    //        if (!File.Exists("Mods\\GalaxyAtWar\\" + FileName)) return;
    //        Logger.Log("PostDeserialization Postfix");
    //        Core.WarStatus = DeserializeWar();
    //    }
    //}

    //[HarmonyPatch(typeof(SimGameState), nameof(SimGameState.Rehydrate))]
    //public static class SimGameState_Rehydrate_Patch
    //{
    //    public static void Postfix()
    //    {
    //        if (!File.Exists("Mods\\GalaxyAtWar\\" + FileName)) return;
    //        Logger.Log("Rehydrate Postfix");
    //        Core.WarStatus = DeserializeWar();
    //    }
    //}

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
            var hotkey = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.D);
            if (hotkey)
            {
                Core.WarStatus = DeserializeWar();
            }
        }
    }

    internal static void SerializeWar()
    {
        using (var writer = new StreamWriter("Mods\\GalaxyAtWar\\" + FileName))
            writer.Write(JsonConvert.SerializeObject(Core.WarStatus));
        Logger.Log(">>> Serialization complete");
    }

    internal static WarStatus DeserializeWar()
    {
        Logger.Log(">>> Deserialization");
        using (var reader = new StreamReader("Mods\\GalaxyAtWar\\" + FileName))
        {
            var warStatus = JsonConvert.DeserializeObject<WarStatus>(reader.ReadToEnd());
            using (var writer = new StreamWriter("Mods\\GalaxyAtWar\\Dump.txt"))
                Dumper.Dump(warStatus, "WarStatus", writer);
            return warStatus;
        }

    }
}
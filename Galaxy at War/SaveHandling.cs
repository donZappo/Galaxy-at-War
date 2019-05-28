using System.IO;
using BattleTech;
using BattleTech.Save;
using BattleTech.Save.Core;
using BattleTech.Save.Test;
using Harmony;
using Newtonsoft.Json;

public class SaveHandling
{
    [HarmonyPatch(typeof(GameInstanceSave), "PostDeserialization")]
    public class GameInstanceSave_PostDeserialization_Patch
    {
        public static void Postfix()
        {
            if (UnityGameInstance.BattleTechGame.Simulation == null) return;
            Logger.Log("PostDeserialization Postfix");
            Core.WarStatus = DeserializeWar();
        }
    }

    [HarmonyPatch(typeof(SimGameState), nameof(SimGameState.AttachUX))]
    public static class SimGameState_AttachUX_Patch
    {
        public static void Postfix()
        {
            Logger.Log("AttachUX Postfix");
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

    // TODO when quitting - clear 
    //[HarmonyPatch(

    internal static void SerializeWar()
    {
        var fileName = $"WarStatus_{UnityGameInstance.BattleTechGame.Simulation.InstanceGUID}.json";
        using (var writer = new StreamWriter("Mods\\GalaxyAtWar\\" + fileName))
            writer.Write(JsonConvert.SerializeObject(Core.WarStatus));
        Logger.Log(">>> Serialization complete");
    }

    internal static WarStatus DeserializeWar()
    {
        var fileName = $"WarStatus_{UnityGameInstance.BattleTechGame.Simulation.InstanceGUID}.json";
        Logger.Log(">>> Deserialization");
        using (var reader = new StreamReader("Mods\\GalaxyAtWar\\" + fileName))
            return JsonConvert.DeserializeObject<WarStatus>(reader.ReadToEnd());
    }
}
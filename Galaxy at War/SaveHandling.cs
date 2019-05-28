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
            DeserializeWar();
            
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

    internal static void SerializeWar()
    {
        var fileName = $"WarStatus_{UnityGameInstance.BattleTechGame.Simulation.InstanceGUID}.json";
        using (var writer = new StreamWriter("Mods\\GalaxyAtWar\\" + fileName))
            writer.Write(JsonConvert.SerializeObject(Core.WarStatus));
        Logger.Log(">>> Serialization complete");
    }

    internal static void DeserializeWar()
    {
        var fileName = $"WarStatus_{UnityGameInstance.BattleTechGame.Simulation.InstanceGUID}.json";
        using (var reader = new StreamReader("Mods\\GalaxyAtWar\\" + fileName))
            Core.WarStatus = JsonConvert.DeserializeObject<WarStatus>(reader.ReadToEnd());
        Logger.Log(">>> Deserialization complete");
    }
}
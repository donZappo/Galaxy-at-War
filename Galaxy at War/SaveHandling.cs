using System.IO;
using BattleTech;
using BattleTech.Save;
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
            DeserializeWar();
        }
    }

    [HarmonyPatch(typeof(GameInstanceSave), "PreSerialization")]
    public class GameInstanceSave_PreSerialization_Patch
    {
        public static void Postfix()
        {
            if (UnityGameInstance.BattleTechGame.Simulation == null) return;
            SerializeWar();
        }
    }

    internal static void SerializeWar()
    {
        var fileName = $"WarStatus_{UnityGameInstance.BattleTechGame.Simulation.InstanceGUID}.json";
        using (var writer = new StreamWriter("Mods\\GalaxyAtWar\\" + fileName))
            writer.Write(JsonConvert.SerializeObject(Core.WarStatus));
    }

    internal static void DeserializeWar()
    {
        var fileName = $"WarStatus_{UnityGameInstance.BattleTechGame.Simulation.InstanceGUID}.json";
        using (var reader = new StreamReader("Mods\\GalaxyAtWar\\" + fileName))
            Core.WarStatus = JsonConvert.DeserializeObject<WarStatus>(reader.ReadToEnd());
    }
}
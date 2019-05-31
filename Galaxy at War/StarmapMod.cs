using System.Collections.Generic;
using System.Linq;
using BattleTech;
using GalaxyAtWar;
using Harmony;
using UnityEngine;

public class StarmapMod
{
    private static readonly SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

    [HarmonyPatch(typeof(StarmapRenderer), "GetSystemRenderer")]
    [HarmonyPatch(new[] {typeof(StarSystemNode)})]
    public static class StarmapRenderer_GetSystemRenderer_Patch
    {
        public static void Postfix(ref StarmapSystemRenderer __result)
        {
            var contendedSystems = Core.WarStatus.systems.Where(x => x.influenceTracker[x.owner] < 60).Select(x => x.name);

            if (contendedSystems.Contains(__result.name))
            {
                var visitedStarSystems = Traverse.Create(sim).Field("VisitedStarSystems").GetValue<List<string>>();
                var wasVisited = visitedStarSystems.Contains(__result.name);
                __result.Init(__result.system, Color.magenta, __result.CanTravel, wasVisited);
            }
        }
    }

    //[HarmonyPatch(typeof(StarmapRenderer), "FactionColor")]
    //public static class Starmap_TEST
    //{
    //    public static bool Prefix(Starmap __instance, Color __result)
    //    {
    //        __result = new Color(255, 0, 0, 255);
    //        return false;
    //    }
    //}

    //[HarmonyPatch(typeof(SimGameUXCreator), "OnSimGameInitializeComplete")]
    //public static class OOoPooopOOooopoopp
    //{
    //    public static void Postfix()
    //    {
    //        Logger.Log("POOP");
    //        Core.WarStatus = SaveHandling.DeserializeWar();
    //    }
    //}

    //[HarmonyPatch(typeof(StarmapRenderer), "RefreshSystems")]
    //public static class PatchyPOOOOP
    //{

    //    }
    //}

    //[HarmonyPatch(typeof(StarmapRenderer), "InitializeSysRenderer")]
    //public static class PATCHPATCHFUCK
    //{
    //    public static void Postfix(StarmapRenderer __instance, StarSystemNode node, StarmapSystemRenderer renderer)
    //    {
    //        }
    //    }
    //}
}
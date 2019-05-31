using System.Collections.Generic;
using System.Linq;
using BattleTech;
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
            var contendedSystems = new List<string>();
            foreach (SystemStatus systemStatus in Core.WarStatus.systems)
            {
                float highest = 0;
                Faction highestFaction = systemStatus.owner;
                foreach (Faction faction in systemStatus.influenceTracker.Keys)
                {
                    if (systemStatus.influenceTracker[faction] > highest)
                    {
                        highest = systemStatus.influenceTracker[faction];
                        highestFaction = faction;
                    }
                }
                if (highestFaction != systemStatus.owner && (highest - systemStatus.influenceTracker[systemStatus.owner]) < Core.Settings.TakeoverThreshold)
                    contendedSystems.Add(systemStatus.name);
            }
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
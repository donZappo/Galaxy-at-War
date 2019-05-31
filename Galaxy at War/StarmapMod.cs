using System.Collections.Generic;
using System.Linq;
using BattleTech;
using GalaxyAtWar;
using Harmony;
using UnityEngine;

public class StarmapMod
{
    private static readonly SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

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
    //    public static bool Prefix(StarmapRenderer __instance)
    //    {
    //        __instance.starmapCamera.gameObject.SetActive(true);
    //        var systemDictionary = Traverse.Create(__instance).Field("systemDictionary").GetValue<Dictionary<GameObject, StarmapSystemRenderer>>();
    //        foreach (StarmapSystemRenderer renderer in systemDictionary.Values)
    //        {
    //            global::Logger.LogDebug("Color: " + renderer.systemColor);
    //            renderer.systemColor = Color.magenta;
    //            global::Logger.LogDebug("Color: " + renderer.systemColor);
    //            renderer.Init(renderer.system, Color.magenta, renderer.CanTravel, false);
    //            global::Logger.LogDebug("Color: " + renderer.systemColor);
    //            Traverse.Create(__instance).Method("InitializeSysRenderer", new[] {typeof(StarSystemNode), typeof(StarmapSystemRenderer)}).GetValue(renderer.system, renderer);
    //            //__instance.InitializeSysRenderer(renderer.system, renderer);
    //            if (__instance.starmap.CurSelected != null && __instance.starmap.CurSelected.System.ID == renderer.system.System.ID)
    //                renderer.Selected();
    //            else
    //                renderer.Deselected();
    //        }
    //
    //        //__instance.PlaceLogo(Faction.AuriganRestoration, __instance.restorationLogo);
    //
    //        Traverse.Create(__instance).Method("PlaceLogo", new[] {typeof(Faction), typeof(GameObject)}).GetValue(Faction.AuriganDirectorate, __instance.directorateLogo);
    //        Traverse.Create(__instance).Method("PlaceLogo", new[] {typeof(Faction), typeof(GameObject)}).GetValue(Faction.AuriganRestoration, __instance.restorationLogo);
    //        return false;
    //
    //        //__instance.PlaceLogo(Faction.AuriganDirectorate, this.directorateLogo);
    //        //__instance.PlaceLogo(Faction.AuriganRestoration, this.restorationLogo);
    //
    //        //var war = Core.WarStatus;
    //        //__instance.starmapCamera.gameObject.SetActive(true);
    //        //var systemDictionary = Traverse.Create(__instance).Field("systemDictionary").GetValue<Dictionary<GameObject, StarmapSystemRenderer>>();
    //        //foreach (StarmapSystemRenderer starmapSystemRenderer in systemDictionary.Values)
    //        //{
    //        //    //Logger.Log(starmapSystemRenderer.system.System.Name);
    //        //    //Logger.Log(war.systems.Count.ToString());
    //        //    var systemStatus = WarStatus.systems.Find(x => x.name == starmapSystemRenderer.system.System.Name);
    //        //
    //        //    try
    //        //    {
    //        //        starmapSystemRenderer.systemColor = Color.magenta;
    //        //    }
    //        //    catch (Exception ex)
    //        //    {
    //        //        Logger.Error(ex);
    //        //    }
    //        //
    //        //    Traverse.Create(__instance).Method("InitializeSysRenderer").GetValue(starmapSystemRenderer.system, starmapSystemRenderer);
    //        //    //__instance.InitializeSysRenderer(starmapSystemRenderer.system, starmapSystemRenderer);
    //        //    if (__instance.starmap.CurSelected != null && __instance.starmap.CurSelected.System.ID == starmapSystemRenderer.system.System.ID)
    //        //    {
    //        //        starmapSystemRenderer.Selected();
    //        //    }
    //        //    else
    //        //    {
    //        //        starmapSystemRenderer.Deselected();
    //        //    }
    //        //}
    //        //
    //        //Traverse.Create(__instance).Method("PlaceLogo").GetValue(Faction.AuriganDirectorate, __instance.directorateLogo);
    //        ////__instance.PlaceLogo(Faction.AuriganDirectorate, __instance.directorateLogo);
    //        //Traverse.Create(__instance).Method("PlaceLogo").GetValue(Faction.AuriganRestoration, __instance.restorationLogo);
    //    }
    //}

    [HarmonyPatch(typeof(StarmapRenderer), "GetSystemRenderer")]
    [HarmonyPatch(new[] {typeof(StarSystemNode)})]
    public static class StarmapRenderer_GetSystemRenderer_Patch
    {
        public static void Postfix(ref StarmapSystemRenderer __result)
        {
            var contendedSystems = Core.WarStatus.systems.Where(x => x.influenceTracker[x.owner] < 70).Select(x => x.name);
            var visitedStarSystems = Traverse.Create(sim).Field("VisitedStarSystems").GetValue<List<string>>();
            var wasVisited = visitedStarSystems.Contains(__result.name);

            if (contendedSystems.Contains(__result.name))
                __result.Init(__result.system, Color.magenta, __result.CanTravel, wasVisited);
        }
    }
}

//[HarmonyPatch(typeof(StarmapRenderer), "InitializeSysRenderer")]
//public static class PATCHPATCHFUCK
//{
//    public static void Postfix(StarmapRenderer __instance, StarSystemNode node, StarmapSystemRenderer renderer)
//    {
//        Logger.Log("Postfix");
//        //Core.WarStatus = SaveHandling.DeserializeWar();
//        //var foo = node.System.Name;
//        //var systemStatus = Core.WarStatus.systems.Find(x => x.name == node.System.Name);
//        
//        //var influence = systemStatus.influenceTracker[systemStatus.owner];
//        
//        //if (influence < 75)
//        //{
//        //    renderer.Init(node, Color.magenta, true);
//        //
//        //    __instance.RefreshBorders();
//        //}
//        //
//        //if (node.System.Name.StartsWith("A"))
//            renderer.Init(node, Color.magenta, true, false);
//
//        foreach (StarSystemNode starSystemNode in map.VisisbleSystem)
//        {
//            GameObject gameObject = Object.Instantiate(__instance.starPrototype);
//            gameObject.name = starSystemNode.System.Name;
//            gameObject.transform.parent = __instance.starParent;
//            StarmapSystemRenderer component = gameObject.GetComponent<StarmapSystemRenderer>();
//            component.systemColor = Color.blue;
//            //component.CanTravel = true;
//            //component.blackMarketObj.SetActive(true);
//        
//            ___systemDictionary.Add(gameObject, component);
//            //Traverse.Create(__instance)
//            //    .Method("InitializeSysRenderer", new [] {typeof(StarSystemNode), typeof(StarmapSystemRenderer)})
//            //    .GetValue(starSystemNode, component);
//            try
//            {
//                __instance.InitializeSysRenderer(starSystemNode, component);
//            }
//            catch (Exception ex)
//            {
//                Logger.Error(ex);
//            }
//        }
//    }
//}
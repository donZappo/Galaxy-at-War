using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.DataObjects;
using BattleTech.UI;
using BestHTTP.SocketIO;
using Harmony;
using UnityEngine;
using Object = UnityEngine.Object;

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

    [HarmonyPatch(typeof(StarmapRenderer), "RefreshSystems")]
    public static class PatchyPOOOOP
    {
        public static bool Prefix(StarmapRenderer __instance)
        {
            var war = Core.WarStatus;
            __instance.starmapCamera.gameObject.SetActive(true);
            foreach (StarmapSystemRenderer starmapSystemRenderer in __instance.systemDictionary.Values)
            {
                //Logger.Log(starmapSystemRenderer.system.System.Name);
                //Logger.Log(war.systems.Count.ToString());
                var systemStatus = war.systems.Find(x => x.name == starmapSystemRenderer.system.System.Name);

                try
                {
                    //var influence = systemStatus?.influenceTracker[systemStatus.owner];
                    //if (influence < 90)
                    {
                        starmapSystemRenderer.systemColor = Color.magenta;
                        //var mpb = StarmapSystemRenderer.mpb;
                        // starmapSystemRenderer.starOuter.SetPropertyBlock(mpb);
                        // starmapSystemRenderer.starInner.SetPropertyBlock(mpb);
                        //starmapSystemRenderer.Set(starmapSystemRenderer.system, Color.magenta, false);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }

                __instance.InitializeSysRenderer(starmapSystemRenderer.system, starmapSystemRenderer);
                if (__instance.starmap.CurSelected != null && __instance.starmap.CurSelected.System.ID == starmapSystemRenderer.system.System.ID)
                {
                    starmapSystemRenderer.Selected();
                }
                else
                {
                    starmapSystemRenderer.Deselected();
                }
            }

            __instance.PlaceLogo(Faction.AuriganDirectorate, __instance.directorateLogo);
            __instance.PlaceLogo(Faction.AuriganRestoration, __instance.restorationLogo);
            return false;
        }
    }

    //[HarmonyPatch(typeof(StarmapRenderer), "InitializeSysRenderer")]
    public static class PATCHPATCHFUCK
    {
        public static void Postfix(StarmapRenderer __instance, StarSystemNode node, StarmapSystemRenderer renderer)
        {
            Logger.Log("Postfix");
            //Core.WarStatus = SaveHandling.DeserializeWar();
            //var foo = node.System.Name;
            //var systemStatus = Core.WarStatus.systems.Find(x => x.name == node.System.Name);
            //
            //var influence = systemStatus.influenceTracker[systemStatus.owner];
            //
            //if (influence < 75)
            //{
            //    renderer.Init(node, Color.magenta, true);
            //
            //    __instance.RefreshBorders();
            //}

            //if (node.System.Name.StartsWith("A"))
            //    renderer.Init(node, Color.magenta, true);

            //foreach (StarSystemNode starSystemNode in map.VisisbleSystem)
            //{
            //    GameObject gameObject = Object.Instantiate(__instance.starPrototype);
            //    gameObject.name = starSystemNode.System.Name;
            //    gameObject.transform.parent = __instance.starParent;
            //    StarmapSystemRenderer component = gameObject.GetComponent<StarmapSystemRenderer>();
            //    component.systemColor = Color.blue;
            //    component.CanTravel = true;
            //    component.blackMarketObj.SetActive(true);
            //
            //    ___systemDictionary.Add(gameObject, component);
            //    //Traverse.Create(__instance)
            //    //    .Method("InitializeSysRenderer", new [] {typeof(StarSystemNode), typeof(StarmapSystemRenderer)})
            //    //    .GetValue(starSystemNode, component);
            //    try
            //    {
            //        __instance.InitializeSysRenderer(starSystemNode, component);
            //    }
            //    catch (Exception ex)
            //    {
            //        Logger.Error(ex);
            //    }
            //}
        }
    }
}
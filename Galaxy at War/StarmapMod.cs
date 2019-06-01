using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BattleTech;
using BattleTech.UI;
using BattleTech.UI.Tooltips;
using Harmony;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using static Logger;

public class StarmapMod
{
    private static readonly SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

    //[HarmonyPatch(typeof(TooltipPrefab_Faction), "SetData")]
    public static class POoOoooooooop
    {
        public static bool Prefix(TooltipPrefab_Faction __instance, object data,
            TextMeshProUGUI ___enemyAllyNotice, TextMeshProUGUI ___body, TextMeshProUGUI ___header,
            SGFactionRelationshipDisplay ___factionEnemyRelationships, GameObject ___factionObject,
            UIColorRefTracker ___enemyAllyColorControl)
        {
            ___body.textInfo.Clear();
            ___enemyAllyNotice.gameObject.SetActive(false);
            FactionDef factionDef = (FactionDef) data;
            if (factionDef == null)
            {
                return false; // false;
            }

            Traverse.Create(__instance).Method("DisableIcon").GetValue();

            var factionString = new StringBuilder();

            factionString.Append(factionDef.Description);
            
            
            ___header.SetText(factionDef.Name, new object[0]);
            ___body.SetText(factionDef.Description, new object[0]);
            string spriteName = factionDef.GetSpriteName();
            ___body.SetLayoutDirty();
            ___body.SetVerticesDirty();

            var flag = Traverse.Create(__instance).Method("SetIcon", new[] {typeof(string)}).GetValue<bool>(spriteName);

            if (!flag)
            {
                Traverse.Create(__instance).Method("SetIcon", new[] {typeof(string)}).GetValue("uixTxrIcon_atlas");
            }

            SimGameState simulation = UnityGameInstance.BattleTechGame.Simulation;
            if (simulation != null)
            {
                if (factionDef.Enemies.Length > 0 && ___factionEnemyRelationships != null)
                {
                    ___factionObject.gameObject.SetActive(true);
                    ___factionEnemyRelationships.Init(simulation);
                    ___factionEnemyRelationships.DisplayEnemiesOfFaction(factionDef.Faction);
                }
                else
                {
                    ___factionObject.gameObject.SetActive(false);
                }

                if (simulation.IsFactionAlly(factionDef.Faction, null))
                {
                    ___enemyAllyNotice.gameObject.SetActive(true);
                    ___enemyAllyNotice.SetText("You are allied with __instance faction and can't fall below 0 reputation", new object[0]);
                    ___enemyAllyColorControl.SetUIColor(UIColor.Green);
                }
                else if (simulation.IsFactionEnemy(factionDef.Faction, null))
                {
                    ___enemyAllyNotice.gameObject.SetActive(true);
                    ___enemyAllyNotice.SetText("You are enemies with __instance faction and can't go above 0 reputation", new object[0]);
                    ___enemyAllyColorControl.SetUIColor(UIColor.Orange);
                }
                else
                {
                    ___enemyAllyNotice.gameObject.SetActive(false);
                }
            }

            return false; // true;
        }
    }

    //[HarmonyPatch(typeof(SGNavigationScreen), "RefreshAllCallouts")]
    public static class POoOooop
    {
        public static void Prefix(ref SGNavStarSystemCallout ___HoverCallout)
        {
            LogDebug("wooop");
            var labelField = Traverse.Create(___HoverCallout).Field("LabelField").GetValue<TextMeshProUGUI>();
            using (var writer = new StreamWriter($"dump{Guid.NewGuid()}.json"))
            {
                writer.Write(JsonConvert.SerializeObject(labelField));
            }

            //labelField.text = "POOP!";
        }
    }

    //[HarmonyPatch(typeof(SGNavigationActiveFactionWidget), "Init")]
    //public static class Pooooopp
    //{
    //    public static bool Prefixj(SimGameState sim, SimGameState ___simState, SGNavigationActiveFactionWidget __instance, List<Faction> ___FactionsList, List<Image> ___FactionIcons)
    //    {
    //        ___simState = sim;
    //        for (int index = 0; index < ___FactionIcons.Count; ++index)
    //        {
    //            FactionDef factionDef;
    //            if (___simState.FactionsDict.TryGetValue(___FactionsList[index], out factionDef))
    //            {
    //                ___FactionIcons[index].sprite = factionDef.GetSprite();
    //                HBSTooltip component = ___FactionIcons[index].GetComponent<HBSTooltip>();
    //                if ((UnityEngine.Object) component != (UnityEngine.Object) null)
    //                    component.SetDefaultStateData(TooltipUtilities.GetStateDataFromObject((object) factionDef));
    //            }
    //        }
    //
    //        __instance.DeactivateAllFactions();
    //        return false;
    //    }
    //
    //    public static void Postfix(List<Image> ___FactionIcons)
    //    {
    //        for (var i = 0; i < ___FactionIcons.Count; i++)
    //        {
    //            HBSTooltip component = ___FactionIcons[i].GetComponent<HBSTooltip>();
    //            //using (var writer = new StreamWriter($"dump{Guid.NewGuid()}.json"))
    //            //{
    //            //    writer.Write(JsonConvert.SerializeObject(component));
    //            //}
    //        }
    //    }
    //}

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
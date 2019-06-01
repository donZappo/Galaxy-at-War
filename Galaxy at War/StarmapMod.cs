using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BattleTech;
using BattleTech.UI;
using BattleTech.UI.Tooltips;
using Harmony;
using TMPro;
using UnityEngine;

public class StarmapMod
{
    private static readonly SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

    //[HarmonyPatch(typeof(SGStarmapStatusWidget), "OnPlanetClick")]
    public static class SGStarmapStatusWidget_OnPlanetClick_Patch
    {
        public static void Postfix(StarSystem targetSystem)
        {
            Core.SelectedSystem = targetSystem;
        }
    }

    [HarmonyPatch(typeof(Starmap), "SetSelectedSystem")]
    [HarmonyPatch(new[] {typeof(StarSystem)})]
    public static class Starmap_SetSelectedSystem_Patch
    {
        public static void Postfix(StarSystem sys)
        {
            Core.SelectedSystem = sys;
        }
    }

    [HarmonyPatch(typeof(TooltipPrefab_Faction), "SetData")]
    public static class TooltipPrefab_Faction_SetData_Patch
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
                return false;
            }

            Traverse.Create(__instance).Method("DisableIcon").GetValue();

            var factionString = new StringBuilder();
            factionString.AppendLine(factionDef.Description);
            factionString.AppendLine();
            var tracker = Core.WarStatus.systems.Find(x => x.name == Core.SelectedSystem.Name);
            foreach (var foo in tracker.influenceTracker.OrderByDescending(x => x.Value))
            {
                factionString.AppendLine($" {Math.Round(foo.Value),-6:#} {foo.Key}");
            }

            ___header.SetText(factionDef.Name);
            ___body.SetText(factionString.ToString());
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

                if (simulation.IsFactionAlly(factionDef.Faction))
                {
                    ___enemyAllyNotice.gameObject.SetActive(true);
                    ___enemyAllyNotice.SetText("You are allied with __instance faction and can't fall below 0 reputation");
                    ___enemyAllyColorControl.SetUIColor(UIColor.Green);
                }
                else if (simulation.IsFactionEnemy(factionDef.Faction))
                {
                    ___enemyAllyNotice.gameObject.SetActive(true);
                    ___enemyAllyNotice.SetText("You are enemies with __instance faction and can't go above 0 reputation");
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
}
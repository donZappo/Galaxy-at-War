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

    [HarmonyPatch(typeof(Starmap), "SetSelectedSystem")]
    [HarmonyPatch(new[] { typeof(StarSystem) })]
    public static class Starmap_SetSelectedSystem_Patch
    {
        public static void Postfix(StarSystem sys)
        {
            Core.SelectedSystem = sys;
        }
    }

    [HarmonyPatch(typeof(TooltipPrefab_Planet), "GetPlanetPop")]
    public static class TooltipPrefab_Planet_GetPlanetPop_Patch
    {
        public static void Prefix(StarSystem starSystem)
        {
            Core.SelectedSystem = starSystem;
        }
    }

    [HarmonyPatch(typeof(TooltipPrefab_Planet), "SetData")]
    public static class TooltipPrefab_Planet_SetData_Patch
    {
        public static void Prefix(object data, ref string __state)
        {
            var starSystem = (StarSystem)data;
            if (starSystem == null)
            {
                return;
            }

            __state = starSystem.Def.Description.Details;
            var factionString = new StringBuilder();
            factionString.AppendLine(starSystem.Def.Description.Details);
            factionString.AppendLine();
            var tracker = Core.WarStatus.systems.Find(x => x.name == Core.SelectedSystem.Name);
            foreach (var foo in tracker.influenceTracker.OrderByDescending(x => x.Value))
            {
                factionString.AppendLine($" {Math.Round(foo.Value) + "%",-6:#} {foo.Key}");
            }

            Traverse.Create(starSystem.Def.Description).Property("Details").SetValue(factionString.ToString());
        }

        public static void Postfix(object data, string __state)
        {
            var starSystem = (StarSystem)data;
            if (starSystem == null)
            {
                return;
            }

            Traverse.Create(starSystem.Def.Description).Property("Details").SetValue(__state);
        }
    }

    [HarmonyPatch(typeof(TooltipPrefab_Faction), "SetData")]
    public static class TooltipPrefab_Faction_SetData_Patch
    {
        public static void Prefix(object data, ref string __state)
        {
            var factionDef = (FactionDef)data;
            if (factionDef == null)
            {
                return;
            }

            __state = factionDef.Description;
            var factionString = new StringBuilder();
            factionString.AppendLine(factionDef.Description);
            factionString.AppendLine();
            var tracker = Core.WarStatus.systems.Find(x => x.name == Core.SelectedSystem.Name);
            foreach (var foo in tracker.influenceTracker.OrderByDescending(x => x.Value))
            {
                factionString.AppendLine($" {Math.Round(foo.Value) + "%",-6:#} {foo.Key}");
            }

            Traverse.Create(factionDef).Property("Description").SetValue(factionString.ToString());
        }

        public static void Postfix(object data, string __state)
        {
            var factionDef = (FactionDef)data;
            if (factionDef == null)
            {
                return;
            }

            Traverse.Create(factionDef).Property("Description").SetValue(__state);
        }
    }

    [HarmonyPatch(typeof(StarmapRenderer), "GetSystemRenderer")]
    [HarmonyPatch(new[] { typeof(StarSystemNode) })]
    public static class StarmapRenderer_GetSystemRenderer_Patch
    {
        public static void Postfix(ref StarmapSystemRenderer __result)
        {
            var contendedSystems = new List<string>();
            foreach (SystemStatus systemStatus in Core.WarStatus.systems)
            {
                float highest = 0;
                var highestFaction = systemStatus.owner;
                foreach (var faction in systemStatus.influenceTracker.Keys)
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
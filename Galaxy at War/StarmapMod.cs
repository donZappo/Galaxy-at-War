using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BattleTech;
using BattleTech.UI.Tooltips;
using Harmony;
using UnityEngine;
using static Logger;

// ReSharper disable InconsistentNaming

public class StarmapMod
{
    [HarmonyPatch(typeof(TooltipPrefab_Planet), "SetData")]
    public static class TooltipPrefab_Planet_SetData_Patch
    {
        public static void Prefix(object data, ref string __state)
        {
            var starSystem = (StarSystem) data;
            if (starSystem == null)
            {
                return;
            }

            SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
            __state = starSystem.Def.Description.Details;
            var factionString = new StringBuilder();
            factionString.AppendLine(starSystem.Def.Description.Details);
            factionString.AppendLine();
            var tracker = Core.WarStatus.systems.Find(x => x.name == starSystem.Name);
            foreach (var influence in tracker.influenceTracker.OrderByDescending(x => x.Value))
            {
                string number;
                if (influence.Value <= float.Epsilon)
                    continue;
                if (Math.Abs(influence.Value - 100) < 0.999)
                    number = "100%";
                else if (influence.Value < 1)
                    number = "< 1%";
                else if (influence.Value > 99)
                    number = "> 99%";
                else
                    number = $"> {influence.Value:#.0}%";

                factionString.AppendLine($"{number,-15}{Core.Settings.FactionNames[influence.Key]}");
            }

            Traverse.Create(starSystem.Def.Description).Property("Details").SetValue(factionString.ToString());
        }

        public static void Postfix(object data, string __state)
        {
            var starSystem = (StarSystem) data;
            if (starSystem == null)
            {
                return;
            }

            Traverse.Create(starSystem.Def.Description).Property("Details").SetValue(__state);
        }
    }

    [HarmonyPatch(typeof(StarmapRenderer), "GetSystemRenderer")]
    [HarmonyPatch(new[] {typeof(StarSystemNode)})]
    public static class StarmapRenderer_GetSystemRenderer_Patch
    {
        public static void Postfix(ref StarmapSystemRenderer __result)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;

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

                var infDiff = highest - systemStatus.influenceTracker[systemStatus.owner];
                if (highestFaction != systemStatus.owner && infDiff < Core.Settings.TakeoverThreshold && infDiff >= 1)
                    contendedSystems.Add(systemStatus.name);
            }

            var visitedStarSystems = Traverse.Create(sim).Field("VisitedStarSystems").GetValue<List<string>>();
            var wasVisited = visitedStarSystems.Contains(__result.name);
            if (contendedSystems.Contains(__result.name))
                MakeSystemPurple(__result, wasVisited);
            else if (__result.systemColor == Color.magenta)
                MakeSystemNormal(__result, wasVisited);

            contendedSystems.Clear();
        }
    }

    private static void MakeSystemPurple(StarmapSystemRenderer __result, bool wasVisited)
    {
        var blackMarketIsActive = __result.blackMarketObj.gameObject.activeInHierarchy;
        var fpAvailableIsActive = __result.flashpointAvailableObj.gameObject.activeInHierarchy;
        var fpActiveIsActive = __result.flashpointActiveObj.gameObject.activeInHierarchy;

        __result.Init(__result.system, Color.magenta, __result.CanTravel, wasVisited);
        if (fpAvailableIsActive)
            __result.flashpointAvailableObj.SetActive(true);
        if (fpActiveIsActive)
            __result.flashpointActiveObj.SetActive(true);
        if (blackMarketIsActive)
            __result.blackMarketObj.gameObject.SetActive(true);
        Traverse.Create(__result).Field("selectedScale").SetValue(10f);
        Traverse.Create(__result).Field("deselectedScale").SetValue(8f);
    }

    private static void MakeSystemNormal(StarmapSystemRenderer __result, bool wasVisited)
    {
        __result.Init(__result.system, __result.systemColor, __result.CanTravel, wasVisited);
        Traverse.Create(__result).Field("selectedScale").SetValue(6f);
        Traverse.Create(__result).Field("deselectedScale").SetValue(4f);
        __result.starOuter.gameObject.SetActive(false);
    }
}
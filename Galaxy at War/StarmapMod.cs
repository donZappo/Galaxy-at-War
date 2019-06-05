using System.Collections.Generic;
using System.Linq;
using System.Text;
using BattleTech;
using BattleTech.UI.Tooltips;
using Harmony;
using UnityEngine;

public class StarmapMod
{
    private static readonly SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
    internal static TextPopup tooltip = new TextPopup("GaW", true);
    
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

            __state = starSystem.Def.Description.Details;
            var factionString = new StringBuilder();
            factionString.AppendLine(starSystem.Def.Description.Details);
            factionString.AppendLine();
            var tracker = Core.WarStatus.systems.Find(x => x.name == starSystem.Name);
            foreach (var foo in tracker.influenceTracker.OrderByDescending(x => x.Value))
            {
                string number;
                if (foo.Value == 100)
                    number = "100%";
                else if (foo.Value < 1)
                    number = "< 1%";
                else if (foo.Value > 99)
                    number = "> 99%";
                else
                    number = "> " + (int) foo.Value + "%";
                
                factionString.AppendLine($"{number,-15} {foo.Key}");
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
            //var foo = new GameObject("GaW");
            //foo.transform.SetParent(__result.transform);
            //foo.AddComponent<DotTooltip>();
            //var dotTooltip = foo.AddComponent<DotTooltip>();
            //dotTooltip.TooltipPopup = tooltip;
            //tooltip.SetText("POOOP");
            var mpb = Traverse.Create(__result).Property("mpb");
            Traverse.Create(mpb).Method("SetColor", new[] {typeof(Color)}).GetValue(Color.magenta);
            //StarmapSystemRenderer.mpb.SetColor("_Color", Color.magenta);
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
            
            //var red = 255 - __result.systemColor.r;
            //var green = 255 - __result.systemColor.g;
            //var blue = 255 - __result.systemColor.b;
            ////var alpha = 255 - __result.systemColor.a;
            //
            //var color = new Color(red, green, blue, __result.systemColor.a);
            //Logger.LogDebug($"{red} {green} {blue} {__result.systemColor.a}");

            if (contendedSystems.Contains(__result.name))
            {
                var visitedStarSystems = Traverse.Create(sim).Field("VisitedStarSystems").GetValue<List<string>>();
                var wasVisited = visitedStarSystems.Contains(__result.name);
                //__result.Init(__result.system, Color.magenta, __result.CanTravel, wasVisited);
                
            }
        }
    }
}
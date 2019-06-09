using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BattleTech;
using Harmony;
using Newtonsoft.Json;
using static Logger;
using Random = System.Random;
using BattleTech.UI;
using fastJSON;
using HBS;
using Error = BestHTTP.SocketIO.Error;

namespace Galaxy_at_War
{
    class HotSpots
    {
        public static Dictionary<string, float> contendedSystems = new Dictionary<string, float>();

        [HarmonyPatch(typeof(SimGameState), "StartGeneratePotentialContractsRoutine")]
        public static class SimGameState_StartGeneratePotentialContractsRoutine_Patch
        {
            static void Prefix(SimGameState __instance, ref StarSystem systemOverride)
            {
                try
                {
                    var usingBreadcrumbs = systemOverride != null;
                    if (usingBreadcrumbs)
                        systemOverride = __instance.StarSystems.Find(x => x.Name == "Detroit");
                }
                catch (Exception e)
                {
                    Error(e);
                }
            }
        }
        public static void ProcessHotSpots()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var DominantFaction = sim.CurSystem.Owner;
            var warFaction = Core.WarStatus.warFactionTracker.Find(x => x.faction == DominantFaction);
            var FullPriorityList = new Dictionary<StarSystem, float>();
            var PriorityList = new Dictionary<string, float>();

        contendedSystems.Clear();

            foreach (SystemStatus systemStatus in Core.WarStatus.systems)
            {
                systemStatus.PriorityAttack = false;
                systemStatus.PriorityDefense = false;

                if (systemStatus.owner == DominantFaction && systemStatus.Contended)
                {
                    systemStatus.PriorityDefense = true;
                    if (!FullPriorityList.Keys.Contains(systemStatus.starSystem))
                        FullPriorityList.Add(systemStatus.starSystem, systemStatus.TotalResources);
                }

                foreach (var targetFaction in warFaction.attackTargets.Keys)
                {
                    var factionDLT = Core.WarStatus.deathListTracker.Find(x => x.faction == warFaction.faction);
                    if (factionDLT.deathList[targetFaction] < Core.Settings.PriorityHatred) continue;

                    if (warFaction.attackTargets[targetFaction].Contains(systemStatus.starSystem))
                    {
                        systemStatus.PriorityAttack = true;
                        if (!FullPriorityList.Keys.Contains(systemStatus.starSystem))
                            FullPriorityList.Add(systemStatus.starSystem, systemStatus.TotalResources);
                    }
                }
            }


            int i = 0;
            while (i < 10 && FullPriorityList.Count != 0)
            {
                var highKey = FullPriorityList.OrderByDescending(x => x.Value).Select(x => x.Key).First();
                PriorityList.Add(highKey.Name, FullPriorityList[highKey]);
                FullPriorityList.Remove(highKey);
                i++;
            }
            contendedSystems = PriorityList;
        } 
    }
}

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
using UnityEngine;
using UnityEngine.UI;
using BattleTech.Framework;
using BattleTech.Save;
using BattleTech.Save.SaveGameStructure;
using TMPro;
using fastJSON;
using HBS;
using Error = BestHTTP.SocketIO.Error;
using DG.Tweening;
using BattleTech.Data;

namespace Galaxy_at_War
{
    public static class HotSpots
    {
        public static List<string> contendedSystems = new List<string>();
        public static List<StarSystem> BCTargets = new List<StarSystem>();
        public static bool isBreadcrumb = true;
        public static List<Faction> EmployerHolder = new List<Faction>();
        public static List<Faction> TargetHolder = new List<Faction>();

        public static void ProcessHotSpots()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var DominantFaction = sim.CurSystem.Owner;
            var warFaction = Core.WarStatus.warFactionTracker.Find(x => x.faction == DominantFaction);
            var FullPriorityList = new Dictionary<StarSystem, float>();

            contendedSystems.Clear();
            BCTargets.Clear();
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
                if (Core.Settings.DefensiveFactions.Contains(DominantFaction)) continue;
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
            warFaction.PriorityList.Clear();
            int i = 0;
            while (i < 6 && FullPriorityList.Count != 0)
            {
                var highKey = FullPriorityList.OrderByDescending(x => x.Value).Select(x => x.Key).First();
                warFaction.PriorityList.Add(highKey.Name);
                contendedSystems.Add(highKey.Name);
                BCTargets.Add(highKey);
                FullPriorityList.Remove(highKey);
                i++;
            }
        }
       
        [HarmonyPatch(typeof(StarSystem), "GenerateInitialContracts")]
        public static class SimGameState_GenerateInitialContracts_Patch
        {
            static void Prefix(StarSystem __instance)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                Traverse.Create(sim.CurSystem).Property("MissionsCompleted").SetValue(0);
                Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(0);
                Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(0);
            }

            static void Postfix(StarSystem __instance)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                Traverse.Create(sim.CurSystem).Property("MissionsCompleted").SetValue(20);
                Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(1);
                Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(1);
               
                ProcessHotSpots();
                if (BCTargets.Count != 0)
                {
                    var MainBCTarget = BCTargets[0];
                    TemporaryFlip(MainBCTarget);
                    sim.GeneratePotentialContracts(true, null, MainBCTarget, false);
                    Core.RefreshContracts(MainBCTarget);
                    BCTargets.RemoveAt(0);
                    if (BCTargets.Count != 0)
                    {
                        int i = 2;
                        foreach (var BCTarget in BCTargets)
                        {
                            Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(i);
                            Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(i);
                            TemporaryFlip(BCTarget);
                            sim.GeneratePotentialContracts(false, null, BCTarget, false);
                            Core.RefreshContracts(BCTarget);
                            i++;
                        }
                    }
                }
            }
        }

        public static void TemporaryFlip(StarSystem starSystem)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var owner = sim.CurSystem.Owner;
            starSystem.Def.ContractEmployers.Clear();
            starSystem.Def.ContractTargets.Clear();

            starSystem.Def.ContractEmployers.Add(owner);

            var tracker = Core.WarStatus.systems.Find(x => x.name == starSystem.Name);
            foreach (var influence in tracker.influenceTracker.OrderByDescending(x => x.Value))
            {
                if (influence.Key != sim.CurSystem.Owner)
                {
                    starSystem.Def.ContractTargets.Add(influence.Key);
                    break;
                }
            }
        }
    }
}
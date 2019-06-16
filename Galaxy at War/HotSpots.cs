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
        public static List<StarSystem> BreadcrumbTargets = new List<StarSystem>();
        public static bool isBreadcrumb = true;
        public static Dictionary<Faction, List<StarSystem>> ExternalPriorityList = new Dictionary<Faction, List<StarSystem>>();
        public static  System.Random rand = new System.Random();

        public static void ProcessHotSpots()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var DominantFaction = sim.CurSystem.Owner;
            var FullPriorityList = new Dictionary<StarSystem, int>();
            var FullExternalPriorityList = new Dictionary<Faction, List<KeyValuePair<StarSystem, int>>>();
            var warFaction = Core.WarStatus.warFactionTracker.Find(x => x.faction == DominantFaction);
            System.Random rand = new System.Random();

            ExternalPriorityList.Clear();
            contendedSystems.Clear();
            BreadcrumbTargets.Clear();

            var FactRepDict = new Dictionary<Faction, int>();
            foreach (var faction in Core.Settings.IncludedFactions)
            {
                if (Core.Settings.DefensiveFactions.Contains(faction)) continue;
                var MaxC = HotSpots.ProcessReputation(sim.GetRawReputation(faction));
                FactRepDict.Add(faction, MaxC);
            }

            //Populate lists with planets that are in danger of flipping
            foreach (SystemStatus systemStatus in Core.WarStatus.systems)
            {
                systemStatus.PriorityAttack = false;
                systemStatus.PriorityDefense = false;

                if (systemStatus.owner == DominantFaction && systemStatus.Contended && systemStatus.DifficultyRating <= FactRepDict[DominantFaction]
                     && systemStatus.DifficultyRating >= FactRepDict[DominantFaction] - 2)
                {
                    systemStatus.PriorityDefense = true;
                    if (!FullPriorityList.Keys.Contains(systemStatus.starSystem))
                        FullPriorityList.Add(systemStatus.starSystem, systemStatus.DifficultyRating);
                }
                else if (systemStatus.Contended && systemStatus.DifficultyRating <= FactRepDict[systemStatus.owner] && systemStatus.DifficultyRating >= FactRepDict[systemStatus.owner] - 2)
                {
                    systemStatus.PriorityDefense = true;
                    var tempKvP = new KeyValuePair<StarSystem, int>(systemStatus.starSystem, systemStatus.DifficultyRating);
                    List<KeyValuePair<StarSystem, int>> TempList = new List<KeyValuePair<StarSystem, int>>();
                    TempList.Add(tempKvP);

                    if (!FullExternalPriorityList.Keys.Contains(systemStatus.owner))
                        FullExternalPriorityList.Add(systemStatus.owner, TempList);
                    else
                        FullExternalPriorityList[systemStatus.owner].Add(tempKvP);
                }

                //Populate priority attack targets.
                if (Core.Settings.DefensiveFactions.Contains(DominantFaction)) continue;
                foreach (var targetFaction in warFaction.attackTargets.Keys)
                {
                    var factionDLT = Core.WarStatus.deathListTracker.Find(x => x.faction == DominantFaction);
                    if (factionDLT.deathList[targetFaction] >= Core.Settings.PriorityHatred && warFaction.attackTargets[targetFaction].Contains(systemStatus.starSystem) 
                        && systemStatus.DifficultyRating <= FactRepDict[DominantFaction] && systemStatus.DifficultyRating >= FactRepDict[DominantFaction] - 2)
                    {
                        systemStatus.PriorityAttack = true;
                        if (!FullPriorityList.Keys.Contains(systemStatus.starSystem))
                            FullPriorityList.Add(systemStatus.starSystem, systemStatus.DifficultyRating);
                    }
                }
            }
            foreach (Faction extFaction in WarStatus.PriorityTargets.Keys)
            {
                if (extFaction == DominantFaction) continue;
                if (!FullExternalPriorityList.Keys.Contains(extFaction))
                    FullExternalPriorityList.Add(extFaction, WarStatus.PriorityTargets[extFaction]);
                else
                    FullExternalPriorityList[extFaction].AddRange(WarStatus.PriorityTargets[extFaction]);
            }

            warFaction.PriorityList.Clear();

            while (FullPriorityList.Count != 0)
            {
                var highKey = FullPriorityList.OrderByDescending(x => x.Value).Select(x => x.Key).First();
                warFaction.PriorityList.Add(highKey.Name);
                if (contendedSystems.Count < 6)
                    contendedSystems.Add(highKey.Name);
                BreadcrumbTargets.Add(highKey);
                FullPriorityList.Remove(highKey);
            }

            var ExternalFactionRep = sim.GetAllCareerFactionReputations();
            foreach (var ExtFact in ExternalFactionRep.OrderByDescending(x => x.Value))
            {
                if (!FullExternalPriorityList.Keys.Contains(ExtFact.Key)) continue;
                var SortedEnumList = FullExternalPriorityList[ExtFact.Key].OrderByDescending(x => x.Value);
                var SortedList = new List<StarSystem>();
                foreach (var item in SortedEnumList)
                    SortedList.Add(item.Key);
                ExternalPriorityList.Add(ExtFact.Key, SortedList);
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
                sim.CurSystem.SystemBreadcrumbs.Clear();
                Traverse.Create(sim.CurSystem).Property("MissionsCompleted").SetValue(20);
                Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(1);
                Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(1);

                ProcessHotSpots();
                if (BreadcrumbTargets.Count != 0)
                {
                    while (sim.CurSystem.SystemBreadcrumbs.Count == 0 && BreadcrumbTargets.Count != 0)
                    {
                        var RandomSystem = rand.Next(0, BreadcrumbTargets.Count - 1);
                        var MainBCTarget = BreadcrumbTargets[RandomSystem];
                        TemporaryFlip(MainBCTarget, sim.CurSystem.Owner);
                        sim.GeneratePotentialContracts(true, null, MainBCTarget, false);
                        Core.RefreshContracts(MainBCTarget);
                        Log("A");
                        Log(MainBCTarget.Name);
                        BreadcrumbTargets.RemoveAt(RandomSystem);
                    }

                    if (BreadcrumbTargets.Count != 0)
                    {
                        int i = 2;
                        foreach (var BCTarget in BreadcrumbTargets)
                        {
                            Log("B");
                            if (i == Core.Settings.InternalHotSpots + 1) break;
                            Log(BCTarget.Name);
                            Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(i);
                            Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(i);
                            TemporaryFlip(BCTarget, sim.CurSystem.Owner);
                            sim.GeneratePotentialContracts(false, null, BCTarget, false);
                            Core.RefreshContracts(BCTarget);
                            i = sim.CurSystem.SystemBreadcrumbs.Count + 1;
                        }
                    }
                }
                if (ExternalPriorityList.Count != 0)
                {
                    int startBC = sim.CurSystem.SystemBreadcrumbs.Count;
                    int j = startBC;
                    foreach (var ExtTarget in ExternalPriorityList.Keys)
                    {
                        if (ExternalPriorityList[ExtTarget].Count == 0) continue;
                        
                        do
                        {
                            Log("C");
                            var RandTarget = rand.Next(0, ExternalPriorityList[ExtTarget].Count - 1);
                            Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(j + 1);
                            Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(j + 1);
                            TemporaryFlip(ExternalPriorityList[ExtTarget][RandTarget], ExtTarget);
                            sim.GeneratePotentialContracts(false, null, ExternalPriorityList[ExtTarget][RandTarget], false);
                            Core.RefreshContracts(ExternalPriorityList[ExtTarget][RandTarget]);
                            Log(ExternalPriorityList[ExtTarget][RandTarget].Name);
                            Log(ExternalPriorityList[ExtTarget][RandTarget].Def.GetDifficulty(SimGameState.SimGameType.CAREER).ToString());
                            ExternalPriorityList[ExtTarget].RemoveAt(RandTarget);
                        } while (sim.CurSystem.SystemBreadcrumbs.Count == j || ExternalPriorityList[ExtTarget].Count == 0);

                        j = sim.CurSystem.SystemBreadcrumbs.Count;
                        if (j - startBC == Core.Settings.ExternalHotSpots)
                            break;
                    }
                }
            }
        }

        public static void TemporaryFlip(StarSystem starSystem, Faction faction)
        {
            starSystem.Def.ContractEmployers.Clear();
            starSystem.Def.ContractTargets.Clear();

            starSystem.Def.ContractEmployers.Add(faction);

            var tracker = Core.WarStatus.systems.Find(x => x.name == starSystem.Name);
            foreach (var influence in tracker.influenceTracker.OrderByDescending(x => x.Value))
            {
                if (!Core.Settings.DefensiveFactions.Contains(influence.Key) && influence.Value > 1
                    && influence.Key != faction)
                {
                    starSystem.Def.ContractTargets.Add(influence.Key);
                    break;
                }
            }

            if (starSystem.Def.ContractTargets.Count == 0)
            starSystem.Def.ContractTargets.AddRange(Core.Settings.DefensiveFactions);
        }

        //Deployments area.
        [HarmonyPatch(typeof(SimGameState), "PrepareBreadcrumb")]
        public static class SimGameState_PrepareBreadcrumb_Patch
        {
            static void Postfix(Contract contract)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                var starSystem = sim.StarSystems.Find(x => x.Def.Description.Id == contract.TargetSystem);
                Core.WarStatus.systems.Find(x => x.name == starSystem.Name).HotBox = true;
            }
        }

        [HarmonyPatch(typeof(SGNavigationScreen), "OnTravelCourseAccepted")]
        public static class SGNavigationScreen_OnTravelCourseAccepted_Patch
        {
            static void Postfix(SGNavigationScreen __instance)
            {
                var system = UnityGameInstance.BattleTechGame.Simulation.CurSystem;
                Core.WarStatus.systems.Find(x => x.name == system.Name).HotBox = false;
            }
        }

        [HarmonyPatch(typeof(SGTravelManager), "DisplayEnteredOrbitPopup")]
        public static class Entered_Orbit_Patch
        {
            static void Postfix()
            {
                WarStatus.JustArrived = true;
                WarStatus.DeploymentDays = Core.Settings.DeploymentDays;
            }
        }

        [HarmonyPatch(typeof(AAR_SalvageScreen), "OnCompleted")]
        public static class AAR_SalvageScreen_OnCompleted_Patch
        {
            static void Postfix(AAR_SalvageScreen __instance)
            {
                WarStatus.JustArrived = false;
            }
        }

        [HarmonyPatch(typeof(TaskTimelineWidget), "RegenerateEntries")]
        public static class TaskTimelineWidget_RegenerateEntries_Patch
        {
            static void Postfix(TaskTimelineWidget __instance)
            {
                if (!WarStatus.JustArrived)
                {
                    WarStatus.DeploymentEnd = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric,
                        "Days Until Deployment Ends", "Days Until Deployment Ends");
                    WarStatus.DeploymentEnd.SetCost(WarStatus.DeploymentDays);
                    __instance.AddEntry(WarStatus.DeploymentEnd, false);
                }
                __instance.RefreshEntries();
            }
        }

        [HarmonyPatch(typeof(TaskTimelineWidget), "RemoveEntry")]
        public static class TaskTimelineWidget_RemoveEntry_Patch
        {
            static bool Prefix(WorkOrderEntry entry)
            {
                if (!WarStatus.JustArrived && (entry.ID.Equals("Days Until Deployment Ends")) && Core.Settings.DeploymentDays > 0)
                {
                    return false;
                }
                return true;
            }
        }
        
        public static void CompleteDeployment()
        {

        }

        public static int ProcessReputation(float FactionRep)
        {
            var simStory = UnityGameInstance.BattleTechGame.Simulation.Constants.Story;
            var simCareer = UnityGameInstance.BattleTechGame.Simulation.Constants.CareerMode;
            int MaxContracts = 1;

            
            if (FactionRep <= 100)
                MaxContracts = (int)simCareer.HonoredMaxContractDifficulty;
            if (FactionRep <= simStory.HonoredReputation)
                MaxContracts = (int)simCareer.FriendlyMaxContractDifficulty;
            if (FactionRep <= simStory.FriendlyReputation)
                MaxContracts = (int)simCareer.LikedMaxContractDifficulty;
            if (FactionRep <= simStory.LikedReputation)
                MaxContracts = (int)simCareer.IndifferentMaxContractDifficulty;
            if (FactionRep <= simStory.DislikedReputation)
                MaxContracts = (int)simCareer.DislikedMaxContractDifficulty;
            if (FactionRep <= simStory.HatedReputation)
                MaxContracts = (int)simCareer.HatedMaxContractDifficulty;
            if (FactionRep <= simStory.LoathedReputation)
                MaxContracts = (int)simCareer.LoathedMaxContractDifficulty;
            return MaxContracts;
        }
    }
}
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
        public static List<StarSystem> HomeContendedSystems = new List<StarSystem>();
        public static List<string> HomeContendedStrings = new List<string>();
        public static bool isBreadcrumb = true;
        public static  System.Random rand = new System.Random();
        public static bool EnemyAdded = false;
        public static Faction EnemyFaction;

        public static void ProcessHotSpots()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var DominantFaction = sim.CurSystem.Owner;
            var warFaction = Core.WarStatus.warFactionTracker.Find(x => x.faction == DominantFaction);
            System.Random rand = new System.Random();
            var FullHomeContendedSystems = new Dictionary<StarSystem, int>();
            WarStatus.ExternalPriorityTargets.Clear();
            HomeContendedSystems.Clear();
            HomeContendedStrings.Clear();

            var FactRepDict = new Dictionary<Faction, int>();
            foreach (var faction in Core.Settings.IncludedFactions)
            {
                WarStatus.ExternalPriorityTargets.Add(faction, new List<StarSystem>());
                if (Core.Settings.DefensiveFactions.Contains(faction)) continue;
                var MaxContracts = HotSpots.ProcessReputation(sim.GetRawReputation(faction));
                FactRepDict.Add(faction, MaxContracts);
            }

            //Populate lists with planets that are in danger of flipping
            foreach (SystemStatus systemStatus in Core.WarStatus.systems)
            {
                if (systemStatus.Contended && systemStatus.DifficultyRating <= FactRepDict[systemStatus.owner] && systemStatus.DifficultyRating >= FactRepDict[systemStatus.owner] - 2)
                    systemStatus.PriorityDefense = true;
                if (systemStatus.PriorityDefense)
                {
                    if (systemStatus.owner == DominantFaction)
                        FullHomeContendedSystems.Add(systemStatus.starSystem, systemStatus.DifficultyRating);
                    else
                        WarStatus.ExternalPriorityTargets[systemStatus.owner].Add(systemStatus.starSystem);
                }
                if (systemStatus.PriorityAttack)
                {
                    foreach (var attacker in systemStatus.CurrentlyAttackedBy)
                    {
                        if (attacker == DominantFaction)
                        {
                            if (!FullHomeContendedSystems.ContainsKey(systemStatus.starSystem))
                                FullHomeContendedSystems.Add(systemStatus.starSystem, systemStatus.DifficultyRating);
                        }
                        else
                        {
                            if (!WarStatus.ExternalPriorityTargets[attacker].Contains(systemStatus.starSystem))
                                WarStatus.ExternalPriorityTargets[attacker].Add(systemStatus.starSystem);
                        }
                    }
                }
            }
            var i = 0;
            foreach (var ContendedSystem in FullHomeContendedSystems.OrderByDescending(key => key.Value))
            {
                if (i == 6) break;
                HomeContendedSystems.Add(ContendedSystem.Key);
                HomeContendedStrings.Add(ContendedSystem.Key.Name);
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
                sim.CurSystem.SystemBreadcrumbs.Clear();
                Traverse.Create(sim.CurSystem).Property("MissionsCompleted").SetValue(20);
                Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(1);
                Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(1);

                ProcessHotSpots();
                if (HomeContendedSystems.Count != 0)
                {
                    Log(sim.CurSystem.SystemBreadcrumbs.Count.ToString());
                    while (sim.CurSystem.SystemBreadcrumbs.Count == 0 && HomeContendedSystems.Count != 0)
                    {
                        var RandomSystem = rand.Next(0, HomeContendedSystems.Count);
                        var MainBCTarget = HomeContendedSystems[RandomSystem];
                        TemporaryFlip(MainBCTarget, sim.CurSystem.Owner);
                        sim.GeneratePotentialContracts(true, null, MainBCTarget, false);
                        Core.RefreshContracts(MainBCTarget);
                        Log("A");
                        Log(MainBCTarget.Name);
                        HomeContendedSystems.RemoveAt(RandomSystem);
                    }
                    Log(sim.CurSystem.SystemBreadcrumbs.Count.ToString());
                    if (HomeContendedSystems.Count != 0)
                    {
                        int i = 2;
                        while (HomeContendedSystems.Count != 0)
                        {
                            Log("B");
                            var RandomSystem = rand.Next(0, HomeContendedSystems.Count);
                            var MainBCTarget = HomeContendedSystems[RandomSystem];
                            if (i == Core.Settings.InternalHotSpots + 1) break;
                            Log(MainBCTarget.Name);
                            Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(i);
                            Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(i);
                            TemporaryFlip(MainBCTarget, sim.CurSystem.Owner);
                            sim.GeneratePotentialContracts(false, null, MainBCTarget, false);
                            Core.RefreshContracts(MainBCTarget);
                            HomeContendedSystems.RemoveAt(RandomSystem);
                            i = sim.CurSystem.SystemBreadcrumbs.Count + 1;
                        }
                    }
                }
                Log(sim.CurSystem.SystemBreadcrumbs.Count.ToString());
                var ExternalPriorityTargets = WarStatus.ExternalPriorityTargets;
                if (ExternalPriorityTargets.Count != 0)
                {
                    int startBC = sim.CurSystem.SystemBreadcrumbs.Count;
                    int j = startBC;
                    foreach (var ExtTarget in ExternalPriorityTargets.Keys)
                    {
                        Log(sim.CurSystem.SystemBreadcrumbs.Count.ToString());
                        if (ExternalPriorityTargets[ExtTarget].Count == 0) continue;
                        do
                        {
                            Log("C");
                            var RandTarget = rand.Next(0, ExternalPriorityTargets[ExtTarget].Count);
                            Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(j + 1);
                            Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(j + 1);
                            TemporaryFlip(ExternalPriorityTargets[ExtTarget][RandTarget], ExtTarget);
                            sim.GeneratePotentialContracts(false, null, ExternalPriorityTargets[ExtTarget][RandTarget], false);
                            if (EnemyAdded)
                            {
                                sim.FactionsDict[ExtTarget].Enemies.
                            }
                            Core.RefreshContracts(ExternalPriorityTargets[ExtTarget][RandTarget]);
                            Log(ExternalPriorityTargets[ExtTarget][RandTarget].Name);
                            Log(ExternalPriorityTargets[ExtTarget][RandTarget].Def.GetDifficulty(SimGameState.SimGameType.CAREER).ToString());
                            ExternalPriorityTargets[ExtTarget].RemoveAt(RandTarget);
                        } while (sim.CurSystem.SystemBreadcrumbs.Count == j && ExternalPriorityTargets[ExtTarget].Count != 0);

                        j = sim.CurSystem.SystemBreadcrumbs.Count;
                        if (j - startBC == Core.Settings.ExternalHotSpots)
                            break;
                    }
                }
            }
        }

        public static void TemporaryFlip(StarSystem starSystem, Faction faction)
        {
            var FactionDef = UnityGameInstance.BattleTechGame.Simulation.FactionsDict[faction];
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
                    if (!FactionDef.Enemies.Contains(influence.Key))
                    {
                        FactionDef.Enemies.Add(influence.Key);
                        EnemyAdded = true;
                        EnemyFaction = influence.Key;
                    }
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
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
    public class HotSpots
    {
        public static bool isBreadcrumb = false;
        public static Random rand = new Random();
        public static int BonusMoney = 0;

        public static Dictionary<Faction, List<StarSystem>> ExternalPriorityTargets = new Dictionary<Faction, List<StarSystem>>();
        public static List<StarSystem> HomeContendedSystems = new List<StarSystem>();
        public static Dictionary<StarSystem, float> FullHomeContendedSystems = new Dictionary<StarSystem, float>();

        public static void ProcessHotSpots()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var DominantFaction = Core.WarStatus.systems.Find(x => x.starSystem.Name == Core.WarStatus.CurSystem).owner;
            FullHomeContendedSystems.Clear();
            HomeContendedSystems.Clear();
            ExternalPriorityTargets.Clear();
            Core.WarStatus.HomeContendedStrings.Clear();
            Core.WarStatus.ContendedStrings.Clear();
            var FactRepDict = new Dictionary<Faction, int>();
            foreach (var faction in Core.Settings.IncludedFactions)
            {
                ExternalPriorityTargets.Add(faction, new List<StarSystem>());
                var MaxContracts = ProcessReputation(sim.GetRawReputation(faction));
                FactRepDict.Add(faction, MaxContracts);
            }

            //Populate lists with planets that are in danger of flipping
            foreach (var systemStatus in Core.WarStatus.systems)
            {
                if (systemStatus.name != sim.CurSystem.Name || (systemStatus.name == sim.CurSystem.Name && !Core.WarStatus.HotBox.Contains(sim.CurSystem.Name)))
                {
                    systemStatus.BonusCBills = false;
                    systemStatus.BonusSalvage = false;
                    systemStatus.BonusXP = false;
                }

                if (systemStatus.Contended)
                    Core.WarStatus.ContendedStrings.Add(systemStatus.name);
                if (systemStatus.Contended && systemStatus.DifficultyRating <= FactRepDict[systemStatus.owner] 
                    && systemStatus.DifficultyRating >= FactRepDict[systemStatus.owner] - 4)
                    systemStatus.PriorityDefense = true;
                if (systemStatus.PriorityDefense)
                {
                    if (systemStatus.owner == DominantFaction)
                        FullHomeContendedSystems.Add(systemStatus.starSystem, systemStatus.TotalResources);
                    else
                        ExternalPriorityTargets[systemStatus.owner].Add(systemStatus.starSystem);
                }
                if (systemStatus.PriorityAttack)
                {
                    foreach (var attacker in systemStatus.CurrentlyAttackedBy)
                    {
                        if (attacker == DominantFaction)
                        {
                            if (!FullHomeContendedSystems.Keys.Contains(systemStatus.starSystem))
                                FullHomeContendedSystems.Add(systemStatus.starSystem, systemStatus.TotalResources);
                        }
                        else
                        {
                            if (!ExternalPriorityTargets[attacker].Contains(systemStatus.starSystem))
                                ExternalPriorityTargets[attacker].Add(systemStatus.starSystem);
                        }
                    }
                }
            }
            var i = 0;
            foreach (var system in FullHomeContendedSystems.OrderByDescending(x => x.Value))
            {
                if (i < 6)
                {
                    Core.WarStatus.HomeContendedStrings.Add(system.Key.Name);
                }

                HomeContendedSystems.Add(system.Key);
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
                isBreadcrumb = true;
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                sim.CurSystem.SystemBreadcrumbs.Clear();
                Traverse.Create(sim.CurSystem).Property("MissionsCompleted").SetValue(20);
                Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(1);
                Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(1);
                if (HomeContendedSystems.Count != 0)
                {
                    int i = 0;
                    while (HomeContendedSystems.Count != 0)
                    {
                        Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(i + 1);
                        Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(i + 1);
                        var RandomSystem = rand.Next(0, HomeContendedSystems.Count);
                        var MainBCTarget = HomeContendedSystems[RandomSystem];
                        TemporaryFlip(MainBCTarget, sim.CurSystem.Owner);
                        if (sim.CurSystem.SystemBreadcrumbs.Count == 0)
                            sim.GeneratePotentialContracts(true, null, MainBCTarget, false);
                        else
                            sim.GeneratePotentialContracts(false, null, MainBCTarget, false);
                        SystemBonuses(MainBCTarget);
                        Core.RefreshContracts(MainBCTarget);
                        HomeContendedSystems.Remove(MainBCTarget);
                        if (sim.CurSystem.SystemBreadcrumbs.Count == Core.Settings.InternalHotSpots)
                            break;
                        i = sim.CurSystem.SystemBreadcrumbs.Count;
                    }
                }
                if (ExternalPriorityTargets.Count != 0)
                {
                    int startBC = sim.CurSystem.SystemBreadcrumbs.Count;
                    int j = startBC;
                    foreach (var ExtTarget in ExternalPriorityTargets.Keys)
                    {
                        if (ExternalPriorityTargets[ExtTarget].Count == 0) continue;
                        do
                        {
                            var RandTarget = rand.Next(0, ExternalPriorityTargets[ExtTarget].Count);
                            Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(j + 1);
                            Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(j + 1);
                            TemporaryFlip(ExternalPriorityTargets[ExtTarget][RandTarget], ExtTarget);
                            if (sim.CurSystem.SystemBreadcrumbs.Count == 0)
                                sim.GeneratePotentialContracts(true, null, ExternalPriorityTargets[ExtTarget][RandTarget], false);
                            else
                                sim.GeneratePotentialContracts(false, null, ExternalPriorityTargets[ExtTarget][RandTarget], false);
                            SystemBonuses(ExternalPriorityTargets[ExtTarget][RandTarget]);
                            Core.RefreshContracts(ExternalPriorityTargets[ExtTarget][RandTarget]);
                            ExternalPriorityTargets[ExtTarget].RemoveAt(RandTarget);
                        } while (sim.CurSystem.SystemBreadcrumbs.Count == j && ExternalPriorityTargets[ExtTarget].Count != 0);

                        j = sim.CurSystem.SystemBreadcrumbs.Count;
                        if (j - startBC == Core.Settings.ExternalHotSpots)
                            break;
                    }
                }
                isBreadcrumb = false;
            }
        }
        [HarmonyPatch(typeof(StarSystem))]
        [HarmonyPatch("InitialContractsFetched", MethodType.Getter)]
        public static class StarSystem_InitialContractsFetched_Patch
        {
            static void Postfix(ref bool __result)
            {
                __result = true;
            }
        }

        [HarmonyPatch(typeof(SimGameState), "GetDifficultyRangeForContract")]
        public static class SimGameState_GetDifficultyRangeForContracts_Patch
        {
            static void Prefix(SimGameState __instance, ref int __state)
            {
                if (isBreadcrumb)
                {
                    __state = __instance.Constants.Story.ContractDifficultyVariance;
                    __instance.Constants.Story.ContractDifficultyVariance = 0;
                }
            }

            static void Postfix(SimGameState __instance, ref int __state)
            {
                if (isBreadcrumb)
                {
                    __instance.Constants.Story.ContractDifficultyVariance = __state;
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
                    break;
                }
            }

            if (starSystem.Def.ContractTargets.Count == 0)
            {
                starSystem.Def.ContractTargets.Add(Faction.AuriganPirates);
                if (!Core.WarStatus.AbandonedSystems.Contains(starSystem.Name))
                    starSystem.Def.ContractTargets.Add(Faction.Locals);
            }
            
        }

        //Deployments area.
        [HarmonyPatch(typeof(SimGameState), "PrepareBreadcrumb")]
        public static class SimGameState_PrepareBreadcrumb_Patch
        {
            static void Postfix(SimGameState __instance, Contract contract)
            {
                if (!__instance.CurSystem.Def.Description.Id.StartsWith(contract.TargetSystem))
                {
                    var starSystem = __instance.StarSystems.Find(x => x.Def.Description.Id.StartsWith(contract.TargetSystem));
                    Core.WarStatus.HotBox.Add(starSystem.Name);
                    Core.WarStatus.HotBoxTravelling = true;
                    TemporaryFlip(starSystem, contract.Override.employerTeam.faction);
                    var curSystem = Core.WarStatus.systems.Find(x => x.starSystem == __instance.CurSystem);
                    if (Core.WarStatus.HotBox.Contains(__instance.CurSystem.Name))
                    {
                        Core.WarStatus.HotBox.Remove(__instance.CurSystem.Name);
                        Core.WarStatus.EscalationDays = 0;
                        Core.WarStatus.Escalation = false;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(SGNavigationScreen), "OnTravelCourseAccepted")]
        public static class SGNavigationScreen_OnTravelCourseAccepted_Patch
        {
            static void Postfix(SGNavigationScreen __instance)
            {
                var system = UnityGameInstance.BattleTechGame.Simulation.CurSystem;
                Core.WarStatus.HotBox.Remove(system.Name);
                Core.WarStatus.Escalation = false;
                Core.WarStatus.EscalationDays = 0;
                Core.RefreshContracts(system);
            }
        }

        [HarmonyPatch(typeof(SGTravelManager), "DisplayEnteredOrbitPopup")]
        public static class Entered_Orbit_Patch
        {
            static void Postfix()
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                Core.WarStatus.JustArrived = true;
                Core.WarStatus.EscalationDays = Core.Settings.EscalationDays;
                ProcessHotSpots();
                if (!Core.WarStatus.HotBoxTravelling && !Core.WarStatus.HotBox.Contains(sim.CurSystem.Name))
                {
                    var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                    sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                }
            }
        }

        [HarmonyPatch(typeof(AAR_SalvageScreen), "OnCompleted")]
        public static class AAR_SalvageScreen_OnCompleted_Patch
        {
            static void Postfix(AAR_SalvageScreen __instance)
            {
                Core.WarStatus.JustArrived = false;
                Core.WarStatus.HotBoxTravelling = false;
            }
        }

        [HarmonyPatch(typeof(TaskTimelineWidget), "RegenerateEntries")]
        public static class TaskTimelineWidget_RegenerateEntries_Patch
        {
            static void Postfix(TaskTimelineWidget __instance)
            {
                if (Core.WarStatus != null && Core.WarStatus.Escalation)
                {
                    Core.WarStatus.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric,
                        "Escalation Days Remaining", "Escalation Days Remaining");
                    Core.WarStatus.EscalationOrder.SetCost(Core.WarStatus.EscalationDays);
                    __instance.AddEntry(Core.WarStatus.EscalationOrder, false);
                    __instance.RefreshEntries();
                }
            }
        }

        [HarmonyPatch(typeof(TaskTimelineWidget), "RemoveEntry")]
        public static class TaskTimelineWidget_RemoveEntry_Patch
        {
            static bool Prefix(WorkOrderEntry entry)
            {
                if (!Core.WarStatus.JustArrived && (entry.ID.Equals("Escalation Days Remaining")) && Core.WarStatus.EscalationDays > 0)
                {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(SimGameState), "OnBreadcrumbArrival")]
        public static class SimGameState_OnBreadcrumbArrival_Patch
        {
            static void Postfix(SimGameState __instance)
            {
                if (!__instance.ActiveTravelContract.IsPriorityContract)
                {
                    Core.WarStatus.Escalation = true;
                    Core.WarStatus.EscalationDays = Core.Settings.EscalationDays;
                    Core.WarStatus.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric, "Escalation Days Remaining", "Escalation Days Remaining");
                    Core.WarStatus.EscalationOrder.SetCost(Core.WarStatus.EscalationDays);
                    __instance.RoomManager.AddWorkQueueEntry(Core.WarStatus.EscalationOrder);
                    __instance.RoomManager.SortTimeline();
                    __instance.RoomManager.RefreshTimeline();
                }
            }
        }
        [HarmonyPatch(typeof(Contract), "GenerateSalvage")]
        public static class Contract_GenerateSalvage_Patch
        {
            static void Postfix(Contract __instance)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                var system = Core.WarStatus.systems.Find(x => x.starSystem == sim.CurSystem);

                if (system.BonusSalvage && Core.WarStatus.HotBox.Contains(sim.CurSystem.Name))
                {
                    var NewSalvageCount = __instance.FinalSalvageCount + 1;
                    Traverse.Create(__instance).Property("FinalSalvageCount").SetValue(NewSalvageCount);

                    if (__instance.FinalPrioritySalvageCount < 7)
                    {
                        var NewPrioritySalvage = __instance.FinalPrioritySalvageCount + 1;
                        Traverse.Create(__instance).Property("FinalPrioritySalvageCount").SetValue(NewPrioritySalvage);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(AAR_ContractObjectivesWidget), "FillInObjectives")]
        public static class AAR_ContractObjectivesWidget_FillInObjectives
        {
            static void Postfix(AAR_ContractObjectivesWidget __instance)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                var system = Core.WarStatus.systems.Find(x => x.starSystem == sim.CurSystem);

                if (system.BonusCBills && Core.WarStatus.HotBox.Contains(sim.CurSystem.Name))
                {
                    string missionObjectiveResultString = $"BONUS FROM ESCALTION: ¢{String.Format("{0:n0}", BonusMoney)}";
                    MissionObjectiveResult missionObjectiveResult = new MissionObjectiveResult(missionObjectiveResultString, "7facf07a-626d-4a3b-a1ec-b29a35ff1ac0", false, true, ObjectiveStatus.Succeeded, false);
                    Traverse.Create(__instance).Method("AddObjective", missionObjectiveResult).GetValue();
                }
            }
        }

        [HarmonyPatch(typeof(Contract), "CompleteContract")]
        public static class Contract_CompleteContract_Patch
        {
            static void Postfix(Contract __instance)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                var system = Core.WarStatus.systems.Find(x => x.starSystem == sim.CurSystem);
                if (system.BonusCBills && Core.WarStatus.HotBox.Contains(sim.CurSystem.Name))
                {
                    BonusMoney = (int)(__instance.MoneyResults * Core.Settings.BonusCbillsFactor);
                    int newMoneyResults = Mathf.FloorToInt(__instance.MoneyResults + BonusMoney);
                    Traverse.Create(__instance).Property("set_MoneyResults").SetValue(newMoneyResults);
                }
            }
        }
        [HarmonyPatch(typeof(AAR_UnitStatusWidget), "FillInPilotData")]
        public static class AAR_UnitStatusWidget_Patch
        {
            static void Prefix(ref int xpEarned)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                var system = Core.WarStatus.systems.Find(x => x.name == Core.WarStatus.CurSystem);
                if (system.BonusXP && Core.WarStatus.HotBox.Contains(system.name))
                {
                    xpEarned = xpEarned + (int)(xpEarned * Core.Settings.BonusXPFactor);
                }
            }
        }

        public static void SystemBonuses(StarSystem starSystem)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var system = Core.WarStatus.systems.Find(x => x.starSystem == starSystem);
            System.Random rand = new System.Random();

            if (starSystem.Name != sim.CurSystem.Name || (starSystem.Name == sim.CurSystem.Name && !Core.WarStatus.HotBox.Contains(sim.CurSystem.Name)))
            {
                system.BonusCBills = false;
                system.BonusSalvage = false;
                system.BonusXP = false;

                if (system.DifficultyRating <= 4)
                {
                    var bonus = rand.Next(0, 3);
                    if (bonus == 0)
                        system.BonusCBills = true;
                    if (bonus == 1)
                        system.BonusXP = true;
                    if (bonus == 2)
                        system.BonusSalvage = true;
                }
                if (system.DifficultyRating <= 8 && system.DifficultyRating > 4)
                {
                    system.BonusCBills = true;
                    system.BonusSalvage = true;
                    system.BonusXP = true;
                    var bonus = rand.Next(0, 3);
                    if (bonus == 0)
                        system.BonusCBills = false;
                    if (bonus == 1)
                        system.BonusXP = false;
                    if (bonus == 2)
                        system.BonusSalvage = false;
                }
                if (system.DifficultyRating <= 10 && system.DifficultyRating > 8)
                {
                    system.BonusCBills = true;
                    system.BonusSalvage = true;
                    system.BonusXP = true;
                }
            }
        }

        public static void CompleteEscalation()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var system = Core.WarStatus.systems.Find(x => x.starSystem == sim.CurSystem);
            system.BonusCBills = false;

            system.BonusSalvage = false;
            system.BonusXP = false;
            Core.WarStatus.HotBox.Remove(system.name);
            Core.RefreshContracts(system.starSystem);
            var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
            sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
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
        [HarmonyPatch(typeof(SGRoomController_CmdCenter), "StartContractScreen")]
        public static class SGRoomController_CmdCenter_StartContractScreen_Patch
        {
            static void Prefix(SGRoomController_CmdCenter __instance)
            {
                if (Core.WarStatus != null && !Core.WarStatus.StartGameInitialized)
                {
                    ProcessHotSpots();
                   // StarmapMod.SetupRelationPanel();
                    Core.WarStatus.StartGameInitialized = true;
                }
            }
        }

    }
}
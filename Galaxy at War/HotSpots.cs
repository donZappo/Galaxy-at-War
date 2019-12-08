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

        public static Dictionary<string, List<StarSystem>> ExternalPriorityTargets = new Dictionary<string, List<StarSystem>>();
        public static List<StarSystem> HomeContendedSystems = new List<StarSystem>();
        public static Dictionary<StarSystem, float> FullHomeContendedSystems = new Dictionary<StarSystem, float>();

        public static void ProcessHotSpots()
        {
            try { }
            catch (Exception e)
            {
                Error(e);
            }
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var DominantFaction = Core.WarStatus.systems.Find(x => x.starSystem.Name == Core.WarStatus.CurSystem).owner;
            FullHomeContendedSystems.Clear();
            HomeContendedSystems.Clear();
            ExternalPriorityTargets.Clear();
            Core.WarStatus.HomeContendedStrings.Clear();
            Core.WarStatus.ContendedStrings.Clear();
            var FactRepDict = new Dictionary<string, int>();
            foreach (var faction in Core.Settings.IncludedFactions)
            {
                ExternalPriorityTargets.Add(faction, new List<StarSystem>());
                var MaxContracts = ProcessReputation(sim.GetRawReputation(Core.FactionValues.Find(x => x.Name == faction)));
                FactRepDict.Add(faction, MaxContracts);
            }

            //Populate lists with planets that are in danger of flipping
            foreach (var systemStatus in Core.WarStatus.systems)
            {
                if (!Core.WarStatus.HotBox.Contains(systemStatus.name))
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
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                Traverse.Create(sim.CurSystem).Property("MissionsCompleted").SetValue(0);
                Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(0);
                Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(0);
            }


            static void Postfix(StarSystem __instance)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                if (Core.NeedsProcessing)
                    ProcessHotSpots();
                
                isBreadcrumb = true;
                sim.CurSystem.SystemBreadcrumbs.Clear();
                Traverse.Create(sim.CurSystem).Property("MissionsCompleted").SetValue(20);
                Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(1);
                Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(1);
                
                if (HomeContendedSystems.Count != 0)
                {
                    int i = 0;
                    int twiddle = 1;
                    var RandomSystem = 0;
                    while (HomeContendedSystems.Count != 0)
                    {
                        Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(i + 1);
                        Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(i + 1);
                        if (twiddle == 1)
                            RandomSystem = rand.Next(HomeContendedSystems.Count / 2, HomeContendedSystems.Count);
                        if (twiddle == -1)
                            RandomSystem = rand.Next(0, HomeContendedSystems.Count / 2);
                       
                        var MainBCTarget = HomeContendedSystems[RandomSystem];
                        
                        if (MainBCTarget == sim.CurSystem || (sim.CurSystem.OwnerValue.Name == "Locals" && MainBCTarget.OwnerValue.Name != "Locals"))
                        {
                            HomeContendedSystems.Remove(MainBCTarget);
                            continue;
                        }
                        TemporaryFlip(MainBCTarget, sim.CurSystem.OwnerValue.Name);
                        if (sim.CurSystem.SystemBreadcrumbs.Count == 0)
                        {
                            sim.GeneratePotentialContracts(true, null, MainBCTarget, false);
                            
                        }
                        else
                        {
                            sim.GeneratePotentialContracts(false, null, MainBCTarget, false);
                            SystemBonuses(MainBCTarget);
                        }

                        Core.RefreshContracts(MainBCTarget);
                        HomeContendedSystems.Remove(MainBCTarget);
                        if (sim.CurSystem.SystemBreadcrumbs.Count == Core.Settings.InternalHotSpots)
                            break;

                        i = sim.CurSystem.SystemBreadcrumbs.Count;
                        twiddle *= -1;
                    }
                }
                var PrioritySystem =
                    sim.CurSystem.SystemBreadcrumbs.FirstOrDefault(x => x.Name != sim.CurSystem.Name);
                Traverse.Create(PrioritySystem.Override).Field("contractDisplayStyle").SetValue(ContractDisplayStyle.BaseCampaignStory);
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
                            if (ExternalPriorityTargets[ExtTarget][RandTarget] == sim.CurSystem)
                            {
                                ExternalPriorityTargets[ExtTarget].Remove(sim.CurSystem);
                                continue;
                            }
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

        //[HarmonyPatch(typeof(SimGameState), "GeneratePotentialContracts")]
        //public static class SimGameState_GeneratePotentialContracts_Patch
        //{
        //    static void Prefix(ref StarSystem systemOverride)
        //    {
        //        if (systemOverride != null && !Core.NeedsProcessing)
        //            systemOverride = null;
        //    }
        //}


        [HarmonyPatch(typeof(StarSystem))]
        [HarmonyPatch("InitialContractsFetched", MethodType.Getter)]
        public static class StarSystem_InitialContractsFetched_Patch
        {
            static void Postfix(ref bool __result)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                __result = true;
            }
        }

        [HarmonyPatch(typeof(SimGameState), "GetDifficultyRangeForContract")]
        public static class SimGameState_GetDifficultyRangeForContracts_Patch
        {
            static void Prefix(SimGameState __instance, ref int __state)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                if (isBreadcrumb)
                {
                    __state = __instance.Constants.Story.ContractDifficultyVariance;
                    __instance.Constants.Story.ContractDifficultyVariance = 0;
                }
            }

            static void Postfix(SimGameState __instance, ref int __state)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                if (isBreadcrumb)
                {
                    __instance.Constants.Story.ContractDifficultyVariance = __state;
                }
            }
        }


        public static void TemporaryFlip(StarSystem starSystem, string faction)
        {
            var FactionDef = UnityGameInstance.BattleTechGame.Simulation.GetFactionDef(faction);
            starSystem.Def.ContractEmployerIDList.Clear();
            starSystem.Def.ContractTargetIDList.Clear();

            starSystem.Def.ContractEmployerIDList.Add(faction);

            var tracker = Core.WarStatus.systems.Find(x => x.name == starSystem.Name);
            foreach (var influence in tracker.influenceTracker.OrderByDescending(x => x.Value))
            {
                if (!Core.Settings.DefensiveFactions.Contains(influence.Key) && influence.Value > 1
                    && influence.Key != faction)
                {
                    starSystem.Def.ContractTargetIDList.Add(influence.Key);
                    break;
                }
            }
            if (starSystem.Def.ContractTargetIDList.Count <= 1)
            {
                starSystem.Def.ContractTargetIDList.Add("AuriganPirates");
                if (!Core.WarStatus.AbandonedSystems.Contains(starSystem.Name))
                    starSystem.Def.ContractTargetIDList.Add("Locals");
            }
        }

        //Deployments area.
        [HarmonyPatch(typeof(SimGameState), "PrepareBreadcrumb")]
        public static class SimGameState_PrepareBreadcrumb_Patch
        {
            static void Postfix(SimGameState __instance, Contract contract)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                if (!__instance.CurSystem.Def.Description.Id.StartsWith(contract.TargetSystem))
                {
                    var starSystem = __instance.StarSystems.Find(x => x.Def.Description.Id.StartsWith(contract.TargetSystem));
                    Core.WarStatus.HotBox.Add(starSystem.Name);
                    Core.WarStatus.HotBoxTravelling = true;
                    TemporaryFlip(starSystem, contract.Override.employerTeam.FactionValue.Name);
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
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

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
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                bool HasFlashpoint = false;
                Core.WarStatus.JustArrived = true;
                Core.WarStatus.EscalationDays = Core.Settings.EscalationDays;
                foreach (var contract in sim.CurSystem.SystemContracts)
                {
                    if (contract.IsFlashpointContract)
                        HasFlashpoint = true;
                }
                if (!Core.WarStatus.HotBoxTravelling && !Core.WarStatus.HotBox.Contains(sim.CurSystem.Name) && !HasFlashpoint)
                {
                    Core.NeedsProcessing = true;
                    var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                    sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                    Core.NeedsProcessing = false;
                }
            }
        }

        [HarmonyPatch(typeof(AAR_SalvageScreen), "OnCompleted")]
        public static class AAR_SalvageScreen_OnCompleted_Patch
        {
            static void Postfix(AAR_SalvageScreen __instance)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                Core.WarStatus.JustArrived = false;
                Core.WarStatus.HotBoxTravelling = false;
            }
        }

        [HarmonyPatch(typeof(TaskTimelineWidget), "RegenerateEntries")]
        public static class TaskTimelineWidget_RegenerateEntries_Patch
        {
            static void Postfix(TaskTimelineWidget __instance)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

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
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return true;

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
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

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
                //Log("****Generate Salvage****");
                //Log("Sim Null? " + (sim == null).ToString());
                //Log("CurSystem Null? " + (sim.CurSystem == null).ToString());
                //Log("CurSystem: " + sim.CurSystem.Name);
                //Log("WarStatus Null? " + (Core.WarStatus == null).ToString());
                //Log("WarStatus System Null? " + (null ==Core.WarStatus.systems.Find(x => x.name == sim.CurSystem.Name)).ToString());
                //foreach (SystemStatus systemstatus in Core.WarStatus.systems)
                //{
                //    Log(systemstatus.name);
                //    Log(systemstatus.starSystem.Name);
                //}

                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = Core.WarStatus.systems.Find(x => x.name == sim.CurSystem.Name);
                if (Core.WarStatus.HotBox == null)
                    Core.WarStatus.HotBox = new List<string>();

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
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = Core.WarStatus.systems.Find(x => x.name == sim.CurSystem.Name);

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
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = Core.WarStatus.systems.Find(x => x.name == sim.CurSystem.Name);
                if (system.BonusCBills && Core.WarStatus.HotBox.Contains(sim.CurSystem.Name))
                {
                    BonusMoney = (int)(__instance.MoneyResults * Core.Settings.BonusCbillsFactor);
                    int newMoneyResults = Mathf.FloorToInt(__instance.MoneyResults + BonusMoney);
                    Traverse.Create(__instance).Property("MoneyResults").SetValue(newMoneyResults);
                }
            }
        }
        [HarmonyPatch(typeof(AAR_UnitStatusWidget), "FillInPilotData")]
        public static class AAR_UnitStatusWidget_Patch
        {
            static void Prefix(ref int xpEarned, UnitResult ___UnitData)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = Core.WarStatus.systems.Find(x => x.name == Core.WarStatus.CurSystem);
                if (system.BonusXP && Core.WarStatus.HotBox.Contains(system.name))
                {
                    xpEarned = xpEarned + (int)(xpEarned * Core.Settings.BonusXPFactor);
                    int unspentXP = ___UnitData.pilot.UnspentXP;
                    int XPCorrection = (int)(xpEarned * Core.Settings.BonusXPFactor);
                    ___UnitData.pilot.StatCollection.Set<int>("ExperienceUnspent", unspentXP + XPCorrection);
                }
            }
        }

        public static void SystemBonuses(StarSystem starSystem)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var system = Core.WarStatus.systems.Find(x => x.starSystem == starSystem);
            System.Random rand = new System.Random();

            if (!Core.WarStatus.HotBox.Contains(starSystem.Name))
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
            bool HasFlashpoint = false;
            foreach (var contract in sim.CurSystem.SystemContracts)
            {
                if (contract.IsFlashpointContract)
                    HasFlashpoint = true;
            }
            if (!HasFlashpoint)
            {
                Core.NeedsProcessing = true;
                var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                Core.NeedsProcessing = false;
            }
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
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                if (Core.WarStatus != null && !Core.WarStatus.StartGameInitialized)
                {
                    ProcessHotSpots();
                    // StarmapMod.SetupRelationPanel();
                    Core.WarStatus.StartGameInitialized = true;
                }
            }
        }

        [HarmonyPatch(typeof(TaskTimelineWidget), "OnTaskDetailsClicked")]
        public static class TaskTimelineWidget_OnTaskDetailsClicked_Patch
        {
            public static bool Prefix(TaskManagementElement element)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return true;

                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    AdvanceToTask.StartAdvancing(element.Entry);
                    return false;
                }

                return true;
            }
        }

        //Make contracts always available for escalations
        [HarmonyPatch(typeof(StarSystem), "CompletedContract")]
        public static class StarSystem_CompletedContract_Patch
        {
            public static void Prefix(StarSystem __instance, ref float __state)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                __state = __instance.Sim.Constants.Story.ContractSuccessReduction;
                if (Core.WarStatus.HotBox.Contains(sim.CurSystem.Name))
                    __instance.Sim.Constants.Story.ContractSuccessReduction = 0;
            }

            public static void Postfix(StarSystem __instance, ref float __state)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                __instance.Sim.Constants.Story.ContractSuccessReduction = __state;
            }
        }
    }
}
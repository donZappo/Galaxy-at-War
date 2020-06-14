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
            foreach (var faction in Core.IncludedFactions)
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
                if (i < FullHomeContendedSystems.Count())
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
            static void Prefix(StarSystem __instance, ref float __state)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                Traverse.Create(sim.CurSystem).Property("MissionsCompleted").SetValue(0);
                Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(0);
                Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(0);
                __state = sim.CurSystem.CurMaxContracts;

                foreach (var theFaction in Core.IncludedFactions)
                {
                    if (Core.WarStatus.deathListTracker.Find(x => x.faction == theFaction) == null)
                        continue;

                    var deathListTracker = Core.WarStatus.deathListTracker.Find(x => x.faction == theFaction);
                    Core.AdjustDeathList(deathListTracker, sim, true);
                }


                if (Core.WarStatus.Deployment)
                {
                    Traverse.Create(sim.CurSystem).Property("CurMaxContracts").SetValue(Core.Settings.DeploymentContracts);

                }
            }


            static void Postfix(StarSystem __instance, ref float __state)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                if (Core.NeedsProcessing)
                    ProcessHotSpots();
                
                isBreadcrumb = true;
                sim.CurSystem.activeSystemBreadcrumbs.Clear();
                Traverse.Create(sim.CurSystem).Property("MissionsCompleted").SetValue(20);
                Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(1);
                Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(1);
                Core.WarStatus.DeploymentContracts.Clear();

                if (HomeContendedSystems.Count != 0  && !Core.Settings.DefensiveFactions.Contains(sim.CurSystem.OwnerValue.Name) && !Core.WarStatus.Deployment)
                {
                    int i = 0;
                    int twiddle = 0;
                    var RandomSystem = 0;
                    Core.WarStatus.HomeContendedStrings.Clear();
                    while (HomeContendedSystems.Count != 0)
                    {
                        Traverse.Create(sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(i + 1);
                        Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(i + 1);
                        if (twiddle == 0)
                            twiddle = -1;
                        else if (twiddle == 1)
                            RandomSystem = rand.Next(0 , 3 * HomeContendedSystems.Count / 4);
                        else if (twiddle == -1)
                            RandomSystem = rand.Next(HomeContendedSystems.Count / 4, 3 * HomeContendedSystems.Count / 4);
                       
                        var MainBCTarget = HomeContendedSystems[RandomSystem];
                        
                        if (MainBCTarget == sim.CurSystem || (sim.CurSystem.OwnerValue.Name == "Locals" && MainBCTarget.OwnerValue.Name != "Locals") ||
                            !Core.IncludedFactions.Contains(MainBCTarget.OwnerValue.Name))
                        {
                            HomeContendedSystems.Remove(MainBCTarget);
                            Core.WarStatus.HomeContendedStrings.Remove(MainBCTarget.Name);
                            continue;
                        }
                        TemporaryFlip(MainBCTarget, sim.CurSystem.OwnerValue.Name);
                        if (sim.CurSystem.SystemBreadcrumbs.Count == 0 && MainBCTarget.OwnerValue.Name != sim.CurSystem.OwnerValue.Name)
                        {
                            sim.GeneratePotentialContracts(true, null, MainBCTarget, false);
                            SystemBonuses(MainBCTarget);

                            var PrioritySystem = sim.CurSystem.SystemBreadcrumbs.Find(x => x.TargetSystem == MainBCTarget.ID);
                            Traverse.Create(PrioritySystem.Override).Field("contractDisplayStyle").SetValue(ContractDisplayStyle.BaseCampaignStory);
                            Core.WarStatus.DeploymentContracts.Add(PrioritySystem.Override.contractName);
                        }
                        else if (twiddle == -1 || MainBCTarget.OwnerValue.Name == sim.CurSystem.OwnerValue.Name)
                        {
                            sim.GeneratePotentialContracts(false, null, MainBCTarget, false);
                            SystemBonuses(MainBCTarget);
                        }
                        else if (twiddle == 1)
                        {
                            sim.GeneratePotentialContracts(false, null, MainBCTarget, false);
                            SystemBonuses(MainBCTarget);

                            var PrioritySystem = sim.CurSystem.SystemBreadcrumbs.Find(x => x.TargetSystem == MainBCTarget.ID);
                            Traverse.Create(PrioritySystem.Override).Field("contractDisplayStyle").SetValue(ContractDisplayStyle.BaseCampaignStory);
                            Core.WarStatus.DeploymentContracts.Add(PrioritySystem.Override.contractName);
                        }

                        Core.RefreshContracts(MainBCTarget);

                        HomeContendedSystems.Remove(MainBCTarget);
                        Core.WarStatus.HomeContendedStrings.Add(MainBCTarget.Name);
                        if (sim.CurSystem.SystemBreadcrumbs.Count == Core.Settings.InternalHotSpots)
                            break;

                        i = sim.CurSystem.SystemBreadcrumbs.Count;
                        twiddle *= -1;
                    }
                }

                if (ExternalPriorityTargets.Count != 0)
                {
                    int startBC = sim.CurSystem.SystemBreadcrumbs.Count;
                    int j = startBC;
                    foreach (var ExtTarget in ExternalPriorityTargets.Keys)
                    {
                        if (ExternalPriorityTargets[ExtTarget].Count == 0 || Core.Settings.DefensiveFactions.Contains(ExtTarget) || 
                            !Core.IncludedFactions.Contains(ExtTarget)) continue;
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
                Traverse.Create(sim.CurSystem).Property("CurMaxContracts").SetValue(__state);

            }

        }

       

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
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var FactionDef = UnityGameInstance.BattleTechGame.Simulation.GetFactionDef(faction);
            starSystem.Def.contractEmployerIDs.Clear();
            starSystem.Def.contractTargetIDs.Clear();
            var tracker = Core.WarStatus.systems.Find(x => x.name == starSystem.Name);

            if (Core.Settings.NoOffensiveContracts.Contains(faction))
            {
                if (!Core.Settings.NoOffensiveContracts.Contains(tracker.OriginalOwner))
                {
                    starSystem.Def.contractEmployerIDs.Add(tracker.OriginalOwner);
                    starSystem.Def.contractTargetIDs.Add(faction);
                }
                else
                {
                    List<string> factionList = new List<string>();
                    if (Core.Settings.ISMCompatibility)
                        factionList = new List<string>(Core.Settings.IncludedFactions_ISM);
                    else
                        factionList = new List<string>(Core.Settings.IncludedFactions);

                    factionList.Shuffle();
                    string factionEmployer = "Davion";
                    foreach (var foo in factionList)
                    {
                        if (Core.Settings.NoOffensiveContracts.Contains(foo) || Core.Settings.DefensiveFactions.Contains(foo) ||
                             Core.Settings.ImmuneToWar.Contains(foo))
                            continue;
                        else
                        {
                            factionEmployer = foo;
                            break;
                        }
                    }

                    starSystem.Def.contractEmployerIDs.Add(factionEmployer);
                    starSystem.Def.contractTargetIDs.Add(faction);
                }
                return;
            }


            starSystem.Def.contractEmployerIDs.Add(faction);
            if (faction == Core.WarStatus.ComstarAlly)
                starSystem.Def.contractEmployerIDs.Add(Core.Settings.GaW_Police);

            
            foreach (var influence in tracker.influenceTracker.OrderByDescending(x => x.Value))
            {
                if (Core.WarStatus.PirateDeployment)
                    break;
                if (influence.Value > 1 && influence.Key != faction)
                {
                    if (!starSystem.Def.contractTargetIDs.Contains(influence.Key))
                        starSystem.Def.contractTargetIDs.Add(influence.Key);
                    if (!FactionDef.Enemies.Contains(influence.Key))  
                    {
                        var enemies = new List<string>(FactionDef.Enemies);
                        enemies.Add(influence.Key);
                        Traverse.Create(FactionDef).Property("Enemies").SetValue(enemies.ToArray());
                    }
                    if (FactionDef.Allies.Contains(influence.Key))
                    {
                        var allies = new List<string>(FactionDef.Allies);
                        allies.Remove(influence.Key);
                        Traverse.Create(FactionDef).Property("Allies").SetValue(allies.ToArray());
                    }

                }
                if (starSystem.Def.contractTargetIDs.Count() == 2)
                    break;
            }
            if (starSystem.Def.contractTargetIDs.Contains(Core.WarStatus.ComstarAlly))
                starSystem.Def.contractTargetIDs.Add(Core.WarStatus.ComstarAlly);

            if (starSystem.Def.contractTargetIDs.Count() == 0)
                starSystem.Def.contractTargetIDs.Add("AuriganPirates");
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

                    if (contract.Override.contractDisplayStyle == ContractDisplayStyle.BaseCampaignStory)
                    {
                        Core.WarStatus.Deployment = true;
                        Core.WarStatus.DeploymentInfluenceIncrease = 1.0;
                        Core.WarStatus.DeploymentEmployer = contract.Override.employerTeam.FactionValue.Name;
                    }
                    else
                    {
                        Core.WarStatus.Deployment = false;
                        Core.WarStatus.DeploymentInfluenceIncrease = 1.0;
                        Core.WarStatus.PirateDeployment = false;
                    }

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
            static bool Prefix(SGNavigationScreen __instance)
            {
                try
                {
                    var sim = UnityGameInstance.BattleTechGame.Simulation;
                    if (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete"))
                        return true;

                    if (Core.WarStatus.Deployment)
                    {
                        UIManager uiManager = (UIManager)AccessTools.Field(typeof(SGNavigationScreen), "uiManager").GetValue(__instance);
                        SimGameState simState = (SimGameState)AccessTools.Field(typeof(SGNavigationScreen), "simState").GetValue(__instance);
                        Action cleanup = delegate () {
                            uiManager.ResetFader(UIManagerRootType.PopupRoot);
                            simState.Starmap.Screen.AllowInput(true);
                        };
                        string primaryButtonText = "Break Deployment";
                        string message = "WARNING: This action will break your current Deployment. Your reputation with the employer and the MRB will be negatively impacted.";
                        PauseNotification.Show("Navigation Change", message, simState.GetCrewPortrait(SimGameCrew.Crew_Sumire), string.Empty, true, delegate {
                            cleanup();
                            Core.WarStatus.Deployment = false;
                            Core.WarStatus.PirateDeployment = false;
                            if (simState.GetFactionDef(Core.WarStatus.DeploymentEmployer).FactionValue.DoesGainReputation)
                            {
                                float employerRepBadFaithMod = simState.Constants.Story.EmployerRepBadFaithMod;
                                int num = 1;

                                if (Core.Settings.ChangeDifficulty)
                                    num = Mathf.RoundToInt((float)simState.CurSystem.Def.GetDifficulty(SimGameState.SimGameType.CAREER) * employerRepBadFaithMod);
                                else
                                    num = Mathf.RoundToInt((float)(simState.CurSystem.Def.DefaultDifficulty + simState.GlobalDifficulty) * employerRepBadFaithMod);

                                if (num != 0)
                                {
                                    simState.SetReputation(simState.GetFactionDef(Core.WarStatus.DeploymentEmployer).FactionValue, num, StatCollection.StatOperation.Int_Add, null);
                                    simState.SetReputation(simState.GetFactionValueFromString("faction_MercenaryReviewBoard"), num, StatCollection.StatOperation.Int_Add, null);
                                }
                            }
                            string targetsystem = "";
                            if (Core.WarStatus.HotBox.Count() == 2)
                            {
                                targetsystem = Core.WarStatus.HotBox[0];
                                Core.WarStatus.HotBox.RemoveAt(0);
                            }
                            else if (Core.WarStatus.HotBox.Count() != 0)
                            {
                                targetsystem = Core.WarStatus.HotBox[0];
                                Core.WarStatus.HotBox.Clear();
                            }

                            Core.WarStatus.Deployment = false;
                            Core.WarStatus.PirateDeployment = false;
                            Core.WarStatus.DeploymentInfluenceIncrease = 1.0;
                            Core.WarStatus.Escalation = false;
                            Core.WarStatus.EscalationDays = 0;
                            Core.RefreshContracts(simState.CurSystem);
                            if (Core.WarStatus.HotBox.Count == 0)
                                Core.WarStatus.HotBoxTravelling = false;

                            if (Core.WarStatus.EscalationOrder != null)
                            {
                                Core.WarStatus.EscalationOrder.SetCost(0);
                                TaskManagementElement taskManagementElement = null;
                                TaskTimelineWidget timelineWidget = (TaskTimelineWidget)AccessTools.Field(typeof(SGRoomManager), "timelineWidget").GetValue(simState.RoomManager);
                                Dictionary<WorkOrderEntry, TaskManagementElement> ActiveItems =
                                    (Dictionary<WorkOrderEntry, TaskManagementElement>)AccessTools.Field(typeof(TaskTimelineWidget), "ActiveItems").GetValue(timelineWidget);
                                if (ActiveItems.TryGetValue(Core.WarStatus.EscalationOrder, out taskManagementElement))
                                {
                                    taskManagementElement.UpdateItem(0);
                                }
                            }
                            simState.Starmap.SetActivePath();
                            simState.SetSimRoomState(DropshipLocation.SHIP);
                        }, primaryButtonText, cleanup, "Cancel");
                        simState.Starmap.Screen.AllowInput(false);
                        uiManager.SetFaderColor(uiManager.UILookAndColorConstants.PopupBackfill, UIManagerFader.FadePosition.FadeInBack, UIManagerRootType.PopupRoot, true);
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                    return true;
                }
            }

                static void Postfix(SGNavigationScreen __instance)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = UnityGameInstance.BattleTechGame.Simulation.CurSystem;
                if (Core.WarStatus.HotBox.Contains(system.Name))
                {
                    Core.WarStatus.HotBox.Remove(system.Name);
                }
                Core.WarStatus.Escalation = false;
                Core.WarStatus.EscalationDays = 0;
                Core.RefreshContracts(system);
                if (Core.WarStatus.HotBox.Count == 0)
                    Core.WarStatus.HotBoxTravelling = false;
            }
        }

        [HarmonyPatch(typeof(SGNavigationScreen), "OnFlashpointAccepted")]
        public static class SGNavigationScreen_OnFlashpointAccepted_Patch
        {
            static bool Prefix(SGNavigationScreen __instance)
            {
                try
                {
                    var sim = UnityGameInstance.BattleTechGame.Simulation;
                    if (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete"))
                        return true;

                    if (Core.WarStatus.Deployment)
                    {
                        UIManager uiManager = (UIManager)AccessTools.Field(typeof(SGNavigationScreen), "uiManager").GetValue(__instance);
                        SimGameState simState = (SimGameState)AccessTools.Field(typeof(SGNavigationScreen), "simState").GetValue(__instance);
                        Action cleanup = delegate () {
                            uiManager.ResetFader(UIManagerRootType.PopupRoot);
                            simState.Starmap.Screen.AllowInput(true);
                        };
                        string primaryButtonText = "Break Deployment";
                        string message = "WARNING: This action will break your current Deployment. Your reputation with the employer and the MRB will be negatively impacted.";
                        PauseNotification.Show("Navigation Change", message, simState.GetCrewPortrait(SimGameCrew.Crew_Sumire), string.Empty, true, delegate {
                            cleanup();
                            Core.WarStatus.Deployment = false;
                            Core.WarStatus.PirateDeployment = false;
                            if (simState.GetFactionDef(Core.WarStatus.DeploymentEmployer).FactionValue.DoesGainReputation)
                            {
                                float employerRepBadFaithMod = simState.Constants.Story.EmployerRepBadFaithMod;
                                int num = 1;
                                if (Core.Settings.ChangeDifficulty)
                                    num = Mathf.RoundToInt((float)simState.CurSystem.Def.GetDifficulty(SimGameState.SimGameType.CAREER) * employerRepBadFaithMod);
                                else
                                    num = Mathf.RoundToInt((float)(simState.CurSystem.Def.DefaultDifficulty + simState.GlobalDifficulty) * employerRepBadFaithMod);

                                if (num != 0)
                                {
                                    simState.SetReputation(simState.GetFactionDef(Core.WarStatus.DeploymentEmployer).FactionValue, num, StatCollection.StatOperation.Int_Add, null);
                                    simState.SetReputation(simState.GetFactionValueFromString("faction_MercenaryReviewBoard"), num, StatCollection.StatOperation.Int_Add, null);
                                }
                            }
                            string targetsystem = "";
                            if (Core.WarStatus.HotBox.Count() == 2)
                            {
                                targetsystem = Core.WarStatus.HotBox[0];
                                Core.WarStatus.HotBox.RemoveAt(0);
                            }
                            else if (Core.WarStatus.HotBox.Count() != 0)
                            {
                                targetsystem = Core.WarStatus.HotBox[0];
                                Core.WarStatus.HotBox.Clear();
                            }

                            Core.WarStatus.Deployment = false;
                            Core.WarStatus.PirateDeployment = false;
                            Core.WarStatus.DeploymentInfluenceIncrease = 1.0;
                            Core.WarStatus.Escalation = false;
                            Core.WarStatus.EscalationDays = 0;
                            Core.RefreshContracts(simState.CurSystem);
                            if (Core.WarStatus.HotBox.Count == 0)
                                Core.WarStatus.HotBoxTravelling = false;

                            if (Core.WarStatus.EscalationOrder != null)
                            {
                                Core.WarStatus.EscalationOrder.SetCost(0);
                                TaskManagementElement taskManagementElement = null;
                                TaskTimelineWidget timelineWidget = (TaskTimelineWidget)AccessTools.Field(typeof(SGRoomManager), "timelineWidget").GetValue(simState.RoomManager);
                                Dictionary<WorkOrderEntry, TaskManagementElement> ActiveItems =
                                    (Dictionary<WorkOrderEntry, TaskManagementElement>)AccessTools.Field(typeof(TaskTimelineWidget), "ActiveItems").GetValue(timelineWidget);
                                if (ActiveItems.TryGetValue(Core.WarStatus.EscalationOrder, out taskManagementElement))
                                {
                                    taskManagementElement.UpdateItem(0);
                                }
                            }
                            simState.Starmap.SetActivePath();
                            simState.SetSimRoomState(DropshipLocation.SHIP);
                        }, primaryButtonText, cleanup, "Cancel");
                        simState.Starmap.Screen.AllowInput(false);
                        uiManager.SetFaderColor(uiManager.UILookAndColorConstants.PopupBackfill, UIManagerFader.FadePosition.FadeInBack, UIManagerRootType.PopupRoot, true);
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                    return true;
                }
            }

            static void Postfix(SGNavigationScreen __instance)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = UnityGameInstance.BattleTechGame.Simulation.CurSystem;
                if (Core.WarStatus.HotBox.Contains(system.Name))
                {
                    Core.WarStatus.HotBox.Remove(system.Name);
                }
                Core.WarStatus.Escalation = false;
                Core.WarStatus.EscalationDays = 0;
                Core.RefreshContracts(system);
                if (Core.WarStatus.HotBox.Count == 0)
                    Core.WarStatus.HotBoxTravelling = false;
            }
        }

        [HarmonyPatch(typeof(SGTravelManager), "WarmingEngines_CanEnter")]
        public static class Completed_Jump_Patch
        {
            static void Postfix()
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                Core.HoldContracts = false;
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

                var TargetSystem = Core.WarStatus.systems.Find(x => x.name == sim.CurSystem.Name);
                bool HasFlashpoint = false;
                Core.WarStatus.JustArrived = true;

                if (!Core.WarStatus.Deployment)
                    Core.WarStatus.EscalationDays = Core.Settings.EscalationDays;
                else
                {
                    Random rand = new Random();
                    Core.WarStatus.EscalationDays = rand.Next(Core.Settings.DeploymentMinDays, Core.Settings.DeploymentMaxDays + 1);
                    if (Core.WarStatus.EscalationDays < Core.Settings.DeploymentRerollBound * Core.WarStatus.EscalationDays || 
                        Core.WarStatus.EscalationDays > (1 - Core.Settings.DeploymentRerollBound) * Core.WarStatus.EscalationDays)
                        Core.WarStatus.EscalationDays = rand.Next(Core.Settings.DeploymentMinDays, Core.Settings.DeploymentMaxDays + 1);
                }

                foreach (var contract in sim.CurSystem.SystemContracts)
                {
                    if (contract.IsFlashpointContract || contract.IsFlashpointCampaignContract)
                        HasFlashpoint = true;
                }
                if (!Core.WarStatus.HotBoxTravelling && !Core.WarStatus.HotBox.Contains(sim.CurSystem.Name) && !HasFlashpoint && !Core.HoldContracts)
                {
                    Core.NeedsProcessing = true;
                    var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                    sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                    Core.NeedsProcessing = false;
                }
                Core.HoldContracts = false;
            }
        }

        [HarmonyPatch(typeof(AAR_SalvageScreen), "OnCompleted")]
        public static class AAR_SalvageScreen_OnCompleted_Patch
        {
            static void Prefix(AAR_SalvageScreen __instance)
            {
                try
                {
                    var sim = UnityGameInstance.BattleTechGame.Simulation;
                    if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                        return;

                    Core.WarStatus.JustArrived = false;
                    Core.WarStatus.HotBoxTravelling = false;
                }
                catch (Exception e)
                {
                    Error(e);
                }
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
                    if (!Core.WarStatus.Deployment)
                    {
                        Core.WarStatus.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric,
                            "Escalation Days Remaining", "Escalation Days Remaining");
                        Core.WarStatus.EscalationOrder.SetCost(Core.WarStatus.EscalationDays);
                        __instance.AddEntry(Core.WarStatus.EscalationOrder, false);
                        __instance.RefreshEntries();
                    }
                    else
                    {
                        Core.WarStatus.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric,
                            "Escalation Days Remaining", "Forced Deployment Mission");
                        Core.WarStatus.EscalationOrder.SetCost(Core.WarStatus.EscalationDays);
                        __instance.AddEntry(Core.WarStatus.EscalationOrder, false);
                        __instance.RefreshEntries();
                    }
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

                if (!Core.WarStatus.JustArrived && (entry.ID.Equals("Escalation Days Remaining")) && entry.GetRemainingCost() != 0)
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
                    if (!Core.WarStatus.Deployment)
                    {
                        Core.WarStatus.Escalation = true;
                        Core.WarStatus.EscalationDays = Core.Settings.EscalationDays;
                        Core.WarStatus.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric, "Escalation Days Remaining", "Escalation Days Remaining");
                        Core.WarStatus.EscalationOrder.SetCost(Core.WarStatus.EscalationDays);
                        __instance.RoomManager.AddWorkQueueEntry(Core.WarStatus.EscalationOrder);
                        __instance.RoomManager.SortTimeline();
                        __instance.RoomManager.RefreshTimeline(false);
                    }
                    else
                    {
                        var rand = new Random();
                        Core.WarStatus.EscalationDays = rand.Next(Core.Settings.DeploymentMinDays, Core.Settings.DeploymentMaxDays + 1);
                        if (Core.WarStatus.EscalationDays < Core.Settings.DeploymentRerollBound * Core.WarStatus.EscalationDays ||
                        Core.WarStatus.EscalationDays > (1 - Core.Settings.DeploymentRerollBound) * Core.WarStatus.EscalationDays)
                            Core.WarStatus.EscalationDays = rand.Next(Core.Settings.DeploymentMinDays, Core.Settings.DeploymentMaxDays + 1);

                        Core.WarStatus.Escalation = true;
                        Core.WarStatus.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric, "Escalation Days Remaining", "Forced Deployment Mission");
                        Core.WarStatus.EscalationOrder.SetCost(Core.WarStatus.EscalationDays);
                        __instance.RoomManager.AddWorkQueueEntry(Core.WarStatus.EscalationOrder);
                        __instance.RoomManager.SortTimeline();
                        __instance.RoomManager.RefreshTimeline(false);
                    }
                }
            }
        }

        //Need to clear out the old stuff if a contract is cancelled to prevent crashing.
        [HarmonyPatch(typeof(SimGameState), "OnBreadcrumbCancelledByUser")]
        public static class SimGameState_BreadCrumbCancelled_Patch
        {
            //static bool Prefix(SimGameState __instance)
            //{
            //    try
            //    {
            //        var sim = __instance;
            //        if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
            //            return true;

            //        SimGameState Sim = (SimGameState)AccessTools.Property(typeof(SGContractsWidget), "Sim").GetValue(__instance, null);

            //        if (__instance.SelectedContract.Override.contractDisplayStyle == ContractDisplayStyle.BaseCampaignStory)
            //        {
            //            string message = "Commander, this contract will bring us right to the front lines. If we accept it, we will be forced to take missions when our employer needs us to simultaneously attack in support of their war effort. We will be commited to this Deployment until the system is taken or properly defended and will lose significant reputation if we end up backing out before the job is done. But, oh man, they will certainly reward us well if their operation is ultimately successful! This Deployment may require missions to be done without time between them for repairs or to properly rest our pilots. I stronfgety encourage you to only accept this arrangement if you think we're up to it.";
            //            PauseNotification.Show("Deployment", message,
            //                Sim.GetCrewPortrait(SimGameCrew.Crew_Darius), string.Empty, true, delegate {
            //                    __instance.NegotiateContract(__instance.SelectedContract, null);
            //                }, "Do it anyways", null, "Cancel");
            //            return false;
            //        }
            //        else
            //        {
            //            return true;
            //        }
            //    }
            //    catch (Exception e)
            //    {
            //        Logger.Error(e);
            //        return true;
            //    }
            //}


            static void Postfix()
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = UnityGameInstance.BattleTechGame.Simulation.CurSystem;
                string targetsystem = "";
                if (Core.WarStatus.HotBox.Count() == 2)
                {
                    //targetsystem = Core.WarStatus.HotBox[0];
                    Core.WarStatus.HotBox.RemoveAt(0);
                }
                else
                {
                    //targetsystem = Core.WarStatus.HotBox[0];
                    Core.WarStatus.HotBox.Clear();
                }

                Core.WarStatus.Deployment = false;
                Core.WarStatus.PirateDeployment = false;
                Core.WarStatus.DeploymentInfluenceIncrease = 1.0;
                Core.WarStatus.Escalation = false;
                Core.WarStatus.EscalationDays = 0;
                Core.RefreshContracts(system);
                if (Core.WarStatus.HotBox.Count == 0)
                    Core.WarStatus.HotBoxTravelling = false;
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
                    string missionObjectiveResultString = $"BONUS FROM ESCALATION: ¢{String.Format("{0:n0}", BonusMoney)}";
                    if (Core.WarStatus.Deployment)
                        missionObjectiveResultString = $"BONUS FROM DEPLOYMENT: ¢{String.Format("{0:n0}", BonusMoney)}";
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
            int systemDifficulty = 1;
            if (Core.Settings.ChangeDifficulty)
                systemDifficulty = system.DifficultyRating;
            else
                systemDifficulty = system.DifficultyRating + (int)sim.GlobalDifficulty;

            if (!Core.WarStatus.HotBox.Contains(starSystem.Name))
            {
                system.BonusCBills = false;
                system.BonusSalvage = false;
                system.BonusXP = false;

                if (systemDifficulty <= 4)
                {
                    var bonus = rand.Next(0, 3);
                    if (bonus == 0)
                        system.BonusCBills = true;
                    if (bonus == 1)
                        system.BonusXP = true;
                    if (bonus == 2)
                        system.BonusSalvage = true;
                }
                if (systemDifficulty <= 8 && systemDifficulty > 4)
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
                if (systemDifficulty > 8)
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
            Core.WarStatus.Deployment = false;
            Core.WarStatus.PirateDeployment = false;
            Core.WarStatus.DeploymentInfluenceIncrease = 1.0;
            Core.WarStatus.HotBox.Remove(system.name);
            Core.RefreshContracts(system.starSystem);
            bool HasFlashpoint = false;
            foreach (var contract in sim.CurSystem.SystemContracts)
            {
                if (contract.IsFlashpointContract || contract.IsFlashpointCampaignContract)
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

        [HarmonyPatch(typeof(SimGameState), "ContractUserMeetsReputation")]
        public static class SimGameState_ContractUserMeetsReputation_Patch
        {
            static void Postfix(ref bool __result, Contract c)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete"))
                    return;

                if (Core.WarStatus.Deployment)
                    __result = true;
            }
        }

        public static int ProcessReputation(float FactionRep)
        {
            var simStory = UnityGameInstance.BattleTechGame.Simulation.Constants.Story;
            var simCareer = UnityGameInstance.BattleTechGame.Simulation.Constants.CareerMode;
            int MaxContracts = 1;

            if (FactionRep <= simStory.LoathedReputation)
                MaxContracts = (int)simCareer.LoathedMaxContractDifficulty;
            else if (FactionRep <= simStory.HatedReputation)
                MaxContracts = (int)simCareer.HatedMaxContractDifficulty;
            else if (FactionRep <= simStory.DislikedReputation)
                MaxContracts = (int)simCareer.DislikedMaxContractDifficulty;
            else if (FactionRep <= simStory.LikedReputation)
                MaxContracts = (int)simCareer.IndifferentMaxContractDifficulty;
            else if (FactionRep <= simStory.FriendlyReputation)
                MaxContracts = (int)simCareer.LikedMaxContractDifficulty;
            else if (FactionRep <= simStory.HonoredReputation)
                MaxContracts = (int)simCareer.FriendlyMaxContractDifficulty;
            else
                MaxContracts = (int)simCareer.HonoredMaxContractDifficulty;

            if (MaxContracts > 10)
                MaxContracts = 10;
            if (MaxContracts < 1)
                MaxContracts = 1;

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

                Core.HoldContracts = true;
            }
        }

        [HarmonyPatch(typeof(SGContractsWidget), "OnNegotiateClicked")]
        public static class SGContractsWidget_OnNegotiateClicked_Patch
        {
            static bool Prefix(SGContractsWidget __instance)
            {
                try
                {
                    var sim = UnityGameInstance.BattleTechGame.Simulation;
                    if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                        return true;

                    SimGameState Sim = (SimGameState)AccessTools.Property(typeof(SGContractsWidget), "Sim").GetValue(__instance, null);

                    if (__instance.SelectedContract.Override.contractDisplayStyle == ContractDisplayStyle.BaseCampaignStory)
                    {
                        string message = "Commander, this contract will bring us right to the front lines. If we accept it, we will be forced to take missions when our employer needs us to simultaneously attack in support of their war effort. We will be commited to this Deployment until the system is taken or properly defended and will lose significant reputation if we end up backing out before the job is done. But, oh man, they will certainly reward us well if their operation is ultimately successful! This Deployment may require missions to be done without time between them for repairs or to properly rest our pilots. I strongly encourage you to only accept this arrangement if you think we're up to it.";
                        PauseNotification.Show("Deployment", message,
                            Sim.GetCrewPortrait(SimGameCrew.Crew_Darius), string.Empty, true, delegate {
                                __instance.NegotiateContract(__instance.SelectedContract, null);
                            }, "Do it anyways", null, "Cancel");
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SGTimePlayPause), "ToggleTime")]
        public static class SGTimePlayPause_ToggleTime_Patch
        {
            static bool Prefix(SGTimePlayPause __instance)
            {
                try
                {
                    var sim = UnityGameInstance.BattleTechGame.Simulation;
                    if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                        return true;

                    if (!Core.Settings.ResetMap && Core.WarStatus.Deployment && !Core.WarStatus.HotBoxTravelling && Core.WarStatus.EscalationDays <= 0)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                    return true;
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

                bool HasFlashpoint = false;
                foreach (var contract in sim.CurSystem.SystemContracts)
                {
                    if (contract.IsFlashpointContract || contract.IsFlashpointCampaignContract)
                        HasFlashpoint = true;
                }
                if (Core.WarStatus.HotBox.Contains(sim.CurSystem.Name))
                {
                    if (!Core.WarStatus.Deployment)
                    {
                        //__instance.Sim.Constants.Story.ContractSuccessReduction = 0;
                    }
                    else
                    {
                        __instance.Sim.Constants.Story.ContractSuccessReduction = 100;
                        Core.WarStatus.DeploymentInfluenceIncrease *= Core.Settings.DeploymentEscalationFactor;
                        if (!HasFlashpoint)
                        {
                            sim.CurSystem.activeSystemContracts.Clear();
                            sim.CurSystem.activeSystemBreadcrumbs.Clear();
                        }

                        if (Core.WarStatus.EscalationOrder != null)
                        {
                            Core.WarStatus.EscalationOrder.SetCost(0);
                            TaskManagementElement taskManagementElement = null;
                            TaskTimelineWidget timelineWidget = (TaskTimelineWidget)AccessTools.Field(typeof(SGRoomManager), "timelineWidget").GetValue(sim.RoomManager);
                            Dictionary<WorkOrderEntry, TaskManagementElement> ActiveItems =
                                (Dictionary<WorkOrderEntry, TaskManagementElement>)AccessTools.Field(typeof(TaskTimelineWidget), "ActiveItems").GetValue(timelineWidget);
                            if (ActiveItems.TryGetValue(Core.WarStatus.EscalationOrder, out taskManagementElement))
                            {
                                taskManagementElement.UpdateItem(0);
                            }
                        }
                        Core.WarStatus.Escalation = true;
                        var rand = new Random();
                        Core.WarStatus.EscalationDays = rand.Next(Core.Settings.DeploymentMinDays, Core.Settings.DeploymentMaxDays + 1);
                        if (Core.WarStatus.EscalationDays < Core.Settings.DeploymentRerollBound * Core.WarStatus.EscalationDays ||
                        Core.WarStatus.EscalationDays > (1 - Core.Settings.DeploymentRerollBound) * Core.WarStatus.EscalationDays)
                            Core.WarStatus.EscalationDays = rand.Next(Core.Settings.DeploymentMinDays, Core.Settings.DeploymentMaxDays + 1);

                        Core.WarStatus.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric, "Escalation Days Remaining", "Forced Deployment Mission");
                        Core.WarStatus.EscalationOrder.SetCost(Core.WarStatus.EscalationDays);
                        sim.RoomManager.AddWorkQueueEntry(Core.WarStatus.EscalationOrder);
                        sim.RoomManager.SortTimeline();
                        sim.RoomManager.RefreshTimeline(false);
                    }
                }
                 
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
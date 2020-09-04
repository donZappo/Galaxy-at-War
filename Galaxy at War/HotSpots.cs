using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.Framework;
using BattleTech.UI;
using Harmony;
using UnityEngine;
using static GalaxyatWar.Logger;
using Random = System.Random;
using static GalaxyatWar.Globals;
using static GalaxyatWar.Helpers;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedType.Global

namespace GalaxyatWar
{
    public class HotSpots
    {
        private static bool isBreadcrumb;
        public static int BonusMoney = 0;

        public static Dictionary<string, List<StarSystem>> ExternalPriorityTargets = new Dictionary<string, List<StarSystem>>();
        public static readonly List<StarSystem> HomeContendedSystems = new List<StarSystem>();
        public static readonly Dictionary<StarSystem, float> FullHomeContendedSystems = new Dictionary<StarSystem, float>();

        private static void ProcessHotSpots()
        {
            var dominantFaction = WarStatusTracker.SystemStatuses.Find(x => x.starSystem.Name == WarStatusTracker.CurSystem).owner;
            FullHomeContendedSystems.Clear();
            HomeContendedSystems.Clear();
            ExternalPriorityTargets.Clear();
            WarStatusTracker.HomeContendedStrings.Clear();
            var factRepDict = new Dictionary<string, int>();
            foreach (var faction in IncludedFactions)
            {
                ExternalPriorityTargets.Add(faction, new List<StarSystem>());
                var maxContracts = ProcessReputation(Sim.GetRawReputation(FactionValues.Find(x => x.Name == faction)));
                factRepDict.Add(faction, maxContracts);
            }

            //Populate lists with planets that are in danger of flipping
            foreach (var systemStatus in WarStatusTracker.SystemStatuses)
            {
                if (!WarStatusTracker.HotBox.Contains(systemStatus.name))
                {
                    systemStatus.BonusCBills = false;
                    systemStatus.BonusSalvage = false;
                    systemStatus.BonusXP = false;
                }

                if (systemStatus.Contended && systemStatus.DifficultyRating <= factRepDict[systemStatus.owner]
                                           && systemStatus.DifficultyRating >= factRepDict[systemStatus.owner] - 4)
                    systemStatus.PriorityDefense = true;
                if (systemStatus.PriorityDefense)
                {
                    if (systemStatus.owner == dominantFaction)
                        FullHomeContendedSystems.Add(systemStatus.starSystem, systemStatus.TotalResources);
                    else
                        ExternalPriorityTargets[systemStatus.owner].Add(systemStatus.starSystem);
                }

                if (systemStatus.PriorityAttack)
                {
                    foreach (var attacker in systemStatus.CurrentlyAttackedBy)
                    {
                        if (attacker == dominantFaction)
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
                if (i < FullHomeContendedSystems.Count)
                {
                    WarStatusTracker.HomeContendedStrings.Add(system.Key.Name);
                }

                HomeContendedSystems.Add(system.Key);
                i++;
            }
        }

        [HarmonyPatch(typeof(StarSystem), "GenerateInitialContracts")]
        public static class SimGameStateGenerateInitialContractsPatch
        {
            private static void Prefix(ref float __state)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                Traverse.Create(Sim.CurSystem).Property("MissionsCompleted").SetValue(0);
                Traverse.Create(Sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(0);
                Traverse.Create(Sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(0);
                __state = Sim.CurSystem.CurMaxContracts;

                foreach (var theFaction in IncludedFactions)
                {
                    if (WarStatusTracker.DeathListTrackers.Find(x => x.faction == theFaction) == null)
                        continue;

                    var deathListTracker = WarStatusTracker.DeathListTrackers.Find(x => x.faction == theFaction);
                    AdjustDeathList(deathListTracker, true);
                }

                if (Settings.LimitSystemContracts.ContainsKey(Sim.CurSystem.Name))
                {
                    Traverse.Create(Sim.CurSystem).Property("CurMaxContracts").SetValue(Settings.LimitSystemContracts[Sim.CurSystem.Name]);
                }

                if (WarStatusTracker.Deployment)
                {
                    Traverse.Create(Sim.CurSystem).Property("CurMaxContracts").SetValue(Settings.DeploymentContracts);
                }
            }


            private static void Postfix(ref float __state)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (NeedsProcessing)
                    ProcessHotSpots();

                isBreadcrumb = true;
                Sim.CurSystem.activeSystemBreadcrumbs.Clear();
                Traverse.Create(Sim.CurSystem).Property("MissionsCompleted").SetValue(20);
                Traverse.Create(Sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(1);
                Traverse.Create(Sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(1);
                WarStatusTracker.DeploymentContracts.Clear();

                if (HomeContendedSystems.Count != 0 && !Settings.DefensiveFactions.Contains(Sim.CurSystem.OwnerValue.Name) && !WarStatusTracker.Deployment)
                {
                    var i = 0;
                    var twiddle = 0;
                    var RandomSystem = 0;
                    WarStatusTracker.HomeContendedStrings.Clear();
                    while (HomeContendedSystems.Count != 0)
                    {
                        Traverse.Create(Sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(i + 1);
                        Traverse.Create(Sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(i + 1);
                        if (twiddle == 0)
                            twiddle = -1;
                        else if (twiddle == 1)
                            RandomSystem = Rng.Next(0, 3 * HomeContendedSystems.Count / 4);
                        else if (twiddle == -1)
                            RandomSystem = Rng.Next(HomeContendedSystems.Count / 4, 3 * HomeContendedSystems.Count / 4);

                        var MainBCTarget = HomeContendedSystems[RandomSystem];

                        if (MainBCTarget == Sim.CurSystem || (Sim.CurSystem.OwnerValue.Name == "Locals" && MainBCTarget.OwnerValue.Name != "Locals") ||
                            !IncludedFactions.Contains(MainBCTarget.OwnerValue.Name))
                        {
                            HomeContendedSystems.Remove(MainBCTarget);
                            WarStatusTracker.HomeContendedStrings.Remove(MainBCTarget.Name);
                            continue;
                        }

                        TemporaryFlip(MainBCTarget, Sim.CurSystem.OwnerValue.Name);
                        if (Sim.CurSystem.SystemBreadcrumbs.Count == 0 && MainBCTarget.OwnerValue.Name != Sim.CurSystem.OwnerValue.Name)
                        {
                            Sim.GeneratePotentialContracts(true, null, MainBCTarget);
                            SystemBonuses(MainBCTarget);

                            var PrioritySystem = Sim.CurSystem.SystemBreadcrumbs.Find(x => x.TargetSystem == MainBCTarget.ID);
                            Traverse.Create(PrioritySystem.Override).Field("contractDisplayStyle").SetValue(ContractDisplayStyle.BaseCampaignStory);
                            WarStatusTracker.DeploymentContracts.Add(PrioritySystem.Override.contractName);
                        }
                        else if (twiddle == -1 || MainBCTarget.OwnerValue.Name == Sim.CurSystem.OwnerValue.Name)
                        {
                            Sim.GeneratePotentialContracts(false, null, MainBCTarget);
                            SystemBonuses(MainBCTarget);
                        }
                        else if (twiddle == 1)
                        {
                            Sim.GeneratePotentialContracts(false, null, MainBCTarget);
                            SystemBonuses(MainBCTarget);

                            var PrioritySystem = Sim.CurSystem.SystemBreadcrumbs.Find(x => x.TargetSystem == MainBCTarget.ID);
                            Traverse.Create(PrioritySystem.Override).Field("contractDisplayStyle").SetValue(ContractDisplayStyle.BaseCampaignStory);
                            WarStatusTracker.DeploymentContracts.Add(PrioritySystem.Override.contractName);
                        }

                        RefreshContracts(MainBCTarget);

                        HomeContendedSystems.Remove(MainBCTarget);
                        WarStatusTracker.HomeContendedStrings.Add(MainBCTarget.Name);
                        if (Sim.CurSystem.SystemBreadcrumbs.Count == Settings.InternalHotSpots)
                            break;

                        i = Sim.CurSystem.SystemBreadcrumbs.Count;
                        twiddle *= -1;
                    }
                }

                if (ExternalPriorityTargets.Count != 0)
                {
                    var startBC = Sim.CurSystem.SystemBreadcrumbs.Count;
                    var j = startBC;
                    foreach (var ExtTarget in ExternalPriorityTargets.Keys)
                    {
                        if (ExternalPriorityTargets[ExtTarget].Count == 0 || Settings.DefensiveFactions.Contains(ExtTarget) ||
                            !IncludedFactions.Contains(ExtTarget)) continue;
                        do
                        {
                            var randTarget = Rng.Next(0, ExternalPriorityTargets[ExtTarget].Count);
                            Traverse.Create(Sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(j + 1);
                            Traverse.Create(Sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(j + 1);
                            if (ExternalPriorityTargets[ExtTarget][randTarget] == Sim.CurSystem)
                            {
                                ExternalPriorityTargets[ExtTarget].Remove(Sim.CurSystem);
                                continue;
                            }

                            TemporaryFlip(ExternalPriorityTargets[ExtTarget][randTarget], ExtTarget);
                            if (Sim.CurSystem.SystemBreadcrumbs.Count == 0)
                                Sim.GeneratePotentialContracts(true, null, ExternalPriorityTargets[ExtTarget][randTarget]);
                            else
                                Sim.GeneratePotentialContracts(false, null, ExternalPriorityTargets[ExtTarget][randTarget]);
                            SystemBonuses(ExternalPriorityTargets[ExtTarget][randTarget]);
                            RefreshContracts(ExternalPriorityTargets[ExtTarget][randTarget]);
                            ExternalPriorityTargets[ExtTarget].RemoveAt(randTarget);
                        } while (Sim.CurSystem.SystemBreadcrumbs.Count == j && ExternalPriorityTargets[ExtTarget].Count != 0);

                        j = Sim.CurSystem.SystemBreadcrumbs.Count;
                        if (j - startBC == Settings.ExternalHotSpots)
                            break;
                    }
                }

                isBreadcrumb = false;
                Traverse.Create(Sim.CurSystem).Property("CurMaxContracts").SetValue(__state);
            }
        }


        [HarmonyPatch(typeof(StarSystem))]
        [HarmonyPatch("InitialContractsFetched", MethodType.Getter)]
        public static class StarSystemInitialContractsFetchedPatch
        {
            private static void Postfix(ref bool __result)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (WarStatusTracker.StartGameInitialized)
                    __result = true;
            }
        }

        [HarmonyPatch(typeof(SimGameState), "GetDifficultyRangeForContract")]
        public static class SimGameStateGetDifficultyRangeForContractsPatch
        {
            private static void Prefix(SimGameState __instance, ref int __state)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (isBreadcrumb)
                {
                    __state = __instance.Constants.Story.ContractDifficultyVariance;
                    __instance.Constants.Story.ContractDifficultyVariance = 0;
                }
            }

            private static void Postfix(SimGameState __instance, ref int __state)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
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
            starSystem.Def.contractEmployerIDs.Clear();
            starSystem.Def.contractTargetIDs.Clear();
            var tracker = WarStatusTracker.SystemStatuses.Find(x => x.name == starSystem.Name);

            if (Settings.NoOffensiveContracts.Contains(faction))
            {
                if (!Settings.NoOffensiveContracts.Contains(tracker.OriginalOwner))
                {
                    starSystem.Def.contractEmployerIDs.Add(tracker.OriginalOwner);
                    starSystem.Def.contractTargetIDs.Add(faction);
                }
                else
                {
                    List<string> factionList;
                    if (Settings.ISMCompatibility)
                        factionList = Settings.IncludedFactions_ISM;
                    else
                        factionList = Settings.IncludedFactions;

                    factionList.Shuffle();
                    string factionEmployer = "Davion";
                    foreach (var employer in factionList)
                    {
                        if (Settings.NoOffensiveContracts.Contains(employer) ||
                            Settings.DefensiveFactions.Contains(employer) ||
                            Settings.ImmuneToWar.Contains(employer))
                            continue;
                        factionEmployer = employer;
                        break;
                    }

                    starSystem.Def.contractEmployerIDs.Add(factionEmployer);
                    starSystem.Def.contractTargetIDs.Add(faction);
                }

                return;
            }


            starSystem.Def.contractEmployerIDs.Add(faction);
            if (Settings.GaW_PoliceSupport && faction == WarStatusTracker.ComstarAlly)
                starSystem.Def.contractEmployerIDs.Add(Settings.GaW_Police);


            foreach (var influence in tracker.influenceTracker.OrderByDescending(x => x.Value))
            {
                if (WarStatusTracker.PirateDeployment)
                    break;
                if (influence.Value > 1 && influence.Key != faction)
                {
                    if (!starSystem.Def.contractTargetIDs.Contains(influence.Key))
                        starSystem.Def.contractTargetIDs.Add(influence.Key);
                    if (!FactionDef.Enemies.Contains(influence.Key))
                    {
                        var enemies = new List<string>(FactionDef.Enemies)
                        {
                            influence.Key
                        };
                        Traverse.Create(FactionDef).Property("Enemies").SetValue(enemies.ToArray());
                    }

                    if (FactionDef.Allies.Contains(influence.Key))
                    {
                        var allies = new List<string>(FactionDef.Allies);
                        allies.Remove(influence.Key);
                        Traverse.Create(FactionDef).Property("Allies").SetValue(allies.ToArray());
                    }
                }

                if (starSystem.Def.contractTargetIDs.Count == 2)
                    break;
            }

            if (starSystem.Def.contractTargetIDs.Contains(WarStatusTracker.ComstarAlly))
                starSystem.Def.contractTargetIDs.Add(WarStatusTracker.ComstarAlly);

            if (starSystem.Def.contractTargetIDs.Count == 0)
                starSystem.Def.contractTargetIDs.Add("AuriganPirates");
        }

        //Deployments area.
        [HarmonyPatch(typeof(SimGameState), "PrepareBreadcrumb")]
        public static class SimGameStatePrepareBreadcrumbPatch
        {
            private static void Postfix(SimGameState __instance, Contract contract)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (!__instance.CurSystem.Def.Description.Id.StartsWith(contract.TargetSystem))
                {
                    var starSystem = Sim.StarSystems.Find(x => x.Def.Description.Id.StartsWith(contract.TargetSystem));
                    WarStatusTracker.HotBox.Add(starSystem.Name);
                    WarStatusTracker.HotBoxTravelling = true;

                    if (contract.Override.contractDisplayStyle == ContractDisplayStyle.BaseCampaignStory)
                    {
                        WarStatusTracker.Deployment = true;
                        WarStatusTracker.DeploymentInfluenceIncrease = 1.0;
                        WarStatusTracker.DeploymentEmployer = contract.Override.employerTeam.FactionValue.Name;
                    }
                    else
                    {
                        WarStatusTracker.Deployment = false;
                        WarStatusTracker.DeploymentInfluenceIncrease = 1.0;
                        WarStatusTracker.PirateDeployment = false;
                    }

                    TemporaryFlip(starSystem, contract.Override.employerTeam.FactionValue.Name);
                    if (WarStatusTracker.HotBox.Contains(__instance.CurSystem.Name))
                    {
                        WarStatusTracker.HotBox.Remove(__instance.CurSystem.Name);
                        WarStatusTracker.EscalationDays = 0;
                        WarStatusTracker.Escalation = false;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(SGNavigationScreen), "OnTravelCourseAccepted")]
        public static class SGNavigationScreenOnTravelCourseAcceptedPatch
        {
            private static bool Prefix(SGNavigationScreen __instance)
            {
                try
                {
                    if (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete"))
                        return true;

                    if (WarStatusTracker.Deployment)
                    {
                        var uiManager = (UIManager) AccessTools.Field(typeof(SGNavigationScreen), "uiManager").GetValue(__instance);

                        void Cleanup()
                        {
                            uiManager.ResetFader(UIManagerRootType.PopupRoot);
                            Sim.Starmap.Screen.AllowInput(true);
                        }

                        var primaryButtonText = "Break Deployment";
                        var message = "WARNING: This action will break your current Deployment. Your reputation with the employer and the MRB will be negatively impacted.";
                        PauseNotification.Show("Navigation Change", message, Sim.GetCrewPortrait(SimGameCrew.Crew_Sumire), string.Empty, true, delegate
                        {
                            Cleanup();
                            WarStatusTracker.Deployment = false;
                            WarStatusTracker.PirateDeployment = false;
                            if (Sim.GetFactionDef(WarStatusTracker.DeploymentEmployer).FactionValue.DoesGainReputation)
                            {
                                var employerRepBadFaithMod = Sim.Constants.Story.EmployerRepBadFaithMod;
                                int num;

                                if (Settings.ChangeDifficulty)
                                    num = Mathf.RoundToInt(Sim.CurSystem.Def.GetDifficulty(SimGameState.SimGameType.CAREER) * employerRepBadFaithMod);
                                else
                                    num = Mathf.RoundToInt((Sim.CurSystem.Def.DefaultDifficulty + Sim.GlobalDifficulty) * employerRepBadFaithMod);

                                if (num != 0)
                                {
                                    Sim.SetReputation(Sim.GetFactionDef(WarStatusTracker.DeploymentEmployer).FactionValue, num);
                                    Sim.SetReputation(Sim.GetFactionValueFromString("faction_MercenaryReviewBoard"), num);
                                }
                            }

                            if (WarStatusTracker.HotBox.Count == 2)
                            {
                                WarStatusTracker.HotBox.RemoveAt(0);
                            }
                            else if (WarStatusTracker.HotBox.Count != 0)
                            {
                                WarStatusTracker.HotBox.Clear();
                            }

                            WarStatusTracker.Deployment = false;
                            WarStatusTracker.PirateDeployment = false;
                            WarStatusTracker.DeploymentInfluenceIncrease = 1.0;
                            WarStatusTracker.Escalation = false;
                            WarStatusTracker.EscalationDays = 0;
                            RefreshContracts(Sim.CurSystem);
                            if (WarStatusTracker.HotBox.Count == 0)
                                WarStatusTracker.HotBoxTravelling = false;

                            if (WarStatusTracker.EscalationOrder != null)
                            {
                                WarStatusTracker.EscalationOrder.SetCost(0);
                                var ActiveItems = Globals.TaskTimelineWidget.ActiveItems;
                                if (ActiveItems.TryGetValue(WarStatusTracker.EscalationOrder, out var taskManagementElement))
                                {
                                    taskManagementElement.UpdateItem(0);
                                }
                            }

                            Sim.Starmap.SetActivePath();
                            Sim.SetSimRoomState(DropshipLocation.SHIP);
                        }, primaryButtonText, Cleanup, "Cancel");
                        Sim.Starmap.Screen.AllowInput(false);
                        uiManager.SetFaderColor(uiManager.UILookAndColorConstants.PopupBackfill, UIManagerFader.FadePosition.FadeInBack, UIManagerRootType.PopupRoot);
                        return false;
                    }

                    return true;
                }
                catch (Exception e)
                {
                    Error(e);
                    return true;
                }
            }

            private static void Postfix()
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = UnityGameInstance.BattleTechGame.Simulation.CurSystem;
                if (WarStatusTracker.HotBox.Contains(system.Name))
                {
                    WarStatusTracker.HotBox.Remove(system.Name);
                }

                WarStatusTracker.Escalation = false;
                WarStatusTracker.EscalationDays = 0;
                RefreshContracts(system);
                if (WarStatusTracker.HotBox.Count == 0)
                    WarStatusTracker.HotBoxTravelling = false;
            }
        }

        [HarmonyPatch(typeof(SGNavigationScreen), "OnFlashpointAccepted")]
        public static class SGNavigationScreenOnFlashpointAcceptedPatch
        {
            private static bool Prefix(SGNavigationScreen __instance)
            {
                try
                {
                    if (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete"))
                        return true;

                    if (WarStatusTracker.Deployment)
                    {
                        var uiManager = (UIManager) AccessTools.Field(typeof(SGNavigationScreen), "uiManager").GetValue(__instance);

                        void Cleanup()
                        {
                            uiManager.ResetFader(UIManagerRootType.PopupRoot);
                            Sim.Starmap.Screen.AllowInput(true);
                        }

                        var primaryButtonText = "Break Deployment";
                        var message = "WARNING: This action will break your current Deployment. Your reputation with the employer and the MRB will be negatively impacted.";
                        PauseNotification.Show("Navigation Change", message, Sim.GetCrewPortrait(SimGameCrew.Crew_Sumire), string.Empty, true, delegate
                        {
                            Cleanup();
                            WarStatusTracker.Deployment = false;
                            WarStatusTracker.PirateDeployment = false;
                            if (Sim.GetFactionDef(WarStatusTracker.DeploymentEmployer).FactionValue.DoesGainReputation)
                            {
                                var employerRepBadFaithMod = Sim.Constants.Story.EmployerRepBadFaithMod;
                                int num;
                                if (Settings.ChangeDifficulty)
                                    num = Mathf.RoundToInt(Sim.CurSystem.Def.GetDifficulty(SimGameState.SimGameType.CAREER) * employerRepBadFaithMod);
                                else
                                    num = Mathf.RoundToInt((Sim.CurSystem.Def.DefaultDifficulty + Sim.GlobalDifficulty) * employerRepBadFaithMod);

                                if (num != 0)
                                {
                                    Sim.SetReputation(Sim.GetFactionDef(WarStatusTracker.DeploymentEmployer).FactionValue, num);
                                    Sim.SetReputation(Sim.GetFactionValueFromString("faction_MercenaryReviewBoard"), num);
                                }
                            }

                            if (WarStatusTracker.HotBox.Count == 2)
                            {
                                WarStatusTracker.HotBox.RemoveAt(0);
                            }
                            else if (WarStatusTracker.HotBox.Count != 0)
                            {
                                WarStatusTracker.HotBox.Clear();
                            }

                            WarStatusTracker.Deployment = false;
                            WarStatusTracker.PirateDeployment = false;
                            WarStatusTracker.DeploymentInfluenceIncrease = 1.0;
                            WarStatusTracker.Escalation = false;
                            WarStatusTracker.EscalationDays = 0;
                            RefreshContracts(Sim.CurSystem);
                            if (WarStatusTracker.HotBox.Count == 0)
                                WarStatusTracker.HotBoxTravelling = false;

                            if (WarStatusTracker.EscalationOrder != null)
                            {
                                WarStatusTracker.EscalationOrder.SetCost(0);
                                var ActiveItems = Globals.TaskTimelineWidget.ActiveItems;
                                if (ActiveItems.TryGetValue(WarStatusTracker.EscalationOrder, out var taskManagementElement))
                                {
                                    taskManagementElement.UpdateItem(0);
                                }
                            }

                            Sim.Starmap.SetActivePath();
                            Sim.SetSimRoomState(DropshipLocation.SHIP);
                        }, primaryButtonText, Cleanup, "Cancel");
                        Sim.Starmap.Screen.AllowInput(false);
                        uiManager.SetFaderColor(uiManager.UILookAndColorConstants.PopupBackfill, UIManagerFader.FadePosition.FadeInBack, UIManagerRootType.PopupRoot);
                        return false;
                    }

                    return true;
                }
                catch (Exception e)
                {
                    Error(e);
                    return true;
                }
            }

            private static void Postfix()
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = UnityGameInstance.BattleTechGame.Simulation.CurSystem;
                if (WarStatusTracker.HotBox.Contains(system.Name))
                {
                    WarStatusTracker.HotBox.Remove(system.Name);
                }

                WarStatusTracker.Escalation = false;
                WarStatusTracker.EscalationDays = 0;
                RefreshContracts(system);
                if (WarStatusTracker.HotBox.Count == 0)
                    WarStatusTracker.HotBoxTravelling = false;
            }
        }

        [HarmonyPatch(typeof(SGTravelManager), "WarmingEngines_CanEnter")]
        public static class CompletedJumpPatch
        {
            private static void Postfix()
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                HoldContracts = false;
            }
        }

        [HarmonyPatch(typeof(SGTravelManager), "DisplayEnteredOrbitPopup")]
        public static class EnteredOrbitPatch
        {
            private static void Postfix()
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                var HasFlashpoint = false;
                WarStatusTracker.JustArrived = true;

                if (!WarStatusTracker.Deployment)
                    WarStatusTracker.EscalationDays = Settings.EscalationDays;
                else
                {
                    var rand = new Random();
                    WarStatusTracker.EscalationDays = rand.Next(Settings.DeploymentMinDays, Settings.DeploymentMaxDays + 1);
                    if (WarStatusTracker.EscalationDays < Settings.DeploymentRerollBound * WarStatusTracker.EscalationDays ||
                        WarStatusTracker.EscalationDays > (1 - Settings.DeploymentRerollBound) * WarStatusTracker.EscalationDays)
                        WarStatusTracker.EscalationDays = rand.Next(Settings.DeploymentMinDays, Settings.DeploymentMaxDays + 1);
                }

                foreach (var contract in Sim.CurSystem.SystemContracts)
                {
                    if (contract.IsFlashpointContract || contract.IsFlashpointCampaignContract)
                        HasFlashpoint = true;
                }

                if (!WarStatusTracker.HotBoxTravelling && !WarStatusTracker.HotBox.Contains(Sim.CurSystem.Name) && !HasFlashpoint && !HoldContracts)
                {
                    NeedsProcessing = true;
                    var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                    Sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                    NeedsProcessing = false;
                }

                HoldContracts = false;
            }
        }

        [HarmonyPatch(typeof(AAR_SalvageScreen), "OnCompleted")]
        public static class AAR_SalvageScreenOnCompletedPatch
        {
            private static void Prefix()
            {
                try
                {
                    if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                        return;

                    WarStatusTracker.JustArrived = false;
                    WarStatusTracker.HotBoxTravelling = false;
                }
                catch (Exception e)
                {
                    Error(e);
                }
            }
        }

        [HarmonyPatch(typeof(TaskTimelineWidget), "RegenerateEntries")]
        public static class TaskTimelineWidgetRegenerateEntriesPatch
        {
            private static void Postfix(TaskTimelineWidget __instance)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (WarStatusTracker != null && WarStatusTracker.Escalation)
                {
                    if (!WarStatusTracker.Deployment)
                    {
                        WarStatusTracker.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric,
                            "Escalation Days Remaining", "Escalation Days Remaining");
                        WarStatusTracker.EscalationOrder.SetCost(WarStatusTracker.EscalationDays);
                        __instance.AddEntry(WarStatusTracker.EscalationOrder, false);
                        __instance.RefreshEntries();
                    }
                    else
                    {
                        WarStatusTracker.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric,
                            "Escalation Days Remaining", "Forced Deployment Mission");
                        WarStatusTracker.EscalationOrder.SetCost(WarStatusTracker.EscalationDays);
                        __instance.AddEntry(WarStatusTracker.EscalationOrder, false);
                        __instance.RefreshEntries();
                    }
                }
            }
        }

        [HarmonyPatch(typeof(TaskTimelineWidget), "RemoveEntry")]
        public static class TaskTimelineWidgetRemoveEntryPatch
        {
            private static bool Prefix(WorkOrderEntry entry)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return true;

                if (!WarStatusTracker.JustArrived && (entry.ID.Equals("Escalation Days Remaining")) && entry.GetRemainingCost() != 0)
                {
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(SimGameState), "OnBreadcrumbArrival")]
        public static class SimGameStateOnBreadcrumbArrivalPatch
        {
            private static void Postfix(SimGameState __instance)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (!__instance.ActiveTravelContract.IsPriorityContract)
                {
                    if (!WarStatusTracker.Deployment)
                    {
                        WarStatusTracker.Escalation = true;
                        WarStatusTracker.EscalationDays = Settings.EscalationDays;
                        WarStatusTracker.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric, "Escalation Days Remaining", "Escalation Days Remaining");
                        WarStatusTracker.EscalationOrder.SetCost(WarStatusTracker.EscalationDays);
                        __instance.RoomManager.AddWorkQueueEntry(WarStatusTracker.EscalationOrder);
                        __instance.RoomManager.SortTimeline();
                        __instance.RoomManager.RefreshTimeline();
                    }
                    else
                    {
                        var rand = new Random();
                        WarStatusTracker.EscalationDays = rand.Next(Settings.DeploymentMinDays, Settings.DeploymentMaxDays + 1);
                        if (WarStatusTracker.EscalationDays < Settings.DeploymentRerollBound * WarStatusTracker.EscalationDays ||
                            WarStatusTracker.EscalationDays > (1 - Settings.DeploymentRerollBound) * WarStatusTracker.EscalationDays)
                            WarStatusTracker.EscalationDays = rand.Next(Settings.DeploymentMinDays, Settings.DeploymentMaxDays + 1);

                        WarStatusTracker.Escalation = true;
                        WarStatusTracker.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric, "Escalation Days Remaining", "Forced Deployment Mission");
                        WarStatusTracker.EscalationOrder.SetCost(WarStatusTracker.EscalationDays);
                        __instance.RoomManager.AddWorkQueueEntry(WarStatusTracker.EscalationOrder);
                        __instance.RoomManager.SortTimeline();
                        __instance.RoomManager.RefreshTimeline();
                    }
                }
            }
        }

        //Need to clear out the old stuff if a contract is cancelled to prevent crashing.
        [HarmonyPatch(typeof(SimGameState), "OnBreadcrumbCancelledByUser")]
        public static class SimGameStateBreadCrumbCancelledPatch
        {
            //static bool Prefix(SimGameState __instance)
            //{
            //    try
            //    {
            //        //        if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
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


            private static void Postfix()
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = UnityGameInstance.BattleTechGame.Simulation.CurSystem;
                if (WarStatusTracker.HotBox.Count == 2)
                {
                    WarStatusTracker.HotBox.RemoveAt(0);
                }
                else
                {
                    WarStatusTracker.HotBox.Clear();
                }

                WarStatusTracker.Deployment = false;
                WarStatusTracker.PirateDeployment = false;
                WarStatusTracker.DeploymentInfluenceIncrease = 1.0;
                WarStatusTracker.Escalation = false;
                WarStatusTracker.EscalationDays = 0;
                RefreshContracts(system);
                if (WarStatusTracker.HotBox.Count == 0)
                    WarStatusTracker.HotBoxTravelling = false;
            }
        }


        [HarmonyPatch(typeof(Contract), "GenerateSalvage")]
        public static class ContractGenerateSalvagePatch
        {
            private static void Postfix(Contract __instance)
            {
                //Log("****Generate Salvage****");
                //Log("Sim Null? " + (Sim == null).ToString());
                //Log("CurSystem Null? " + (Sim.CurSystem == null).ToString());
                //Log("CurSystem: " + Sim.CurSystem.Name);
                //Log("WarStatus Null? " + (WarStatusTracker == null).ToString());
                //Log("WarStatus System Null? " + (null ==WarStatusTracker.systems.Find(x => x.name == Sim.CurSystem.Name)).ToString());
                //foreach (SystemStatus systemstatus in WarStatusTracker.systems)
                //{
                //    Log(systemstatus.name);
                //    Log(systemstatus.starSystem.Name);
                //}

                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = WarStatusTracker.SystemStatuses.Find(x => x.starSystem == Sim.CurSystem);
                if (WarStatusTracker.HotBox == null)
                    WarStatusTracker.HotBox = new List<string>();

                if (system.BonusSalvage && WarStatusTracker.HotBox.Contains(Sim.CurSystem.Name))
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
            private static void Postfix(AAR_ContractObjectivesWidget __instance)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = WarStatusTracker.SystemStatuses.Find(x => x.starSystem == Sim.CurSystem);

                if (system.BonusCBills && WarStatusTracker.HotBox.Contains(Sim.CurSystem.Name))
                {
                    var missionObjectiveResultString = $"BONUS FROM ESCALATION: ¢{String.Format("{0:n0}", BonusMoney)}";
                    if (WarStatusTracker.Deployment)
                        missionObjectiveResultString = $"BONUS FROM DEPLOYMENT: ¢{String.Format("{0:n0}", BonusMoney)}";
                    var missionObjectiveResult = new MissionObjectiveResult(missionObjectiveResultString, "7facf07a-626d-4a3b-a1ec-b29a35ff1ac0", false, true, ObjectiveStatus.Succeeded, false);
                    Traverse.Create(__instance).Method("AddObjective", missionObjectiveResult).GetValue();
                }
            }
        }

        [HarmonyPatch(typeof(AAR_UnitStatusWidget), "FillInPilotData")]
        public static class AARUnitStatusWidgetPatch
        {
            private static void Prefix(ref int xpEarned, UnitResult ___UnitData)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = WarStatusTracker.SystemStatuses.Find(x => x.name == WarStatusTracker.CurSystem);
                if (system.BonusXP && WarStatusTracker.HotBox.Contains(system.name))
                {
                    xpEarned = xpEarned + (int) (xpEarned * Settings.BonusXPFactor);
                    var unspentXP = ___UnitData.pilot.UnspentXP;
                    var XPCorrection = (int) (xpEarned * Settings.BonusXPFactor);
                    ___UnitData.pilot.StatCollection.Set("ExperienceUnspent", unspentXP + XPCorrection);
                }
            }
        }

        public static void SystemBonuses(StarSystem starSystem)
        {
            var systemStatus = WarStatusTracker.SystemStatuses.Find(x => x.starSystem == starSystem);
            int systemDifficulty;
            if (Settings.ChangeDifficulty)
                systemDifficulty = systemStatus.DifficultyRating;
            else
                systemDifficulty = systemStatus.DifficultyRating + (int) Sim.GlobalDifficulty;

            if (!WarStatusTracker.HotBox.Contains(starSystem.Name))
            {
                systemStatus.BonusCBills = false;
                systemStatus.BonusSalvage = false;
                systemStatus.BonusXP = false;

                if (systemDifficulty <= 4)
                {
                    var bonus = Rng.Next(0, 3);
                    if (bonus == 0)
                        systemStatus.BonusCBills = true;
                    if (bonus == 1)
                        systemStatus.BonusXP = true;
                    if (bonus == 2)
                        systemStatus.BonusSalvage = true;
                }

                if (systemDifficulty <= 8 && systemDifficulty > 4)
                {
                    systemStatus.BonusCBills = true;
                    systemStatus.BonusSalvage = true;
                    systemStatus.BonusXP = true;
                    var bonus = Rng.Next(0, 3);
                    if (bonus == 0)
                        systemStatus.BonusCBills = false;
                    if (bonus == 1)
                        systemStatus.BonusXP = false;
                    if (bonus == 2)
                        systemStatus.BonusSalvage = false;
                }

                if (systemDifficulty > 8)
                {
                    systemStatus.BonusCBills = true;
                    systemStatus.BonusSalvage = true;
                    systemStatus.BonusXP = true;
                }
            }
        }

        public static void CompleteEscalation()
        {
            var systemStatus = WarStatusTracker.SystemStatuses.Find(x => x.starSystem == Sim.CurSystem);
            systemStatus.BonusCBills = false;
            systemStatus.BonusSalvage = false;
            systemStatus.BonusXP = false;
            WarStatusTracker.Deployment = false;
            WarStatusTracker.PirateDeployment = false;
            WarStatusTracker.DeploymentInfluenceIncrease = 1.0;
            WarStatusTracker.HotBox.Remove(systemStatus.name);
            RefreshContracts(systemStatus.starSystem);
            var hasFlashpoint = false;
            foreach (var contract in Sim.CurSystem.SystemContracts)
            {
                if (contract.IsFlashpointContract || contract.IsFlashpointCampaignContract)
                    hasFlashpoint = true;
            }

            if (!hasFlashpoint)
            {
                NeedsProcessing = true;
                var cmdCenter = Sim.RoomManager.CmdCenterRoom;
                Sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                NeedsProcessing = false;
            }
        }

        [HarmonyPatch(typeof(SimGameState), "ContractUserMeetsReputation")]
        public static class SimGameStateContractUserMeetsReputationPatch
        {
            private static void Postfix(ref bool __result)
            {
                if (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete"))
                    return;

                if (WarStatusTracker.Deployment)
                    __result = true;
            }
        }

        public static int ProcessReputation(float FactionRep)
        {
            var simStory = Sim.Constants.Story;
            var simCareer = Sim.Constants.CareerMode;
            int maxContracts;

            if (FactionRep <= simStory.LoathedReputation)
                maxContracts = Convert.ToInt32(simCareer.LoathedMaxContractDifficulty);
            else if (FactionRep <= simStory.HatedReputation)
                maxContracts = Convert.ToInt32(simCareer.HatedMaxContractDifficulty);
            else if (FactionRep <= simStory.DislikedReputation)
                maxContracts = Convert.ToInt32(simCareer.DislikedMaxContractDifficulty);
            else if (FactionRep <= simStory.LikedReputation)
                maxContracts = Convert.ToInt32(simCareer.IndifferentMaxContractDifficulty);
            else if (FactionRep <= simStory.FriendlyReputation)
                maxContracts = Convert.ToInt32(simCareer.LikedMaxContractDifficulty);
            else if (FactionRep <= simStory.HonoredReputation)
                maxContracts = Convert.ToInt32(simCareer.FriendlyMaxContractDifficulty);
            else
                maxContracts = Convert.ToInt32(simCareer.HonoredMaxContractDifficulty);

            if (maxContracts > 10)
                maxContracts = 10;
            if (maxContracts < 1)
                maxContracts = 1;

            return maxContracts;
        }

        [HarmonyPatch(typeof(SGRoomController_CmdCenter), "StartContractScreen")]
        public static class SGRoomControllerCmdCenterStartContractScreenPatch
        {
            private static void Prefix()
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (WarStatusTracker != null && !WarStatusTracker.StartGameInitialized)
                {
                    ProcessHotSpots();
                    // StarmapMod.SetupRelationPanel();
                    WarStatusTracker.StartGameInitialized = true;
                }

                HoldContracts = true;
            }
        }

        [HarmonyPatch(typeof(SGContractsWidget), "OnNegotiateClicked")]
        public static class SGContractsWidgetOnNegotiateClickedPatch
        {
            private static bool Prefix(SGContractsWidget __instance)
            {
                try
                {
                    if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                        return true;

                    if (__instance.SelectedContract.Override.contractDisplayStyle == ContractDisplayStyle.BaseCampaignStory)
                    {
                        var message = "Commander, this contract will bring us right to the front lines. If we accept it, we will be forced to take missions when our employer needs us to simultaneously attack in support of their war effort. We will be commited to this Deployment until the system is taken or properly defended and will lose significant reputation if we end up backing out before the job is done. But, oh man, they will certainly reward us well if their operation is ultimately successful! This Deployment may require missions to be done without time between them for repairs or to properly rest our pilots. I strongly encourage you to only accept this arrangement if you think we're up to it.";
                        PauseNotification.Show("Deployment", message,
                            Sim.GetCrewPortrait(SimGameCrew.Crew_Darius), string.Empty, true, delegate { __instance.NegotiateContract(__instance.SelectedContract); }, "Do it anyways", null, "Cancel");
                        return false;
                    }

                    return true;
                }
                catch (Exception e)
                {
                    Error(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SGTimePlayPause), "ToggleTime")]
        public static class SGTimePlayPauseToggleTimePatch
        {
            private static bool Prefix()
            {
                try
                {
                    if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                        return true;

                    if (!Settings.ResetMap && WarStatusTracker.Deployment && !WarStatusTracker.HotBoxTravelling && WarStatusTracker.EscalationDays <= 0)
                    {
                        return false;
                    }

                    return true;
                }
                catch (Exception e)
                {
                    Error(e);
                    return true;
                }
            }
        }


        [HarmonyPatch(typeof(TaskTimelineWidget), "OnTaskDetailsClicked")]
        public static class TaskTimelineWidgetOnTaskDetailsClickedPatch
        {
            public static bool Prefix(TaskManagementElement element)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
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
        public static class StarSystemCompletedContractPatch
        {
            public static void Prefix(StarSystem __instance, ref float __state)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                __state = __instance.Sim.Constants.Story.ContractSuccessReduction;

                var HasFlashpoint = false;
                foreach (var contract in Sim.CurSystem.SystemContracts)
                {
                    if (contract.IsFlashpointContract || contract.IsFlashpointCampaignContract)
                        HasFlashpoint = true;
                }

                if (WarStatusTracker.HotBox.Contains(Sim.CurSystem.Name))
                {
                    __instance.Sim.Constants.Story.ContractSuccessReduction = 100;
                    WarStatusTracker.DeploymentInfluenceIncrease *= Settings.DeploymentEscalationFactor;
                    if (!HasFlashpoint)
                    {
                        Sim.CurSystem.activeSystemContracts.Clear();
                        Sim.CurSystem.activeSystemBreadcrumbs.Clear();
                    }

                    if (WarStatusTracker.EscalationOrder != null)
                    {
                        WarStatusTracker.EscalationOrder.SetCost(0);
                        var ActiveItems = Globals.TaskTimelineWidget.ActiveItems;
                        if (ActiveItems.TryGetValue(WarStatusTracker.EscalationOrder, out var taskManagementElement))
                        {
                            taskManagementElement.UpdateItem(0);
                        }
                    }

                    WarStatusTracker.Escalation = true;
                    var rand = new Random();
                    WarStatusTracker.EscalationDays = rand.Next(Settings.DeploymentMinDays, Settings.DeploymentMaxDays + 1);
                    if (WarStatusTracker.EscalationDays < Settings.DeploymentRerollBound * WarStatusTracker.EscalationDays ||
                        WarStatusTracker.EscalationDays > (1 - Settings.DeploymentRerollBound) * WarStatusTracker.EscalationDays)
                        WarStatusTracker.EscalationDays = rand.Next(Settings.DeploymentMinDays, Settings.DeploymentMaxDays + 1);

                    WarStatusTracker.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric, "Escalation Days Remaining", "Forced Deployment Mission");
                    WarStatusTracker.EscalationOrder.SetCost(WarStatusTracker.EscalationDays);
                    Sim.RoomManager.AddWorkQueueEntry(WarStatusTracker.EscalationOrder);
                    Sim.RoomManager.SortTimeline();
                    Sim.RoomManager.RefreshTimeline();
                }
            }

            public static void Postfix(StarSystem __instance, ref float __state)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                __instance.Sim.Constants.Story.ContractSuccessReduction = __state;
            }
        }
    }
}

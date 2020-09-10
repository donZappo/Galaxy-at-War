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

        public static void ProcessHotSpots()
        {
            try
            {
                var dominantFaction = Globals.WarStatusTracker.systems.Find(x => x.name == Globals.WarStatusTracker.CurSystem).owner;
                FullHomeContendedSystems.Clear();
                HomeContendedSystems.Clear();
                ExternalPriorityTargets.Clear();
                Globals.WarStatusTracker.HomeContendedStrings.Clear();
                var factRepDict = new Dictionary<string, int>();
                foreach (var faction in Globals.IncludedFactions)
                {
                    ExternalPriorityTargets.Add(faction, new List<StarSystem>());
                    var maxContracts = ProcessReputation(Globals.Sim.GetRawReputation(Globals.FactionValues.Find(x => x.Name == faction)));
                    factRepDict.Add(faction, maxContracts);
                }

                //Populate lists with planets that are in danger of flipping
                foreach (var systemStatus in Globals.WarStatusTracker.systems)
                {
                    if (!Globals.WarStatusTracker.HotBox.Contains(systemStatus.name))
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
                        Globals.WarStatusTracker.HomeContendedStrings.Add(system.Key.Name);
                    }

                    HomeContendedSystems.Add(system.Key);
                    i++;
                }
            }
            catch (Exception ex)
            {
                LogDebug(ex);
            }
        }

        [HarmonyPatch(typeof(StarSystem), "GenerateInitialContracts")]
        public static class SimGameStateGenerateInitialContractsPatch
        {
            private static void Prefix(ref float __state)
            {
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                Traverse.Create(Globals.Sim.CurSystem).Property("MissionsCompleted").SetValue(0);
                Traverse.Create(Globals.Sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(0);
                Traverse.Create(Globals.Sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(0);
                __state = Globals.Sim.CurSystem.CurMaxContracts;

                foreach (var theFaction in Globals.IncludedFactions)
                {
                    // g - commented out 9/9/20
                    //if (Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == theFaction) == null)
                    //    continue;

                    var deathListTracker = Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == theFaction);
                    AdjustDeathList(deathListTracker, true);
                }

                if (Globals.Settings.LimitSystemContracts.ContainsKey(Globals.Sim.CurSystem.Name))
                {
                    Traverse.Create(Globals.Sim.CurSystem).Property("CurMaxContracts").SetValue(Globals.Settings.LimitSystemContracts[Globals.Sim.CurSystem.Name]);
                }

                if (Globals.WarStatusTracker.Deployment)
                {
                    Traverse.Create(Globals.Sim.CurSystem).Property("CurMaxContracts").SetValue(Globals.Settings.DeploymentContracts);
                }
            }


            private static void Postfix(ref float __state)
            {
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (Globals.WarStatusTracker.systems.Count > 0)
                    ProcessHotSpots();

                isBreadcrumb = true;
                Globals.Sim.CurSystem.activeSystemBreadcrumbs.Clear();
                Traverse.Create(Globals.Sim.CurSystem).Property("MissionsCompleted").SetValue(20);
                Traverse.Create(Globals.Sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(1);
                Traverse.Create(Globals.Sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(1);
                Globals.WarStatusTracker.DeploymentContracts.Clear();

                if (HomeContendedSystems.Count != 0 && !Globals.Settings.DefensiveFactions.Contains(Globals.Sim.CurSystem.OwnerValue.Name) && !Globals.WarStatusTracker.Deployment)
                {
                    var i = 0;
                    var twiddle = 0;
                    var RandomSystem = 0;
                    Globals.WarStatusTracker.HomeContendedStrings.Clear();
                    while (HomeContendedSystems.Count != 0)
                    {
                        Traverse.Create(Globals.Sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(i + 1);
                        Traverse.Create(Globals.Sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(i + 1);
                        if (twiddle == 0)
                            twiddle = -1;
                        else if (twiddle == 1)
                            RandomSystem = Globals.Rng.Next(0, 3 * HomeContendedSystems.Count / 4);
                        else if (twiddle == -1)
                            RandomSystem = Globals.Rng.Next(HomeContendedSystems.Count / 4, 3 * HomeContendedSystems.Count / 4);

                        var MainBCTarget = HomeContendedSystems[RandomSystem];

                        if (MainBCTarget == Globals.Sim.CurSystem || (Globals.Sim.CurSystem.OwnerValue.Name == "Locals" && MainBCTarget.OwnerValue.Name != "Locals") ||
                            !Globals.IncludedFactions.Contains(MainBCTarget.OwnerValue.Name))
                        {
                            HomeContendedSystems.Remove(MainBCTarget);
                            Globals.WarStatusTracker.HomeContendedStrings.Remove(MainBCTarget.Name);
                            continue;
                        }

                        TemporaryFlip(MainBCTarget, Globals.Sim.CurSystem.OwnerValue.Name);
                        if (Globals.Sim.CurSystem.SystemBreadcrumbs.Count == 0 && MainBCTarget.OwnerValue.Name != Globals.Sim.CurSystem.OwnerValue.Name)
                        {
                            Globals.Sim.GeneratePotentialContracts(true, null, MainBCTarget);
                            SystemBonuses(MainBCTarget);

                            var PrioritySystem = Globals.Sim.CurSystem.SystemBreadcrumbs.Find(x => x.TargetSystem == MainBCTarget.ID);
                            Traverse.Create(PrioritySystem.Override).Field("contractDisplayStyle").SetValue(ContractDisplayStyle.BaseCampaignStory);
                            Globals.WarStatusTracker.DeploymentContracts.Add(PrioritySystem.Override.contractName);
                        }
                        else if (twiddle == -1 || MainBCTarget.OwnerValue.Name == Globals.Sim.CurSystem.OwnerValue.Name)
                        {
                            Globals.Sim.GeneratePotentialContracts(false, null, MainBCTarget);
                            SystemBonuses(MainBCTarget);
                        }
                        else if (twiddle == 1)
                        {
                            Globals.Sim.GeneratePotentialContracts(false, null, MainBCTarget);
                            SystemBonuses(MainBCTarget);

                            var PrioritySystem = Globals.Sim.CurSystem.SystemBreadcrumbs.Find(x => x.TargetSystem == MainBCTarget.ID);
                            Traverse.Create(PrioritySystem.Override).Field("contractDisplayStyle").SetValue(ContractDisplayStyle.BaseCampaignStory);
                            Globals.WarStatusTracker.DeploymentContracts.Add(PrioritySystem.Override.contractName);
                        }

                        var systemStatus = Globals.WarStatusTracker.systems.Find(x => x.name == MainBCTarget.Name);
                        RefreshContracts(systemStatus);
                        HomeContendedSystems.Remove(MainBCTarget);
                        Globals.WarStatusTracker.HomeContendedStrings.Add(MainBCTarget.Name);
                        if (Globals.Sim.CurSystem.SystemBreadcrumbs.Count == Globals.Settings.InternalHotSpots)
                            break;

                        i = Globals.Sim.CurSystem.SystemBreadcrumbs.Count;
                        twiddle *= -1;
                    }
                }

                if (ExternalPriorityTargets.Count != 0)
                {
                    var startBC = Globals.Sim.CurSystem.SystemBreadcrumbs.Count;
                    var j = startBC;
                    foreach (var ExtTarget in ExternalPriorityTargets.Keys)
                    {
                        if (ExternalPriorityTargets[ExtTarget].Count == 0 || Globals.Settings.DefensiveFactions.Contains(ExtTarget) ||
                            !Globals.IncludedFactions.Contains(ExtTarget)) continue;
                        do
                        {
                            var randTarget = Globals.Rng.Next(0, ExternalPriorityTargets[ExtTarget].Count);
                            Traverse.Create(Globals.Sim.CurSystem).Property("CurBreadcrumbOverride").SetValue(j + 1);
                            Traverse.Create(Globals.Sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(j + 1);
                            if (ExternalPriorityTargets[ExtTarget][randTarget] == Globals.Sim.CurSystem)
                            {
                                ExternalPriorityTargets[ExtTarget].Remove(Globals.Sim.CurSystem);
                                continue;
                            }

                            TemporaryFlip(ExternalPriorityTargets[ExtTarget][randTarget], ExtTarget);
                            if (Globals.Sim.CurSystem.SystemBreadcrumbs.Count == 0)
                                Globals.Sim.GeneratePotentialContracts(true, null, ExternalPriorityTargets[ExtTarget][randTarget]);
                            else
                                Globals.Sim.GeneratePotentialContracts(false, null, ExternalPriorityTargets[ExtTarget][randTarget]);
                            SystemBonuses(ExternalPriorityTargets[ExtTarget][randTarget]);
                            var systemStatus = Globals.WarStatusTracker.systems.Find(x =>
                                x.name == ExternalPriorityTargets[ExtTarget][randTarget].Name);
                            RefreshContracts(systemStatus);
                            ExternalPriorityTargets[ExtTarget].RemoveAt(randTarget);
                        } while (Globals.Sim.CurSystem.SystemBreadcrumbs.Count == j && ExternalPriorityTargets[ExtTarget].Count != 0);

                        j = Globals.Sim.CurSystem.SystemBreadcrumbs.Count;
                        if (j - startBC == Globals.Settings.ExternalHotSpots)
                            break;
                    }
                }

                isBreadcrumb = false;
                Traverse.Create(Globals.Sim.CurSystem).Property("CurMaxContracts").SetValue(__state);
            }
        }


        [HarmonyPatch(typeof(StarSystem))]
        [HarmonyPatch("InitialContractsFetched", MethodType.Getter)]
        public static class StarSystemInitialContractsFetchedPatch
        {
            private static void Postfix(ref bool __result)
            {
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (Globals.WarStatusTracker.StartGameInitialized)
                    __result = true;
            }
        }

        [HarmonyPatch(typeof(SimGameState), "GetDifficultyRangeForContract")]
        public static class SimGameStateGetDifficultyRangeForContractsPatch
        {
            private static void Prefix(SimGameState __instance, ref int __state)
            {
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (isBreadcrumb)
                {
                    __state = __instance.Constants.Story.ContractDifficultyVariance;
                    __instance.Constants.Story.ContractDifficultyVariance = 0;
                }
            }

            private static void Postfix(SimGameState __instance, ref int __state)
            {
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
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
            var tracker = Globals.WarStatusTracker.systems.Find(x => x.name == starSystem.Name);

            if (Globals.Settings.NoOffensiveContracts.Contains(faction))
            {
                if (!Globals.Settings.NoOffensiveContracts.Contains(tracker.OriginalOwner))
                {
                    starSystem.Def.contractEmployerIDs.Add(tracker.OriginalOwner);
                    starSystem.Def.contractTargetIDs.Add(faction);
                }
                else
                {
                    List<string> factionList;
                    if (Globals.Settings.ISMCompatibility)
                        factionList = Globals.Settings.IncludedFactions_ISM;
                    else
                        factionList = Globals.Settings.IncludedFactions;

                    factionList.Shuffle();
                    string factionEmployer = "Davion";
                    foreach (var employer in factionList)
                    {
                        if (Globals.Settings.NoOffensiveContracts.Contains(employer) ||
                            Globals.Settings.DefensiveFactions.Contains(employer) ||
                            Globals.Settings.ImmuneToWar.Contains(employer))
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
            if (Globals.Settings.GaW_PoliceSupport && faction == Globals.WarStatusTracker.ComstarAlly)
                starSystem.Def.contractEmployerIDs.Add(Globals.Settings.GaW_Police);


            foreach (var influence in tracker.influenceTracker.OrderByDescending(x => x.Value))
            {
                if (Globals.WarStatusTracker.PirateDeployment)
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

            if (starSystem.Def.contractTargetIDs.Contains(Globals.WarStatusTracker.ComstarAlly))
                starSystem.Def.contractTargetIDs.Add(Globals.WarStatusTracker.ComstarAlly);

            if (starSystem.Def.contractTargetIDs.Count == 0)
                starSystem.Def.contractTargetIDs.Add("AuriganPirates");

            if (!starSystem.Def.contractTargetIDs.Contains("Locals"))
                starSystem.Def.contractTargetIDs.Add("Locals");
        }

        //Deployments area.
        [HarmonyPatch(typeof(SimGameState), "PrepareBreadcrumb")]
        public static class SimGameStatePrepareBreadcrumbPatch
        {
            private static void Postfix(SimGameState __instance, Contract contract)
            {
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (!__instance.CurSystem.Def.Description.Id.StartsWith(contract.TargetSystem))
                {
                    var starSystem = Globals.Sim.StarSystems.Find(x => x.Def.Description.Id.StartsWith(contract.TargetSystem));
                    Globals.WarStatusTracker.HotBox.Add(starSystem.Name);
                    Globals.WarStatusTracker.HotBoxTravelling = true;

                    if (contract.Override.contractDisplayStyle == ContractDisplayStyle.BaseCampaignStory)
                    {
                        Globals.WarStatusTracker.Deployment = true;
                        Globals.WarStatusTracker.DeploymentInfluenceIncrease = 1.0;
                        Globals.WarStatusTracker.DeploymentEmployer = contract.Override.employerTeam.FactionValue.Name;
                    }
                    else
                    {
                        Globals.WarStatusTracker.Deployment = false;
                        Globals.WarStatusTracker.DeploymentInfluenceIncrease = 1.0;
                        Globals.WarStatusTracker.PirateDeployment = false;
                    }

                    TemporaryFlip(starSystem, contract.Override.employerTeam.FactionValue.Name);
                    if (Globals.WarStatusTracker.HotBox.Contains(__instance.CurSystem.Name))
                    {
                        Globals.WarStatusTracker.HotBox.Remove(__instance.CurSystem.Name);
                        Globals.WarStatusTracker.EscalationDays = 0;
                        Globals.WarStatusTracker.Escalation = false;
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
                    if (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                        return true;

                    if (Globals.WarStatusTracker.Deployment)
                    {
                        var uiManager = (UIManager) AccessTools.Field(typeof(SGNavigationScreen), "uiManager").GetValue(__instance);

                        void Cleanup()
                        {
                            uiManager.ResetFader(UIManagerRootType.PopupRoot);
                            Globals.Sim.Starmap.Screen.AllowInput(true);
                        }

                        var primaryButtonText = "Break Deployment";
                        var message = "WARNING: This action will break your current Deployment. Your reputation with the employer and the MRB will be negatively impacted.";
                        PauseNotification.Show("Navigation Change", message, Globals.Sim.GetCrewPortrait(SimGameCrew.Crew_Sumire), string.Empty, true, delegate
                        {
                            Cleanup();
                            Globals.WarStatusTracker.Deployment = false;
                            Globals.WarStatusTracker.PirateDeployment = false;
                            if (Globals.Sim.GetFactionDef(Globals.WarStatusTracker.DeploymentEmployer).FactionValue.DoesGainReputation)
                            {
                                var employerRepBadFaithMod = Globals.Sim.Constants.Story.EmployerRepBadFaithMod;
                                int num;

                                if (Globals.Settings.ChangeDifficulty)
                                    num = Mathf.RoundToInt(Globals.Sim.CurSystem.Def.GetDifficulty(SimGameState.SimGameType.CAREER) * employerRepBadFaithMod);
                                else
                                    num = Mathf.RoundToInt((Globals.Sim.CurSystem.Def.DefaultDifficulty + Globals.Sim.GlobalDifficulty) * employerRepBadFaithMod);

                                if (num != 0)
                                {
                                    Globals.Sim.SetReputation(Globals.Sim.GetFactionDef(Globals.WarStatusTracker.DeploymentEmployer).FactionValue, num);
                                    Globals.Sim.SetReputation(Globals.Sim.GetFactionValueFromString("faction_MercenaryReviewBoard"), num);
                                }
                            }

                            if (Globals.WarStatusTracker.HotBox.Count == 2)
                            {
                                Globals.WarStatusTracker.HotBox.RemoveAt(0);
                            }
                            else if (Globals.WarStatusTracker.HotBox.Count != 0)
                            {
                                Globals.WarStatusTracker.HotBox.Clear();
                            }

                            Globals.WarStatusTracker.Deployment = false;
                            Globals.WarStatusTracker.PirateDeployment = false;
                            Globals.WarStatusTracker.DeploymentInfluenceIncrease = 1.0;
                            Globals.WarStatusTracker.Escalation = false;
                            Globals.WarStatusTracker.EscalationDays = 0;
                            var systemStatus = Globals.WarStatusTracker.systems.Find(x => x.name == Globals.Sim.CurSystem.Name);
                            RefreshContracts(systemStatus);
                            if (Globals.WarStatusTracker.HotBox.Count == 0)
                                Globals.WarStatusTracker.HotBoxTravelling = false;

                            if (Globals.WarStatusTracker.EscalationOrder != null)
                            {
                                Globals.WarStatusTracker.EscalationOrder.SetCost(0);
                                var ActiveItems = Globals.TaskTimelineWidget.ActiveItems;
                                if (ActiveItems.TryGetValue(Globals.WarStatusTracker.EscalationOrder, out var taskManagementElement))
                                {
                                    taskManagementElement.UpdateItem(0);
                                }
                            }

                            Globals.Sim.Starmap.SetActivePath();
                            Globals.Sim.SetSimRoomState(DropshipLocation.SHIP);
                        }, primaryButtonText, Cleanup, "Cancel");
                        Globals.Sim.Starmap.Screen.AllowInput(false);
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
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = UnityGameInstance.BattleTechGame.Simulation.CurSystem;
                if (Globals.WarStatusTracker.HotBox.Contains(system.Name))
                {
                    Globals.WarStatusTracker.HotBox.Remove(system.Name);
                }

                Globals.WarStatusTracker.Escalation = false;
                Globals.WarStatusTracker.EscalationDays = 0;
                var systemStatus = Globals.WarStatusTracker.systems.Find(x => x.name == system.Name);
                RefreshContracts(systemStatus);
                if (Globals.WarStatusTracker.HotBox.Count == 0)
                    Globals.WarStatusTracker.HotBoxTravelling = false;
            }
        }

        [HarmonyPatch(typeof(SGNavigationScreen), "OnFlashpointAccepted")]
        public static class SGNavigationScreenOnFlashpointAcceptedPatch
        {
            private static bool Prefix(SGNavigationScreen __instance)
            {
                try
                {
                    if (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                        return true;

                    if (Globals.WarStatusTracker.Deployment)
                    {
                        var uiManager = (UIManager) AccessTools.Field(typeof(SGNavigationScreen), "uiManager").GetValue(__instance);

                        void Cleanup()
                        {
                            uiManager.ResetFader(UIManagerRootType.PopupRoot);
                            Globals.Sim.Starmap.Screen.AllowInput(true);
                        }

                        var primaryButtonText = "Break Deployment";
                        var message = "WARNING: This action will break your current Deployment. Your reputation with the employer and the MRB will be negatively impacted.";
                        PauseNotification.Show("Navigation Change", message, Globals.Sim.GetCrewPortrait(SimGameCrew.Crew_Sumire), string.Empty, true, delegate
                        {
                            Cleanup();
                            Globals.WarStatusTracker.Deployment = false;
                            Globals.WarStatusTracker.PirateDeployment = false;
                            if (Globals.Sim.GetFactionDef(Globals.WarStatusTracker.DeploymentEmployer).FactionValue.DoesGainReputation)
                            {
                                var employerRepBadFaithMod = Globals.Sim.Constants.Story.EmployerRepBadFaithMod;
                                int num;
                                if (Globals.Settings.ChangeDifficulty)
                                    num = Mathf.RoundToInt(Globals.Sim.CurSystem.Def.GetDifficulty(SimGameState.SimGameType.CAREER) * employerRepBadFaithMod);
                                else
                                    num = Mathf.RoundToInt((Globals.Sim.CurSystem.Def.DefaultDifficulty + Globals.Sim.GlobalDifficulty) * employerRepBadFaithMod);

                                if (num != 0)
                                {
                                    Globals.Sim.SetReputation(Globals.Sim.GetFactionDef(Globals.WarStatusTracker.DeploymentEmployer).FactionValue, num);
                                    Globals.Sim.SetReputation(Globals.Sim.GetFactionValueFromString("faction_MercenaryReviewBoard"), num);
                                }
                            }

                            if (Globals.WarStatusTracker.HotBox.Count == 2)
                            {
                                Globals.WarStatusTracker.HotBox.RemoveAt(0);
                            }
                            else if (Globals.WarStatusTracker.HotBox.Count != 0)
                            {
                                Globals.WarStatusTracker.HotBox.Clear();
                            }

                            Globals.WarStatusTracker.Deployment = false;
                            Globals.WarStatusTracker.PirateDeployment = false;
                            Globals.WarStatusTracker.DeploymentInfluenceIncrease = 1.0;
                            Globals.WarStatusTracker.Escalation = false;
                            Globals.WarStatusTracker.EscalationDays = 0;
                            var systemStatus = Globals.WarStatusTracker.systems.Find(x => x.name == Globals.Sim.CurSystem.Name);
                            RefreshContracts(systemStatus);
                            if (Globals.WarStatusTracker.HotBox.Count == 0)
                                Globals.WarStatusTracker.HotBoxTravelling = false;

                            if (Globals.WarStatusTracker.EscalationOrder != null)
                            {
                                Globals.WarStatusTracker.EscalationOrder.SetCost(0);
                                var ActiveItems = Globals.TaskTimelineWidget.ActiveItems;
                                if (ActiveItems.TryGetValue(Globals.WarStatusTracker.EscalationOrder, out var taskManagementElement))
                                {
                                    taskManagementElement.UpdateItem(0);
                                }
                            }

                            Globals.Sim.Starmap.SetActivePath();
                            Globals.Sim.SetSimRoomState(DropshipLocation.SHIP);
                        }, primaryButtonText, Cleanup, "Cancel");
                        Globals.Sim.Starmap.Screen.AllowInput(false);
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
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = UnityGameInstance.BattleTechGame.Simulation.CurSystem;
                if (Globals.WarStatusTracker.HotBox.Contains(system.Name))
                {
                    Globals.WarStatusTracker.HotBox.Remove(system.Name);
                }

                Globals.WarStatusTracker.Escalation = false;
                Globals.WarStatusTracker.EscalationDays = 0;
                var systemStatus = Globals.WarStatusTracker.systems.Find(x => x.name == system.Name);
                RefreshContracts(systemStatus);
                if (Globals.WarStatusTracker.HotBox.Count == 0)
                    Globals.WarStatusTracker.HotBoxTravelling = false;
            }
        }

        [HarmonyPatch(typeof(SGTravelManager), "WarmingEngines_CanEnter")]
        public static class CompletedJumpPatch
        {
            private static void Postfix()
            {
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                Globals.HoldContracts = false;
            }
        }

        [HarmonyPatch(typeof(SGTravelManager), "DisplayEnteredOrbitPopup")]
        public static class EnteredOrbitPatch
        {
            private static void Postfix()
            {
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                var HasFlashpoint = false;
                Globals.WarStatusTracker.JustArrived = true;

                if (!Globals.WarStatusTracker.Deployment)
                    Globals.WarStatusTracker.EscalationDays = Globals.Settings.EscalationDays;
                else
                {
                    var rand = new Random();
                    Globals.WarStatusTracker.EscalationDays = rand.Next(Globals.Settings.DeploymentMinDays, Globals.Settings.DeploymentMaxDays + 1);
                    if (Globals.WarStatusTracker.EscalationDays < Globals.Settings.DeploymentRerollBound * Globals.WarStatusTracker.EscalationDays ||
                        Globals.WarStatusTracker.EscalationDays > (1 - Globals.Settings.DeploymentRerollBound) * Globals.WarStatusTracker.EscalationDays)
                        Globals.WarStatusTracker.EscalationDays = rand.Next(Globals.Settings.DeploymentMinDays, Globals.Settings.DeploymentMaxDays + 1);
                }

                foreach (var contract in Globals.Sim.CurSystem.SystemContracts)
                {
                    if (contract.IsFlashpointContract || contract.IsFlashpointCampaignContract)
                        HasFlashpoint = true;
                }

                if (!Globals.WarStatusTracker.HotBoxTravelling && !Globals.WarStatusTracker.HotBox.Contains(Globals.Sim.CurSystem.Name) && !HasFlashpoint && !Globals.HoldContracts)
                {
                    var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                    Globals.Sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                }

                Globals.HoldContracts = false;
            }
        }

        [HarmonyPatch(typeof(AAR_SalvageScreen), "OnCompleted")]
        public static class AAR_SalvageScreenOnCompletedPatch
        {
            private static void Prefix()
            {
                try
                {
                    if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                        return;

                    Globals.WarStatusTracker.JustArrived = false;
                    Globals.WarStatusTracker.HotBoxTravelling = false;
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
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (Globals.WarStatusTracker != null && Globals.WarStatusTracker.Escalation)
                {
                    if (!Globals.WarStatusTracker.Deployment)
                    {
                        Globals.WarStatusTracker.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric,
                            "Escalation Days Remaining", "Escalation Days Remaining");
                        Globals.WarStatusTracker.EscalationOrder.SetCost(Globals.WarStatusTracker.EscalationDays);
                        __instance.AddEntry(Globals.WarStatusTracker.EscalationOrder, false);
                        __instance.RefreshEntries();
                    }
                    else
                    {
                        Globals.WarStatusTracker.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric,
                            "Escalation Days Remaining", "Forced Deployment Mission");
                        Globals.WarStatusTracker.EscalationOrder.SetCost(Globals.WarStatusTracker.EscalationDays);
                        __instance.AddEntry(Globals.WarStatusTracker.EscalationOrder, false);
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
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return true;

                if (!Globals.WarStatusTracker.JustArrived && (entry.ID.Equals("Escalation Days Remaining")) && entry.GetRemainingCost() != 0)
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
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (!__instance.ActiveTravelContract.IsPriorityContract)
                {
                    if (!Globals.WarStatusTracker.Deployment)
                    {
                        Globals.WarStatusTracker.Escalation = true;
                        Globals.WarStatusTracker.EscalationDays = Globals.Settings.EscalationDays;
                        Globals.WarStatusTracker.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric, "Escalation Days Remaining", "Escalation Days Remaining");
                        Globals.WarStatusTracker.EscalationOrder.SetCost(Globals.WarStatusTracker.EscalationDays);
                        __instance.RoomManager.AddWorkQueueEntry(Globals.WarStatusTracker.EscalationOrder);
                        __instance.RoomManager.SortTimeline();
                        __instance.RoomManager.RefreshTimeline(false);
                    }
                    else
                    {
                        var rand = new Random();
                        Globals.WarStatusTracker.EscalationDays = rand.Next(Globals.Settings.DeploymentMinDays, Globals.Settings.DeploymentMaxDays + 1);
                        if (Globals.WarStatusTracker.EscalationDays < Globals.Settings.DeploymentRerollBound * Globals.WarStatusTracker.EscalationDays ||
                            Globals.WarStatusTracker.EscalationDays > (1 - Globals.Settings.DeploymentRerollBound) * Globals.WarStatusTracker.EscalationDays)
                            Globals.WarStatusTracker.EscalationDays = rand.Next(Globals.Settings.DeploymentMinDays, Globals.Settings.DeploymentMaxDays + 1);

                        Globals.WarStatusTracker.Escalation = true;
                        Globals.WarStatusTracker.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric, "Escalation Days Remaining", "Forced Deployment Mission");
                        Globals.WarStatusTracker.EscalationOrder.SetCost(Globals.WarStatusTracker.EscalationDays);
                        __instance.RoomManager.AddWorkQueueEntry(Globals.WarStatusTracker.EscalationOrder);
                        __instance.RoomManager.SortTimeline();
                        __instance.RoomManager.RefreshTimeline(false);
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
            //        //        if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
            //            return true;

            //        SimGameState Sim = (SimGameState)AccessTools.Property(typeof(SGContractsWidget), "Sim").GetValue(__instance, null);

            //        if (__instance.SelectedContract.Override.contractDisplayStyle == ContractDisplayStyle.BaseCampaignStory)
            //        {
            //            string message = "Commander, this contract will bring us right to the front lines. If we accept it, we will be forced to take missions when our employer needs us to simultaneously attack in support of their war effort. We will be commited to this Deployment until the system is taken or properly defended and will lose significant reputation if we end up backing out before the job is done. But, oh man, they will certainly reward us well if their operation is ultimately successful! This Deployment may require missions to be done without time between them for repairs or to properly rest our pilots. I stronfgety encourage you to only accept this arrangement if you think we're up to it.";
            //            PauseNotification.Show("Deployment", message,
            //                Globals.Sim.GetCrewPortrait(SimGameCrew.Crew_Darius), string.Empty, true, delegate {
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
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = UnityGameInstance.BattleTechGame.Simulation.CurSystem;
                if (Globals.WarStatusTracker.HotBox.Count == 2)
                {
                    Globals.WarStatusTracker.HotBox.RemoveAt(0);
                }
                else
                {
                    Globals.WarStatusTracker.HotBox.Clear();
                }

                Globals.WarStatusTracker.Deployment = false;
                Globals.WarStatusTracker.PirateDeployment = false;
                Globals.WarStatusTracker.DeploymentInfluenceIncrease = 1.0;
                Globals.WarStatusTracker.Escalation = false;
                Globals.WarStatusTracker.EscalationDays = 0;
                var systemStatus = Globals.WarStatusTracker.systems.Find(x => x.name == system.Name);
                RefreshContracts(systemStatus);
                if (Globals.WarStatusTracker.HotBox.Count == 0)
                    Globals.WarStatusTracker.HotBoxTravelling = false;
            }
        }


        [HarmonyPatch(typeof(Contract), "GenerateSalvage")]
        public static class ContractGenerateSalvagePatch
        {
            private static void Postfix(Contract __instance)
            {
                //Log("****Generate Salvage****");
                //Log("Sim Null? " + (Sim == null).ToString());
                //Log("CurSystem Null? " + (Globals.Sim.CurSystem == null).ToString());
                //Log("CurSystem: " + Globals.Sim.CurSystem.Name);
                //Log("WarStatus Null? " + (Globals.WarStatusTracker == null).ToString());
                //Log("WarStatus System Null? " + (null ==Globals.WarStatusTracker.systems.Find(x => x.name == Globals.Sim.CurSystem.Name)).ToString());
                //foreach (SystemStatus systemstatus in Globals.WarStatusTracker.systems)
                //{
                //    Log(systemstatus.name);
                //    Log(systemstatus.starSystem.Name);
                //}

                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = Globals.WarStatusTracker.systems.Find(x => x.starSystem == Globals.Sim.CurSystem);
                if (Globals.WarStatusTracker.HotBox == null)
                    Globals.WarStatusTracker.HotBox = new List<string>();

                if (system.BonusSalvage && Globals.WarStatusTracker.HotBox.Contains(Globals.Sim.CurSystem.Name))
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
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = Globals.WarStatusTracker.systems.Find(x => x.starSystem == Globals.Sim.CurSystem);

                if (system.BonusCBills && Globals.WarStatusTracker.HotBox.Contains(Globals.Sim.CurSystem.Name))
                {
                    var missionObjectiveResultString = $"BONUS FROM ESCALATION: ¢{String.Format("{0:n0}", BonusMoney)}";
                    if (Globals.WarStatusTracker.Deployment)
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
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                var system = Globals.WarStatusTracker.systems.Find(x => x.name == Globals.WarStatusTracker.CurSystem);
                if (system.BonusXP && Globals.WarStatusTracker.HotBox.Contains(system.name))
                {
                    xpEarned = xpEarned + (int) (xpEarned * Globals.Settings.BonusXPFactor);
                    var unspentXP = ___UnitData.pilot.UnspentXP;
                    var XPCorrection = (int) (xpEarned * Globals.Settings.BonusXPFactor);
                    ___UnitData.pilot.StatCollection.Set("ExperienceUnspent", unspentXP + XPCorrection);
                }
            }
        }

        public static void SystemBonuses(StarSystem starSystem)
        {
            var systemStatus = Globals.WarStatusTracker.systems.Find(x => x.starSystem == starSystem);
            int systemDifficulty;
            if (Globals.Settings.ChangeDifficulty)
                systemDifficulty = systemStatus.DifficultyRating;
            else
                systemDifficulty = systemStatus.DifficultyRating + (int) Globals.Sim.GlobalDifficulty;

            if (!Globals.WarStatusTracker.HotBox.Contains(starSystem.Name))
            {
                systemStatus.BonusCBills = false;
                systemStatus.BonusSalvage = false;
                systemStatus.BonusXP = false;

                if (systemDifficulty <= 4)
                {
                    var bonus = Globals.Rng.Next(0, 3);
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
                    var bonus = Globals.Rng.Next(0, 3);
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
            var systemStatus = Globals.WarStatusTracker.systems.Find(x => x.starSystem == Globals.Sim.CurSystem);
            systemStatus.BonusCBills = false;
            systemStatus.BonusSalvage = false;
            systemStatus.BonusXP = false;
            Globals.WarStatusTracker.Deployment = false;
            Globals.WarStatusTracker.PirateDeployment = false;
            Globals.WarStatusTracker.DeploymentInfluenceIncrease = 1.0;
            Globals.WarStatusTracker.HotBox.Remove(systemStatus.name);
            RefreshContracts(systemStatus);
            var hasFlashpoint = false;
            foreach (var contract in Globals.Sim.CurSystem.SystemContracts)
            {
                if (contract.IsFlashpointContract || contract.IsFlashpointCampaignContract)
                    hasFlashpoint = true;
            }

            if (!hasFlashpoint)
            {
                var cmdCenter = Globals.Sim.RoomManager.CmdCenterRoom;
                Globals.Sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
            }
        }

        [HarmonyPatch(typeof(SimGameState), "ContractUserMeetsReputation")]
        public static class SimGameStateContractUserMeetsReputationPatch
        {
            private static void Postfix(ref bool __result)
            {
                if (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                    return;

                if (Globals.WarStatusTracker.Deployment)
                    __result = true;
            }
        }

        public static int ProcessReputation(float FactionRep)
        {
            var simStory = Globals.Sim.Constants.Story;
            var simCareer = Globals.Sim.Constants.CareerMode;
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
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Globals.WarStatusTracker != null && !Globals.WarStatusTracker.StartGameInitialized)
                {
                    ProcessHotSpots();
                    // StarmapMod.SetupRelationPanel();
                    var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                    sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                    Globals.WarStatusTracker.StartGameInitialized = true;
                }

                Globals.HoldContracts = true;
            }
        }

        [HarmonyPatch(typeof(SGContractsWidget), "OnNegotiateClicked")]
        public static class SGContractsWidgetOnNegotiateClickedPatch
        {
            private static bool Prefix(SGContractsWidget __instance)
            {
                try
                {
                    if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                        return true;

                    if (__instance.SelectedContract.Override.contractDisplayStyle == ContractDisplayStyle.BaseCampaignStory)
                    {
                        var message = "Commander, this contract will bring us right to the front lines. If we accept it, we will be forced to take missions when our employer needs us to simultaneously attack in support of their war effort. We will be committed to this Deployment until the system is taken or properly defended and will lose significant reputation if we end up backing out before the job is done. But, oh man, they will certainly reward us well if their operation is ultimately successful! This Deployment may require missions to be done without time between them for repairs or to properly rest our pilots. I strongly encourage you to only accept this arrangement if you think we're up to it.";
                        PauseNotification.Show("Deployment", message,
                            Globals.Sim.GetCrewPortrait(SimGameCrew.Crew_Darius), string.Empty, true, delegate { __instance.NegotiateContract(__instance.SelectedContract); }, "Do it anyways", null, "Cancel");
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
                    if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                        return true;

                    if (!Globals.Settings.ResetMap && Globals.WarStatusTracker.Deployment && !Globals.WarStatusTracker.HotBoxTravelling && Globals.WarStatusTracker.EscalationDays <= 0)
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
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
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
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                __state = Globals.Sim.Constants.Story.ContractSuccessReduction;

                var HasFlashpoint = false;
                foreach (var contract in Globals.Sim.CurSystem.SystemContracts)
                {
                    if (contract.IsFlashpointContract || contract.IsFlashpointCampaignContract)
                        HasFlashpoint = true;
                }

                if (Globals.WarStatusTracker.HotBox.Contains(Globals.Sim.CurSystem.Name))
                { 
                    Globals.Sim.Constants.Story.ContractSuccessReduction = 100;
                    Globals.WarStatusTracker.DeploymentInfluenceIncrease *= Globals.Settings.DeploymentEscalationFactor;
                    if (!HasFlashpoint)
                    {
                        Globals.Sim.CurSystem.activeSystemContracts.Clear();
                        Globals.Sim.CurSystem.activeSystemBreadcrumbs.Clear();
                    }

                    if (Globals.WarStatusTracker.EscalationOrder != null)
                    {
                        Globals.WarStatusTracker.EscalationOrder.SetCost(0);
                        var ActiveItems = Globals.TaskTimelineWidget.ActiveItems;
                        if (ActiveItems.TryGetValue(Globals.WarStatusTracker.EscalationOrder, out var taskManagementElement))
                        {
                            taskManagementElement.UpdateItem(0);
                        }
                    }

                    Globals.WarStatusTracker.Escalation = true;
                    var rand = new Random();
                    Globals.WarStatusTracker.EscalationDays = rand.Next(Globals.Settings.DeploymentMinDays, Globals.Settings.DeploymentMaxDays + 1);
                    if (Globals.WarStatusTracker.EscalationDays < Globals.Settings.DeploymentRerollBound * Globals.WarStatusTracker.EscalationDays ||
                        Globals.WarStatusTracker.EscalationDays > (1 - Globals.Settings.DeploymentRerollBound) * Globals.WarStatusTracker.EscalationDays)
                        Globals.WarStatusTracker.EscalationDays = rand.Next(Globals.Settings.DeploymentMinDays, Globals.Settings.DeploymentMaxDays + 1);

                    Globals.WarStatusTracker.EscalationOrder = new WorkOrderEntry_Notification(WorkOrderType.NotificationGeneric, "Escalation Days Remaining", "Forced Deployment Mission");
                    Globals.WarStatusTracker.EscalationOrder.SetCost(Globals.WarStatusTracker.EscalationDays);
                    Globals.Sim.RoomManager.AddWorkQueueEntry(Globals.WarStatusTracker.EscalationOrder);
                    Globals.Sim.RoomManager.SortTimeline();
                    Globals.Sim.RoomManager.RefreshTimeline(false);
                }
            }

            public static void Postfix(StarSystem __instance, ref float __state)
            {
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                Globals.Sim.Constants.Story.ContractSuccessReduction = __state;
            }
        }
    }
}

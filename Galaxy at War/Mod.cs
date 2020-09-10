using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.Framework;
using BattleTech.UI;
using Harmony;
using UnityEngine;
using static GalaxyatWar.Logger;
using static GalaxyatWar.Globals;
using static GalaxyatWar.Helpers;
using MissionResult = BattleTech.MissionResult;

// ReSharper disable UnusedMember.Global
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Local  
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

namespace GalaxyatWar
{
    public static class Mod
    {
        //Remove duplicates in the ContractEmployerIDList
        [HarmonyPatch(typeof(SimGameState), "GetValidParticipants")]
        public static class SimGameStateGetValidParticipantsPatch
        {
            public static void Prefix(ref StarSystem system)
            {
                LogDebug("GetValidParticipants");
                system.Def.contractEmployerIDs = system.Def.contractEmployerIDs.Distinct().ToList();
                LogDebug("Contract employers before changes:");
                system.Def.contractEmployerIDs.Do(x => LogDebug($"  {x}"));
            }
        }

        [HarmonyPatch(typeof(SimGameState), "GenerateContractParticipants")]
        public static class SimGameStateGenerateContractParticipantsPatch
        {
            public static void Prefix(FactionDef employer, StarSystemDef system, ref string[] __state)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (system.Tags.Contains("planet_region_hyadesrim") && (system.ownerID == "NoFaction" || system.ownerID == "Locals"))
                    return;

                LogDebug("");
                LogDebug($"GenerateContractParticipants for {employer.Name}");
                var contractTargetIDs = system.contractTargetIDs;
                LogDebug("Existing contractTargetIDs:");
                contractTargetIDs.Do(x => LogDebug($"  {x}"));
                LogDebug("Faction enemies:");
                employer.Enemies.Do(x => LogDebug($"  {x}"));
                var newFactionEnemies = new List<string>(employer.Enemies.ToList());
                foreach (var enemy in contractTargetIDs)
                {
                    if (enemy != employer.FactionValue.Name &&
                        !newFactionEnemies.Contains(enemy) &&
                        !employer.Allies.Contains(enemy) &&
                        !Settings.ImmuneToWar.Contains(enemy))
                    {
                        LogDebug($"Adding new enemy: {enemy}");
                        newFactionEnemies.Add(enemy);
                    }
                }

                foreach (var enemy in Settings.DefensiveFactions.Except(Settings.ImmuneToWar))
                {
                    if (enemy != employer.FactionValue.Name &&
                        !newFactionEnemies.Contains(enemy))
                    {
                        LogDebug($"Adding new enemy: {enemy}");
                        newFactionEnemies.Add(enemy);
                    }
                }

                if (Settings.GaW_PoliceSupport &&
                    system.OwnerValue.Name == WarStatusTracker.ComstarAlly &&
                    employer.Name != WarStatusTracker.ComstarAlly)
                {
                    LogDebug($"Adding new enemy: {Settings.GaW_Police}");
                    newFactionEnemies.Add(Settings.GaW_Police);
                }

                if (Settings.GaW_PoliceSupport &&
                    employer.Name == Settings.GaW_Police &&
                    newFactionEnemies.Contains(WarStatusTracker.ComstarAlly))
                {
                    LogDebug($"Removing enemy (Comstar ally): {WarStatusTracker.ComstarAlly}");
                    newFactionEnemies.Remove(WarStatusTracker.ComstarAlly);
                }

                var array = newFactionEnemies.ToArray();
                employer.Enemies = __state = array;
                if (employer.Enemies == array)
                {
                    LogDebug("No changes.");
                }
                else
                {
                    LogDebug("Adjusted enemies list: ");
                    __state.Do(x => LogDebug($"  {x}"));
                }
            }

            public static void Postfix(FactionDef employer, ref WeightedList<SimGameState.ContractParticipants> __result, string[] __state)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                employer.Enemies = __state;
                var type = __result.Type;
                __result = __result.Distinct().ToWeightedList(type);
            }
        }

        [HarmonyPatch(typeof(SGFactionRelationshipDisplay), "DisplayEnemiesOfFaction")]
        public static class SGFactionRelationShipDisplayDisplayEnemiesOfFactionPatch
        {
            public static void Prefix(FactionValue theFaction)
            {
                LogDebug("DisplayEnemiesOfFaction");
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (WarStatusTracker.deathListTracker.Find(x => x.faction == theFaction.Name) == null)
                    return;

                var deathListTracker = WarStatusTracker.deathListTracker.Find(x => x.faction == theFaction.Name);
                AdjustDeathList(deathListTracker, true);
            }
        }

        [HarmonyPatch(typeof(SGFactionRelationshipDisplay), "DisplayAlliesOfFaction")]
        public static class SGFactionRelationShipDisplayDisplayAlliesOfFactionPatch
        {
            public static void Prefix(string theFactionID)
            {
                LogDebug("DisplayAlliesOfFaction");
                if (WarStatusTracker == null || Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete"))
                    return;

                if (WarStatusTracker.deathListTracker.Find(x => x.faction == theFactionID) == null)
                    return;

                var deathListTracker = WarStatusTracker.deathListTracker.Find(x => x.faction == theFactionID);
                AdjustDeathList(deathListTracker, true);
            }
        }

        [HarmonyPatch(typeof(SGCaptainsQuartersReputationScreen), "Init", typeof(SimGameState))]
        public static class SGCaptainsQuartersReputationScreenInitPatch
        {
            public static void Prefix()
            {
                if (WarStatusTracker == null || Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete"))
                    return;

                LogDebug("");
                LogDebug("SGCaptainsQuartersReputationScreen.Init");
                foreach (var faction in IncludedFactions)
                {
                    if (WarStatusTracker.deathListTracker.Find(x => x.faction == faction) == null)
                        continue;

                    var deathListTracker = WarStatusTracker.deathListTracker.Find(x => x.faction == faction);
                    LogDebug($"{deathListTracker.faction}'s deathListTracker:");
                    LogDebug("Allies currently:");
                    deathListTracker.Allies.Do(x => LogDebug($"  {x}"));
                    LogDebug("Enemies currently:");
                    deathListTracker.Enemies.Do(x => LogDebug($"  {x}"));
                    AdjustDeathList(deathListTracker, true);
                    LogDebug("Allies after:");
                    deathListTracker.Allies.Do(x => LogDebug($"  {x}"));
                    LogDebug($"Enemies after:");
                    deathListTracker.Enemies.Do(x => LogDebug($"  {x}"));
                }
            }
        }

        [HarmonyPatch(typeof(SGCaptainsQuartersReputationScreen), "RefreshWidgets")]
        public static class SGCaptainsQuartersReputationScreenRefreshWidgetsPatch
        {
            public static void Prefix()
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                LogDebug("RefreshWidgets");
                foreach (var faction in IncludedFactions)
                {
                    if (WarStatusTracker.deathListTracker.Find(x => x.faction == faction) == null)
                        continue;

                    var deathListTracker = WarStatusTracker.deathListTracker.Find(x => x.faction == faction);
                    AdjustDeathList(deathListTracker, true);
                }
            }
        }

        [HarmonyBefore("com.DropCostPerMech", "de.morphyum.DropCostPerMech")]
        [HarmonyPatch(typeof(Contract), "CompleteContract")]
        public static class CompleteContractPatch
        {
            public static void Postfix(Contract __instance, MissionResult result, bool isGoodFaithEffort)
            {
                try
                {
                    if (WarStatusTracker == null || Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete"))
                        return;

                    var system = WarStatusTracker.systems.Find(x => x.name == Sim.CurSystem.Name);
                    if (system.BonusCBills && WarStatusTracker.HotBox.Contains(Sim.CurSystem.Name))
                    {
                        HotSpots.BonusMoney = (int) (__instance.MoneyResults * Settings.BonusCbillsFactor);
                        var newMoneyResults = Mathf.FloorToInt(__instance.MoneyResults + HotSpots.BonusMoney);
                        __instance.MoneyResults = newMoneyResults;
                    }

                    // g - dead code?
                    //TeamFaction = __instance.GetTeamFaction("ecc8d4f2-74b4-465d-adf6-84445e5dfc230").Name;
                    //if (Settings.GaW_PoliceSupport && TeamFaction == Settings.GaW_Police)
                    //    TeamFaction = WarStatusTracker.ComstarAlly;
                    //EnemyFaction = __instance.GetTeamFaction("be77cadd-e245-4240-a93e-b99cc98902a5").Name;
                    //if (Settings.GaW_PoliceSupport && EnemyFaction == Settings.GaW_Police)
                    //    EnemyFaction = WarStatusTracker.ComstarAlly;
                    Difficulty = __instance.Difficulty;
                    Globals.MissionResult = result;
                    Globals.ContractType = __instance.Override.ContractTypeValue.Name;
                    if (__instance.IsFlashpointContract || __instance.IsFlashpointCampaignContract)
                        IsFlashpointContract = true;
                    else
                        IsFlashpointContract = false;
                }
                catch (Exception ex)
                {
                    Log(ex.ToString());
                }
            }

            [HarmonyPatch(typeof(SimGameState), "ResolveCompleteContract")]
            public static class SimGameStateResolveCompleteContractPatch
            {
                public static void Postfix(SimGameState __instance)
                {
                    if (WarStatusTracker == null || Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete"))
                        return;

                    if (IsFlashpointContract)
                        return;

                    var warSystem = WarStatusTracker.systems.Find(x => x.name == __instance.CurSystem.Name);

                    if (WarStatusTracker.FlashpointSystems.Contains(warSystem.name))
                        return;

                    if (Globals.MissionResult == MissionResult.Victory)
                    {
                        double deltaInfluence;
                        if (TeamFaction == "AuriganPirates")
                        {
                            deltaInfluence = DeltaInfluence(__instance.CurSystem, Difficulty, Globals.ContractType, EnemyFaction, true);
                            warSystem.PirateActivity += (float) deltaInfluence;
                        }
                        else if (EnemyFaction == "AuriganPirates")
                        {
                            deltaInfluence = DeltaInfluence(__instance.CurSystem, Difficulty, Globals.ContractType, EnemyFaction, true);
                            warSystem.PirateActivity -= (float) deltaInfluence;
                            if (WarStatusTracker.Deployment)
                                WarStatusTracker.PirateDeployment = true;
                        }
                        else
                        {
                            deltaInfluence = DeltaInfluence(__instance.CurSystem, Difficulty, Globals.ContractType, EnemyFaction, false);
                            if (!InfluenceMaxed)
                            {
                                warSystem.influenceTracker[TeamFaction] += (float) deltaInfluence;
                                warSystem.influenceTracker[EnemyFaction] -= (float) deltaInfluence;
                            }
                            else
                            {
                                warSystem.influenceTracker[TeamFaction] += (float) Math.Min(AttackerInfluenceHolder, 100 - warSystem.influenceTracker[TeamFaction]);
                                warSystem.influenceTracker[EnemyFaction] -= (float) deltaInfluence;
                            }
                        }

                        //if (contractType == ContractType.AttackDefend || contractType == ContractType.FireMission)
                        //{
                        //    if (Settings.IncludedFactions.Contains(teamfaction))
                        //    {
                        //        if (!Settings.DefensiveFactions.Contains(teamfaction))
                        //            WarStatusTracker.warFactionTracker.Find(x => x.faction == teamfaction).AttackResources += difficulty;
                        //        else
                        //            WarStatusTracker.warFactionTracker.Find(x => x.faction == teamfaction).DefensiveResources += difficulty;
                        //    }

                        //    if (Settings.IncludedFactions.Contains(enemyfaction))
                        //    {
                        //        WarStatusTracker.warFactionTracker.Find(x => x.faction == enemyfaction).DefensiveResources -= difficulty;
                        //        if (WarStatusTracker.warFactionTracker.Find(x => x.faction == enemyfaction).DefensiveResources < 0)
                        //            WarStatusTracker.warFactionTracker.Find(x => x.faction == enemyfaction).DefensiveResources = 0;
                        //    }
                        //    else if (enemyfaction == "AuriganPirates")
                        //    {
                        //        warsystem.PirateActivity -= difficulty;
                        //        if (warsystem.PirateActivity < 0)
                        //            warsystem.PirateActivity = 0;
                        //    }
                        //}

                        var oldOwner = Sim.CurSystem.OwnerValue.Name;
                        if (WillSystemFlip(__instance.CurSystem, TeamFaction, EnemyFaction, deltaInfluence, false) ||
                            (WarStatusTracker.Deployment && EnemyFaction == "AuriganPirates" && warSystem.PirateActivity < 1))
                        {
                            if (WarStatusTracker.Deployment && EnemyFaction == "AuriganPirates" && warSystem.PirateActivity < 1)
                            {
                                LogDebug($"ComStar Bulletin: Galaxy at War {__instance.CurSystem.Name} defended from Pirates!");
                                Globals.SimGameInterruptManager.QueueGenericPopup_NonImmediate("ComStar Bulletin: Galaxy at War", $"{__instance.CurSystem.Name} defended from Pirates! ", true, null);
                            }
                            else
                            {
                                LogDebug($"ComStar Bulletin: Galaxy at War {__instance.CurSystem.Name} taken!  {Settings.FactionNames[TeamFaction]} conquered from {Settings.FactionNames[oldOwner]}");
                                ChangeSystemOwnership(warSystem.starSystem, TeamFaction, false);
                                Globals.SimGameInterruptManager.QueueGenericPopup_NonImmediate(
                                    $"ComStar Bulletin: Galaxy at War {__instance.CurSystem.Name} taken!", $"{Settings.FactionNames[TeamFaction]} conquered from {Settings.FactionNames[oldOwner]}", true, null);
                                if (Settings.HyadesRimCompatible && WarStatusTracker.InactiveTHRFactions.Contains(TeamFaction))
                                    WarStatusTracker.InactiveTHRFactions.Remove(TeamFaction);
                            }

                            if (WarStatusTracker.HotBox.Contains(Sim.CurSystem.Name))
                            {
                                LogDebug($"HotSpot: {Sim.CurSystem.Name}");
                                if (WarStatusTracker.Deployment)
                                {
                                    LogDebug($"Deployment, difficulty: {warSystem.DeploymentTier}");
                                    var difficultyScale = warSystem.DeploymentTier;
                                    if (difficultyScale == 6)
                                        Sim.InterruptQueue.QueueRewardsPopup(Settings.DeploymentReward_06);
                                    else if (difficultyScale == 5)
                                        Sim.InterruptQueue.QueueRewardsPopup(Settings.DeploymentReward_05);
                                    else if (difficultyScale == 4)
                                        Sim.InterruptQueue.QueueRewardsPopup(Settings.DeploymentReward_04);
                                    else if (difficultyScale == 3)
                                        Sim.InterruptQueue.QueueRewardsPopup(Settings.DeploymentReward_03);
                                    else if (difficultyScale == 2)
                                        Sim.InterruptQueue.QueueRewardsPopup(Settings.DeploymentReward_02);
                                    else
                                        Sim.InterruptQueue.QueueRewardsPopup(Settings.DeploymentReward_01);
                                }

                                WarStatusTracker.JustArrived = false;
                                WarStatusTracker.HotBoxTravelling = false;
                                WarStatusTracker.Escalation = false;
                                WarStatusTracker.HotBox.Clear();
                                WarStatusTracker.EscalationDays = 0;
                                warSystem.BonusCBills = false;
                                warSystem.BonusSalvage = false;
                                warSystem.BonusXP = false;
                                WarStatusTracker.Deployment = false;
                                WarStatusTracker.DeploymentInfluenceIncrease = 1.0;
                                WarStatusTracker.PirateDeployment = false;
                                if (WarStatusTracker.EscalationOrder != null)
                                {
                                    LogDebug("There is an escalation order.");
                                    WarStatusTracker.EscalationOrder.SetCost(0);
                                    var ActiveItems = Globals.TaskTimelineWidget.ActiveItems;
                                    if (ActiveItems.TryGetValue(WarStatusTracker.EscalationOrder, out var taskManagementElement4))
                                    {
                                        LogDebug($"{taskManagementElement4.Entry.Description} has been updated.");
                                        taskManagementElement4.UpdateItem(0);
                                    }
                                }
                            }

                            foreach (var system in WarStatusTracker.SystemChangedOwners)
                            {
                                var systemStatus = WarStatusTracker.systems.Find(x => x.name == system);
                                systemStatus.CurrentlyAttackedBy.Clear();
                                CalculateAttackAndDefenseTargets(systemStatus.starSystem);
                                LogDebug("Refreshing contracts.");
                                RefreshContracts(systemStatus);
                            }

                            WarStatusTracker.SystemChangedOwners.Clear();

                            var HasFlashpoint = false;
                            foreach (var contract in __instance.CurSystem.SystemContracts)
                            {
                                if (contract.IsFlashpointContract || contract.IsFlashpointCampaignContract)
                                {
                                    LogDebug($"{contract.Name} is a Flashpoint.");
                                    HasFlashpoint = true;
                                }
                            }

                            if (!HasFlashpoint)
                            {
                                var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                                __instance.CurSystem.GenerateInitialContracts(() => cmdCenter.OnContractsFetched());
                            }

                            __instance.StopPlayMode();
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(SGContractsWidget), "GetContractComparePriority")]
        public static class SGContractsWidgetGetContractComparePriorityPatch
        {
            private static bool Prefix(ref int __result, Contract contract)
            {
                if (WarStatusTracker == null || Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete"))
                    return true;

                var difficulty = contract.Override.GetUIDifficulty();
                int result;
                if (Sim.ContractUserMeetsReputation(contract))
                {
                    if (contract.IsFlashpointContract || contract.IsFlashpointCampaignContract)
                        result = 0;
                    else if (contract.Override.contractDisplayStyle == ContractDisplayStyle.BaseCampaignRestoration)
                        result = 1;
                    else if (contract.Override.contractDisplayStyle == ContractDisplayStyle.BaseCampaignStory)
                        result = difficulty + 11;
                    else if (contract.TargetSystem == Sim.CurSystem.ID)
                        result = difficulty + 1;
                    else
                    {
                        result = difficulty + 21;
                    }
                }
                else
                {
                    result = difficulty + 31;
                }

                __result = result;
                LogDebug($"GetContractComparePriority set to {__result, -3}Is Flashpoint? {contract.IsFlashpointContract}.  Is Campaign Flashpoint? {contract.IsFlashpointCampaignContract}");
                return false;
            }
        }

        //Show on the Contract Description how this will impact the war. 
        [HarmonyPatch(typeof(SGContractsWidget), "PopulateContract")]
        public static class SGContractsWidgetPopulateContractPatch
        {
            public static void Prefix(ref Contract contract, ref string __state)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                LogDebug($"Contract: {contract.Name}.");
                if (contract.IsFlashpointContract || contract.IsFlashpointCampaignContract)
                {
                    LogDebug("is Flashpoint, skipping.");
                    return;
                }

                __state = contract.Override.shortDescription;
                var StringHolder = contract.Override.shortDescription;
                var EmployerFaction = contract.GetTeamFaction("ecc8d4f2-74b4-465d-adf6-84445e5dfc230").Name;
                if (Settings.GaW_PoliceSupport && EmployerFaction == Settings.GaW_Police)
                    EmployerFaction = WarStatusTracker.ComstarAlly;
                var DefenseFaction = contract.GetTeamFaction("be77cadd-e245-4240-a93e-b99cc98902a5").Name;
                if (Settings.GaW_PoliceSupport && DefenseFaction == Settings.GaW_Police)
                    DefenseFaction = WarStatusTracker.ComstarAlly;
                var TargetSystem = contract.TargetSystem;
                var SystemName = Sim.StarSystems.Find(x => x.ID == TargetSystem);
                bool pirates = EmployerFaction == "AuriganPirates" || DefenseFaction == "AuriganPirates";
                var DeltaInfluence = Helpers.DeltaInfluence(SystemName, contract.Difficulty, contract.Override.ContractTypeValue.Name, DefenseFaction, pirates);
                var SystemFlip = false;
                if (EmployerFaction != "AuriganPirates" && DefenseFaction != "AuriganPirates")
                {
                    SystemFlip = WillSystemFlip(SystemName, EmployerFaction, DefenseFaction, DeltaInfluence, true);
                    LogDebug($"System {SystemName.Name} will flip.");
                }

                var AttackerString = Settings.FactionNames[EmployerFaction] + ": +" + DeltaInfluence;
                var DefenderString = Settings.FactionNames[DefenseFaction] + ": -" + DeltaInfluence;

                if (EmployerFaction != "AuriganPirates" && DefenseFaction != "AuriganPirates")
                {
                    if (!SystemFlip)
                        StringHolder = "<b>Impact on System Conflict:</b>\n   " + AttackerString + "; " + DefenderString;
                    else
                        StringHolder = "<b>***SYSTEM WILL CHANGE OWNERS*** Impact on System Conflict:</b>\n   " + AttackerString + "; " + DefenderString;
                }
                else if (EmployerFaction == "AuriganPirates")
                    StringHolder = "<b>Impact on Pirate Activity:</b>\n   " + AttackerString;
                else if (DefenseFaction == "AuriganPirates")
                    StringHolder = "<b>Impact on Pirate Activity:</b>\n   " + DefenderString;

                var system = WarStatusTracker.systems.Find(x => x.starSystem == SystemName);

                if (system.BonusCBills || system.BonusSalvage || system.BonusXP)
                {
                    StringHolder = StringHolder + "\n<b>Escalation Bonuses:</b> ";
                    if (system.BonusCBills)
                        StringHolder = StringHolder + "+C-Bills ";
                    if (system.BonusSalvage)
                        StringHolder = StringHolder + "+Salvage ";
                    if (system.BonusXP)
                        StringHolder = StringHolder + "+XP";
                }

                if (contract.Override.contractDisplayStyle == ContractDisplayStyle.BaseCampaignStory)
                {
                    var estimatedMissions = CalculateFlipMissions(EmployerFaction, SystemName);
                    int totalDifficulty;

                    if (Settings.ChangeDifficulty)
                        totalDifficulty = estimatedMissions * SystemName.Def.GetDifficulty(SimGameState.SimGameType.CAREER);
                    else
                        totalDifficulty = estimatedMissions * (int) (SystemName.Def.DefaultDifficulty + Sim.GlobalDifficulty);

                    if (totalDifficulty >= 150)
                        system.DeploymentTier = 6;
                    else if (totalDifficulty >= 100)
                        system.DeploymentTier = 5;
                    else if (totalDifficulty >= 75)
                        system.DeploymentTier = 4;
                    else if (totalDifficulty >= 50)
                        system.DeploymentTier = 3;
                    else if (totalDifficulty >= 25)
                        system.DeploymentTier = 2;
                    else
                        system.DeploymentTier = 1;

                    StringHolder = StringHolder + "\n<b>Estimated Missions to Wrest Control of System:</b> " + estimatedMissions;
                    StringHolder = StringHolder + "\n   Deployment Reward: Tier " + system.DeploymentTier;
                }

                StringHolder = StringHolder + "\n\n" + __state;
                contract.Override.shortDescription = StringHolder;
            }

            public static void Postfix(ref Contract contract, ref string __state)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                contract.Override.shortDescription = __state;
            }
        }

        [HarmonyPatch(typeof(ListElementController_InventoryWeapon_NotListView), "RefreshQuantity")]
        public static class Bug_Tracing_Fix
        {
            static bool Prefix(ListElementController_InventoryWeapon_NotListView __instance, InventoryItemElement_NotListView theWidget)
            {
                try
                {
                    if (__instance.quantity == -2147483648)
                    {
                        theWidget.qtyElement.SetActive(false);
                        return false;
                    }

                    theWidget.qtyElement.SetActive(true);
                    theWidget.quantityValue.SetText("{0}", __instance.quantity);
                    theWidget.quantityValueColor.SetUIColor((__instance.quantity > 0 || __instance.quantity == int.MinValue) ? UIColor.White : UIColor.Red);
                    return false;
                }
                catch (Exception e)
                {
                    Log("*****Exception thrown with ListElementController_InventoryWeapon_NotListView");
                    Log($"theWidget null: {theWidget == null}");
                    Log($"theWidget.qtyElement null: {theWidget.qtyElement == null}");
                    Log($"theWidget.quantityValue null: {theWidget.quantityValue == null}");
                    Log($"theWidget.quantityValueColor null: {theWidget.quantityValueColor == null}");
                    if (theWidget.itemName != null)
                    {
                        Log("theWidget.itemName");
                        Log(theWidget.itemName.ToString());
                    }

                    if (__instance.GetName() != null)
                    {
                        Log("__instance.GetName");
                        Log(__instance.GetName());
                    }

                    Error(e);
                    return false;
                }
            }
        }

        [HarmonyPatch(typeof(DesignResult), "Trigger")]
        public static class Temporary_Bug_Fix
        {
            static bool Prefix()
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(SGRoomManager), "OnSimGameInitialize")]
        public class SGRoomManagerOnSimGameInitializedPatch
        {
            private static void Postfix(TaskTimelineWidget ___timelineWidget)
            {
                Globals.TaskTimelineWidget = ___timelineWidget;
            }
        }

        //internal static void WarSummary(string eventString)
        //{
        //    var simGame = UnityGameInstance.BattleTechGame.Simulation;
        //    var eventDef = new SimGameEventDef(
        //            SimGameEventDef.EventPublishState.PUBLISHED,
        //            SimGameEventDef.SimEventType.UNSELECTABLE,
        //            EventScope.Company,
        //            new DescriptionDef(
        //                "SalvageOperationsEventID",
        //                "Salvage Operations",
        //                eventString,
        //                "uixTxrSpot_YangWorking.png",
        //                0, 0, false, "", "", ""),
        //            new RequirementDef { Scope = EventScope.Company },
        //            new RequirementDef[0],
        //            new SimGameEventObject[0],
        //            null, 1, false);


        //    var eventTracker = new SimGameEventTracker();
        //    eventTracker.Init(new[] { EventScope.Company }, 0, 0, SimGameEventDef.SimEventType.NORMAL, simGame);
        //    simGame.InterruptQueue.QueueEventPopup(eventDef, EventScope.Company, eventTracker);


        //}
    }
}

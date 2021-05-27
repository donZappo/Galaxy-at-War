using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.Framework;
using BattleTech.UI;
using Harmony;
using UnityEngine;
using static GalaxyatWar.Logger;
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
        // this belongs in a different mod
        [HarmonyPatch(typeof(Shop), "OnItemCollectionRetrieved")]
        public class ShopOnItemCollectionRetrievedHackFixPatch
        {
            public static bool Prefix(Shop __instance, ItemCollectionDef def)
            {
                if (def == null)
                {
                    Log(__instance.system.Name);
                }

                return def != null;
            }
        }
        
        //Remove duplicates in the ContractEmployerIDList
        [HarmonyPatch(typeof(SimGameState), "GetValidParticipants")]
        public static class SimGameStateGetValidParticipantsPatch
        {
            public static void Prefix(ref StarSystem system)
            {
                system.Def.contractEmployerIDs = system.Def.contractEmployerIDs.Distinct().ToList();
                LogDebug("Contract employers:");
                system.Def.contractEmployerIDs.Do(x => LogDebug($"  {x}"));
            }
        }
        
        [HarmonyPatch(typeof(SimGameState), "GenerateContractParticipants")]
        public static class SimGameStateGenerateContractParticipantsPatch
        {
            public static void Prefix(FactionDef employer, StarSystemDef system, ref string[] __state)
            {
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (system.Tags.Contains("planet_region_hyadesrim") && (system.ownerID == "NoFaction" || system.ownerID == "Locals"))
                    return;

                LogDebug($"GenerateContractParticipants for {employer.Name} in {system.Description.Name}");
                var contractTargetIDs = system.contractTargetIDs;
                var newFactionEnemies = new List<string>(employer.Enemies.ToList());
                foreach (var enemy in contractTargetIDs)
                {
                    if (enemy != employer.FactionValue.Name &&
                        !newFactionEnemies.Contains(enemy) &&
                        !employer.Allies.Contains(enemy) &&
                        !Globals.Settings.ImmuneToWar.Contains(enemy))
                    {
                        //LogDebug($"Adding new enemy: {enemy}");
                        newFactionEnemies.Add(enemy);
                    }
                }

                foreach (var enemy in Globals.Settings.DefensiveFactions.Except(Globals.Settings.ImmuneToWar))
                {
                    if (enemy != employer.FactionValue.Name &&
                        !newFactionEnemies.Contains(enemy))
                    {
                        //LogDebug($"Adding new enemy: {enemy}");
                        newFactionEnemies.Add(enemy);
                    }
                }

                if (Globals.Settings.GaW_PoliceSupport &&
                    system.OwnerValue.Name == Globals.WarStatusTracker.ComstarAlly &&
                    employer.Name != Globals.WarStatusTracker.ComstarAlly)
                {
                    //LogDebug($"Adding new enemy: {Globals.Settings.GaW_Police}");
                    newFactionEnemies.Add(Globals.Settings.GaW_Police);
                }

                if (Globals.Settings.GaW_PoliceSupport &&
                    employer.Name == Globals.Settings.GaW_Police &&
                    newFactionEnemies.Contains(Globals.WarStatusTracker.ComstarAlly))
                {
                    //LogDebug($"Removing enemy (Comstar ally): {Globals.WarStatusTracker.ComstarAlly}");
                    newFactionEnemies.Remove(Globals.WarStatusTracker.ComstarAlly);
                }

                if (Globals.Settings.GaW_PoliceSupport &&
                    employer.Name == Globals.WarStatusTracker.ComstarAlly &&
                    newFactionEnemies.Contains(Globals.Settings.GaW_Police))
                {
                    //LogDebug($"Removing enemy (Comstar ally): {Globals.WarStatusTracker.ComstarAlly}");
                    newFactionEnemies.RemoveAll(x => x == Globals.Settings.GaW_Police);
                }

                var array = newFactionEnemies.ToArray();
                if (employer.Enemies == array)
                {
                    //LogDebug("No changes to enemies.");
                }
                else
                {
                    //LogDebug("Adjusted enemies list: ");
                    employer.Enemies = __state = array;
                    //__state.Do(x => LogDebug($"  {x}"));
                }
            }

            public static void Postfix(FactionDef employer, ref WeightedList<SimGameState.ContractParticipants> __result, string[] __state)
            {
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
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
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == theFaction.Name) == null)
                    return;

                var deathListTracker = Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == theFaction.Name);
                AdjustDeathList(deathListTracker, true);
            }
        }

        [HarmonyPatch(typeof(SGFactionRelationshipDisplay), "DisplayAlliesOfFaction")]
        public static class SGFactionRelationShipDisplayDisplayAlliesOfFactionPatch
        {
            public static void Prefix(string theFactionID)
            {
                LogDebug("DisplayAlliesOfFaction");
                if (Globals.WarStatusTracker == null || Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                    return;

                if (Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == theFactionID) == null)
                    return;

                var deathListTracker = Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == theFactionID);
                AdjustDeathList(deathListTracker, true);
            }
        }

        [HarmonyPatch(typeof(SGCaptainsQuartersReputationScreen), "Init", typeof(SimGameState))]
        public static class SGCaptainsQuartersReputationScreenInitPatch
        {
            public static void Prefix()
            {
                if (Globals.WarStatusTracker == null || Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                    return;

                foreach (var faction in Globals.IncludedFactions)
                {
                    if (Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == faction) == null)
                        continue;

                    var deathListTracker = Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == faction);
                    LogDebug($"{deathListTracker.faction}'s deathListTracker:");
                    //LogDebug("Allies currently:");
                    deathListTracker.Allies.Do(x => LogDebug($"  {x}"));
                    //LogDebug("Enemies currently:");
                    deathListTracker.Enemies.Do(x => LogDebug($"  {x}"));
                    AdjustDeathList(deathListTracker, true);
                    //LogDebug("Allies after:");
                    deathListTracker.Allies.Do(x => LogDebug($"  {x}"));
                    //LogDebug("Enemies after:");
                    deathListTracker.Enemies.Do(x => LogDebug($"  {x}"));
                }
            }
        }

        [HarmonyPatch(typeof(SGCaptainsQuartersReputationScreen), "RefreshWidgets")]
        public static class SGCaptainsQuartersReputationScreenRefreshWidgetsPatch
        {
            public static void Prefix()
            {
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
                    return;

                foreach (var faction in Globals.IncludedFactions)
                {
                    if (Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == faction) == null)
                        continue;

                    var deathListTracker = Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == faction);
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
                    if (Globals.WarStatusTracker == null || Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                        return;

                    var system = Globals.WarStatusTracker.systems.Find(x => x.name == Globals.Sim.CurSystem.Name);
                    if (system.BonusCBills && Globals.WarStatusTracker.HotBox.Contains(Globals.Sim.CurSystem.Name))
                    {
                        HotSpots.BonusMoney = (int) (__instance.MoneyResults * Globals.Settings.BonusCbillsFactor);
                        var newMoneyResults = Mathf.FloorToInt(__instance.MoneyResults + HotSpots.BonusMoney);
                        __instance.MoneyResults = newMoneyResults;
                    }
                    
                    Globals.TeamFaction = __instance.GetTeamFaction("ecc8d4f2-74b4-465d-adf6-84445e5dfc230").Name;
                    if (Globals.Settings.GaW_PoliceSupport && Globals.TeamFaction == Globals.Settings.GaW_Police)
                        Globals.TeamFaction = Globals.WarStatusTracker.ComstarAlly;
                    Globals.EnemyFaction = __instance.GetTeamFaction("be77cadd-e245-4240-a93e-b99cc98902a5").Name;
                    if (Globals.Settings.GaW_PoliceSupport && Globals.EnemyFaction == Globals.Settings.GaW_Police)
                        Globals.EnemyFaction = Globals.WarStatusTracker.ComstarAlly;
                    Globals.Difficulty = __instance.Difficulty;
                    Globals.MissionResult = result;
                    Globals.ContractType = __instance.Override.ContractTypeValue.Name;
                    if (__instance.IsFlashpointContract || __instance.IsFlashpointCampaignContract)
                        Globals.IsFlashpointContract = true;
                    else
                        Globals.IsFlashpointContract = false;
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
                    if (Globals.WarStatusTracker == null || Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                        return;

                    if (Globals.IsFlashpointContract)
                        return;

                    var warSystem = Globals.WarStatusTracker.systems.Find(x => x.name == __instance.CurSystem.Name);

                    if (Globals.WarStatusTracker.FlashpointSystems.Contains(warSystem.name))
                        return;

                    if (Globals.MissionResult == MissionResult.Victory)
                    {
                        double deltaInfluence;
                        if (Globals.TeamFaction == "AuriganPirates")
                        {
                            deltaInfluence = DeltaInfluence(__instance.CurSystem, Globals.Difficulty, Globals.ContractType, Globals.EnemyFaction, true);
                            warSystem.PirateActivity += (float) deltaInfluence;
                        }
                        else if (Globals.EnemyFaction == "AuriganPirates")
                        {
                            //Logger.Log("Fought against Pirates");
                            deltaInfluence = DeltaInfluence(__instance.CurSystem, Globals.Difficulty, Globals.ContractType, Globals.EnemyFaction, true);
                            //Logger.Log(warSystem.PirateActivity.ToString());
                            //Logger.Log(deltaInfluence.ToString());
                            warSystem.PirateActivity -= (float) deltaInfluence;
                            //Logger.Log(warSystem.PirateActivity.ToString());
                            if (Globals.WarStatusTracker.Deployment)
                                Globals.WarStatusTracker.PirateDeployment = true;
                        }
                        else
                        {
                            deltaInfluence = DeltaInfluence(__instance.CurSystem, Globals.Difficulty, Globals.ContractType, Globals.EnemyFaction, false);
                            if (!Globals.InfluenceMaxed)
                            {
                                warSystem.InfluenceTracker[Globals.TeamFaction] += (float) deltaInfluence;
                                warSystem.InfluenceTracker[Globals.EnemyFaction] -= (float) deltaInfluence;
                            }
                            else
                            {
                                warSystem.InfluenceTracker[Globals.TeamFaction] += (float) Math.Min(Globals.AttackerInfluenceHolder, 100 - warSystem.InfluenceTracker[Globals.TeamFaction]);
                                warSystem.InfluenceTracker[Globals.EnemyFaction] -= (float) deltaInfluence;
                            }
                        }

                        //if (contractType == ContractType.AttackDefend || contractType == ContractType.FireMission)
                        //{
                        //    if (Globals.Settings.Globals.IncludedFactions.Contains(teamfaction))
                        //    {
                        //        if (!Globals.Settings.DefensiveFactions.Contains(teamfaction))
                        //            Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == teamfaction).AttackResources += difficulty;
                        //        else
                        //            Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == teamfaction).DefensiveResources += difficulty;
                        //    }

                        //    if (Globals.Settings.Globals.IncludedFactions.Contains(enemyfaction))
                        //    {
                        //        Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == enemyfaction).DefensiveResources -= difficulty;
                        //        if (Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == enemyfaction).DefensiveResources < 0)
                        //            Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == enemyfaction).DefensiveResources = 0;
                        //    }
                        //    else if (enemyfaction == "AuriganPirates")
                        //    {
                        //        warsystem.PirateActivity -= difficulty;
                        //        if (warsystem.PirateActivity < 0)
                        //            warsystem.PirateActivity = 0;
                        //    }
                        //}

                        var oldOwner = Globals.Sim.CurSystem.OwnerValue.Name;
                        if (WillSystemFlip(__instance.CurSystem, Globals.TeamFaction, Globals.EnemyFaction, deltaInfluence, false) ||
                            Globals.WarStatusTracker.Deployment && Globals.EnemyFaction == "AuriganPirates" && warSystem.PirateActivity < 1)
                        {
                            if (Globals.WarStatusTracker.Deployment && Globals.EnemyFaction == "AuriganPirates" && warSystem.PirateActivity < 1)
                            {
                                LogDebug($"ComStar Bulletin: Galaxy at War {__instance.CurSystem.Name} defended from Pirates!");
                                Globals.SimGameInterruptManager.QueueGenericPopup_NonImmediate("ComStar Bulletin: Galaxy at War", $"{__instance.CurSystem.Name} defended from Pirates! ", true, null);
                            }
                            else
                            {
                                LogDebug($"ComStar Bulletin: Galaxy at War {__instance.CurSystem.Name} taken!  {Globals.Settings.FactionNames[Globals.TeamFaction]} conquered from {Globals.Settings.FactionNames[oldOwner]}");
                                ChangeSystemOwnership(warSystem.starSystem, Globals.TeamFaction, false);
                                Globals.SimGameInterruptManager.QueueGenericPopup_NonImmediate(
                                    $"ComStar Bulletin: Galaxy at War {__instance.CurSystem.Name} taken!", $"{Globals.Settings.FactionNames[Globals.TeamFaction]} conquered from {Globals.Settings.FactionNames[oldOwner]}", true, null);
                                if (Globals.Settings.HyadesRimCompatible && Globals.WarStatusTracker.InactiveTHRFactions.Contains(Globals.TeamFaction))
                                    Globals.WarStatusTracker.InactiveTHRFactions.Remove(Globals.TeamFaction);
                            }

                            if (Globals.WarStatusTracker.HotBox.Contains(Globals.Sim.CurSystem.Name))
                            {
                                LogDebug($"HotSpot: {Globals.Sim.CurSystem.Name}");
                                if (Globals.WarStatusTracker.Deployment)
                                {
                                    LogDebug($"Deployment, difficulty: {warSystem.DeploymentTier}");
                                    var difficultyScale = warSystem.DeploymentTier;
                                    if (difficultyScale == 6)
                                        Globals.Sim.InterruptQueue.QueueRewardsPopup(Globals.Settings.DeploymentReward_06);
                                    else if (difficultyScale == 5)
                                        Globals.Sim.InterruptQueue.QueueRewardsPopup(Globals.Settings.DeploymentReward_05);
                                    else if (difficultyScale == 4)
                                        Globals.Sim.InterruptQueue.QueueRewardsPopup(Globals.Settings.DeploymentReward_04);
                                    else if (difficultyScale == 3)
                                        Globals.Sim.InterruptQueue.QueueRewardsPopup(Globals.Settings.DeploymentReward_03);
                                    else if (difficultyScale == 2)
                                        Globals.Sim.InterruptQueue.QueueRewardsPopup(Globals.Settings.DeploymentReward_02);
                                    else
                                        Globals.Sim.InterruptQueue.QueueRewardsPopup(Globals.Settings.DeploymentReward_01);
                                }

                                Globals.WarStatusTracker.JustArrived = false;
                                Globals.WarStatusTracker.HotBoxTravelling = false;
                                Globals.WarStatusTracker.Escalation = false;
                                Globals.WarStatusTracker.HotBox.Clear();
                                Globals.WarStatusTracker.EscalationDays = 0;
                                warSystem.BonusCBills = false;
                                warSystem.BonusSalvage = false;
                                warSystem.BonusXP = false;
                                Globals.WarStatusTracker.Deployment = false;
                                Globals.WarStatusTracker.DeploymentInfluenceIncrease = 1.0;
                                Globals.WarStatusTracker.PirateDeployment = false;
                                if (Globals.WarStatusTracker.EscalationOrder != null)
                                {
                                    LogDebug("There is an escalation order.");
                                    Globals.WarStatusTracker.EscalationOrder.SetCost(0);
                                    var ActiveItems = Globals.TaskTimelineWidget.ActiveItems;
                                    if (ActiveItems.TryGetValue(Globals.WarStatusTracker.EscalationOrder, out var taskManagementElement4))
                                    {
                                        LogDebug($"{taskManagementElement4.Entry.Description} has been updated.");
                                        taskManagementElement4.UpdateItem(0);
                                    }
                                }
                            }

                            foreach (var system in Globals.WarStatusTracker.SystemChangedOwners)
                            {
                                var systemStatus = Globals.WarStatusTracker.systems.Find(x => x.name == system);
                                systemStatus.CurrentlyAttackedBy.Clear();
                                CalculateAttackAndDefenseTargets(systemStatus.starSystem);
                                RefreshContractsEmployersAndTargets(systemStatus);
                            }

                            Globals.WarStatusTracker.SystemChangedOwners.Clear();

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
                                LogDebug("Refresh contracts after system flip.");
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
                if (Globals.WarStatusTracker == null || Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                    return true;

                var difficulty = contract.Override.GetUIDifficulty();
                int result;
                if (Globals.Sim.ContractUserMeetsReputation(contract))
                {
                    if (contract.IsFlashpointContract || contract.IsFlashpointCampaignContract)
                        result = 0;
                    else if (contract.Override.contractDisplayStyle == ContractDisplayStyle.BaseCampaignRestoration)
                        result = 1;
                    else if (contract.Override.contractDisplayStyle == ContractDisplayStyle.BaseCampaignStory)
                        result = difficulty + 11;
                    else if (contract.TargetSystem == Globals.Sim.CurSystem.ID)
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
                //LogDebug($"\nContract {contract.Name}, difficulty {__result,3} ({contract.Override.employerTeam.FactionValue.Name} vs {contract.Override.targetTeam.FactionValue.Name}) ..\nFlashpoint? {contract.IsFlashpointContract}.  Campaign Flashpoint? {contract.IsFlashpointCampaignContract}.  Priority contract? {contract.IsPriorityContract}.  Travel contract? {contract.Override.travelSeed != 0}");
                return false;
            }
        }

        //Show on the Contract Description how this will impact the war. 
        [HarmonyPatch(typeof(SGContractsWidget), "PopulateContract")]
        public static class SGContractsWidgetPopulateContractPatch
        {
            public static void Prefix(ref Contract contract, ref string __state)
            {
                if (Globals.WarStatusTracker == null || Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                    return;

                LogDebug($"Contract: {contract.Name}.");
                try
                {
                    if (contract.IsFlashpointContract || contract.IsFlashpointCampaignContract)
                    {
                        LogDebug("is Flashpoint, skipping.");
                        return;
                    }
                    var targetSystem = contract.TargetSystem;
                    var systemName = Globals.GaWSystems.Find(x => x.ID == targetSystem);

                    __state = contract.Override.shortDescription;
                    var stringHolder = contract.Override.shortDescription;
                    var employerFaction = contract.GetTeamFaction("ecc8d4f2-74b4-465d-adf6-84445e5dfc230").Name;
                    if (Globals.Settings.GaW_PoliceSupport && employerFaction == Globals.Settings.GaW_Police)
                        employerFaction = Globals.WarStatusTracker.ComstarAlly;
                    var defenseFaction = contract.GetTeamFaction("be77cadd-e245-4240-a93e-b99cc98902a5").Name;
                    if (Globals.Settings.GaW_PoliceSupport && defenseFaction == Globals.Settings.GaW_Police)
                        defenseFaction = Globals.WarStatusTracker.ComstarAlly;
                    bool pirates = employerFaction == "AuriganPirates" || defenseFaction == "AuriganPirates";
                    var deltaInfluence = DeltaInfluence(systemName, contract.Difficulty, contract.Override.ContractTypeValue.Name, defenseFaction, pirates);
                    var systemFlip = false;
                    if (employerFaction != "AuriganPirates" && defenseFaction != "AuriganPirates")
                    {
                        systemFlip = WillSystemFlip(systemName, employerFaction, defenseFaction, deltaInfluence, true);
                        LogDebug($"System {systemName.Name} will flip.");
                    }

                    var attackerString = Globals.Settings.FactionNames[employerFaction] + ": +" + deltaInfluence;
                    var defenderString = Globals.Settings.FactionNames[defenseFaction] + ": -" + deltaInfluence;

                    if (employerFaction != "AuriganPirates" && defenseFaction != "AuriganPirates")
                    {
                        if (!systemFlip)
                            stringHolder = "<b>Impact on System Conflict:</b>\n   " + attackerString + "; " + defenderString;
                        else
                            stringHolder = "<b>***SYSTEM WILL CHANGE OWNERS*** Impact on System Conflict:</b>\n   " + attackerString + "; " + defenderString;
                    }
                    else if (employerFaction == "AuriganPirates")
                        stringHolder = "<b>Impact on Pirate Activity:</b>\n   " + attackerString;
                    else if (defenseFaction == "AuriganPirates")
                        stringHolder = "<b>Impact on Pirate Activity:</b>\n   " + defenderString;

                    var system = Globals.WarStatusTracker.systems.Find(x => x.starSystem == systemName);
                    if (system == null)
                    {
                        LogDebug($"CRITICAL:  System {systemName.Name} not found");
                        return;
                    }

                    if (system.BonusCBills || system.BonusSalvage || system.BonusXP)
                    {
                        stringHolder = stringHolder + "\n<b>Escalation Bonuses:</b> ";
                        if (system.BonusCBills)
                            stringHolder = stringHolder + "+C-Bills ";
                        if (system.BonusSalvage)
                            stringHolder = stringHolder + "+Salvage ";
                        if (system.BonusXP)
                            stringHolder = stringHolder + "+XP";
                    }

                    if (contract.Override.contractDisplayStyle == ContractDisplayStyle.BaseCampaignStory)
                    {
                        var estimatedMissions = CalculateFlipMissions(employerFaction, systemName);
                        int totalDifficulty;

                        if (Globals.Settings.ChangeDifficulty)
                            totalDifficulty = estimatedMissions * systemName.Def.GetDifficulty(SimGameState.SimGameType.CAREER);
                        else
                            totalDifficulty = estimatedMissions * (int) (systemName.Def.DefaultDifficulty + Globals.Sim.GlobalDifficulty);

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

                        stringHolder = stringHolder + "\n<b>Estimated Missions to Wrest Control of System:</b> " + estimatedMissions;
                        stringHolder = stringHolder + "\n   Deployment Reward: Tier " + system.DeploymentTier;
                    }

                    stringHolder = stringHolder + "\n\n" + __state;
                    contract.Override.shortDescription = stringHolder;
                }
                catch (Exception ex)
                {
                    Error(ex);
                }
            }

            public static void Postfix(ref Contract contract, ref string __state)
            {
                if (Globals.WarStatusTracker == null || (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete")))
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

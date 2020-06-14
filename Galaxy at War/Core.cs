using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.Data;
using BattleTech.Framework;
using BattleTech.UI;
using Galaxy_at_War;
using Harmony;
using HBS;
using UnityEngine;
using static Logger;
using Random = System.Random;
using Stopwatch = System.Diagnostics.Stopwatch;
using Newtonsoft.Json;
using System.Reflection;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

public static class Core
{
    internal static ModSettings Settings;
    public static WarStatus WarStatus;
    public static readonly Random Random = new Random();
    public static string teamfaction;
    public static string enemyfaction;
    public static double difficulty;
    public static MissionResult missionResult;
    public static bool isGoodFaithEffort;
    public static List<string> FactionEnemyHolder = new List<string>();
    public static Dictionary<string, List<StarSystem>> attackTargets = new Dictionary<string, List<StarSystem>>();
    public static List<StarSystem> defenseTargets = new List<StarSystem>();
    public static string contractType;
    public static bool NeedsProcessing;
    public static bool BorkedSave;
    public static bool IsFlashpointContract;
    public static int LoopCounter;
    public static Contract LoopContract;
    public static bool HoldContracts = false;
    internal static List<string> IncludedFactions;
    internal static List<FactionValue> OffensiveFactions;
    internal static List<FactionValue> FactionValues => FactionEnumeration.FactionList;
    public static double attackerInfluenceHolder = 0; 
    public static bool influenceMaxed = false;
    
    internal static IEnumerable<FactionValue> GetFactionValuesFromStrings(List<string> factionStrings)
    {
        return FactionValues.Where(x => IncludedFactions.Contains(x.Name));
    }

    internal static void CopySettingsToState()
    {
        if (Settings.ISMCompatibility)
            IncludedFactions = new List<string>(Settings.IncludedFactions_ISM);
        else
            IncludedFactions = new List<string>(Settings.IncludedFactions);
        OffensiveFactions = 
            FactionValues.Except(GetFactionValuesFromStrings(Settings.DefensiveFactions)).ToList();
    }

    [HarmonyPatch(typeof(SimGameState), "OnDayPassed")]
    public static class SimGameState_OnDayPassed_Patch
    {
        static void Prefix(SimGameState __instance, int timeLapse)
        {
            LogDebug("OnDayPassed");
            LoopCounter = 0;
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete"))
                return;

            LogDebug(WarStatus == null ? "WarStatus null" : "WarStatus not null");
            LogDebug("BorkedSave? " + BorkedSave);
            LogDebug("Settings.ResetMap? " + Settings.ResetMap);
            if (WarStatus == null || BorkedSave || Settings.ResetMap)
            {
                LogDebug("Resetting");
                WarStatus = new WarStatus();
                SystemDifficulty();
                WarTick(true, true);
                //BorkedSave = false;
                //
                //GameInstance game = LazySingletonBehavior<UnityGameInstance>.Instance.Game;
                //SimGameInterruptManager interruptQueue = (SimGameInterruptManager)AccessTools
                //    .Field(typeof(SimGameState), "interruptQueue").GetValue(game.Simulation);
                //interruptQueue.QueueGenericPopup_NonImmediate("Borked Save", "Commander, the entire Galaxy is borked! Save the game, exit to desktop, turn ResetMap to false  in the mod.json (if necessary), and load 'er back up!", true, null);
                //sim.StopPlayMode();
            }
            WarStatus.CurSystem = sim.CurSystem.Name;
            if (WarStatus.HotBox.Contains(sim.CurSystem.Name) && !WarStatus.HotBoxTravelling)
            {
                WarStatus.EscalationDays--;

                if (!WarStatus.Deployment)
                {
                    if (WarStatus.EscalationDays == 0)
                    {
                        HotSpots.CompleteEscalation();
                    }

                    if (WarStatus.EscalationOrder != null)
                    {
                        WarStatus.EscalationOrder.PayCost(1);
                        TaskManagementElement taskManagementElement4 = null;
                        TaskTimelineWidget timelineWidget = (TaskTimelineWidget) AccessTools.Field(typeof(SGRoomManager), "timelineWidget").GetValue(__instance.RoomManager);
                        Dictionary<WorkOrderEntry, TaskManagementElement> ActiveItems =
                            (Dictionary<WorkOrderEntry, TaskManagementElement>) AccessTools.Field(typeof(TaskTimelineWidget), "ActiveItems").GetValue(timelineWidget);
                        if (ActiveItems.TryGetValue(WarStatus.EscalationOrder, out taskManagementElement4))
                        {
                            taskManagementElement4.UpdateItem(0);
                        }
                    }
                }
                else
                {
                    if (WarStatus.EscalationOrder != null)
                    {
                        WarStatus.EscalationOrder.PayCost(1);
                        TaskManagementElement taskManagementElement4 = null;
                        TaskTimelineWidget timelineWidget = (TaskTimelineWidget) AccessTools.Field(typeof(SGRoomManager), "timelineWidget").GetValue(__instance.RoomManager);
                        Dictionary<WorkOrderEntry, TaskManagementElement> ActiveItems =
                            (Dictionary<WorkOrderEntry, TaskManagementElement>) AccessTools.Field(typeof(TaskTimelineWidget), "ActiveItems").GetValue(timelineWidget);
                        if (ActiveItems.TryGetValue(WarStatus.EscalationOrder, out taskManagementElement4))
                        {
                            taskManagementElement4.UpdateItem(0);
                        }
                    }

                    if (WarStatus.EscalationDays <= 0)
                    {
                        sim.StopPlayMode();

                        sim.CurSystem.activeSystemContracts.Clear();
                        sim.CurSystem.activeSystemBreadcrumbs.Clear();
                        HotSpots.TemporaryFlip(sim.CurSystem, WarStatus.DeploymentEmployer);

                        var MaxHolder = sim.CurSystem.CurMaxBreadcrumbs;
                        var rand = Random.Next(1, (int) Settings.DeploymentContracts);

                        Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(rand);
                        sim.GeneratePotentialContracts(true, null, sim.CurSystem);
                        Traverse.Create(sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(MaxHolder);

                        SimGameInterruptManager interruptQueue = (SimGameInterruptManager) AccessTools.Field(typeof(SimGameState), "interruptQueue").GetValue(__instance);
                        Action primaryAction = delegate { __instance.QueueCompleteBreadcrumbProcess(true); };
                        interruptQueue.QueueTravelPauseNotification("New Mission", "Our Employer has launched an attack. We must take a mission to support their operation. Let's check out our contracts and get to it!", __instance.GetCrewPortrait(SimGameCrew.Crew_Darius),
                            string.Empty, null, "Proceed");
                    }
                }
            }

            if (!WarStatus.StartGameInitialized)
            {
                NeedsProcessing = true;
                var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                WarStatus.StartGameInitialized = true;
                NeedsProcessing = false;
            }
        }

        public static void Postfix(SimGameState __instance)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            if (!WarStatus.GaW_Event_PopUp)
            {
                GaW_Notification();
                WarStatus.GaW_Event_PopUp = true;
            }

            //TEST: run 100 WarTicks and stop
            if (Settings.LongWarTesting)
            {
                for (var i = 0; i < 100; i++)
                {
                    WarTick(true, false);
                    WarTick(true, false);
                    WarTick(true, false);
                    WarTick(true, true);
                }
                __instance.StopPlayMode();
                return;
            }

            //Remove systems from the protected pool.
            foreach (var tag in sim.CompanyTags)
            {
                if (Settings.FlashpointReleaseSystems.Keys.Contains(tag))
                {
                    if (WarStatus.FlashpointSystems.Contains(Settings.FlashpointReleaseSystems[tag]))
                        WarStatus.FlashpointSystems.Remove(Settings.FlashpointReleaseSystems[tag]);
                }
            }

            if (__instance.DayRemainingInQuarter % Settings.WarFrequency == 0)
            {
                //LogDebug(">>> PROC");
                if (__instance.DayRemainingInQuarter != 30)
                {
                    WarTick(false, false);
                }
                else
                {
                    WarTick(true, true);
                    var hasFlashPoint = sim.CurSystem.SystemContracts.Any(x => x.IsFlashpointContract || x.IsFlashpointCampaignContract);
                    if (!WarStatus.HotBoxTravelling && !WarStatus.HotBox.Contains(sim.CurSystem.Name) && !hasFlashPoint)
                    {
                        NeedsProcessing = true;
                        var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                        sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                        NeedsProcessing = false;
                    }
                }

                SaveHandling.SerializeWar();
                LogDebug(">>> DONE PROC");
            }
        }
    }

    internal static void CalculateComstarSupport()
    {
        if (WarStatus.ComstarCycle < Settings.GaW_Police_SupportTime)
        {
            WarStatus.ComstarCycle++;
            return;
        }
        WarStatus.ComstarCycle = 1;
        List<WarFaction> warFactionList = new List<WarFaction>();
        foreach (var warFarTemp in WarStatus.warFactionTracker)
        {
            warFarTemp.ComstarSupported = false;
            if (Settings.DefensiveFactions.Contains(warFarTemp.faction) || Settings.HyadesPirates.Contains(warFarTemp.faction) ||
                warFarTemp.faction == "AuriganPirates" || Settings.NoOffensiveContracts.Contains(warFarTemp.faction))
                continue;
            warFactionList.Add(warFarTemp);
        }
        var warFactionHolder = warFactionList.OrderBy(x => x.TotalSystemsChanged).ElementAt(0);
        var warFactionListTrimmed = warFactionList.FindAll(x => x.TotalSystemsChanged == warFactionHolder.TotalSystemsChanged);
        warFactionListTrimmed.Shuffle();
        var warFaction = WarStatus.warFactionTracker.Find(x => x.faction == warFactionListTrimmed.ElementAt(0).faction);
        warFaction.ComstarSupported = true;
        WarStatus.ComstarAlly = warFaction.faction;
    }
    
    internal static void WarTick(bool UseFullSet, bool CheckForSystemChange)
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        WarStatus.PrioritySystems.Clear();
        int SystemSubsetSize = WarStatus.systems.Count;
        if (Settings.UseSubsetOfSystems && !UseFullSet)
            SystemSubsetSize = (int) (SystemSubsetSize * Settings.SubSetFraction);
        var SystemSubset = WarStatus.systems.OrderBy(x => Guid.NewGuid()).Take(SystemSubsetSize);

        if (CheckForSystemChange && Settings.GaW_PoliceSupport)
            CalculateComstarSupport();

        if (WarStatus.InitializeAtStart)
        {
            float lowestAR = 5000f;
            float lowestDR = 5000f;
            foreach (var faction in IncludedFactions)
            {

                var warFaction = WarStatus.warFactionTracker.Find(x => x.faction == faction);
                var systemCount = WarStatus.systems.FindAll(x => x.owner == faction).Count();
                if (!Settings.ISMCompatibility && systemCount != 0)
                {
                    warFaction.AR_PerPlanet = Settings.BonusAttackResources[faction] / systemCount;
                    warFaction.DR_PerPlanet = Settings.BonusDefensiveResources[faction] / systemCount;
                    if (warFaction.AR_PerPlanet < lowestAR)
                        lowestAR = warFaction.AR_PerPlanet;
                    if (warFaction.DR_PerPlanet < lowestDR)
                        lowestDR = warFaction.DR_PerPlanet;
                }
                else if (systemCount != 0)
                {
                    warFaction.AR_PerPlanet = Settings.BonusAttackResources_ISM[faction] / systemCount;
                    warFaction.DR_PerPlanet = Settings.BonusDefensiveResources_ISM[faction] / systemCount;
                    if (warFaction.AR_PerPlanet < lowestAR)
                        lowestAR = warFaction.AR_PerPlanet;
                    if (warFaction.DR_PerPlanet < lowestDR)
                        lowestDR = warFaction.DR_PerPlanet;
                }
            }

            foreach (var faction in IncludedFactions)
            {
                var warFaction = WarStatus.warFactionTracker.Find(x => x.faction == faction);
                warFaction.AR_PerPlanet = Mathf.Min(warFaction.AR_PerPlanet, 2 * lowestAR);
                warFaction.DR_PerPlanet = Mathf.Min(warFaction.DR_PerPlanet, 2 * lowestDR);
            }
            foreach (var systemStatus in SystemSubset)
            {
                //Spread out bonus resources and make them fair game for the taking.
                var warFaction = WarStatus.warFactionTracker.Find(x => x.faction == systemStatus.owner);
                systemStatus.AttackResources += warFaction.AR_PerPlanet;
                systemStatus.TotalResources += warFaction.AR_PerPlanet;
                systemStatus.DefenseResources += warFaction.DR_PerPlanet;
                systemStatus.TotalResources += warFaction.DR_PerPlanet;
            }
        }
        //Distribute Pirate Influence throughout the StarSystems
        PiratesAndLocals.CorrectResources();
        PiratesAndLocals.PiratesStealResources();
        PiratesAndLocals.CurrentPAResources = WarStatus.PirateResources;
        PiratesAndLocals.DistributePirateResources();
        PiratesAndLocals.DefendAgainstPirates();

        if (CheckForSystemChange && Settings.HyadesRimCompatible && WarStatus.InactiveTHRFactions.Count() != 0
                        && WarStatus.HyadesRimGeneralPirateSystems.Count() != 0)
        {
            int rand = Random.Next(0, 100);
            if (rand < WarStatus.HyadesRimsSystemsTaken)
            {
                WarStatus.InactiveTHRFactions.Shuffle();
                WarStatus.HyadesRimGeneralPirateSystems.Shuffle();
                var flipSystem = WarStatus.systems.Find(x => x.name == WarStatus.HyadesRimGeneralPirateSystems[0]).starSystem;

                ChangeSystemOwnership(sim, flipSystem, WarStatus.InactiveTHRFactions[0], true);
                WarStatus.InactiveTHRFactions.RemoveAt(0);
                WarStatus.HyadesRimGeneralPirateSystems.RemoveAt(0);
            }

        }

        foreach (var systemStatus in SystemSubset)
        {
            systemStatus.PriorityAttack = false;
            systemStatus.PriorityDefense = false;
            if (WarStatus.InitializeAtStart)
            {
                systemStatus.CurrentlyAttackedBy.Clear();
                CalculateAttackAndDefenseTargets(systemStatus.starSystem);
                RefreshContracts(systemStatus.starSystem);
            }

            if (systemStatus.Contended || WarStatus.HotBox.Contains(systemStatus.name))
                continue;

            if (!systemStatus.owner.Equals("Locals") && systemStatus.influenceTracker.Keys.Contains("Locals") && 
                !WarStatus.FlashpointSystems.Contains(systemStatus.name))
            {
                systemStatus.influenceTracker["Locals"] *= 1.1f;
            }

            //Add resources from neighboring systems.
            if (systemStatus.neighborSystems.Count != 0)
            {
                foreach (var neighbor in systemStatus.neighborSystems.Keys)
                {
                    if (!Settings.ImmuneToWar.Contains(neighbor) && !Settings.DefensiveFactions.Contains(neighbor) &&
                        !WarStatus.FlashpointSystems.Contains(systemStatus.name))
                    {
                        var PushFactor = Settings.APRPush * Random.Next(1, Settings.APRPushRandomizer + 1);
                        systemStatus.influenceTracker[neighbor] += systemStatus.neighborSystems[neighbor] * PushFactor;
                    }
                }
            }

            //Revolt on previously taken systems.
            if (systemStatus.owner != systemStatus.OriginalOwner)
                systemStatus.influenceTracker[systemStatus.OriginalOwner] *= 0.10f;

            float PirateSystemFlagValue = Settings.PirateSystemFlagValue;

            if (Settings.ISMCompatibility)
                PirateSystemFlagValue = Settings.PirateSystemFlagValue_ISM;

            var TotalPirates = systemStatus.PirateActivity * systemStatus.TotalResources / 100;

            if (TotalPirates >= PirateSystemFlagValue)
            {
                if (!WarStatus.PirateHighlight.Contains(systemStatus.name))
                    WarStatus.PirateHighlight.Add(systemStatus.name);
            }
            else
            {
                if (WarStatus.PirateHighlight.Contains(systemStatus.name))
                    WarStatus.PirateHighlight.Remove(systemStatus.name);
            }
        }
        
        WarStatus.InitializeAtStart = false;
        //Attack!
        //LogDebug("Attacking Fool");
        timer.Restart();
        foreach (var warFaction in WarStatus.warFactionTracker)
        {
            DivideAttackResources(warFaction, UseFullSet);
            AllocateAttackResources(warFaction);
        }

        LogDebug("AllocateAttackResources " + timer.Elapsed);

        CalculateDefensiveSystems();

        timer.Restart();
        foreach (var warFaction in WarStatus.warFactionTracker)
        {
            AllocateDefensiveResources(warFaction, UseFullSet);
        }

        LogDebug("AllocateDefensiveResources " + timer.Elapsed);

        timer.Restart();
        UpdateInfluenceFromAttacks(sim, CheckForSystemChange);
        LogDebug("UpdateInfluenceFromAttacks " + timer.Elapsed);

        //Increase War Escalation or decay defenses.
        foreach (var warfaction in WarStatus.warFactionTracker)
        {
            if (!warfaction.GainedSystem)
                warfaction.DaysSinceSystemAttacked += 1;
            else
            {
                warfaction.DaysSinceSystemAttacked = 0;
                warfaction.GainedSystem = false;
            }

            if (!warfaction.LostSystem)
                warfaction.DaysSinceSystemLost += 1;
            else
            {
                warfaction.DaysSinceSystemLost = 0;
                warfaction.LostSystem = false;
            }
        }

        foreach (var system in WarStatus.SystemChangedOwners)
        {
            var systemStatus = WarStatus.systems.Find(x => x.name == system);
            systemStatus.CurrentlyAttackedBy.Clear();
            CalculateAttackAndDefenseTargets(systemStatus.starSystem);
            RefreshContracts(systemStatus.starSystem);
        }

        LogDebug("Changed " + WarStatus.SystemChangedOwners.Count);
        WarStatus.SystemChangedOwners.Clear();
        if (StarmapMod.eventPanel != null)
        {
            StarmapMod.UpdatePanelText();
        }

        //Log("===================================================");
        //Log("TESTING ZONE");
        //Log("===================================================");
        //////TESTING ZONE
        //foreach (WarFaction WF in WarStatus.warFactionTracker)
        //{
        //    Log("----------------------------------------------");
        //    Log(WF.faction.ToString());
        //    try
        //    {
        //        var DLT = WarStatus.deathListTracker.Find(x => x.faction == WF.faction);
        //        //                Log("\tAttacked By :");
        //        //                foreach (Faction fac in DLT.AttackedBy)
        //        //                    Log("\t\t" + fac.ToString());
        //        //                Log("\tOwner :" + DLT.);
        //        //                Log("\tAttack Resources :" + WF.AttackResources.ToString());
        //        //                Log("\tDefensive Resources :" + WF.DefensiveResources.ToString());
        //        Log("\tDeath List:");
        //        foreach (var faction in DLT.deathList.Keys)
        //        {
        //            Log("\t\t" + faction.ToString() + ": " + DLT.deathList[faction]);
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Error(e);
        //    }
        //}

    }

    public static void CalculateAttackAndDefenseTargets(StarSystem starSystem)
    {

        SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
        var warSystem = WarStatus.systems.Find(x => x.name == starSystem.Name);
        var OwnerNeighborSystems = warSystem.neighborSystems;
        OwnerNeighborSystems.Clear();
        if (starSystem == null || sim.Starmap.GetAvailableNeighborSystem(starSystem).Count == 0)
            return;

        foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
        {
            if (!neighborSystem.OwnerValue.Name.Equals(starSystem.OwnerValue.Name) && !WarStatus.FlashpointSystems.Contains(starSystem.Name) && 
                !Settings.ImmuneToWar.Contains(neighborSystem.OwnerValue.Name))
            {
                var warFac = WarStatus.warFactionTracker.Find(x => x.faction == starSystem.OwnerValue.Name);
                if (warFac == null)
                    return;

                if (!warFac.attackTargets.ContainsKey(neighborSystem.OwnerValue.Name))
                {
                    var tempList = new List<string> { neighborSystem.Name };
                    warFac.attackTargets.Add(neighborSystem.OwnerValue.Name, tempList);
                }
                else if (warFac.attackTargets.ContainsKey(neighborSystem.OwnerValue.Name) 
                         && !warFac.attackTargets[neighborSystem.OwnerValue.Name].Contains(neighborSystem.Name))
                {
                    warFac.attackTargets[neighborSystem.OwnerValue.Name].Add(neighborSystem.Name);
                }
                //if (!warFac.defenseTargets.Contains(starSystem.Name))
                //{
                //    warFac.defenseTargets.Add(starSystem.Name);
                //}
                if (!warFac.adjacentFactions.Contains(starSystem.OwnerValue.Name) && !Settings.DefensiveFactions.Contains(starSystem.OwnerValue.Name))
                    warFac.adjacentFactions.Add(starSystem.OwnerValue.Name);
            }
            RefreshNeighbors(OwnerNeighborSystems, neighborSystem);
        }

    }

    public static void RefreshNeighbors(Dictionary<string, int> starSystem, StarSystem neighborSystem)
    {
        if (WarStatus.FlashpointSystems.Contains(neighborSystem.Name))
            return;

        var neighborSystemOwner = neighborSystem.OwnerValue.Name;

        if (starSystem.ContainsKey(neighborSystemOwner))
            starSystem[neighborSystemOwner] += 1;
        else
            starSystem.Add(neighborSystemOwner, 1);
    }

    //public static void CalculateDefenseTargets(StarSystem starSystem)
    //{
    //    SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

    //    foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
    //    {
    //        var warFac = WarStatus.warFactionTracker.Find(x => x.faction == starSystem.Owner);
    //        if (warFac == null)
    //        {
    //            return;
    //        }

    //        if ((neighborSystem.Owner != starSystem.Owner) && !warFac.defenseTargets.Contains(starSystem))
    //        {
    //            warFac.defenseTargets.Add(starSystem);
    //        }
    //    }
    //}

    public static void DivideAttackResources(WarFaction warFaction, bool UseFullSet)
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        //Log("Attacking");
        var deathList = WarStatus.deathListTracker.Find(x => x.faction == warFaction.faction);
        var warFAR = warFaction.warFactionAttackResources;
        warFAR.Clear();
        var tempTargets = new Dictionary<string, float>();
        foreach (string fact in warFaction.attackTargets.Keys)
        {
            tempTargets.Add(fact, deathList.deathList[fact]);
        }

        var total = tempTargets.Values.Sum();
        float attackResources = warFaction.AttackResources - warFaction.AR_Against_Pirates;
        if (warFaction.ComstarSupported)
            attackResources += Settings.GaW_Police_ARBonus;
        warFaction.AR_Against_Pirates = 0;
        if (Settings.AggressiveToggle && !Settings.DefensiveFactions.Contains(warFaction.faction))
            attackResources += sim.Constants.Finances.LeopardBaseMaintenanceCost;
        
        attackResources = attackResources * (1 + warFaction.DaysSinceSystemAttacked * Settings.AResourceAdjustmentPerCycle / 100);
        attackResources += attackResources * (float)(Random.Next(-1, 1) * Settings.ResourceSpread);
        foreach (string Rfact in tempTargets.Keys)
        {
            warFAR.Add(Rfact, tempTargets[Rfact] * attackResources / total);
        }
    }
    
    public static void AllocateAttackResources(WarFaction warFaction)
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        var FactionRep = sim.GetRawReputation(FactionValues.Find(x => x.Name == warFaction.faction));
        int maxContracts = HotSpots.ProcessReputation(FactionRep);
        if (warFaction.warFactionAttackResources.Keys.Count == 0)
            return;
        var warFAR = warFaction.warFactionAttackResources;
        //Go through the different resources allocated from attacking faction to spend against each targetFaction
        var factionDLT = WarStatus.deathListTracker.Find(x => x.faction == warFaction.faction);
        foreach (var targetFaction in warFAR.Keys)
        {
            if (!warFaction.attackTargets.Keys.Contains(targetFaction))
                break;
            var targetFAR = warFAR[targetFaction];
            var startingtargetFAR = targetFAR;
            var targets = warFaction.attackTargets[targetFaction];
            var hatred = factionDLT.deathList[targetFaction];
            
            while (targetFAR > 0)
            {
                if (targets.Count == 0)
                    break;

                var rand = Random.Next(0, targets.Count);
                var system = WarStatus.systems.Find(f => f.name == targets[rand]);
                if (system.owner == warFaction.faction || WarStatus.FlashpointSystems.Contains(system.name))
                {
                    targets.RemoveAt(rand);
                    continue;
                }

                //Find most valuable target for attacking for later. Used in HotSpots.
                if (hatred >= Settings.PriorityHatred &&
                    system.DifficultyRating <= maxContracts &&
                    system.DifficultyRating >= maxContracts - 4)
                {
                    system.PriorityAttack = true;
                    if (!system.CurrentlyAttackedBy.Contains(warFaction.faction))
                    {
                        system.CurrentlyAttackedBy.Add(warFaction.faction);
                    }
                    if (!WarStatus.PrioritySystems.Contains(system.starSystem.Name))
                    {
                        WarStatus.PrioritySystems.Add(system.starSystem.Name);
                    }
                }

                //Distribute attacking resources to systems.
                if (system.Contended || WarStatus.HotBox.Contains(system.name))
                {
                    targets.Remove(system.starSystem.Name);
                    if (targets.Count == 0 || !warFaction.attackTargets.Keys.Contains(targetFaction))
                    {
                        break;
                    }

                    continue;
                }

                var ARFactor = UnityEngine.Random.Range(Settings.MinimumResourceFactor, Settings.MaximumResourceFactor);
                var spendAR = Mathf.Min(startingtargetFAR * ARFactor, targetFAR);
                spendAR = spendAR < 1 ? 1 : spendAR;

                var maxValueList = system.influenceTracker.Values.OrderByDescending(x => x).ToList();
                float PmaxValue = 200.0f;
                if (maxValueList.Count > 1)
                    PmaxValue = maxValueList[1];

                var ITValue = system.influenceTracker[warFaction.faction];
                float basicAR = (float)(11 - system.DifficultyRating) / 2;

                float bonusAR = 0f;
                if (ITValue > PmaxValue)
                    bonusAR = (ITValue - PmaxValue) * 0.15f;

                float TotalAR = (basicAR + bonusAR) + spendAR;

                if (targetFAR > TotalAR)
                {
                    system.influenceTracker[warFaction.faction] += TotalAR;
                    targetFAR -= TotalAR;
                    WarStatus.warFactionTracker.Find(x => x.faction == targetFaction).defenseTargets.Add(system.name);
                }
                else
                {
                    system.influenceTracker[warFaction.faction] += targetFAR;
                    targetFAR = 0;
                }
            }
        }
    }

    public static void CalculateDefensiveSystems()
    {
        foreach (var warFaction in WarStatus.warFactionTracker)
            warFaction.defenseTargets.Clear();

        foreach (var system in WarStatus.systems)
        {
            if (WarStatus.FlashpointSystems.Contains(system.name))
                continue;

            var totalInfluence = system.influenceTracker.Values.Sum();
            if ((totalInfluence - 100) / 100 > Settings.SystemDefenseCutoff)
            {
                var warfaction = WarStatus.warFactionTracker.Find(x => x.faction == system.owner);
                warfaction.defenseTargets.Add(system.name);
            }
        }

        //foreach (var warFaction in WarStatus.warFactionTracker)
        //{
        //    Log("=============");
        //    Log(warFaction.faction);
        //    foreach (var system in warFaction.defenseTargets)
        //        Log("   " + system);
        //}
    }

    public static void AllocateDefensiveResources(WarFaction warFaction, bool UseFullSet)
    {
        if (warFaction.defenseTargets.Count == 0 || !WarStatus.warFactionTracker.Contains(warFaction))
            return;

        var sim = UnityGameInstance.BattleTechGame.Simulation;
        var faction = warFaction.faction;
        float defensiveResources = warFaction.DefensiveResources + warFaction.DR_Against_Pirates;
        if (warFaction.ComstarSupported)
            defensiveResources += Settings.GaW_Police_DRBonus;
        warFaction.DR_Against_Pirates = 0;
        if (Settings.AggressiveToggle && Settings.DefensiveFactions.Contains(warFaction.faction))
            defensiveResources += sim.Constants.Finances.LeopardBaseMaintenanceCost;

        var defensiveCorrection = defensiveResources * (100 * Settings.GlobalDefenseFactor -
                Settings.DResourceAdjustmentPerCycle * warFaction.DaysSinceSystemLost) / 100;

        defensiveResources = Math.Max(defensiveResources, defensiveCorrection); 
        defensiveResources += defensiveResources * (float)(Random.Next(-1,1) * (Settings.ResourceSpread));
        var startingdefensiveResources = defensiveResources;
        List<string> duplicateDefenseTargets = new List<string>(warFaction.defenseTargets);
        int rand = 0;
        string system = "";

        // spend and decrement defensiveResources
        while (defensiveResources > float.Epsilon)
        {
            //LogDebug(spendDR);
            float highest = 0f;
            string highestFaction = faction;
            float spendDR = 1.0f;
            if (duplicateDefenseTargets.Count != 0)
            {
                rand = Random.Next(0, duplicateDefenseTargets.Count);
                system = duplicateDefenseTargets[rand];
                duplicateDefenseTargets.Remove(system);
            }
            else
            {
                rand = Random.Next(0, warFaction.defenseTargets.Count);
                system = warFaction.defenseTargets[rand];
                var DRFactor = UnityEngine.Random.Range(Settings.MinimumResourceFactor, Settings.MaximumResourceFactor);
                spendDR = Mathf.Min(startingdefensiveResources * DRFactor, defensiveResources);
                spendDR = spendDR < 1 ? 1 : spendDR;
            }
            var systemStatus = WarStatus.systems.Find(x => x.name == system);
            if (systemStatus.Contended || WarStatus.HotBox.Contains(systemStatus.name))
            {
                warFaction.defenseTargets.Remove(systemStatus.starSystem.Name);
                if (warFaction.defenseTargets.Count == 0 || warFaction.defenseTargets == null)
                {
                    break;
                }
                continue;
            }

            float Total = systemStatus.influenceTracker.Values.Sum();
            var sequence = systemStatus.influenceTracker
                .Where(x => x.Value != 0)
                .Select(x => x.Key);
            foreach (string factionStr in sequence)
            {
                if (systemStatus.influenceTracker[factionStr] > highest)
                {
                    highest = systemStatus.influenceTracker[factionStr];
                    highestFaction = factionStr;
                }

                if (highest / Total >= 0.5)
                    break;
            }
            //Log("===========");
            //Log(system);
            //Log("Before Defense");
            //foreach (var foo in systemStatus.influenceTracker.Keys)
            //    Log("    " + foo + ": " + systemStatus.influenceTracker[foo]);
            //LogDebug("1 " + timer.Elapsed);
            //foreach (string tempfaction in systemStatus.influenceTracker.Keys)
            //{
            //    if (systemStatus.influenceTracker[tempfaction] > highest)
            //    {
            //        highest = systemStatus.influenceTracker[tempfaction];
            //        highestFaction = tempfaction;
            //    }
            //}

            //highest = systemStatus.influenceTracker.Values.Max();
            //highestFaction = systemStatus.influenceTracker
            //    .Where(x => x.Value == highest)
            //    .Select(y => y.Key)
            //    .First();

            
            if (highestFaction == faction)
            {
                if (defensiveResources > 0)
                {
                    systemStatus.influenceTracker[faction] += spendDR;
                    defensiveResources -= spendDR;
                }
                else
                {
                    systemStatus.influenceTracker[faction] += defensiveResources;
                    defensiveResources = 0;
                }
            }
            else
            {
                var totalInfluence = systemStatus.influenceTracker.Values.Sum();
                var diffRes = systemStatus.influenceTracker[highestFaction] / totalInfluence - systemStatus.influenceTracker[faction] / totalInfluence;
                var bonusDefense = spendDR + (diffRes * totalInfluence - (Settings.TakeoverThreshold / 100) * totalInfluence) / (Settings.TakeoverThreshold / 100 + 1);
                //LogDebug(bonusDefense);
                if (100 * diffRes > Settings.TakeoverThreshold)
                    if (defensiveResources >= bonusDefense)
                    {
                        systemStatus.influenceTracker[faction] += bonusDefense;
                        defensiveResources -= bonusDefense;
                    }
                    else
                    {
                        systemStatus.influenceTracker[faction] += Math.Min(defensiveResources, 50);
                        defensiveResources -= Math.Min(defensiveResources, 50);
                    }
                else
                {
                    systemStatus.influenceTracker[faction] += Math.Min(defensiveResources, 50);
                    defensiveResources -= Math.Min(defensiveResources, 50);
                }
            }
            //Log("After Defense");
            //foreach (var foo in systemStatus.influenceTracker.Keys)
            //    Log("    " + foo + ": " + systemStatus.influenceTracker[foo]);
        }
    }

    public static void ChangeSystemOwnership(SimGameState sim, StarSystem system, string faction, bool ForceFlip)
    {
        if (faction != system.OwnerValue.Name || ForceFlip)
        {
            FactionValue OldFaction = system.OwnerValue;
            if ((OldFaction.Name == "NoFaction" || OldFaction.Name == "Locals") && system.Def.Tags.Contains("planet_region_hyadesrim") && !ForceFlip)
            {
                if (WarStatus.HyadesRimGeneralPirateSystems.Contains(system.Name))
                    WarStatus.HyadesRimGeneralPirateSystems.Remove(system.Name);
                WarStatus.HyadesRimsSystemsTaken++;
            }

            if (system.Def.Tags.Contains(Settings.FactionTags[OldFaction.Name]))
                system.Def.Tags.Remove(Settings.FactionTags[OldFaction.Name]);
            system.Def.Tags.Add(Settings.FactionTags[faction]);

            if (!WarStatus.AbandonedSystems.Contains(system.Name))
            {
                if (system.Def.SystemShopItems.Count != 0)
                {
                    List<string> TempList = system.Def.SystemShopItems;
                    TempList.Add(Settings.FactionShops[system.OwnerValue.Name]);
                    Traverse.Create(system.Def).Property("SystemShopItems").SetValue(TempList);
                }

                if (system.Def.FactionShopItems != null)
                {
                    Traverse.Create(system.Def).Property("FactionShopOwnerValue").SetValue(FactionValues.Find(x => x.Name == faction));
                    Traverse.Create(system.Def).Property("FactionShopOwnerID").SetValue(faction);
                    List<string> FactionShops = system.Def.FactionShopItems;
                    if (FactionShops.Contains(Settings.FactionShopItems[system.Def.OwnerValue.Name]))
                        FactionShops.Remove(Settings.FactionShopItems[system.Def.OwnerValue.Name]);
                    FactionShops.Add(Settings.FactionShopItems[faction]);
                    Traverse.Create(system.Def).Property("FactionShopItems").SetValue(FactionShops);
                }
            }
            var systemStatus = WarStatus.systems.Find(x => x.name == system.Name);
            var oldOwner = systemStatus.owner;
            systemStatus.owner = faction;
            Traverse.Create(system.Def).Property("OwnerID").SetValue(faction);
            Traverse.Create(system.Def).Property("OwnerValue").SetValue(FactionValues.Find(x => x.Name == faction));
            
            //Change the Kill List for the factions.
            var TotalAR = GetTotalAttackResources(system);
            var TotalDR = GetTotalDefensiveResources(system);
            var SystemValue = TotalAR + TotalDR;
            
            WarFaction WFWinner = WarStatus.warFactionTracker.Find(x => x.faction == faction);
            WFWinner.GainedSystem = true;
            WFWinner.MonthlySystemsChanged += 1;
            WFWinner.TotalSystemsChanged += 1;
            if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(WFWinner.faction))
            {
                WFWinner.DefensiveResources += TotalAR;
                WFWinner.DefensiveResources += TotalDR;
            }
            else
            {
                WFWinner.AttackResources += TotalAR;
                WFWinner.DefensiveResources += TotalDR;
            }
            
            WarFaction WFLoser = WarStatus.warFactionTracker.Find(x => x.faction == OldFaction.Name);
            WFLoser.LostSystem = true;
            WFLoser.MonthlySystemsChanged -= 1;
            WFLoser.TotalSystemsChanged -= 1;
            RemoveAndFlagSystems(WFLoser, system);
            if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(WFWinner.faction))
            {
                WFLoser.DefensiveResources -= TotalAR;
                WFLoser.DefensiveResources -= TotalDR;
            }
            else
            {
                WFLoser.AttackResources -= TotalAR;
                WFLoser.DefensiveResources -= TotalDR;
            }
            if (WFLoser.AttackResources < 0)
                WFLoser.AttackResources = 0;
            if (WFLoser.DefensiveResources < 0)
                WFLoser.DefensiveResources = 0;

            if (!WarStatus.SystemChangedOwners.Contains(system.Name))
                WarStatus.SystemChangedOwners.Add(system.Name);

            if (ForceFlip)
            {
                RecalculateSystemInfluence(systemStatus, faction, oldOwner);
                systemStatus.PirateActivity = 0;
            }

            foreach (var neighbor in sim.Starmap.GetAvailableNeighborSystem(system))
            {
                if (!WarStatus.SystemChangedOwners.Contains(neighbor.Name))
                    WarStatus.SystemChangedOwners.Add(neighbor.Name);
            }
        }
    }

    public static void ChangeDeathlistFromAggression(StarSystem system, string faction, string OldFaction)
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        var TotalAR = GetTotalAttackResources(system);
        var TotalDR = GetTotalDefensiveResources(system);
        var SystemValue = TotalAR + TotalDR;
        var KillListDelta = Math.Max(10, SystemValue);
        if (WarStatus.deathListTracker.Find(x => x.faction == OldFaction) == null)
            return;

        var factionTracker = WarStatus.deathListTracker.Find(x => x.faction == OldFaction);
        if (factionTracker.deathList[faction] < 50)
            factionTracker.deathList[faction] = 50;

        factionTracker.deathList[faction] += KillListDelta;
        //Allies are upset that their friend is being beaten up.
        if (!Settings.DefensiveFactions.Contains(OldFaction))
        {
            foreach (var ally in sim.GetFactionDef(OldFaction).Allies)
            {
                if (!IncludedFactions.Contains(ally) || faction  == ally || WarStatus.deathListTracker.Find(x => x.faction == ally) == null)
                    continue;
                var factionAlly = WarStatus.deathListTracker.Find(x => x.faction == ally);
                factionAlly.deathList[faction] += KillListDelta / 2;
            }
        }
        //Enemies of the target faction are happy with the faction doing the beating.
        if (!Settings.DefensiveFactions.Contains(OldFaction))
        {
            foreach (var enemy in sim.GetFactionDef(OldFaction).Enemies)
            {
                if (!IncludedFactions.Contains(enemy) || enemy == faction || WarStatus.deathListTracker.Find(x => x.faction == enemy) == null)
                    continue;
                var factionEnemy = WarStatus.deathListTracker.Find(x => x.faction == enemy);
                factionEnemy.deathList[faction] -= KillListDelta / 2;
            }
        }
        factionTracker.AttackedBy.Add(faction);
    }

    public static void CalculateHatred()
    {
        foreach (var warFaction in WarStatus.warFactionTracker)
        {
            Dictionary<string, int> AttackCount = new Dictionary<string, int>();
            foreach (var target in warFaction.attackTargets)
            {
                if (Settings.DefensiveFactions.Contains(target.Key) || Settings.ImmuneToWar.Contains(target.Key))
                    continue;
                AttackCount.Add(target.Key, target.Value.Count);
            }
            int i = 0;
            int tophalf = AttackCount.Count / 2;
            foreach (var attacktarget in AttackCount.OrderByDescending(x => x.Value))
            {
                var warfaction = WarStatus.warFactionTracker.Find(x => x.faction == attacktarget.Key);
                if (i < tophalf)
                    warfaction.IncreaseAggression[warFaction.faction] = true;
                else
                    warFaction.IncreaseAggression[warFaction.faction] = false;
                i++;
            }
        }
    }

    private static void RemoveAndFlagSystems(WarFaction OldOwner, StarSystem system)
    {
        //OldOwner.defenseTargets.Remove(system.Name);
        if (!WarStatus.SystemChangedOwners.Contains(system.Name))
            WarStatus.SystemChangedOwners.Add(system.Name);
        foreach (var neighborsystem in UnityGameInstance.BattleTechGame.Simulation.Starmap.GetAvailableNeighborSystem(system))
        {
            var WFAT = WarStatus.warFactionTracker.Find(x => x.faction == neighborsystem.OwnerValue.Name).attackTargets;
            if (WFAT.Keys.Contains(OldOwner.faction) && WFAT[OldOwner.faction].Contains(system.Name))
                WFAT[OldOwner.faction].Remove(system.Name);
        }
    }

    private static void UpdateInfluenceFromAttacks(SimGameState sim, bool CheckForSystemChange)
    {
        var tempRTFactions = WarStatus.deathListTracker;
        foreach (var deathListTracker in tempRTFactions)
        {
            deathListTracker.AttackedBy.Clear();
        }

        if (CheckForSystemChange)
            WarStatus.LostSystems.Clear();

        LogDebug($"Updating influence for {WarStatus.systems.Count.ToString()} systems");
        foreach (var systemStatus in WarStatus.systems)
        {
            var tempDict = new Dictionary<string, float>();
            var totalInfluence = systemStatus.influenceTracker.Values.Sum();
            var highest = 0f;
            var highestfaction = systemStatus.owner;
            foreach (var kvp in systemStatus.influenceTracker)
            {
                tempDict.Add(kvp.Key, kvp.Value / totalInfluence * 100);
                if (kvp.Value > highest)
                {
                    highest = kvp.Value;
                    highestfaction = kvp.Key;
                }
            }

            systemStatus.influenceTracker = tempDict;
            var diffStatus = systemStatus.influenceTracker[highestfaction] - systemStatus.influenceTracker[systemStatus.owner];
            var starSystem = systemStatus.starSystem;
            
            if (highestfaction != systemStatus.owner && !WarStatus.FlashpointSystems.Contains(systemStatus.name) && 
                (diffStatus > Settings.TakeoverThreshold && !WarStatus.HotBox.Contains(systemStatus.name)
                && (!Settings.DefensiveFactions.Contains(highestfaction) || highestfaction == "Locals") && 
                !Settings.ImmuneToWar.Contains(starSystem.OwnerValue.Name)))
            {
                if (!systemStatus.Contended)
                {
                    systemStatus.Contended = true;
                    ChangeDeathlistFromAggression(starSystem, highestfaction, starSystem.OwnerValue.Name);
                }
                else if (CheckForSystemChange)
                { 
                    ChangeSystemOwnership(sim, starSystem, highestfaction, false);
                    systemStatus.Contended = false;
                    WarStatus.LostSystems.Add(starSystem.Name);
                }
            }
            //Local Government can take a system.
            if (systemStatus.owner != "Locals" && systemStatus.OriginalOwner == "Locals" &&
                (highestfaction == "Locals" && systemStatus.influenceTracker[highestfaction] >= 75)) 
            {
                ChangeSystemOwnership(sim, starSystem, "Locals", true);
                systemStatus.Contended = false;
                WarStatus.LostSystems.Add(starSystem.Name);
            }
        }
        CalculateHatred();
        foreach (var deathListTracker in tempRTFactions)
        {
            AdjustDeathList(deathListTracker, sim, false);
        }
    }

    public static string MonthlyWarReport()
    {
        string summaryString = "";
        string summaryString2 = "";
        string combinedString = "";

        foreach (string faction in IncludedFactions)
        {
            WarFaction warFaction = WarStatus.warFactionTracker.Find(x => x.faction == faction);
            combinedString = combinedString + "<b><u>" + Settings.FactionNames[faction] + "</b></u>\n";
            summaryString = "Monthly Change in Systems: " + warFaction.MonthlySystemsChanged + "\n";
            summaryString2 = "Overall Change in Systems: " + warFaction.TotalSystemsChanged + "\n\n";

            combinedString = combinedString + summaryString + summaryString2;
            warFaction.MonthlySystemsChanged = 0;
        }

        char[] trim = {'\n'};
        combinedString = combinedString.TrimEnd(trim);
        return combinedString;
    }

    public static void RefreshContracts(StarSystem starSystem)
    {
        //LogDebug("RefreshContracts for " + starSystem.Name);
        if (WarStatus.HotBox.Contains(starSystem.Name) || (starSystem.Tags.Contains("planet_region_hyadesrim") &&
            (starSystem.OwnerDef.Name == "Locals" || starSystem.OwnerDef.Name == "NoFaction")))
        {
            LogDebug("Skipping HotBox or THR Neutrals");
            return;
        }

        var ContractEmployers = starSystem.Def.contractEmployerIDs;
        var ContractTargets = starSystem.Def.contractTargetIDs;
        SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
        var owner = starSystem.OwnerValue;
        ContractEmployers.Clear();
        ContractTargets.Clear();

        ContractEmployers.Add("Locals");
        ContractTargets.Add("Locals");

        if (starSystem.Tags.Contains("planet_other_pirate") || WarStatus.AbandonedSystems.Contains(starSystem.Name))
        {
            ContractEmployers.Add("AuriganPirates");
            ContractTargets.Add("AuriganPirates");
        }

        if (owner != FactionValues.FirstOrDefault(f => f.Name == "NoFaction") && owner != FactionValues.FirstOrDefault(f => f.Name == "Locals"))
        {
            ContractEmployers.Add(owner.Name);
            ContractTargets.Add(owner.Name);
        }
        if (WarStatus.ComstarAlly == owner.Name)
        {
            ContractEmployers.Add(Settings.GaW_Police);
            ContractTargets.Add(Settings.GaW_Police);
        }

        var WarSystem = WarStatus.systems.Find(x => x.name == starSystem.Name);
        var neighborSystems = WarSystem.neighborSystems;
        foreach (var systemNeighbor in neighborSystems.Keys)
        {
            if (Settings.ImmuneToWar.Contains(systemNeighbor) || Settings.DefensiveFactions.Contains(systemNeighbor))
                continue;

            if (!ContractEmployers.Contains(systemNeighbor))
                ContractEmployers.Add(systemNeighbor);

            if (!ContractTargets.Contains(systemNeighbor))
                ContractTargets.Add(systemNeighbor);
        }

        if ((WarSystem.PirateActivity > 0) && !ContractEmployers.Contains("AuriganPirates"))
        {
            ContractEmployers.Add("AuriganPirates");
            ContractTargets.Add("AuriganPirates");
        }

        if (ContractEmployers.Count == 1)
        {
            var faction = OffensiveFactions[Random.Next(OffensiveFactions.Count)];
            ContractEmployers.Add(faction.Name);
            if (!ContractTargets.Contains(faction.Name))
                ContractTargets.Add(faction.Name);
        }
        if (starSystem.Tags.Contains("planet_region_hyadesrim") && Settings.HyadesRimCompatible)
        {
            foreach (var alliedFaction in owner.FactionDef.Allies)
            {
                if (!ContractEmployers.Contains(alliedFaction) && !Settings.HyadesTargetsOnly.Contains(alliedFaction))
                    ContractEmployers.Add(alliedFaction);
            }
            foreach (var enemyFaction in owner.FactionDef.Enemies)
            {
                if (!ContractTargets.Contains(enemyFaction) && !Settings.HyadesEmployersOnly.Contains(enemyFaction))
                    ContractTargets.Add(enemyFaction);
            }
        }
        var TempContractEmployers = new List<string>(ContractEmployers);
        foreach (var tempEmployer in TempContractEmployers)
        {
            if (Settings.NoOffensiveContracts.Contains(tempEmployer))
                ContractEmployers.Remove(tempEmployer);
        }
    }

    //Remove duplicates in the ContracteEmployerIDList
    [HarmonyPatch(typeof(SimGameState), "GetValidParticipants")]
    public static class SimGameState_GetValidParticipants_Patch
    {
        public static void Prefix(ref StarSystem system)
        {
            system.Def.contractEmployerIDs = system.Def.contractEmployerIDs.Distinct().ToList();
        }
    }

    [HarmonyPatch(typeof(SimGameState), "GenerateContractParticipants")]
    public static class SimGameState_GenerateContractParticipants_Patch
    {
        public static void Prefix(FactionDef employer, StarSystemDef system)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            if (system.Tags.Contains("planet_region_hyadesrim") && (system.ownerID == "NoFaction" || system.ownerID == "Locals"))
                return;

            FactionEnemyHolder.Clear();
            var NewEnemies = system.contractTargetIDs;
            FactionEnemyHolder = employer.Enemies.ToList();
            var NewFactionEnemies = FactionEnemyHolder;
            foreach (var Enemy in NewEnemies)
            {
                if (!NewFactionEnemies.Contains(Enemy) && !employer.Allies.Contains(Enemy) && Enemy != employer.FactionValue.Name &&
                    !Settings.ImmuneToWar.Contains(Enemy))
                {
                    NewFactionEnemies.Add(Enemy);
                }
            }
            foreach (var faction in Settings.DefensiveFactions)
            {
                if (!NewFactionEnemies.Contains(faction) && faction != employer.FactionValue.Name)
                {
                    if (!Settings.ImmuneToWar.Contains(faction))
                        NewFactionEnemies.Add(faction);
                }
            }
            if (system.OwnerValue.Name == WarStatus.ComstarAlly && employer.Name != WarStatus.ComstarAlly)
                NewFactionEnemies.Add(Settings.GaW_Police);
            if (employer.Name == Settings.GaW_Police && NewFactionEnemies.Contains(WarStatus.ComstarAlly))
                NewFactionEnemies.Remove(WarStatus.ComstarAlly);

            Traverse.Create(employer).Property("Enemies").SetValue(NewFactionEnemies.ToArray());
        }

        public static void Postfix(FactionDef employer, ref WeightedList<SimGameState.ContractParticipants> __result)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            Traverse.Create(employer).Property("Enemies").SetValue(FactionEnemyHolder.ToArray());
            var type = __result.Type;
            __result = __result.Distinct().ToWeightedList(type);
        }
    }


    public static void AdjustDeathList(DeathListTracker deathListTracker, SimGameState sim, bool ReloadFromSave)
    {
        var deathList = deathListTracker.deathList;
        var deathListFaction = deathListTracker.faction;
        var factionDef = sim.GetFactionDef(deathListFaction);
        var enemies = new List<string>(factionDef.Enemies);
        var allies = new List<string>(factionDef.Allies);

        if (WarStatus.InactiveTHRFactions.Contains(deathListFaction) || WarStatus.NeverControl.Contains(deathListFaction))
            return;
        
        //Check to see if it is an ally or enemy of itself and remove it if so.
        if (deathList.ContainsKey(deathListTracker.faction))
        {
            deathList.Remove(deathListTracker.faction);
            if (allies.Contains(deathListTracker.faction))
            {
                allies.Remove(deathListTracker.faction);
                Traverse.Create(factionDef).Property("Allies").SetValue(allies.ToArray());
            }
            if (enemies.Contains(deathListTracker.faction))
            {
                enemies.Remove(deathListTracker.faction);
                Traverse.Create(factionDef).Property("Enemies").SetValue(enemies.ToArray());
            }
        }

        var KL_List = new List<string>(deathList.Keys);
        var warFaction = WarStatus.warFactionTracker.Find(x => x.faction == deathListTracker.faction);
        bool HasEnemy = false;
        //Defensive Only factions are always neutral
        Settings.DefensiveFactions.Do(x => deathList[x] = 50);
        foreach (string faction in KL_List.Except(Settings.DefensiveFactions))
        {
            if (WarStatus.InactiveTHRFactions.Contains(faction) || WarStatus.NeverControl.Contains(faction))
                continue;

            //Check to see if factions are always allied with each other.
            if (Settings.FactionsAlwaysAllies.Keys.Contains(warFaction.faction) && Settings.FactionsAlwaysAllies[warFaction.faction].Contains(faction))
            {
                deathList[faction] = 99;
                continue;
            }

            if (!ReloadFromSave)
            {
                //Factions adjust hatred based upon how much they are being attacked. But there is diminishing returns further from 50.
                int direction = -1;
                if (warFaction.IncreaseAggression.Keys.Contains(faction) && warFaction.IncreaseAggression[faction])
                    direction = 1;
                {
                    if (deathList[faction] > 50)
                        deathList[faction] += direction * (1 - (deathList[faction] - 50) / 50);
                    else if (deathList[faction] <= 50)
                        deathList[faction] += direction * (1 - (50 - deathList[faction]) / 50);
                }

                //Ceiling and floor for faction enmity. 
                if (deathList[faction] > 99)
                    deathList[faction] = 99;

                if (deathList[faction] < 1)
                    deathList[faction] = 1;
            }

            // BUG is this right? dZ - Yes. If the faction target is Pirates, the faction always hates them. Alternatively, if the faction we are checking
            // the Deathlist for is Pirates themselves, we must set everybody else to be an enemy.
            if (faction == "AuriganPirates")
                deathList[faction] = 80;

            if (deathListFaction == "AuriganPirates")
                deathList[faction] = 80;

            if (deathList[faction] > 75)
            {
                if (faction != "AuriganPirates")
                    HasEnemy = true;
                if (!enemies.Contains(faction))
                {
                    enemies.Add(faction);
                    if (enemies.Contains(deathListFaction))
                        enemies.Remove(deathListFaction);
                    if (enemies.Contains("AuriganDirectorate"))
                        enemies.Remove("AuriganDirectorate");
                    Traverse.Create(factionDef).Property("Enemies").SetValue(enemies.ToArray());
                }

                if (allies.Contains(faction))
                {
                    allies.Remove(faction);
                    if (allies.Contains(deathListFaction))
                        allies.Remove(deathListFaction);
                    if (allies.Contains("AuriganDirectorate"))
                        allies.Remove("AuriganDirectorate");
                    Traverse.Create(factionDef).Property("Allies").SetValue(allies.ToArray());
                }
            }

            if (deathList[faction] <= 75 && deathList[faction] > 25)
            {
                if (enemies.Contains(faction))
                {
                    enemies.Remove(faction);
                    if (enemies.Contains(deathListFaction))
                        enemies.Remove(deathListFaction);
                    if (enemies.Contains("AuriganDirectorate"))
                        enemies.Remove("AuriganDirectorate");
                    Traverse.Create(factionDef).Property("Enemies").SetValue(enemies.ToArray());
                }


                if (allies.Contains(faction))
                {
                    allies.Remove(faction);
                    if (allies.Contains(deathListFaction))
                        allies.Remove(deathListFaction);
                    if (allies.Contains("AuriganDirectorate"))
                        allies.Remove("AuriganDirectorate");
                    Traverse.Create(factionDef).Property("Allies").SetValue(allies.ToArray());
                }
            }

            if (deathList[faction] <= 25)
            {
                if (!allies.Contains(faction))
                {
                    allies.Add(faction);
                    if (allies.Contains(deathListFaction))
                        allies.Remove(deathListFaction);
                    if (allies.Contains("AuriganDirectorate"))
                        allies.Remove("AuriganDirectorate");
                    Traverse.Create(factionDef).Property("Allies").SetValue(allies.ToArray());
                }

                if (enemies.Contains(faction))
                {
                    enemies.Remove(faction);
                    if (enemies.Contains(deathListFaction))
                        enemies.Remove(deathListFaction);
                    if (enemies.Contains("AuriganDirectorate"))
                        enemies.Remove("AuriganDirectorate");
                    Traverse.Create(factionDef).Property("Enemies").SetValue(enemies.ToArray());
                }
            }
        }

        if (!HasEnemy)
        {
            var rand = Random.Next(0, IncludedFactions.Count());
            var NewEnemy =  IncludedFactions[rand];

            while (NewEnemy == deathListFaction || Settings.ImmuneToWar.Contains(NewEnemy) || Settings.DefensiveFactions.Contains(NewEnemy))
            {
                rand = Random.Next(0, IncludedFactions.Count);
                NewEnemy = IncludedFactions[rand];
            }

            if (warFaction.adjacentFactions.Count != 0)
            {
                rand = Random.Next(0, warFaction.adjacentFactions.Count());
                NewEnemy = warFaction.adjacentFactions[rand];
            }
            if (allies.Contains(NewEnemy))
            {
                allies.Remove(NewEnemy);
                if (allies.Contains(deathListFaction))
                    allies.Remove(deathListFaction);
                if (allies.Contains("AuriganDirectorate"))
                    allies.Remove("AuriganDirectorate");
                Traverse.Create(factionDef).Property("Allies").SetValue(allies.ToArray());
            }

            if (!enemies.Contains(NewEnemy))
            {
                enemies.Add(NewEnemy);
                if (enemies.Contains(deathListFaction))
                    enemies.Remove(deathListFaction);
                if (enemies.Contains("AuriganDirectorate"))
                    enemies.Remove("AuriganDirectorate");
                Traverse.Create(factionDef).Property("Enemies").SetValue(enemies.ToArray());
            }
            deathList[NewEnemy] = 80;
        }
    }

    [HarmonyPatch(typeof(SGFactionRelationshipDisplay), "DisplayEnemiesOfFaction")]
    public static class SGFactionRelationShipDisplay_DisplayEnemiesOfFaction_Patch
    {
        public static void Prefix(FactionValue theFaction, SGFactionRelationshipDisplay __instance)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            if (WarStatus.deathListTracker.Find(x => x.faction == theFaction.Name) == null)
                return;

            var deathListTracker = WarStatus.deathListTracker.Find(x => x.faction == theFaction.Name);
            AdjustDeathList(deathListTracker, sim, true);
        }
    }

    [HarmonyPatch(typeof(SGFactionRelationshipDisplay), "DisplayAlliesOfFaction")]
    public static class SGFactionRelationShipDisplay_DisplayAlliesOfFaction_Patch
    {
        public static void Prefix(SGFactionRelationshipDisplay __instance, string theFactionID)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            if (WarStatus.deathListTracker.Find(x => x.faction == theFactionID) == null)
                return;

            var deathListTracker = WarStatus.deathListTracker.Find(x => x.faction == theFactionID);
            AdjustDeathList(deathListTracker, sim, true);
        }
    }

    [HarmonyPatch(typeof(SGCaptainsQuartersReputationScreen), "Init", typeof(SimGameState))]
    public static class SGCQRS_Init_Patch
    {
        public static void Prefix()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            foreach (var theFaction in IncludedFactions)
            {
                if (WarStatus.deathListTracker.Find(x => x.faction == theFaction) == null)
                    continue;

                var deathListTracker = WarStatus.deathListTracker.Find(x => x.faction == theFaction);
                AdjustDeathList(deathListTracker, sim, true);
            }
        }
    }

    [HarmonyPatch(typeof(SGCaptainsQuartersReputationScreen), "RefreshWidgets")]
    public static class SGCQRS_RefreshWidgets_Patch
    {
        public static void Prefix()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            foreach (var theFaction in IncludedFactions)
            {
                if (WarStatus.deathListTracker.Find(x => x.faction == theFaction) == null)
                    continue;

                var deathListTracker = WarStatus.deathListTracker.Find(x => x.faction == theFaction);
                AdjustDeathList(deathListTracker, sim, true);
            }
        }
    }

    public static float GetTotalAttackResources(StarSystem system)
    {
        float result = 0;
        if (system.Tags.Contains("planet_industry_poor"))
            result += Settings.planet_industry_poor;
        if (system.Tags.Contains("planet_industry_mining"))
            result += Settings.planet_industry_mining;
        if (system.Tags.Contains("planet_industry_rich"))
            result += Settings.planet_industry_rich;
        if (system.Tags.Contains("planet_industry_manufacturing"))
            result += Settings.planet_industry_manufacturing;
        if (system.Tags.Contains("planet_industry_research"))
            result += Settings.planet_industry_research;
        if (system.Tags.Contains("planet_other_starleague"))
            result += Settings.planet_other_starleague;

        return result;
    }

    public static float GetTotalDefensiveResources(StarSystem system)
    {
        float result = 0;
        if (system.Tags.Contains("planet_industry_agriculture"))
            result += Settings.planet_industry_agriculture;
        if (system.Tags.Contains("planet_industry_aquaculture"))
            result += Settings.planet_industry_aquaculture;
        if (system.Tags.Contains("planet_other_capital"))
            result += Settings.planet_other_capital;
        if (system.Tags.Contains("planet_other_megacity"))
            result += Settings.planet_other_megacity;
        if (system.Tags.Contains("planet_pop_large"))
            result += Settings.planet_pop_large;
        if (system.Tags.Contains("planet_pop_medium"))
            result += Settings.planet_pop_medium;
        if (system.Tags.Contains("planet_pop_none"))
            result += Settings.planet_pop_none;
        if (system.Tags.Contains("planet_pop_small"))
            result += Settings.planet_pop_small;
        if (system.Tags.Contains("planet_other_hub"))
            result += Settings.planet_other_hub;
        if (system.Tags.Contains("planet_other_comstar"))
            result += Settings.planet_other_comstar;
        return result;
    }

    [HarmonyPatch(typeof(Contract), "CompleteContract")]
    public static class CompleteContract_Patch
    {
        public static void Postfix(Contract __instance, MissionResult result, bool isGoodFaithEffort)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            teamfaction = __instance.GetTeamFaction("ecc8d4f2-74b4-465d-adf6-84445e5dfc230").Name;
            if (teamfaction == Settings.GaW_Police)
                teamfaction = WarStatus.ComstarAlly;
            enemyfaction = __instance.GetTeamFaction("be77cadd-e245-4240-a93e-b99cc98902a5").Name;
            if (enemyfaction == Settings.GaW_Police)
                enemyfaction = WarStatus.ComstarAlly;
            difficulty = __instance.Difficulty;
            missionResult = result;
            contractType = __instance.Override.ContractTypeValue.Name;
            if (__instance.IsFlashpointContract || __instance.IsFlashpointCampaignContract)
                IsFlashpointContract = true;
            else
                IsFlashpointContract = false;
            
        }

        [HarmonyPatch(typeof(SimGameState), "ResolveCompleteContract")]
        public static class SimGameState_ResolveCompleteContract_Patch
        {
            public static void Postfix(SimGameState __instance)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                    return;

                if (IsFlashpointContract)
                    return;

                var warsystem = WarStatus.systems.Find(x => x.name == __instance.CurSystem.Name);

                if (WarStatus.FlashpointSystems.Contains(warsystem.name))
                    return;
                
                if (missionResult == MissionResult.Victory)
                {
                    double deltaInfluence = 0;
                    if (teamfaction == "AuriganPirates")
                    {
                        deltaInfluence = DeltaInfluence(__instance.CurSystem.Name, difficulty, contractType, enemyfaction, true);
                        warsystem.PirateActivity += (float)deltaInfluence;
                    }
                    else if (enemyfaction == "AuriganPirates")
                    {
                        deltaInfluence = DeltaInfluence(__instance.CurSystem.Name, difficulty, contractType, enemyfaction, true);
                        warsystem.PirateActivity -= (float)deltaInfluence;
                        if (WarStatus.Deployment)
                            WarStatus.PirateDeployment = true;
                    }
                    else
                    {
                        deltaInfluence = DeltaInfluence(__instance.CurSystem.Name, difficulty, contractType, enemyfaction, false);
                        if (!influenceMaxed)
                        {
                            warsystem.influenceTracker[teamfaction] += (float)deltaInfluence;
                            warsystem.influenceTracker[enemyfaction] -= (float)deltaInfluence;
                        }
                        else
                        {
                            warsystem.influenceTracker[teamfaction] += (float)Math.Min(attackerInfluenceHolder, 100 - warsystem.influenceTracker[teamfaction]);
                            warsystem.influenceTracker[enemyfaction] -= (float)deltaInfluence;
                        }
                    }

                    //if (contractType == ContractType.AttackDefend || contractType == ContractType.FireMission)
                    //{
                    //    if (Settings.IncludedFactions.Contains(teamfaction))
                    //    {
                    //        if (!Settings.DefensiveFactions.Contains(teamfaction))
                    //            WarStatus.warFactionTracker.Find(x => x.faction == teamfaction).AttackResources += difficulty;
                    //        else
                    //            WarStatus.warFactionTracker.Find(x => x.faction == teamfaction).DefensiveResources += difficulty;
                    //    }

                    //    if (Settings.IncludedFactions.Contains(enemyfaction))
                    //    {
                    //        WarStatus.warFactionTracker.Find(x => x.faction == enemyfaction).DefensiveResources -= difficulty;
                    //        if (WarStatus.warFactionTracker.Find(x => x.faction == enemyfaction).DefensiveResources < 0)
                    //            WarStatus.warFactionTracker.Find(x => x.faction == enemyfaction).DefensiveResources = 0;
                    //    }
                    //    else if (enemyfaction == "AuriganPirates")
                    //    {
                    //        warsystem.PirateActivity -= difficulty;
                    //        if (warsystem.PirateActivity < 0)
                    //            warsystem.PirateActivity = 0;
                    //    }
                    //}

                    var OldOwner = sim.CurSystem.OwnerValue.Name;
                    if (WillSystemFlip(__instance.CurSystem.Name, teamfaction, enemyfaction, deltaInfluence, false) ||
                        (WarStatus.Deployment && enemyfaction == "AuriganPirates" && warsystem.PirateActivity < 1))
                    {
                        if (WarStatus.Deployment && enemyfaction == "AuriganPirates" && warsystem.PirateActivity < 1)
                        {
                            GameInstance game = LazySingletonBehavior<UnityGameInstance>.Instance.Game;
                            SimGameInterruptManager interruptQueue = (SimGameInterruptManager)AccessTools
                                .Field(typeof(SimGameState), "interruptQueue").GetValue(game.Simulation);
                            interruptQueue.QueueGenericPopup_NonImmediate("ComStar Bulletin: Galaxy at War", __instance.CurSystem.Name + " defended from Pirates! ", true, null);
                        }
                        else
                        {
                            ChangeSystemOwnership(__instance, warsystem.starSystem, teamfaction, false);
                            GameInstance game = LazySingletonBehavior<UnityGameInstance>.Instance.Game;
                            SimGameInterruptManager interruptQueue = (SimGameInterruptManager)AccessTools
                                .Field(typeof(SimGameState), "interruptQueue").GetValue(game.Simulation);
                            interruptQueue.QueueGenericPopup_NonImmediate("ComStar Bulletin: Galaxy at War", __instance.CurSystem.Name + " taken! "
                                + Settings.FactionNames[teamfaction] + " conquered from " + Settings.FactionNames[OldOwner], true, null);

                            if (Settings.HyadesRimCompatible && WarStatus.InactiveTHRFactions.Contains(teamfaction))
                                WarStatus.InactiveTHRFactions.Remove(teamfaction);
                        }

                        if (WarStatus.HotBox.Contains(sim.CurSystem.Name))
                        {
                            if (WarStatus.Deployment)
                            {
                                int difficultyScale = warsystem.DeploymentTier;
                                if (difficultyScale == 6)
                                    sim.InterruptQueue.QueueRewardsPopup(Settings.DeploymentReward_06);
                                else if (difficultyScale == 5)
                                    sim.InterruptQueue.QueueRewardsPopup(Settings.DeploymentReward_05);
                                else if (difficultyScale == 4)
                                    sim.InterruptQueue.QueueRewardsPopup(Settings.DeploymentReward_04);
                                else if (difficultyScale == 3)
                                    sim.InterruptQueue.QueueRewardsPopup(Settings.DeploymentReward_03);
                                else if (difficultyScale == 2)
                                    sim.InterruptQueue.QueueRewardsPopup(Settings.DeploymentReward_02);
                                else
                                    sim.InterruptQueue.QueueRewardsPopup(Settings.DeploymentReward_01);
                            }

                            WarStatus.JustArrived = false;
                            WarStatus.HotBoxTravelling = false;
                            WarStatus.Escalation = false;
                            WarStatus.HotBox.Clear();
                            WarStatus.EscalationDays = 0;
                            warsystem.BonusCBills = false;
                            warsystem.BonusSalvage = false;
                            warsystem.BonusXP = false;
                            WarStatus.Deployment = false;
                            WarStatus.DeploymentInfluenceIncrease = 1.0;
                            WarStatus.PirateDeployment = false;
                            if (WarStatus.EscalationOrder != null)
                            {
                                WarStatus.EscalationOrder.SetCost(0);
                                TaskManagementElement taskManagementElement4 = null;
                                TaskTimelineWidget timelineWidget = (TaskTimelineWidget)AccessTools.Field(typeof(SGRoomManager), "timelineWidget").GetValue(__instance.RoomManager);
                                Dictionary<WorkOrderEntry, TaskManagementElement> ActiveItems =
                                    (Dictionary<WorkOrderEntry, TaskManagementElement>)AccessTools.Field(typeof(TaskTimelineWidget), "ActiveItems").GetValue(timelineWidget);
                                if (ActiveItems.TryGetValue(WarStatus.EscalationOrder, out taskManagementElement4))
                                {
                                    taskManagementElement4.UpdateItem(0);
                                }
                            }
                        }

                        foreach (var system in WarStatus.SystemChangedOwners)
                        {
                            var systemStatus = WarStatus.systems.Find(x => x.name == system);
                            systemStatus.CurrentlyAttackedBy.Clear();
                            CalculateAttackAndDefenseTargets(systemStatus.starSystem);
                            RefreshContracts(systemStatus.starSystem);
                        }
                        WarStatus.SystemChangedOwners.Clear();

                        bool HasFlashpoint = false;
                        foreach (var contract in __instance.CurSystem.SystemContracts)
                        {
                            if (contract.IsFlashpointContract || contract.IsFlashpointCampaignContract)
                                HasFlashpoint = true;
                        }
                        if (!HasFlashpoint)
                        {
                            NeedsProcessing = true;
                            var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                            __instance.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                            NeedsProcessing = false;
                        }

                        __instance.StopPlayMode();
                    }
                }
            }
        }
    }

    internal static Stopwatch timer = new Stopwatch();
    public static void SystemDifficulty()
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        var TotalSystems = WarStatus.systems.Count;
        var DifficultyCutoff = TotalSystems / 10;
        int i = 0;
        
        foreach (var system in WarStatus.systems.OrderBy(x => x.TotalResources))
        {
            var SimSystem = sim.StarSystems.Find(x => x.Name == system.name);

            //Define the original owner of the system for revolt purposes.
            if (system.OriginalOwner == null)
                system.OriginalOwner = system.owner;

            if (Settings.ChangeDifficulty && !SimSystem.Tags.Contains("planet_start_world"))
            {
                sim.Constants.Story.ContractDifficultyMod = 0;
                sim.CompanyStats.Set<float>("Difficulty", 0);
                if (i <= DifficultyCutoff)
                {
                    system.DifficultyRating = 1;
                }
                if (i <= DifficultyCutoff * 2 && i > DifficultyCutoff)
                {
                    system.DifficultyRating = 2;
                }
                if (i <= DifficultyCutoff * 3 && i > 2 * DifficultyCutoff)
                {
                    system.DifficultyRating = 3;
                }
                if (i <= DifficultyCutoff * 4 && i > 3 * DifficultyCutoff)
                {
                    system.DifficultyRating = 4;
                }
                if (i <= DifficultyCutoff * 5 && i > 4 * DifficultyCutoff)
                {
                    system.DifficultyRating = 5;
                }
                if (i <= DifficultyCutoff * 6 && i > 5 * DifficultyCutoff)
                {
                    system.DifficultyRating = 6;
                }
                if (i <= DifficultyCutoff * 7 && i > 6 * DifficultyCutoff)
                {
                    system.DifficultyRating = 7;
                }
                if (i <= DifficultyCutoff * 8 && i > 7 * DifficultyCutoff)
                {
                    system.DifficultyRating = 8;
                }
                if (i <= DifficultyCutoff * 9 && i > 8 * DifficultyCutoff)
                {
                    system.DifficultyRating = 9;
                }
                if (i > 9 * DifficultyCutoff)
                {
                    system.DifficultyRating = 10;
                }
                i++;

                var amount = system.DifficultyRating;
                var difficultyList = new List<int> {amount, amount};
                AccessTools.FieldRefAccess<StarSystemDef, List<int>>("DifficultyList")(SimSystem.Def) = difficultyList;
                AccessTools.FieldRefAccess<StarSystemDef, int>("DefaultDifficulty")(SimSystem.Def) = amount; 
            }
            else
            {
                system.DifficultyRating = SimSystem.Def.DefaultDifficulty;
                i++;
            }
            if (SimSystem.Def.OwnerValue.Name != "NoFaction" && SimSystem.Def.SystemShopItems.Count == 0)
            {
                LogDebug("SystemDifficulty fix entry " + timer.Elapsed);
                List<string> TempList = new List<string>();
                TempList.Add("itemCollection_minor_Locals");
                Traverse.Create(SimSystem.Def).Property("SystemShopItems").SetValue(TempList);
                if (sim.CurSystem.Name == SimSystem.Def.Description.Name)
                {
                    Shop.RefreshType refreshShop = Shop.RefreshType.RefreshIfEmpty;
                    SimSystem.SystemShop.Rehydrate(sim, SimSystem, SimSystem.Def.SystemShopItems, refreshShop,
                        Shop.ShopType.System);
                }
            }
        }
    }
    //82 characters per line.
    public static void GaW_Notification()
    {
        //SimGameResultAction simGameResultAction = new SimGameResultAction();
        //simGameResultAction.Type = SimGameResultAction.ActionType.System_ShowSummaryOverlay;
        //simGameResultAction.value = Strings.T("Galaxy at War");
        //simGameResultAction.additionalValues = new string[1];
        //simGameResultAction.additionalValues[0] = Strings.T("In Galaxy at War, the Great Houses of the Inner Sphere will not simply wait for a wedding invitation" +
        //                                                    " to show their disdain for each other. To that end, war will break out as petty bickering turns into all out conflict. Your reputation with the factions" +
        //                                                    " is key - the more they like you, the more they'll bring you to the front lines and the greater the rewards. Perhaps an enterprising mercenary could make their" +
        //                                                    " fortune changing the tides of battle and helping a faction dominate the Inner Sphere.\n\n <b>New features in Galaxy at War:</b>" +
        //                                                    "\n• Each planet generates Attack Resources and Defensive Resources that they will be constantly " +
        //                                                    "spending to spread their influence and protect their own systems." +
        //                                                    "\n• Planetary Resources and Faction Influence can be seen on the Star Map by hovering over any system." +
        //                                                    "\n• Successfully completing missions will swing the influence towards the Faction granting the contract." +
        //                                                    "\n• Target Acquisition Missions & Attack and Defend Missions will give a permanent bonus to the winning faction's Attack Resources and a permanent deduction to the losing faction's Defensive Resources." +
        //                                                    "\n• If you accept a travel contract the Faction will blockade the system for 30 days. A bonus will be granted for every mission you complete within that system during that time." +
        //                                                    "\n• Pirates are active and will reduce Resources in a system. High Pirate activity will be highlighted in red." +
        //                                                    "\n• Sumire will flag the systems in purple on the Star Map that are the most valuable local targets." +
        //                                                    "\n• Sumire will also highlight systems in yellow that have changed ownership during the previous month." +
        //                                                    "\n• Hitting Control-R will bring up a summary of the Faction's relationships and their overall war status." +
        //                                                    "\n\n****Press Enter to Continue****");


        //SimGameState.ApplyEventAction(simGameResultAction, null);
        //UnityGameInstance.BattleTechGame.Simulation.StopPlayMode();
    }

    [HarmonyPatch(typeof(SGContractsWidget), "GetContractComparePriority")]
    public static class SGContractsWidget_GetContractComparePriority_Patch
    {
        static bool Prefix(ref int __result, Contract contract)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return true;

            int difficulty = contract.Override.GetUIDifficulty();
            int result = 100;
            var Sim = UnityGameInstance.BattleTechGame.Simulation;
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
                    result = difficulty + 21;
            }
            else
                result = difficulty + 31;

            __result = result;
            return false;
        }
    }

    public static void RecalculateSystemInfluence(SystemStatus systemStatus, string NewOwner, string OldOwner)
    {
        systemStatus.influenceTracker.Clear();
        systemStatus.influenceTracker.Add(NewOwner, Settings.DominantInfluence);
        systemStatus.influenceTracker.Add(OldOwner, Settings.MinorInfluencePool);

        foreach (var faction in IncludedFactions)
        {
            if (!systemStatus.influenceTracker.Keys.Contains(faction))
                systemStatus.influenceTracker.Add(faction, 0);
        }
    }

    //Show on the Contract Description how this will impact the war. 
    [HarmonyPatch(typeof(SGContractsWidget), "PopulateContract")]
    public static class SGContractsWidget_PopulateContract_Patch
    {
        public static void Prefix(ref Contract contract, ref string __state)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            if (contract.IsFlashpointContract || contract.IsFlashpointCampaignContract)
                return;

            __state = contract.Override.shortDescription;
            var StringHolder = contract.Override.shortDescription;
            var EmployerFaction = contract.GetTeamFaction("ecc8d4f2-74b4-465d-adf6-84445e5dfc230").Name;
            if (EmployerFaction == Settings.GaW_Police)
                EmployerFaction = WarStatus.ComstarAlly;
            var DefenseFaction = contract.GetTeamFaction("be77cadd-e245-4240-a93e-b99cc98902a5").Name;
            if (DefenseFaction == Settings.GaW_Police)
                DefenseFaction = WarStatus.ComstarAlly;

            var TargetSystem = contract.TargetSystem;
            var SystemName = sim.StarSystems.Find(x => x.ID == TargetSystem);

            bool pirates = false;
            if (EmployerFaction == "AuriganPirates" || DefenseFaction == "AuriganPirates")
                pirates = true;

            double DeltaInfluence = Core.DeltaInfluence(SystemName.Name, contract.Difficulty, contract.Override.ContractTypeValue.Name, DefenseFaction, pirates);

            bool SystemFlip = false;
            if (EmployerFaction != "AuriganPirates" && DefenseFaction != "AuriganPirates")
                SystemFlip = WillSystemFlip(SystemName.Name, EmployerFaction, DefenseFaction, DeltaInfluence, true);

            string AttackerString = Settings.FactionNames[EmployerFaction] + ": +" + DeltaInfluence;
            string DefenderString = Settings.FactionNames[DefenseFaction] + ": -" + DeltaInfluence;

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

            var system = WarStatus.systems.Find(x => x.name == SystemName.Name);

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
                int estimatedMissions = CalculateFlipMissions(EmployerFaction, SystemName.Name);
                int totalDifficulty = 1;

                if (Settings.ChangeDifficulty)
                    totalDifficulty = estimatedMissions * SystemName.Def.GetDifficulty(SimGameState.SimGameType.CAREER);
                else
                    totalDifficulty = estimatedMissions * (int)(SystemName.Def.DefaultDifficulty + sim.GlobalDifficulty);

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
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            contract.Override.shortDescription = __state;
        }
    }


    internal static double DeltaInfluence(string system, double contractDifficulty, string contractTypeID, string DefenseFaction, bool PiratesInvolved)
    {
        var TargetSystem = WarStatus.systems.Find(x => x.name == system);
        float MaximumInfluence = 100;

        if (PiratesInvolved && DefenseFaction == "AuriganPirates")
            MaximumInfluence = TargetSystem.PirateActivity;
        else if (PiratesInvolved)
            MaximumInfluence = 100 - TargetSystem.PirateActivity;
        else
            MaximumInfluence = TargetSystem.influenceTracker[DefenseFaction];

        double InfluenceChange = 1;
        contractDifficulty = Mathf.Max((int)contractDifficulty, TargetSystem.DifficultyRating);

        //If contracts are not properly designed, this provides a failsafe.
        try
        {
            InfluenceChange = (11 + contractDifficulty - 2 * TargetSystem.DifficultyRating) * Settings.ContractImpact[contractTypeID] / Settings.InfluenceDivisor;
        }
        catch
        {
            InfluenceChange = (11 + contractDifficulty - 2 * TargetSystem.DifficultyRating) / Settings.InfluenceDivisor;
        }

        //Log("System Delta Influence");
        //Log(TargetSystem.name);
        //Log(WarStatus.DeploymentInfluenceIncrease.ToString());
        //Log(contractDifficulty.ToString());
        //Log(TargetSystem.DifficultyRating.ToString());
        //Log(Settings.InfluenceDivisor.ToString());
        //Log(InfluenceChange.ToString());


        if (PiratesInvolved)
            InfluenceChange *= 2;
        InfluenceChange = WarStatus.DeploymentInfluenceIncrease * Math.Max(InfluenceChange, 0.5);
        if (InfluenceChange > MaximumInfluence && !PiratesInvolved)
        {
            attackerInfluenceHolder = InfluenceChange;
            attackerInfluenceHolder = Math.Round(attackerInfluenceHolder, 1);
            influenceMaxed = true;
        }
        else
            influenceMaxed = false;

        InfluenceChange = Math.Min(InfluenceChange, MaximumInfluence);
        InfluenceChange = Math.Round(InfluenceChange, 1);
        //Log(InfluenceChange.ToString());
        //Log("--------------------------");
        return InfluenceChange;
    }

    internal static bool WillSystemFlip(string system, string Winner, string Loser, double deltainfluence, bool PreBattle)
    {
        var Sim = UnityGameInstance.BattleTechGame.Simulation;
        var warsystem = WarStatus.systems.Find(x => x.name == system);
        var tempIT = new Dictionary<string, float>(warsystem.influenceTracker);

        if (PreBattle && !influenceMaxed)
        {
            tempIT[Winner] += (float)deltainfluence;
            tempIT[Loser] -= (float)deltainfluence;
        }
        else if (PreBattle && influenceMaxed)
        {
            tempIT[Winner] += (float)Math.Min(attackerInfluenceHolder, 100 - tempIT[Winner]);
            tempIT[Loser] -= (float)deltainfluence;
        }
        var highKey = tempIT.OrderByDescending(x => x.Value).Select(x => x.Key).First();
        var highValue = tempIT.OrderByDescending(x => x.Value).Select(x => x.Value).First();
        tempIT.Remove(highKey);
        var secondValue = tempIT.OrderByDescending(x => x.Value).Select(x => x.Value).First();

        if (highKey != warsystem.owner && highKey == Winner && highValue - secondValue > Settings.TakeoverThreshold
            && !WarStatus.FlashpointSystems.Contains(system) && 
            (!Settings.DefensiveFactions.Contains(Winner) && !Settings.ImmuneToWar.Contains(Loser)))
            return true;
        return false;
    }

    internal static int CalculateFlipMissions(string attacker, string system)
    {
        var Sim = UnityGameInstance.BattleTechGame.Simulation;
        var warsystem = WarStatus.systems.Find(x => x.name == system);
        var tempIT = new Dictionary<string, float>(warsystem.influenceTracker);
        int MissionCounter = 0;
        var influenceDifference = 0.0f;
        double contractDifficulty = WarStatus.systems.Find(x => x.name == system).DifficultyRating;
        var DeploymentIFHolder = WarStatus.DeploymentInfluenceIncrease;
        WarStatus.DeploymentInfluenceIncrease = 1;

        while (influenceDifference <= Settings.TakeoverThreshold)
        {
            float defenseInfluence = 0;
            string defenseFaction = "";
            foreach (var faction in tempIT.OrderByDescending(x => x.Value))
            {
                if (faction.Key != attacker)
                {
                    defenseFaction = faction.Key;
                    defenseInfluence = faction.Value;
                    break;
                }
            }

            var influenceChange = DeltaInfluence(system, contractDifficulty, "CaptureBase", defenseFaction, false);
            tempIT[attacker] += (float)influenceChange;
            tempIT[defenseFaction] -= (float)influenceChange;
            influenceDifference = tempIT[attacker] - tempIT[defenseFaction];
            WarStatus.DeploymentInfluenceIncrease *= Settings.DeploymentEscalationFactor;
            MissionCounter++;
        }
        WarStatus.DeploymentInfluenceIncrease = DeploymentIFHolder;
        return MissionCounter;
    }

    //Logging a part of contract generation to see if I can track down an infinite load problem.
    [HarmonyPatch(typeof(SimGameState), "FillMapEncounterContractData")]
    public static class MainMenu_Init_Patch
    {
        static bool Prefix()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return true;

            new WaitForSeconds(0.2f);
            if (LoopCounter >= 100)
            {
                LoopCounter = 0;
                return false;
            }
            LoopCounter++;
            return true;
            //Log("-------------------");
            //Log("Stuck in here?");
            //Log(system.Name);
            //foreach (var i in potentialContracts)
            //    Log(potentialContracts[i.Key].Count().ToString());
            //foreach (var targets in validTargets)
            //{
            //    Log(targets.Key);
            //    foreach (var participant in validTargets[targets.Key])
            //        Log("    " + participant.Target.Name);
            //}
            //LoopCounter++;
            //return true;
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
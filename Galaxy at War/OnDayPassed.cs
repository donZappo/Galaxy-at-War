using System.Linq;
using BattleTech;
using Harmony;
using static GalaxyatWar.Logger;
using static GalaxyatWar.Helpers;
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global


namespace GalaxyatWar
{
    public class OnDayPassed
    {
        [HarmonyPatch(typeof(SimGameState), "OnDayPassed")]
        public static class SimGameStateOnDayPassedPatch
        {
            private static void Prefix()
            {
                LogDebug("OnDayPassed");
                var starSystem = Globals.Sim.CurSystem;
                var contractEmployers = starSystem.Def.contractEmployerIDs;
                var contractTargets = starSystem.Def.contractTargetIDs;
                var owner = starSystem.OwnerValue;
                LogDebug($"{starSystem.Name} owned by {owner.Name}");
                LogDebug($"Employers in {starSystem.Name}");
                contractEmployers.Do(x => LogDebug($"  {x}"));
                LogDebug($"Targets in {starSystem.Name}");
                contractTargets.Do(x => LogDebug($"  {x}"));
                Globals.Sim.GetAllCurrentlySelectableContracts().Do(x => LogDebug($"{x.Name,-25} {x.Difficulty} ({x.Override.GetUIDifficulty()})"));
                var systemStatus = Globals.WarStatusTracker.systems.Find(x => x.starSystem == starSystem);
                var employers = systemStatus.influenceTracker.OrderByDescending(x=> x.Value).Select(x => x.Key).Take(2); 
                foreach (var faction in Globals.Settings.IncludedFactions.Intersect(employers))
                {
                    LogDebug($"{faction} Enemies:");
                    FactionEnumeration.GetFactionByName(faction).factionDef?.Enemies.Distinct().Do(x => LogDebug($"  {x}"));
                    LogDebug($"{faction} Allies:");
                    FactionEnumeration.GetFactionByName(faction).factionDef?.Allies.Do(x => LogDebug($"  {x}"));
                    //Log("");
                }
                LogDebug("Player allies:");
                foreach (var faction in Globals.Sim.AlliedFactions)
                {
                    LogDebug($"  {faction}");
                }
                
                if (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                    return;

                Globals.WarStatusTracker.CurSystem = Globals.Sim.CurSystem.Name;
                if (Globals.WarStatusTracker.HotBox.Contains(Globals.Sim.CurSystem.Name) && !Globals.WarStatusTracker.HotBoxTravelling)
                {
                    Globals.WarStatusTracker.EscalationDays--;

                    if (!Globals.WarStatusTracker.Deployment)
                    {
                        if (Globals.WarStatusTracker.EscalationDays == 0)
                        {
                            HotSpots.CompleteEscalation();
                        }

                        if (Globals.WarStatusTracker.EscalationOrder != null)
                        {
                            Globals.WarStatusTracker.EscalationOrder.PayCost(1);
                            var activeItems = Globals.TaskTimelineWidget.ActiveItems;
                            if (activeItems.TryGetValue(Globals.WarStatusTracker.EscalationOrder, out var taskManagementElement4))
                            {
                                taskManagementElement4.UpdateItem(0);
                            }
                        }
                    }
                    else
                    {
                        if (Globals.WarStatusTracker.EscalationOrder != null)
                        {
                            Globals.WarStatusTracker.EscalationOrder.PayCost(1);
                            var activeItems = Globals.TaskTimelineWidget.ActiveItems;
                            if (activeItems.TryGetValue(Globals.WarStatusTracker.EscalationOrder, out var taskManagementElement4))
                            {
                                taskManagementElement4.UpdateItem(0);
                            }
                        }

                        if (Globals.WarStatusTracker.EscalationDays <= 0)
                        {
                            Globals.Sim.StopPlayMode();

                            Globals.Sim.CurSystem.activeSystemContracts.Clear();
                            Globals.Sim.CurSystem.activeSystemBreadcrumbs.Clear();
                            HotSpots.TemporaryFlip(Globals.Sim.CurSystem, Globals.WarStatusTracker.DeploymentEmployer);

                            var maxHolder = Globals.Sim.CurSystem.CurMaxBreadcrumbs;
                            var rand = Globals.Rng.Next(1, (int) Globals.Settings.DeploymentContracts);

                            Traverse.Create(Globals.Sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(rand);
                            Globals.Sim.GeneratePotentialContracts(true, null, Globals.Sim.CurSystem);
                            Traverse.Create(Globals.Sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(maxHolder);

                            Globals.Sim.QueueCompleteBreadcrumbProcess(true);
                            Globals.SimGameInterruptManager.QueueTravelPauseNotification("New Mission", "Our Employer has launched an attack. We must take a mission to support their operation. Let's check out our contracts and get to it!", Globals.Sim.GetCrewPortrait(SimGameCrew.Crew_Darius),
                                string.Empty, null, "Proceed");
                        }
                    }
                }

                if (!Globals.WarStatusTracker.StartGameInitialized)
                {
                    LogDebug("Reinitializing contracts because !StartGameInitialized");
                    var cmdCenter = Globals.Sim.RoomManager.CmdCenterRoom;
                    Globals.Sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                    Globals.WarStatusTracker.StartGameInitialized = true;
                }
            }

            public static void Postfix()
            {
                if (Globals.WarStatusTracker == null || Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                    return;

                if (!Globals.WarStatusTracker.GaW_Event_PopUp)
                {
                    GaW_Notification();
                    Globals.WarStatusTracker.GaW_Event_PopUp = true;
                }

                //TEST: run 100 WarTicks and stop
                if (Globals.Settings.LongWarTesting)
                {
                    LogDebug("LongWarTesting underway...");
                    for (var i = 0; i < Globals.Settings.LongWarMonths; i++)
                    {
                        WarTick.Tick(true, false);
                        WarTick.Tick(true, false);
                        WarTick.Tick(true, false);
                        WarTick.Tick(true, true);
                    }

                    Globals.Sim.StopPlayMode();
                    //return;
                }

                //Remove systems from the protected pool.
                foreach (var tag in Globals.Sim.CompanyTags)
                {
                    if (Globals.Settings.FlashpointReleaseSystems.Keys.Contains(tag))
                    {
                        if (Globals.WarStatusTracker.FlashpointSystems.Contains(Globals.Settings.FlashpointReleaseSystems[tag]))
                            Globals.WarStatusTracker.FlashpointSystems.Remove(Globals.Settings.FlashpointReleaseSystems[tag]);
                    }
                }

                if (Globals.Sim.DayRemainingInQuarter % Globals.Settings.WarFrequency == 0)
                {
                    //LogDebug(">>> PROC");
                    if (Globals.Sim.DayRemainingInQuarter != 30)
                    {
                        WarTick.Tick(false, false);
                    }
                    else
                    {
                        //GenerateMonthlyContracts();
                        WarTick.Tick(true, true);
                        var hasFlashPoint = Globals.Sim.CurSystem.SystemContracts.Any(x => x.IsFlashpointContract || x.IsFlashpointCampaignContract);
                        if (!Globals.WarStatusTracker.HotBoxTravelling && !Globals.WarStatusTracker.HotBox.Contains(Globals.Sim.CurSystem.Name) && !hasFlashPoint)
                        {
                            LogDebug("Regenerating contracts because month-end.");
                            var cmdCenter = Globals.Sim.RoomManager.CmdCenterRoom;
                            Globals.Sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                        }
                    }

                    LogDebug(">>> DONE PROC");
                }

                ////Variable daily testing zone.
                //foreach (var x in Globals.WarStatusTracker.systems)
                //{
                //    Logger.Log("=========================");
                //    Logger.Log(x.CoreSystemID);
                //    Logger.Log(x.PirateActivity.ToString());
                //    if (x.PirateActivity > 100f)
                //        Logger.Log("EXCESS PIRATE ACTIVITY");
                //}
            }
        }
    }
}

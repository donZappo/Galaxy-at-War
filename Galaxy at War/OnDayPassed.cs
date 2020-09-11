using System.Linq;
using BattleTech;
using Harmony;
using static GalaxyatWar.Globals;
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
                if (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete"))
                    return;

                WarStatusTracker.CurSystem = Sim.CurSystem.Name;
                LogDebug($"HotBox contains CurSystem? {WarStatusTracker.HotBox.Contains(Sim.CurSystem.Name)}");
                LogDebug($"Is it a Deployment? {WarStatusTracker.Deployment}");
                LogDebug($"HotBox Travelling? {WarStatusTracker.HotBoxTravelling}");
                if (WarStatusTracker.HotBox.Contains(Sim.CurSystem.Name) && !WarStatusTracker.HotBoxTravelling)
                {
                    WarStatusTracker.EscalationDays--;

                    if (!WarStatusTracker.Deployment)
                    {
                        if (WarStatusTracker.EscalationDays == 0)
                        {
                            HotSpots.CompleteEscalation();
                        }

                        if (WarStatusTracker.EscalationOrder != null)
                        {
                            WarStatusTracker.EscalationOrder.PayCost(1);
                            var activeItems = TaskTimelineWidget.ActiveItems;
                            if (activeItems.TryGetValue(WarStatusTracker.EscalationOrder, out var taskManagementElement4))
                            {
                                taskManagementElement4.UpdateItem(0);
                            }
                        }
                    }
                    else
                    {
                        if (WarStatusTracker.EscalationOrder != null)
                        {
                            WarStatusTracker.EscalationOrder.PayCost(1);
                            var activeItems = TaskTimelineWidget.ActiveItems;
                            if (activeItems.TryGetValue(WarStatusTracker.EscalationOrder, out var taskManagementElement4))
                            {
                                taskManagementElement4.UpdateItem(0);
                            }
                        }

                        if (WarStatusTracker.EscalationDays <= 0)
                        {
                            Sim.StopPlayMode();

                            Sim.CurSystem.activeSystemContracts.Clear();
                            Sim.CurSystem.activeSystemBreadcrumbs.Clear();
                            HotSpots.TemporaryFlip(Sim.CurSystem, WarStatusTracker.DeploymentEmployer);

                            var maxHolder = Sim.CurSystem.CurMaxBreadcrumbs;
                            var rand = Rng.Next(1, (int) Settings.DeploymentContracts);

                            Traverse.Create(Sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(rand);
                            Sim.GeneratePotentialContracts(true, null, Sim.CurSystem);
                            Traverse.Create(Sim.CurSystem).Property("CurMaxBreadcrumbs").SetValue(maxHolder);

                            Sim.QueueCompleteBreadcrumbProcess(true);
                            SimGameInterruptManager.QueueTravelPauseNotification("New Mission", "Our Employer has launched an attack. We must take a mission to support their operation. Let's check out our contracts and get to it!", Sim.GetCrewPortrait(SimGameCrew.Crew_Darius),
                                string.Empty, null, "Proceed");
                        }
                    }
                }

                if (!WarStatusTracker.StartGameInitialized)
                {
                    var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                    Sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                    WarStatusTracker.StartGameInitialized = true;
                }
            }

            public static void Postfix()
            {
                if (WarStatusTracker == null || Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete"))
                    return;

                if (!WarStatusTracker.GaW_Event_PopUp)
                {
                    GaW_Notification();
                    WarStatusTracker.GaW_Event_PopUp = true;
                }

                //TEST: run 100 WarTicks and stop
                if (Settings.LongWarTesting)
                {
                    LogDebug("LongWarTesting underway...");
                    for (var i = 0; i < 100; i++)
                    {
                        WarTick.Tick(true, false);
                        WarTick.Tick(true, false);
                        WarTick.Tick(true, false);
                        WarTick.Tick(true, true);
                    }

                    Sim.StopPlayMode();
                    return;
                }

                //Remove systems from the protected pool.
                foreach (var tag in Sim.CompanyTags)
                {
                    if (Settings.FlashpointReleaseSystems.Keys.Contains(tag))
                    {
                        if (WarStatusTracker.FlashpointSystems.Contains(Settings.FlashpointReleaseSystems[tag]))
                            WarStatusTracker.FlashpointSystems.Remove(Settings.FlashpointReleaseSystems[tag]);
                    }
                }

                if (Sim.DayRemainingInQuarter % Settings.WarFrequency == 0)
                {
                    //LogDebug(">>> PROC");
                    if (Sim.DayRemainingInQuarter != 30)
                    {
                        WarTick.Tick(false, false);
                    }
                    else
                    {
                        WarTick.Tick(true, true);
                        var hasFlashPoint = Sim.CurSystem.SystemContracts.Any(x => x.IsFlashpointContract || x.IsFlashpointCampaignContract);
                        if (!WarStatusTracker.HotBoxTravelling && !WarStatusTracker.HotBox.Contains(Sim.CurSystem.Name) && !hasFlashPoint)
                        {
                            var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                            Sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                        }
                    }

                    LogDebug(">>> DONE PROC");
                }
            }
        }
    }
}

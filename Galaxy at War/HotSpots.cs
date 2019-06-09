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

namespace Galaxy_at_War
{
    class HotSpots
    {
        public static List<string> contendedSystems = new List<string>();
        public static List<string> BCTargets = new List<string>();

        [HarmonyPatch(typeof(SimGameState), "StartGeneratePotentialContractsRoutine")]
        public static class SimGameState_StartGeneratePotentialContractsRoutine_Patch
        {
            static void Prefix(SimGameState __instance, ref StarSystem systemOverride)
            {
                string BCTarget = BCTargets[0];
                BCTargets.RemoveAt(0);
                try
                {
                    var usingBreadcrumbs = systemOverride != null;
                    if (usingBreadcrumbs)
                        systemOverride = __instance.StarSystems.Find(x => x.Name == BCTarget);
                }
                catch (Exception e)
                {
                    Error(e);
                }
            }
        }
        public static void ProcessHotSpots()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var DominantFaction = sim.CurSystem.Owner;
            var warFaction = Core.WarStatus.warFactionTracker.Find(x => x.faction == DominantFaction);
            var FullPriorityList = new Dictionary<StarSystem, float>();

            contendedSystems.Clear();
            foreach (SystemStatus systemStatus in Core.WarStatus.systems)
            {
                systemStatus.PriorityAttack = false;
                systemStatus.PriorityDefense = false;

                if (systemStatus.owner == DominantFaction && systemStatus.Contended)
                {
                    systemStatus.PriorityDefense = true;
                    if (!FullPriorityList.Keys.Contains(systemStatus.starSystem))
                        FullPriorityList.Add(systemStatus.starSystem, systemStatus.TotalResources);
                }
                if (Core.Settings.DefensiveFactions.Contains(DominantFaction)) continue;
                foreach (var targetFaction in warFaction.attackTargets.Keys)
                {
                    var factionDLT = Core.WarStatus.deathListTracker.Find(x => x.faction == warFaction.faction);
                    if (factionDLT.deathList[targetFaction] < Core.Settings.PriorityHatred) continue;
                    if (warFaction.attackTargets[targetFaction].Contains(systemStatus.starSystem))
                    {
                        systemStatus.PriorityAttack = true;
                        if (!FullPriorityList.Keys.Contains(systemStatus.starSystem))
                            FullPriorityList.Add(systemStatus.starSystem, systemStatus.TotalResources);
                    }
                }
            }
            warFaction.PriorityList.Clear();
            int i = 0;
            while (i < 6 && FullPriorityList.Count != 0)
            {
                var highKey = FullPriorityList.OrderByDescending(x => x.Value).Select(x => x.Key).First();
                warFaction.PriorityList.Add(highKey.Name);
                FullPriorityList.Remove(highKey);
                i++;
            }
            contendedSystems = warFaction.PriorityList;
            BCTargets = warFaction.PriorityList;
        }
    }





    //Morphyum code from MercDeployments
    [HarmonyPatch(typeof(SGNavigationScreen), "OnTravelCourseAccepted")]
    public static class SGNavigationScreen_OnTravelCourseAccepted_Patch
    {
        static bool Prefix(SGNavigationScreen __instance)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var curSystem = sim.CurSystem;
            var system = Core.WarStatus.systems.Find(x => x.name == curSystem.Name);
            try
            {
                if (system.HotBox)
                {
                    UIManager uiManager = (UIManager)AccessTools.Field(typeof(SGNavigationScreen), "uiManager").GetValue(__instance);
                    SimGameState simState = (SimGameState)AccessTools.Field(typeof(SGNavigationScreen), "simState").GetValue(__instance);
                    Action cleanup = delegate () {
                        uiManager.ResetFader(UIManagerRootType.PopupRoot);
                        simState.Starmap.Screen.AllowInput(true);
                    };
                    string primaryButtonText = "Break Contract";
                    string message = "WARNING: This action will break your current deployment contract. Your reputation with this faction and the MRB will be negatively affected.";
                    PauseNotification.Show("Navigation Change", message, simState.GetCrewPortrait(SimGameCrew.Crew_Sumire), string.Empty, true, delegate {
                        cleanup();
                        system.HotBox = false;
                        if (simState.DoesFactionGainReputation(system.owner))
                        if (!Core.Settings.NoReputationGain.Contains(system.owner))
                        {
                            ReflectionHelper.InvokePrivateMethode(simState, "SetReputation", new object[] { system.owner, Core.Settings.DeploymentBreakRepCost, StatCollection.StatOperation.Int_Add, null });
                            ReflectionHelper.InvokePrivateMethode(simState, "SetReputation", new object[] { Faction.MercenaryReviewBoard, Core.Settings.DeploymentBreakMRBRepCost, StatCollection.StatOperation.Int_Add, null });
                            AccessTools.Field(typeof(SimGameState), "activeBreadcrumb").SetValue(simState, null);
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
                Error(e);
                return true;
            }
        }
    }
}

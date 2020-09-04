using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GalaxyatWar.Globals;
using static GalaxyatWar.Helpers;
using static GalaxyatWar.Resource;
using static GalaxyatWar.Logger;

// ReSharper disable ClassNeverInstantiated.Global

namespace GalaxyatWar
{
    public class WarTick
    {
        internal static void Tick(bool useFullSet, bool checkForSystemChange)
        {
            WarStatusTracker.PrioritySystems.Clear();
            var systemSubsetSize = WarStatusTracker.SystemStatuses.Count;
            List<SystemStatus> systemStatuses;
            
            if (Settings.UseSubsetOfSystems && !useFullSet)
            {
                systemSubsetSize = (int) (systemSubsetSize * Settings.SubSetFraction);
                systemStatuses = WarStatusTracker.SystemStatuses
                    .OrderBy(x => Guid.NewGuid()).Take(systemSubsetSize)
                    .ToList();
            }
            else
            {
                systemStatuses = WarStatusTracker.SystemStatuses;
            }

            if (checkForSystemChange && Settings.GaW_PoliceSupport)
                CalculateComstarSupport();

            if (WarStatusTracker.InitializeAtStart)
            {
                var lowestAR = 5000f;
                var lowestDr = 5000f;
                foreach (var faction in WarStatusTracker.warFactionTracker.Where(x => IncludedFactions.Contains(x.faction)))
                {
                    var systemCount = WarStatusTracker.SystemStatuses.Count(x => x.owner == faction.faction);
                    if (!Settings.ISMCompatibility && systemCount != 0)
                    {
                        faction.AR_PerPlanet = (float) Settings.BonusAttackResources[faction.faction] / systemCount;
                        faction.DR_PerPlanet = (float) Settings.BonusDefensiveResources[faction.faction] / systemCount;
                        if (faction.AR_PerPlanet < lowestAR)
                            lowestAR = faction.AR_PerPlanet;
                        if (faction.DR_PerPlanet < lowestDr)
                            lowestDr = faction.DR_PerPlanet;
                    }
                    else if (systemCount != 0)
                    {
                        faction.AR_PerPlanet = (float) Settings.BonusAttackResources_ISM[faction.faction] / systemCount;
                        faction.DR_PerPlanet = (float) Settings.BonusDefensiveResources_ISM[faction.faction] / systemCount;
                        if (faction.AR_PerPlanet < lowestAR)
                            lowestAR = faction.AR_PerPlanet;
                        if (faction.DR_PerPlanet < lowestDr)
                            lowestDr = faction.DR_PerPlanet;
                    }
                }

                foreach (var faction in WarStatusTracker.warFactionTracker.Where(x => IncludedFactions.Contains(x.faction)))
                {
                    faction.AR_PerPlanet = Mathf.Min(faction.AR_PerPlanet, 2 * lowestAR);
                    faction.DR_PerPlanet = Mathf.Min(faction.DR_PerPlanet, 2 * lowestDr);
                }

                for (var index = 0; index < systemStatuses.Count; index++)
                {
                    var systemStatus = systemStatuses[index];
                    //Spread out bonus resources and make them fair game for the taking.
                    var warFaction = WarStatusTracker.warFactionTracker.Find(x => x.faction == systemStatus.owner);
                    systemStatus.AttackResources += warFaction.AR_PerPlanet;
                    systemStatus.TotalResources += warFaction.AR_PerPlanet;
                    systemStatus.DefenseResources += warFaction.DR_PerPlanet;
                    systemStatus.TotalResources += warFaction.DR_PerPlanet;
                }
            }

            //Distribute Pirate Influence throughout the StarSystems
            PiratesAndLocals.CorrectResources();
            PiratesAndLocals.PiratesStealResources();
            PiratesAndLocals.CurrentPAResources = WarStatusTracker.PirateResources;
            PiratesAndLocals.DistributePirateResources();
            PiratesAndLocals.DefendAgainstPirates();

            if (checkForSystemChange && Settings.HyadesRimCompatible && WarStatusTracker.InactiveTHRFactions.Count != 0
                && WarStatusTracker.HyadesRimGeneralPirateSystems.Count != 0)
            {
                var rand = Rng.Next(0, 100);
                if (rand < WarStatusTracker.HyadesRimsSystemsTaken)
                {
                    var hyadesSystem = WarStatusTracker.HyadesRimGeneralPirateSystems.GetRandomElement();
                    var flipSystem = WarStatusTracker.SystemStatuses.Find(x => x.name == hyadesSystem).starSystem;
                    var inactiveFaction = WarStatusTracker.InactiveTHRFactions.GetRandomElement();
                    ChangeSystemOwnership(flipSystem, inactiveFaction, true);
                    WarStatusTracker.InactiveTHRFactions.Remove(inactiveFaction);
                    WarStatusTracker.HyadesRimGeneralPirateSystems.Remove(hyadesSystem);
                }
            }

            for (var i = 0; i < systemStatuses.Count; i++)
            {
                var systemStatus = systemStatuses[i];
                systemStatus.PriorityAttack = false;
                systemStatus.PriorityDefense = false;
                if (WarStatusTracker.InitializeAtStart)
                {
                    systemStatus.CurrentlyAttackedBy.Clear();
                    CalculateAttackAndDefenseTargets(systemStatus.starSystem);
                    RefreshContracts(systemStatus.starSystem);
                }

                if (systemStatus.Contended || WarStatusTracker.HotBox.Contains(systemStatus.name))
                    continue;

                if (!systemStatus.owner.Equals("Locals") && systemStatus.influenceTracker.Keys.Contains("Locals") &&
                    !WarStatusTracker.FlashpointSystems.Contains(systemStatus.name))
                {
                    systemStatus.influenceTracker["Locals"] *= 1.1f;
                }

                //Add resources from neighboring systems.
                if (systemStatus.neighborSystems.Count != 0)
                {
                    foreach (var neighbor in systemStatus.neighborSystems.Keys)
                    {
                        if (!Settings.ImmuneToWar.Contains(neighbor) && !Settings.DefensiveFactions.Contains(neighbor) &&
                            !WarStatusTracker.FlashpointSystems.Contains(systemStatus.name))
                        {
                            var pushFactor = Settings.APRPush * Rng.Next(1, Settings.APRPushRandomizer + 1);
                            systemStatus.influenceTracker[neighbor] += systemStatus.neighborSystems[neighbor] * pushFactor;
                        }
                    }
                }

                //Revolt on previously taken systems.
                if (systemStatus.owner != systemStatus.OriginalOwner)
                    systemStatus.influenceTracker[systemStatus.OriginalOwner] *= 0.10f;

                var pirateSystemFlagValue = Settings.PirateSystemFlagValue;

                if (Settings.ISMCompatibility)
                    pirateSystemFlagValue = Settings.PirateSystemFlagValue_ISM;

                var totalPirates = systemStatus.PirateActivity * systemStatus.TotalResources / 100;

                if (totalPirates >= pirateSystemFlagValue)
                {
                    if (!WarStatusTracker.PirateHighlight.Contains(systemStatus.name))
                        WarStatusTracker.PirateHighlight.Add(systemStatus.name);
                }
                else
                {
                    if (WarStatusTracker.PirateHighlight.Contains(systemStatus.name))
                        WarStatusTracker.PirateHighlight.Remove(systemStatus.name);
                }
            }

            WarStatusTracker.InitializeAtStart = false;
            foreach (var warFaction in WarStatusTracker.warFactionTracker)
            {
                DivideAttackResources(warFaction, useFullSet);
                AllocateAttackResources(warFaction);
            }

            CalculateDefensiveSystems();
            foreach (var warFaction in WarStatusTracker.warFactionTracker)
            {
                AllocateDefensiveResources(warFaction, useFullSet);
            }

            UpdateInfluenceFromAttacks(checkForSystemChange);

            //Increase War Escalation or decay defenses.
            foreach (var warFaction in WarStatusTracker.warFactionTracker)
            {
                if (!warFaction.GainedSystem)
                    warFaction.DaysSinceSystemAttacked += 1;
                else
                {
                    warFaction.DaysSinceSystemAttacked = 0;
                    warFaction.GainedSystem = false;
                }

                if (!warFaction.LostSystem)
                    warFaction.DaysSinceSystemLost += 1;
                else
                {
                    warFaction.DaysSinceSystemLost = 0;
                    warFaction.LostSystem = false;
                }
            }

            foreach (var system in WarStatusTracker.SystemStatuses.Where(x => WarStatusTracker.SystemChangedOwners.Contains(x.name)))
            {
                system.CurrentlyAttackedBy.Clear();
                CalculateAttackAndDefenseTargets(system.starSystem);
                RefreshContracts(system.starSystem);
            }

            LogDebug("Changed " + WarStatusTracker.SystemChangedOwners.Count);
            WarStatusTracker.SystemChangedOwners.Clear();
            if (StarmapMod.eventPanel != null)
            {
                StarmapMod.UpdatePanelText();
            }

            //Log("===================================================");
            //Log("TESTING ZONE");
            //Log("===================================================");
            //////TESTING ZONE
            //foreach (WarFaction WF in WarStatusTracker.warFactionTracker)
            //{
            //    Log("----------------------------------------------");
            //    Log(WF.faction.ToString());
            //    try
            //    {
            //        var DLT = WarStatusTracker.DeathListTrackers.Find(x => x.faction == WF.faction);
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
    }
}

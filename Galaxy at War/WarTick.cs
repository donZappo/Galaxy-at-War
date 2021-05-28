using System;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using UnityEngine;
using static GalaxyatWar.Helpers;
using static GalaxyatWar.Resource;
using static GalaxyatWar.Logger;

// ReSharper disable InconsistentNaming
// ReSharper disable ClassNeverInstantiated.Global

namespace GalaxyatWar
{
    public class WarTick
    {
        internal static void Tick(bool useFullSet, bool checkForSystemChange)
        {
            Globals.WarStatusTracker.PrioritySystems.Clear();
            var systemSubsetSize = Globals.WarStatusTracker.systems.Count;
            List<SystemStatus> systemStatuses;

            if (Globals.Settings.UseSubsetOfSystems && !useFullSet)
            {
                systemSubsetSize = (int) (systemSubsetSize * Globals.Settings.SubSetFraction);
                systemStatuses = Globals.WarStatusTracker.systems
                    .OrderBy(x => Guid.NewGuid()).Take(systemSubsetSize)
                    .ToList();
            }
            else
            {
                systemStatuses = Globals.WarStatusTracker.systems;
            }

            if (checkForSystemChange && Globals.Settings.GaW_PoliceSupport)
                CalculateComstarSupport();

            if (Globals.WarStatusTracker.FirstTickInitialization)
            {
                var lowestAR = 5000f;
                var lowestDR = 5000f;
                var perPlanetAR = 0f;
                var perPlanetDR = 0f;
                var sequence = Globals.WarStatusTracker.warFactionTracker.Where(x =>
                    Globals.IncludedFactions.Contains(x.faction)).ToList();
                foreach (var faction in sequence)
                {
                    var systemCount = Globals.WarStatusTracker.systems.Count(x => x.owner == faction.faction);
                    if (!Globals.Settings.ISMCompatibility && systemCount != 0)
                    {
                        perPlanetAR = (float) Globals.Settings.BonusAttackResources[faction.faction] / systemCount;
                        perPlanetDR = (float) Globals.Settings.BonusDefensiveResources[faction.faction] / systemCount;
                        if (perPlanetAR < lowestAR)
                            lowestAR = perPlanetAR;
                        if (perPlanetDR < lowestDR)
                            lowestDR = perPlanetDR;
                    }
                    else if (systemCount != 0)
                    {
                        perPlanetAR = (float) Globals.Settings.BonusAttackResources_ISM[faction.faction] / systemCount;
                        perPlanetDR = (float) Globals.Settings.BonusDefensiveResources_ISM[faction.faction] / systemCount;
                        if (perPlanetAR < lowestAR)
                            lowestAR = perPlanetDR;
                        if (perPlanetDR < lowestDR)
                            lowestDR = perPlanetDR;
                    }

                    perPlanetAR = Mathf.Min(perPlanetAR, 2 * lowestAR);
                    perPlanetDR = Mathf.Min(perPlanetDR, 2 * lowestDR);
                }

                foreach (var systemStatus in systemStatuses)
                {
                    //Spread out bonus resources and make them fair game for the taking.
                    systemStatus.AttackResources += perPlanetAR;
                    systemStatus.TotalResources += perPlanetAR;
                    systemStatus.DefenseResources += perPlanetDR;
                    systemStatus.TotalResources += perPlanetDR;
                }
            }

            //Distribute Pirate Influence throughout the StarSystems
            LogDebug("Processing pirates.");
            PiratesAndLocals.AdjustPirateResources();
            PiratesAndLocals.PiratesStealResources();
            PiratesAndLocals.DistributePirateResources(Globals.WarStatusTracker.PirateResources);
            PiratesAndLocals.DefendAgainstPirates();

            if (checkForSystemChange && Globals.Settings.HyadesRimCompatible && Globals.WarStatusTracker.InactiveTHRFactions.Count != 0
                && Globals.WarStatusTracker.HyadesRimGeneralPirateSystems.Count != 0)
            {
                var rand = Globals.Rng.Next(0, 100);
                if (rand < Globals.WarStatusTracker.HyadesRimsSystemsTaken)
                {
                    var hyadesSystem = Globals.WarStatusTracker.HyadesRimGeneralPirateSystems.GetRandomElement();
                    var flipSystem = Globals.WarStatusTracker.systems.Find(x => x.name == hyadesSystem).starSystem;
                    var inactiveFaction = Globals.WarStatusTracker.InactiveTHRFactions.GetRandomElement();
                    ChangeSystemOwnership(flipSystem, inactiveFaction, true);
                    Globals.WarStatusTracker.InactiveTHRFactions.Remove(inactiveFaction);
                    Globals.WarStatusTracker.HyadesRimGeneralPirateSystems.Remove(hyadesSystem);
                }
            }

            LogDebug("Processing systems' influence.");
            foreach (var systemStatus in systemStatuses)
            {
                systemStatus.PriorityAttack = false;
                systemStatus.PriorityDefense = false;
                if (Globals.WarStatusTracker.FirstTickInitialization)
                {
                    systemStatus.CurrentlyAttackedBy.Clear();
                    CalculateAttackAndDefenseTargets(systemStatus.starSystem);
                    RefreshContractsEmployersAndTargets(systemStatus);
                }

                if (systemStatus.Contended || Globals.WarStatusTracker.HotBox.Contains(systemStatus.name))
                    continue;

                if (!systemStatus.owner.Equals("Locals") && systemStatus.InfluenceTracker.Keys.Contains("Locals") &&
                    !Globals.WarStatusTracker.FlashpointSystems.Contains(systemStatus.name))
                {
                    systemStatus.InfluenceTracker["Locals"] = Math.Min(systemStatus.InfluenceTracker["Locals"] * 1.1f, 100);
                }
                
                //Add resources from neighboring systems.
                if (systemStatus.neighborSystems.Count != 0)
                {
                    foreach (var neighbor in systemStatus.neighborSystems.Keys)
                    {
                        if (!Globals.Settings.ImmuneToWar.Contains(neighbor) && !Globals.Settings.DefensiveFactions.Contains(neighbor) &&
                            !Globals.WarStatusTracker.FlashpointSystems.Contains(systemStatus.name))
                        {
                            var pushFactor = Globals.Settings.APRPush * Globals.Rng.Next(1, Globals.Settings.APRPushRandomizer + 1);
                            systemStatus.InfluenceTracker[neighbor] += systemStatus.neighborSystems[neighbor] * pushFactor;
                        }
                    }
                }

                //Revolt on previously taken systems.
                // that represents is the previous faction essentially "revolting" against the new faction.
                // So, if they have any influence in the system it gets bigger every turn. The only way to make it stop is to completely wipe them out.
                if (systemStatus.owner != systemStatus.OriginalOwner)
                    systemStatus.InfluenceTracker[systemStatus.OriginalOwner] *= 1.10f;

                var pirateSystemFlagValue = Globals.Settings.PirateSystemFlagValue;

                if (Globals.Settings.ISMCompatibility)
                    pirateSystemFlagValue = Globals.Settings.PirateSystemFlagValue_ISM;

                // LogDebug($"{systemStatus.starSystem.Name} total resources: {systemStatus.TotalResources}.  Pirate activity {systemStatus.PirateActivity}");
                var totalPirates = systemStatus.PirateActivity * systemStatus.TotalResources / 100;
                if (totalPirates >= pirateSystemFlagValue)
                {
                    if (!Globals.WarStatusTracker.PirateHighlight.Contains(systemStatus.name))
                        Globals.WarStatusTracker.PirateHighlight.Add(systemStatus.name);
                }
                else
                {
                    if (Globals.WarStatusTracker.PirateHighlight.Contains(systemStatus.name))
                        Globals.WarStatusTracker.PirateHighlight.Remove(systemStatus.name);
                }
            }

            Globals.WarStatusTracker.FirstTickInitialization = false;
            LogDebug("Processing resource spending.");
            foreach (var warFaction in Globals.WarStatusTracker.warFactionTracker)
            {
                DivideAttackResources(warFaction, useFullSet);
            }

            foreach (var warFaction in Globals.WarStatusTracker.warFactionTracker)
            {
                AllocateAttackResources(warFaction);
            }

            CalculateDefensiveSystems();

            foreach (var warFaction in Globals.WarStatusTracker.warFactionTracker)
            {
                AllocateDefensiveResources(warFaction, useFullSet);
            }

            LogDebug("Processing influence changes.");
            UpdateInfluenceFromAttacks(checkForSystemChange);

            //Increase War Escalation or decay defenses.
            foreach (var warFaction in Globals.WarStatusTracker.warFactionTracker)
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

            LogDebug("Processing flipped systems.");
            foreach (var system in Globals.WarStatusTracker.systems.Where(x => Globals.WarStatusTracker.SystemChangedOwners.Contains(x.name)))
            {
                system.CurrentlyAttackedBy.Clear();
                CalculateAttackAndDefenseTargets(system.starSystem);
                RefreshContractsEmployersAndTargets(system);
            }

            if (Globals.WarStatusTracker.SystemChangedOwners.Count > 0)
            {
                LogDebug($"Changed on {Globals.Sim.CurrentDate.ToShortDateString()}: {Globals.WarStatusTracker.SystemChangedOwners.Count} systems:");
                Globals.WarStatusTracker.SystemChangedOwners.OrderBy(x => x).Do(x =>
                LogDebug($"  {x}"));
                Globals.WarStatusTracker.SystemChangedOwners.Clear();
                if (StarmapMod.eventPanel != null)
                {
                    StarmapMod.UpdatePanelText();
                }
            }

            //Log("===================================================");
            //Log("TESTING ZONE");
            //Log("===================================================");
            //////TESTING ZONE
            //foreach (WarFaction WF in Globals.WarStatusTracker.warFactionTracker)
            //{
            //    Log("----------------------------------------------");
            //    Log(WF.faction.ToString());
            //    try
            //    {
            //        var DLT = Globals.WarStatusTracker.DeathListTrackers.Find(x => x.faction == WF.faction);
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

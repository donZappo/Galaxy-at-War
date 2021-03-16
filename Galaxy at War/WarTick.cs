using System;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using UnityEngine;
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
            /*Globals.WarStatusTracker.PrioritySystems.Clear();
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
                //AddBaseWarTickResourcesPerSystem();

                var sequence = Globals.WarStatusTracker.warFactionTracker.Where(x =>
                    Globals.IncludedFactions.Contains(x.faction)).ToList();
                foreach (var faction in sequence)
                {
                    var systemCount = Globals.WarStatusTracker.systems.Count(x => x.owner == faction.faction);
                    if (!Globals.Settings.ISMCompatibility && systemCount != 0)
                    {
                        faction.AR_PerPlanet = (float) Globals.Settings.BonusAttackResources[faction.faction] / systemCount;
                        faction.DR_PerPlanet = (float) Globals.Settings.BonusDefensiveResources[faction.faction] / systemCount;
                    }
                    else if (systemCount != 0)
                    {
                        faction.AR_PerPlanet = (float) Globals.Settings.BonusAttackResources_ISM[faction.faction] / systemCount;
                        faction.DR_PerPlanet = (float) Globals.Settings.BonusDefensiveResources_ISM[faction.faction] / systemCount;
                    }
                }
                ///-------------

                foreach (var faction in sequence)
                {
                    faction.AR_PerPlanet = Mathf.Min(faction.AR_PerPlanet, 2 * lowestAR);
                    faction.DR_PerPlanet = Mathf.Min(faction.DR_PerPlanet, 2 * lowestDr);
                }

                foreach (var systemStatus in systemStatuses)
                {
                    //Spread out bonus resources and make them fair game for the taking.
                    var warFaction = Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == systemStatus.owner);
                    systemStatus.AttackResources += warFaction.AR_PerPlanet + GetTotalAttackResources(systemStatus.starSystem);          //probably doesn't need to be += as it should be 0        
                    systemStatus.TotalResources += warFaction.AR_PerPlanet + GetTotalAttackResources(systemStatus.starSystem);           //may remove will leave in place for now       
                    systemStatus.DefenseResources += warFaction.DR_PerPlanet + GetTotalDefensiveResources(systemStatus.starSystem);      //probably doesn't need to be += as it should be 0              
                    systemStatus.TotalResources += warFaction.DR_PerPlanet + GetTotalDefensiveResources(systemStatus.starSystem);        //may remove will leave in place for now       
                }
            }
            
            //Distribute Pirate Influence throughout the StarSystems
            LogDebug("Processing pirates.");
            //PiratesAndLocals.CorrectResources();            
           // PiratesAndLocals.PiratesStealResources();
            //PiratesAndLocals.CurrentPAResources = Globals.WarStatusTracker.PirateResources;
            //PiratesAndLocals.DistributePirateResources();
            //PiratesAndLocals.DefendAgainstPirates();    


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

                if (!systemStatus.owner.Equals("Locals") && systemStatus.influenceTracker.Keys.Contains("Locals") &&
                    !Globals.WarStatusTracker.FlashpointSystems.Contains(systemStatus.name))
                {
                    systemStatus.influenceTracker["Locals"] *= 1.1f;
                }

                //Add resources from neighboring systems.
                if (systemStatus.neighborSystems.Count != 0)
                {

                    foreach (var neighbor in systemStatus.neighborSystems.Keys)
                    {
                        if (!Globals.Settings.ImmuneToWar.Contains(neighbor) && !Globals.Settings.DefensiveFactions.Contains(neighbor) &&
                            !Globals.WarStatusTracker.FlashpointSystems.Contains(systemStatus.name))
                        {
                            if (neighbor == systemStatus.owner)
                            {
                                var pushFactor = Globals.Settings.APRPush * Globals.Rng.Next(1, Globals.Settings.APRPushRandomizer + 1);
                                systemStatus.influenceTracker[neighbor] += systemStatus.influenceTracker[neighbor]/100 + pushFactor;
                            }
                            else 
                            {
                                int a = 0;
                                int b = 0;
                                systemStatus.neighborSystems.TryGetValue(, out a);
                                systemStatus.neighborSystems.TryGetValue(neighbor, out b);
                                var diffeence = Math.Abs(a - b);
                            }
                        }
                    }
                }

                //Revolt on previously taken systems.
                if (systemStatus.owner != systemStatus.OriginalOwner)
                    systemStatus.influenceTracker[systemStatus.OriginalOwner] *= 0.10f;

                var pirateSystemFlagValue = Globals.Settings.PirateSystemFlagValue;

                if (Globals.Settings.ISMCompatibility)
                    pirateSystemFlagValue = Globals.Settings.PirateSystemFlagValue_ISM;

                var totalPirates = systemStatus.PirateActivity;

                //var totalPirates = systemStatus.PirateActivity * systemStatus.TotalResources / 100;

                ValueLog("System Name: " + systemStatus.name + "\n" + "Current PA: " + systemStatus.PirateActivity+ "\n" + "current AR: " + systemStatus.AttackResources + "\n" + "CurrentDR: " 
                    + systemStatus.DefenseResources + "\n" + "Current TotalResources: " + systemStatus.TotalResources + "\n" + "totalPirates: " + totalPirates + "\n");
                totalPirates = Helpers.Clamp(totalPirates, Globals.ResourceGenericMax);
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

            /*--------start-next------------
             Intention is to make it so that each system will retain at least it's base min resources and only push out any resources that exceed that base value.
             A system that is requesting resources, will pull the maximum available resource from faction systems withen range that are not in conflict with another system.
             Instead of a global pool of resources that all faction systems pull from systems can only get reources from systems that are near them and the resources they generate
             themselves if neccessary.
             Any system that has excess resources and is not in conflict with another system will divide it's excess reources by the amount of faction systems within range and push those resources out
             */
            LogDebug("Processing resource spending.");

            foreach(SystemStatus system in Globals.WarStatusTracker.systems)
            {
                system.FindNeighbors();
                system.systemResources.hasDoneDistrobution = false;
                system.systemResources.AddBaseWarTickResourcesForSystem();
            }

            //Testing wether it is easier/better to let there be a single tick of resources
            //before starting distrobution (Tick 2)
            //TODO check DistrobuteResourcesToLocalFactionSystems for bugs. Cureently looks fine.
            //Have noticed that to bug check, Total resources should be wired up properly, or i just shouldn't use it for testing output till it is wired up.
            //Have a feeling systems with local faction stopped getting resources will run a few tests first before diving back in.
            //Want to make sure that it is working correctly before moving on otherwise the problems will pile up.
            if (!Globals.WarStatusTracker.FirstTickInitialization)
            {
                foreach (SystemStatus system in Globals.WarStatusTracker.systems)
                {
                    if(system.owner == "Locals")
                    {
                        Logger.ValueLog("Locals system " + system.name + "; ARBefore = " + system.systemResources.AttackResources +"; DRBefore = " + system.systemResources.DefenceResources);
                        Logger.ValueLog("TotalResources = " + system.systemResources.TotalResources);
                        system.DistributeResourcesToLocalFactionSystems();
                        Logger.ValueLog("Locals system " + system.name + "; ARAfter = " + system.systemResources.AttackResources + "; DRAfter = " + system.systemResources.DefenceResources);
                        Logger.ValueLog("TotalResources = " + system.systemResources.TotalResources);
                    }
                    else
                        system.DistributeResourcesToLocalFactionSystems();
                }
            }else
                Globals.WarStatusTracker.FirstTickInitialization = false; 

             /*if (!Globals.WarStatusTracker.FirstTickInitialization)
                 AddBaseWarTickResourcesPerSystem();
             else
                 Globals.WarStatusTracker.FirstTickInitialization = false;*/

             //foreach(var warFaction in Globals.WarStatusTracker.warFactionTracker)
             //{
             //    DistributeResourcesBetweenFactionSystems(warFaction, useFullSet);
             //}

             /*foreach (var warFaction in Globals.WarStatusTracker.warFactionTracker)
             {
                 DivideAttackResources(warFaction, useFullSet);
             }

             CalculateDefensiveSystems();
             foreach (var warFaction in Globals.WarStatusTracker.warFactionTracker)
             {
                 AllocateDefensiveResources(warFaction, useFullSet);
                 AllocateAttackResources(warFaction);
             }*/
             //--------end-next-----------------------------


             LogDebug("Processing influence changes.");
            /*UpdateInfluenceFromAttacks(checkForSystemChange);

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
            }*/

            LogDebug("Processing flipped systems.");
            /* foreach (var system in Globals.WarStatusTracker.systems.Where(x => Globals.WarStatusTracker.SystemChangedOwners.Contains(x.name)))
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
            }*/

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

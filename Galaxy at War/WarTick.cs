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
            var systemSubsetSize = Globals.WarStatusTracker.Systems.Count;
            List<SystemStatus> systemStatuses;

            if (Globals.Settings.UseSubsetOfSystems && !useFullSet)
            {
                systemSubsetSize = (int) (systemSubsetSize * Globals.Settings.SubSetFraction);
                systemStatuses = Globals.WarStatusTracker.Systems
                    .OrderBy(x => Guid.NewGuid()).Take(systemSubsetSize)
                    .ToList();
            }
            else
            {
                systemStatuses = Globals.WarStatusTracker.Systems;
            }

            if (checkForSystemChange && Globals.Settings.GaW_PoliceSupport)
                CalculateComstarSupport();

            if (Globals.WarStatusTracker.FirstTickInitialization)
            {
                var perPlanetAR = 0f;
                var perPlanetDR = 0f;
                var sequence = Globals.WarStatusTracker.WarFactionTracker.Where(x =>
                    Globals.IncludedFactions.Contains(x.FactionName)).ToList();
                var factionPerPlanetAR = new Dictionary<WarFaction, float>();
                var factionPerPlanetDR = new Dictionary<WarFaction, float>();
                Logger.LogDebug("Faction Bonus Resources");
                Logger.LogDebug("=============================");
                foreach (var faction in sequence)
                {
                    var systemCount = Globals.WarStatusTracker.Systems.Count(x => x.Owner == faction.FactionName);
                    if (systemCount != 0)
                    {
                        if (!Globals.Settings.DefensiveFactions.Contains(faction.FactionName))
                        {
                            perPlanetAR = faction.TotalBonusAttackResources / systemCount;
                            perPlanetDR = faction.TotalBonusDefensiveResources / systemCount;
                            factionPerPlanetAR.Add(faction, perPlanetAR);
                            factionPerPlanetDR.Add(faction, perPlanetDR);
                            Logger.LogDebug("Faction: " + faction.FactionName + " Bonus AR: " + faction.TotalBonusAttackResources + " Bonus DR: " + faction.TotalBonusDefensiveResources);
                            Logger.LogDebug("     Systems: " + systemCount + "; perPlanetAR: " + perPlanetAR + "; perPlanetDR: " + perPlanetDR);
                        }
                        else
                        {
                            perPlanetAR = faction.TotalBonusDefensiveResources / systemCount / 2;
                            perPlanetDR = faction.TotalBonusDefensiveResources / systemCount / 2;
                            factionPerPlanetAR.Add(faction, perPlanetAR);
                            factionPerPlanetDR.Add(faction, perPlanetDR);
                            Logger.LogDebug("Faction: " + faction.FactionName + " Bonus AR: " + faction.TotalBonusAttackResources + " Bonus DR: " + faction.TotalBonusDefensiveResources);
                            Logger.LogDebug("     Systems: " + systemCount + "; perPlanetAR: " + perPlanetAR + "; perPlanetDR: " + perPlanetDR);

                        }
                    }
                }

                foreach (var systemStatus in systemStatuses)
                {
                    //Spread out bonus resources and make them fair game for the taking.
                    WarFaction faction = Globals.WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == systemStatus.Owner);
                    systemStatus.AttackResourcesOriginal = Mathf.Min(systemStatus.AttackResources + factionPerPlanetAR[faction], systemStatus.AttackResources * 2);
                    systemStatus.DefenseResourcesOriginal = Mathf.Min(systemStatus.DefenseResources + factionPerPlanetDR[faction], systemStatus.DefenseResources * 2);
                    systemStatus.AttackResources = systemStatus.AttackResourcesOriginal;
                    systemStatus.DefenseResources = systemStatus.DefenseResourcesOriginal;
                    systemStatus.TotalOriginalResources = systemStatus.AttackResources + systemStatus.DefenseResources;
                    systemStatus.TotalResources = systemStatus.TotalOriginalResources;
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
                    var inactiveFaction = Globals.WarStatusTracker.InactiveTHRFactions.GetRandomElement();
                    BattleTech.StarSystem flipSystem = new();
                    if (Globals.Settings.HyadesAppearingPiratesFlipSystems.ContainsKey(inactiveFaction))
                    {
                        foreach (var changeSystem in Globals.Settings.HyadesAppearingPiratesFlipSystems[inactiveFaction])
                        {
                            flipSystem = Globals.WarStatusTracker.Systems.Find(x => x.Name == changeSystem).StarSystem;
                            ChangeSystemOwnership(flipSystem, inactiveFaction, true);
                            if (Globals.WarStatusTracker.HyadesRimGeneralPirateSystems.Contains(changeSystem))
                                Globals.WarStatusTracker.HyadesRimGeneralPirateSystems.Remove(changeSystem);
                        }
                    }
                    else
                    {
                        var hyadesSystem = Globals.WarStatusTracker.HyadesRimGeneralPirateSystems.GetRandomElement();
                        flipSystem = Globals.WarStatusTracker.Systems.Find(x => x.Name == hyadesSystem).StarSystem;
                        ChangeSystemOwnership(flipSystem, inactiveFaction, true);
                        Globals.WarStatusTracker.HyadesRimGeneralPirateSystems.Remove(hyadesSystem);
                    }
                    Globals.WarStatusTracker.InactiveTHRFactions.Remove(inactiveFaction);
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
                    CalculateAttackAndDefenseTargets(systemStatus.StarSystem);
                    RefreshContractsEmployersAndTargets(systemStatus);
                }

                if (systemStatus.Contested || Globals.WarStatusTracker.HotBox.IsHot(systemStatus.Name))
                    continue;

                if (!systemStatus.Owner.Equals("Locals") && systemStatus.InfluenceTracker.Keys.Contains("Locals") &&
                    !Globals.WarStatusTracker.FlashpointSystems.Contains(systemStatus.Name))
                {
                    systemStatus.InfluenceTracker["Locals"] = Math.Min(systemStatus.InfluenceTracker["Locals"] * 1.1f, 100);
                }
                
                //Add resources from neighboring systems.
                if (systemStatus.NeighborSystems.Count != 0)
                {
                    foreach (var neighbor in systemStatus.NeighborSystems.Keys)
                    {
                        if (!Globals.Settings.ImmuneToWar.Contains(neighbor) && !Globals.Settings.DefensiveFactions.Contains(neighbor) &&
                            !Globals.WarStatusTracker.FlashpointSystems.Contains(systemStatus.Name))
                        {
                            var pushFactor = Globals.Settings.APRPush * Globals.Rng.Next(1, Globals.Settings.APRPushRandomizer + 1);
                            systemStatus.InfluenceTracker[neighbor] += systemStatus.NeighborSystems[neighbor] * pushFactor;
                        }
                    }
                }

                //Revolt on previously taken systems.
                // that represents is the previous faction essentially "revolting" against the new faction.
                // So, if they have any influence in the system it gets bigger every turn. The only way to make it stop is to completely wipe them out.
                if (systemStatus.Owner != systemStatus.OriginalOwner)
                    systemStatus.InfluenceTracker[systemStatus.OriginalOwner] *= 1.10f;

                var pirateSystemFlagValue = Globals.Settings.PirateSystemFlagValue;

                if (Globals.Settings.ISMCompatibility)
                    pirateSystemFlagValue = Globals.Settings.PirateSystemFlagValue_ISM;

                // LogDebug($"{systemStatus.starSystem.Name} total resources: {systemStatus.TotalResources}.  Pirate activity {systemStatus.PirateActivity}");
                //var totalPirates = systemStatus.PirateActivity * systemStatus.TotalOriginalResources / 100;
                var totalPirates = systemStatus.PirateActivity * systemStatus.DifficultyRating / 10;
                if (totalPirates >= pirateSystemFlagValue)
                {
                    if (!Globals.WarStatusTracker.PirateHighlight.Contains(systemStatus.Name))
                        Globals.WarStatusTracker.PirateHighlight.Add(systemStatus.Name);
                }
                else
                {
                    if (Globals.WarStatusTracker.PirateHighlight.Contains(systemStatus.Name))
                        Globals.WarStatusTracker.PirateHighlight.Remove(systemStatus.Name);
                }
            }

            Globals.WarStatusTracker.FirstTickInitialization = false;
            LogDebug("Processing resource spending.");
            foreach (var warFaction in Globals.WarStatusTracker.WarFactionTracker)
            {
                DivideAttackResources(warFaction, useFullSet);
            }

            foreach (var warFaction in Globals.WarStatusTracker.WarFactionTracker)
            {
                AllocateAttackResources(warFaction);
            }

            CalculateDefensiveSystems();

            foreach (var warFaction in Globals.WarStatusTracker.WarFactionTracker)
            {
                AllocateDefensiveResources(warFaction, useFullSet);
            }

            LogDebug("Processing influence changes.");
            UpdateInfluenceFromAttacks(checkForSystemChange);

            //Increase War Escalation or decay defenses.
            foreach (var warFaction in Globals.WarStatusTracker.WarFactionTracker)
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
            foreach (var system in Globals.WarStatusTracker.Systems.Where(x => Globals.WarStatusTracker.SystemChangedOwners.Contains(x.Name)))
            {
                system.CurrentlyAttackedBy.Clear();
                CalculateAttackAndDefenseTargets(system.StarSystem);
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

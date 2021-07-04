using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GalaxyatWar.Globals;

// ReSharper disable InconsistentNaming
// ReSharper disable ClassNeverInstantiated.Global

namespace GalaxyatWar
{
    internal class PiratesAndLocals
    {
        internal static readonly HashSet<SystemStatus> FullPirateListSystems = new();

        // if the pirates are getting a lot of resources it lowers the min.
        // So it gives the pirates a continual boost if they need it, and then stops boosting them when they don't.
        // It ensures there will also be some moderate amount of pirate activity going on
        public static void AdjustPirateResources()
        {
            WarStatusTracker.PirateResources -= WarStatusTracker.CurrentPRGain;
            if (WarStatusTracker.LastPRGain >= WarStatusTracker.CurrentPRGain || WarStatusTracker.PirateResources <= 0)
            {
                WarStatusTracker.PirateResources = Mathf.Max(WarStatusTracker.PirateResources, WarStatusTracker.MinimumPirateResources);
                WarStatusTracker.MinimumPirateResources *= 1.1f;
            }
            else
            {
                WarStatusTracker.MinimumPirateResources /= 1.1f;
                if (WarStatusTracker.MinimumPirateResources < WarStatusTracker.StartingPirateResources)
                    WarStatusTracker.MinimumPirateResources = WarStatusTracker.StartingPirateResources;
            }

            foreach (var warFaction in WarStatusTracker.WarFactionTracker)
            {
                warFaction.AttackResources += warFaction.PirateARLoss;
                warFaction.DefensiveResources += warFaction.PirateDRLoss;
            }

            WarStatusTracker.LastPRGain = WarStatusTracker.CurrentPRGain;
        }

        public static void DefendAgainstPirates()
        {
            var factionEscalateDefense = new Dictionary<WarFaction, bool>();
            foreach (var warFaction in WarStatusTracker.WarFactionTracker)
            {
                var defenseValue = 100 * (warFaction.PirateARLoss + warFaction.PirateDRLoss) /
                                   (warFaction.AttackResources + warFaction.DefensiveResources + warFaction.PirateARLoss + warFaction.PirateDRLoss);
                if (defenseValue > 5 || !warFaction.LostSystem)
                    factionEscalateDefense.Add(warFaction, true);
                else
                    factionEscalateDefense.Add(warFaction, false);
            }

            var tempFullPirateListSystems = new List<SystemStatus>(FullPirateListSystems);
            foreach (var system in tempFullPirateListSystems)
            {
                var warFaction = WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == system.Owner);
                float PAChange;

                //Added some obscure logic for making high pirate systems more sticky.
                bool fightHighPirates = false;
                var totalPirates = system.PirateActivity * system.DifficultyRating / 10;
                var pirateSystemFlagValue = Globals.Settings.PirateSystemFlagValue_ISM;
                if (!Globals.Settings.ISMCompatibility)
                    pirateSystemFlagValue = Globals.Settings.PirateSystemFlagValue;
                if (totalPirates >= pirateSystemFlagValue)
                {
                    if (Rng.NextDouble() > 0.5)
                        fightHighPirates = true;
                }

                if (fightHighPirates || factionEscalateDefense[warFaction])
                    PAChange = (float) (Rng.NextDouble() * (system.PirateActivity - system.PirateActivity / 3) + system.PirateActivity / 3);
                else
                    PAChange = (float) (Rng.NextDouble() * (system.PirateActivity / 3));

                var attackResources = warFaction.AttackResources;

                if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(warFaction.FactionName))
                    attackResources = warFaction.DefensiveResources;

                var defenseCost = Mathf.Min(PAChange * system.TotalOriginalResources / 100, warFaction.AttackResources * 0.01f);

                if (attackResources >= defenseCost)
                {
                    PAChange = Math.Min(PAChange, system.PirateActivity);
                    system.PirateActivity -= PAChange;
                    if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(warFaction.FactionName))
                        warFaction.DR_Against_Pirates += defenseCost;
                    else
                        warFaction.AR_Against_Pirates += defenseCost;
                    //warFaction.PirateDRLoss += PAChange * system.TotalResources / 100;
                }
                else
                {
                    PAChange = Math.Min(attackResources, system.PirateActivity);
                    system.PirateActivity -= PAChange;
                    if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(warFaction.FactionName))
                        warFaction.DR_Against_Pirates += defenseCost;
                    else
                        warFaction.AR_Against_Pirates += defenseCost;
                    //warFaction.PirateDRLoss += PAChange * system.TotalResources / 100;
                }

                if (system.PirateActivity == 0)
                {
                    FullPirateListSystems.Remove(system);
                    WarStatusTracker.FullPirateSystems.Remove(system.Name);
                }
            }
        }

        public static void PiratesStealResources()
        {
            WarStatusTracker.CurrentPRGain = 0;
            foreach (var warFaction in WarStatusTracker.WarFactionTracker)
            {
                warFaction.PirateARLoss = 0;
                warFaction.PirateDRLoss = 0;
            }

            var pirateSystems = FullPirateListSystems.ToList();
            for (var i = 0; i < FullPirateListSystems.Count; i++)
            {
                var system = pirateSystems[i];
                WarStatusTracker.PirateResources += system.TotalOriginalResources * system.PirateActivity / 100;
                WarStatusTracker.CurrentPRGain += system.TotalOriginalResources * system.PirateActivity / 100;

                var warFaction = WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == system.Owner);
                var warFARChange = system.AttackResourcesOriginal * system.PirateActivity / 100;
                if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(warFaction.FactionName))
                    warFaction.PirateDRLoss += warFARChange;
                else
                    warFaction.PirateARLoss += warFARChange;

                if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(warFaction.FactionName))
                    warFaction.DefensiveResources -= warFARChange;
                else
                    warFaction.AttackResources -= warFARChange;

                var warFDRChange = system.DefenseResourcesOriginal * system.PirateActivity / 100;
                warFaction.PirateDRLoss += warFDRChange;
                warFaction.DefensiveResources = Math.Max(0, warFaction.DefensiveResources - warFDRChange);
                system.AttackResources = system.AttackResourcesOriginal - warFARChange;
                system.DefenseResources = system.DefenseResourcesOriginal - warFDRChange;
            }
        }

        public static void DistributePirateResources(float CurrentPAResources)
        {
            var i = 0;
            var noPiracySystems = new List<SystemStatus>();
            foreach (var system in WarStatusTracker.Systems)
            {
                if (Settings.ImmuneToWar.Contains(system.Owner) || WarStatusTracker.FlashpointSystems.Contains(system.Name) ||
                    Settings.HyadesPirates.Contains(system.Owner))
                {
                    noPiracySystems.Add(system);
                }

            }
            //var noPiracySystemsStrings = Settings.ImmuneToWar.Concat(
            //        WarStatusTracker.FlashpointSystems.Concat(
            //            WarStatusTracker.HyadesRimGeneralPirateSystems.Concat(
            //                Settings.HyadesPirates)));
            
            //foreach (var systemsString in noPiracySystemsStrings)
            //{
            //    noPiracySystems.Add(WarStatusTracker.Systems.FirstOrDefault(system => system.Name == systemsString));
            //}

            var candidateSystems = WarStatusTracker.Systems
                .Except(noPiracySystems)
                .Except(WarStatusTracker.HotBox)
                .Where(system => system.Owner != "NoFaction").ToList();
            while (CurrentPAResources > 0 && i != 1000)
            {
                var systemStatus = candidateSystems.GetRandomElement();
                const int maxDifficulty = 11;
                var currentPA = systemStatus.PirateActivity;
                float basicPA = maxDifficulty - systemStatus.DifficultyRating;

                var bonusPA = currentPA / 50;
                // How much they are going to spend to attack
                var totalPA = basicPA + bonusPA;

                //var pirateSystemsContainsSystemStatus = WarStatusTracker.FullPirateSystems.Contains(systemStatus.name);
                if (currentPA + totalPA <= 100)
                {
                    if (totalPA <= CurrentPAResources)
                    {
                        systemStatus.PirateActivity += Math.Min(totalPA, 100 - systemStatus.PirateActivity);
                        CurrentPAResources -= Math.Min(totalPA, 100 - systemStatus.PirateActivity);
                        i = 0;
                        //if (!pirateSystemsContainsSystemStatus)
                        //{
                            WarStatusTracker.FullPirateSystems.Add(systemStatus.Name);
                            FullPirateListSystems.Add(systemStatus);
                        //}
                    }
                    else
                    {
                        systemStatus.PirateActivity += Math.Min(CurrentPAResources, 100 - systemStatus.PirateActivity);
                        CurrentPAResources = 0;
                        //if (!pirateSystemsContainsSystemStatus)
                        //{
                            WarStatusTracker.FullPirateSystems.Add(systemStatus.Name);
                            FullPirateListSystems.Add(systemStatus);
                        //}
                    }
                }
                else
                {
                    if (100 - systemStatus.PirateActivity <= CurrentPAResources)
                    {
                        systemStatus.PirateActivity += Math.Min(100, 100 - systemStatus.PirateActivity);
                        CurrentPAResources -= 100 - systemStatus.PirateActivity;
                        i++;
                        //if (!pirateSystemsContainsSystemStatus)
                        //{
                            WarStatusTracker.FullPirateSystems.Add(systemStatus.Name);
                            FullPirateListSystems.Add(systemStatus);
                        //}
                    }
                    else
                    {
                        systemStatus.PirateActivity += Math.Min(CurrentPAResources, 100 - systemStatus.PirateActivity);
                        CurrentPAResources = 0;
                        //if (!pirateSystemsContainsSystemStatus)
                        //{
                            WarStatusTracker.FullPirateSystems.Add(systemStatus.Name);
                            FullPirateListSystems.Add(systemStatus);
                        //}
                    }
                }
            }

            foreach (var system in noPiracySystems.Where(system => system is not null))
            {
                system.PirateActivity = 0;
            }
        }
    }
}

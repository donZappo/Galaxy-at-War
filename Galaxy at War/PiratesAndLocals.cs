using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Harmony;
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
            WarStatusTracker.PirateResources -= WarStatusTracker.TempPRGain;
            if (WarStatusTracker.LastPRGain >= WarStatusTracker.TempPRGain || WarStatusTracker.PirateResources <= 0)
            {
                WarStatusTracker.PirateResources = WarStatusTracker.MinimumPirateResources;
                WarStatusTracker.MinimumPirateResources *= 1.1f;
                Logger.LogDebug($"MinimumPirateResources raised: {WarStatusTracker.MinimumPirateResources}");
            }
            else
            {
                WarStatusTracker.MinimumPirateResources /= 1.1f;
                if (WarStatusTracker.MinimumPirateResources < WarStatusTracker.StartingPirateResources)
                    WarStatusTracker.MinimumPirateResources = WarStatusTracker.StartingPirateResources;
                Logger.LogDebug($"MinimumPirateResources lowered: {WarStatusTracker.MinimumPirateResources}");
            }

            foreach (var warFaction in WarStatusTracker.WarFactionTracker)
            {
                warFaction.AttackResources += warFaction.PirateARLoss;
                warFaction.DefensiveResources += warFaction.PirateDRLoss;
            }

            WarStatusTracker.LastPRGain = WarStatusTracker.TempPRGain;
        }

        public static void DefendAgainstPirates()
        {
            var factionEscalateDefense = new Dictionary<WarFaction, bool>();
            foreach (var warFaction in WarStatusTracker.WarFactionTracker)
            {
                var defenseValue = 100 * (warFaction.PirateARLoss + warFaction.PirateDRLoss) /
                                   (warFaction.AttackResources + warFaction.DefensiveResources + warFaction.PirateARLoss + warFaction.PirateDRLoss);
                if (defenseValue > 5)
                    factionEscalateDefense.Add(warFaction, true);
                else
                    factionEscalateDefense.Add(warFaction, false);
            }

            var tempFullPirateListSystems = new List<SystemStatus>(FullPirateListSystems);
            foreach (var system in tempFullPirateListSystems)
            {
                var warFaction = WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == system.owner);
                float PAChange;
                if (factionEscalateDefense[warFaction])
                    PAChange = (float) (Rng.NextDouble() * (system.PirateActivity - system.PirateActivity / 3) + system.PirateActivity / 3);
                else
                    PAChange = (float) (Rng.NextDouble() * (system.PirateActivity / 3));

                var attackResources = warFaction.AttackResources;

                if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(warFaction.FactionName))
                    attackResources = warFaction.DefensiveResources;

                var defenseCost = Mathf.Min(PAChange * system.TotalResources / 100, warFaction.AttackResources * 0.01f);

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
                    WarStatusTracker.FullPirateSystems.Remove(system.name);
                }
            }
        }

        public static void PiratesStealResources()
        {
            WarStatusTracker.TempPRGain = 0;
            foreach (var warFaction in WarStatusTracker.WarFactionTracker)
            {
                warFaction.PirateARLoss = 0;
                warFaction.PirateDRLoss = 0;
            }

            var pirateSystems = FullPirateListSystems.ToList();
            for (var i = 0; i < FullPirateListSystems.Count; i++)
            {
                var system = pirateSystems[i];
                WarStatusTracker.PirateResources += system.TotalResources * system.PirateActivity / 100;
                WarStatusTracker.TempPRGain += system.TotalResources * system.PirateActivity / 100;

                var warFaction = WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == system.owner);
                var warFARChange = system.AttackResources * system.PirateActivity / 100;
                if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(warFaction.FactionName))
                    warFaction.PirateDRLoss += warFARChange;
                else
                    warFaction.PirateARLoss += warFARChange;

                if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(warFaction.FactionName))
                    warFaction.DefensiveResources -= warFARChange;
                else
                    warFaction.AttackResources -= warFARChange;

                var warFDRChange = system.DefenseResources * system.PirateActivity / 100;
                warFaction.PirateDRLoss += warFDRChange;
                warFaction.DefensiveResources = Math.Max(0, warFaction.DefensiveResources - warFDRChange);
            }
        }

        public static void DistributePirateResources(float CurrentPAResources)
        {
            var i = 0;
            var noPiracySystemsStrings = Settings.ImmuneToWar.Concat(
                WarStatusTracker.HotBox.Concat(
                    WarStatusTracker.FlashpointSystems.Concat(
                        WarStatusTracker.HyadesRimGeneralPirateSystems.Concat(
                            Settings.HyadesPirates))));
            var noPiracySystems = new List<SystemStatus>();
            foreach (var systemsString in noPiracySystemsStrings)
            {
                noPiracySystems.Add(WarStatusTracker.Systems.FirstOrDefault(system => system.name == systemsString));
            }

            var candidateSystems = WarStatusTracker.Systems.Except(noPiracySystems).Where(system => system.owner != "NoFaction").ToList();
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
                            WarStatusTracker.FullPirateSystems.Add(systemStatus.name);
                            FullPirateListSystems.Add(systemStatus);
                        //}
                    }
                    else
                    {
                        systemStatus.PirateActivity += Math.Min(CurrentPAResources, 100 - systemStatus.PirateActivity);
                        CurrentPAResources = 0;
                        //if (!pirateSystemsContainsSystemStatus)
                        //{
                            WarStatusTracker.FullPirateSystems.Add(systemStatus.name);
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
                            WarStatusTracker.FullPirateSystems.Add(systemStatus.name);
                            FullPirateListSystems.Add(systemStatus);
                        //}
                    }
                    else
                    {
                        systemStatus.PirateActivity += Math.Min(CurrentPAResources, 100 - systemStatus.PirateActivity);
                        CurrentPAResources = 0;
                        //if (!pirateSystemsContainsSystemStatus)
                        //{
                            WarStatusTracker.FullPirateSystems.Add(systemStatus.name);
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

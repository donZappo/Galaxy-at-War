using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using static GalaxyatWar.Globals;

// ReSharper disable InconsistentNaming
// ReSharper disable ClassNeverInstantiated.Global

namespace GalaxyatWar
{
    internal class PiratesAndLocals
    {
        public static float CurrentPAResources;
        internal static readonly List<SystemStatus> FullPirateListSystems = new List<SystemStatus>();

        public static void CorrectResources()
        {
            WarStatusTracker.PirateResources -= WarStatusTracker.TempPRGain;
            if (WarStatusTracker.LastPRGain > WarStatusTracker.TempPRGain || WarStatusTracker.PirateResources < 0)
            {
                WarStatusTracker.PirateResources = WarStatusTracker.MinimumPirateResources;
                WarStatusTracker.MinimumPirateResources *= 1.1f;
            }
            else
            {
                WarStatusTracker.MinimumPirateResources /= 1.1f;
                if (WarStatusTracker.MinimumPirateResources < WarStatusTracker.StartingPirateResources)
                    WarStatusTracker.MinimumPirateResources = WarStatusTracker.StartingPirateResources;
            }

            foreach (var warFaction in WarStatusTracker.warFactionTracker)
            {
                warFaction.AttackResources += warFaction.PirateARLoss;
                warFaction.DefensiveResources += warFaction.PirateDRLoss;
            }

            WarStatusTracker.LastPRGain = WarStatusTracker.TempPRGain;
        }

        public static void DefendAgainstPirates()
        {
            var factionEscalateDefense = new Dictionary<WarFaction, bool>();
            foreach (var warFaction in WarStatusTracker.warFactionTracker)
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
                var warFaction = WarStatusTracker.warFactionTracker.Find(x => x.faction == system.owner);
                float PAChange;
                if (factionEscalateDefense[warFaction])
                    PAChange = (float) (Rng.NextDouble() * (system.PirateActivity - system.PirateActivity / 3) + system.PirateActivity / 3);
                else
                    PAChange = (float) (Rng.NextDouble() * (system.PirateActivity / 3));

                var attackResources = warFaction.AttackResources;

                if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(warFaction.faction))
                    attackResources = warFaction.DefensiveResources;

                var defenseCost = Mathf.Min(PAChange * system.TotalResources / 100, warFaction.AttackResources * 0.01f);

                if (attackResources >= defenseCost)
                {
                    PAChange = Math.Min(PAChange, system.PirateActivity);
                    system.PirateActivity -= PAChange;
                    if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(warFaction.faction))
                        warFaction.DR_Against_Pirates += defenseCost;
                    else
                        warFaction.AR_Against_Pirates += defenseCost;
                    //warFaction.PirateDRLoss += PAChange * system.TotalResources / 100;
                }
                else
                {
                    PAChange = Math.Min(attackResources, system.PirateActivity);
                    system.PirateActivity -= PAChange;
                    if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(warFaction.faction))
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
            foreach (var warFaction in WarStatusTracker.warFactionTracker)
            {
                warFaction.PirateARLoss = 0;
                warFaction.PirateDRLoss = 0;
            }

            for (var i = 0; i < FullPirateListSystems.Count; i++)
            {
                var system = FullPirateListSystems[i];
                WarStatusTracker.PirateResources += system.TotalResources * system.PirateActivity / 100;
                WarStatusTracker.TempPRGain += system.TotalResources * system.PirateActivity / 100;

                var warFaction = WarStatusTracker.warFactionTracker.Find(x => x.faction == system.owner);
                var warFARChange = system.AttackResources * system.PirateActivity / 100;
                if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(warFaction.faction))
                    warFaction.PirateDRLoss += warFARChange;
                else
                    warFaction.PirateARLoss += warFARChange;

                if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(warFaction.faction))
                    warFaction.DefensiveResources -= warFARChange;
                else
                    warFaction.AttackResources -= warFARChange;

                var warFDRChange = system.DefenseResources * system.PirateActivity / 100;
                warFaction.PirateDRLoss += warFDRChange;
                warFaction.DefensiveResources = Math.Max(0, warFaction.DefensiveResources - warFDRChange);
            }
        }

        public static void DistributePirateResources()
        {
            var i = 0;
            while (CurrentPAResources > 0 && i != 1000)
            {
                var systemStatus = WarStatusTracker.systems.GetRandomElement();
                if (systemStatus.owner == "NoFaction" ||
                    Settings.ImmuneToWar.Contains(systemStatus.owner) ||
                    WarStatusTracker.HotBox.Contains(systemStatus.name) ||
                    WarStatusTracker.FlashpointSystems.Contains(systemStatus.name) ||
                    WarStatusTracker.HyadesRimGeneralPirateSystems.Contains(systemStatus.name) ||
                    Settings.HyadesPirates.Contains(systemStatus.owner))
                {
                    systemStatus.PirateActivity = 0;
                    continue;
                }

                var currentPA = systemStatus.PirateActivity;
                float basicPA = 11 - systemStatus.DifficultyRating;

                var bonusPA = currentPA / 50;
                var totalPA = basicPA + bonusPA;

                var pirateSystemsContainsSystemStatus = WarStatusTracker.FullPirateSystems.Contains(systemStatus.name);
                //Log(systemStatus.name);
                if (currentPA + totalPA <= 100)
                {
                    if (totalPA <= CurrentPAResources)
                    {
                        systemStatus.PirateActivity += Math.Min(totalPA, 100 - systemStatus.PirateActivity) * SpendFactor;
                        CurrentPAResources -= Math.Min(totalPA, 100 - systemStatus.PirateActivity) * SpendFactor;
                        i = 0;
                        if (!pirateSystemsContainsSystemStatus)
                        {
                            WarStatusTracker.FullPirateSystems.Add(systemStatus.name);
                            FullPirateListSystems.Add(systemStatus);
                        }
                    }
                    else
                    {
                        systemStatus.PirateActivity += Math.Min(CurrentPAResources, 100 - systemStatus.PirateActivity);
                        CurrentPAResources = 0;
                        if (!pirateSystemsContainsSystemStatus)
                        {
                            WarStatusTracker.FullPirateSystems.Add(systemStatus.name);
                            FullPirateListSystems.Add(systemStatus);
                        }
                    }
                }
                else
                {
                    if (100 - systemStatus.PirateActivity <= CurrentPAResources)
                    {
                        systemStatus.PirateActivity += (100 - systemStatus.PirateActivity) * SpendFactor;
                        CurrentPAResources -= (100 - systemStatus.PirateActivity) * SpendFactor;
                        i++;
                        if (!pirateSystemsContainsSystemStatus)
                        {
                            WarStatusTracker.FullPirateSystems.Add(systemStatus.name);
                            FullPirateListSystems.Add(systemStatus);
                        }
                    }
                    else
                    {
                        systemStatus.PirateActivity += Math.Min(CurrentPAResources, 100 - systemStatus.PirateActivity);
                        CurrentPAResources = 0;
                        if (!pirateSystemsContainsSystemStatus)
                        {
                            WarStatusTracker.FullPirateSystems.Add(systemStatus.name);
                            FullPirateListSystems.Add(systemStatus);
                        }
                    }
                }
            }
        }
    }
}

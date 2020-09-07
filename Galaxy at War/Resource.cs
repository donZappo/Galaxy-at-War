using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GalaxyatWar.Logger;

// ReSharper disable ClassNeverInstantiated.Global

namespace GalaxyatWar
{
    public class Resource
    {
        public static void DivideAttackResources(WarFaction warFaction, bool useFullSet)
        {
            //Log("Attacking");
            var factionFaction = warFaction.faction;
            var deathList = Globals.WarStatusTracker.DeathListTrackers.Find(x => x.faction == factionFaction);
            var warFar = warFaction.warFactionAttackResources;
            warFar.Clear();
            var tempTargets = new Dictionary<string, float>();
            foreach (var fact in warFaction.attackTargets.Keys)
            {
                tempTargets.Add(fact, deathList.deathList[fact]);
            }

            var total = tempTargets.Values.Sum();
            var attackResources = warFaction.AttackResources - warFaction.AR_Against_Pirates;
            if (warFaction.ComstarSupported)
                attackResources += Globals.Settings.GaW_Police_ARBonus;
            warFaction.AR_Against_Pirates = 0;
            if (Globals.Settings.AggressiveToggle && !Globals.Settings.DefensiveFactions.Contains(warFaction.faction))
                attackResources += Globals.Sim.Constants.Finances.LeopardBaseMaintenanceCost;

            attackResources = attackResources * (1 + warFaction.DaysSinceSystemAttacked * Globals.Settings.AResourceAdjustmentPerCycle / 100);
            attackResources += attackResources * (float) (Globals.Rng.Next(-1, 1) * Globals.Settings.ResourceSpread);
            foreach (var rfact in tempTargets.Keys)
            {
                warFar.Add(rfact, tempTargets[rfact] * attackResources / total);
            }
        }

        public static void AllocateAttackResources(WarFaction warFaction)
        {
            var factionRep = Globals.Sim.GetRawReputation(Globals.FactionValues.Find(x => x.Name == warFaction.faction));
            var maxContracts = HotSpots.ProcessReputation(factionRep);
            if (warFaction.warFactionAttackResources.Keys.Count == 0)
                return;
            var warFar = warFaction.warFactionAttackResources;
            //Go through the different resources allocated from attacking faction to spend against each targetFaction
            var factionDlt = Globals.WarStatusTracker.DeathListTrackers.Find(x => x.faction == warFaction.faction);
            foreach (var targetFaction in warFar.Keys)
            {
                if (!warFaction.attackTargets.Keys.Contains(targetFaction))
                    break;
                var targetFar = warFar[targetFaction];
                var startingTargetFar = targetFar;
                var targets = warFaction.attackTargets[targetFaction];
                var hatred = factionDlt.deathList[targetFaction];

                while (targetFar > 0)
                {
                    if (targets.Count == 0)
                        break;

                    var rand = Globals.Rng.Next(0, targets.Count);
                    SystemStatus system = default;
                    for (var i = 0; i < Globals.WarStatusTracker.SystemStatuses.Count; i++)
                    {
                        // todo index access
                        if (Globals.WarStatusTracker.SystemStatuses[i].name == targets[rand])
                        {
                            system = Globals.WarStatusTracker.SystemStatuses[i];
                            break;
                        }
                    }

                    if (system == null)
                    {
                        Log("ERROR - No system found at AllocateAttackResources, aborting processing.");
                        return;
                    }

                    if (system.owner == warFaction.faction || Globals.WarStatusTracker.FlashpointSystems.Contains(system.name))
                    {
                        targets.RemoveAt(rand);
                        continue;
                    }

                    //Find most valuable target for attacking for later. Used in HotSpots.
                    if (hatred >= Globals.Settings.PriorityHatred &&
                        system.DifficultyRating <= maxContracts &&
                        system.DifficultyRating >= maxContracts - 4)
                    {
                        system.PriorityAttack = true;
                        if (!system.CurrentlyAttackedBy.Contains(warFaction.faction))
                        {
                            system.CurrentlyAttackedBy.Add(warFaction.faction);
                        }

                        if (!Globals.WarStatusTracker.PrioritySystems.Contains(system.starSystem.Name))
                        {
                            Globals.WarStatusTracker.PrioritySystems.Add(system.starSystem.Name);
                        }
                    }

                    //Distribute attacking resources to systems.
                    if (system.Contended || Globals.WarStatusTracker.HotBox.Contains(system.name))
                    {
                        targets.Remove(system.starSystem.Name);
                        if (targets.Count == 0 || !warFaction.attackTargets.Keys.Contains(targetFaction))
                        {
                            // bug should be continue?
                            break;
                        }

                        continue;
                    }

                    var arFactor = UnityEngine.Random.Range(Globals.Settings.MinimumResourceFactor, Globals.Settings.MaximumResourceFactor);
                    var spendAR = Mathf.Min(startingTargetFar * arFactor, targetFar);
                    spendAR = spendAR < 1 ? 1 : Math.Max(1 * Globals.SpendFactor, spendAR * Globals.SpendFactor);
                    var maxValueList = system.influenceTracker.Values.OrderByDescending(x => x).ToList();
                    var pMaxValue = 200.0f;
                    if (maxValueList.Count > 1)
                        pMaxValue = maxValueList[1];

                    var itValue = system.influenceTracker[warFaction.faction];
                    var basicAR = (float) (11 - system.DifficultyRating) / 2;

                    var bonusAR = 0f;
                    if (itValue > pMaxValue)
                        bonusAR = (itValue - pMaxValue) * 0.15f;

                    var totalAR = (basicAR + bonusAR) + spendAR;

                    if (targetFar > totalAR)
                    {
                        system.influenceTracker[warFaction.faction] += totalAR;
                        targetFar -= totalAR;
                        Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == targetFaction).defenseTargets.Add(system.name);
                    }
                    else
                    {
                        system.influenceTracker[warFaction.faction] += targetFar;
                        targetFar = 0;
                    }
                }
            }
        }

        public static void AllocateDefensiveResources(WarFaction warFaction, bool useFullSet)
        {
            if (warFaction.defenseTargets.Count == 0 || !Globals.WarStatusTracker.warFactionTracker.Contains(warFaction))
                return;

            var faction = warFaction.faction;
            var defensiveResources = warFaction.DefensiveResources + warFaction.DR_Against_Pirates;
            if (warFaction.ComstarSupported)
                defensiveResources += Globals.Settings.GaW_Police_DRBonus;
            warFaction.DR_Against_Pirates = 0;
            if (Globals.Settings.AggressiveToggle && Globals.Settings.DefensiveFactions.Contains(warFaction.faction))
                defensiveResources += Globals.Sim.Constants.Finances.LeopardBaseMaintenanceCost;
            var defensiveCorrection = defensiveResources * (100 * Globals.Settings.GlobalDefenseFactor -
                                                            Globals.Settings.DResourceAdjustmentPerCycle * warFaction.DaysSinceSystemLost) / 100;
            defensiveResources = Math.Max(defensiveResources, defensiveCorrection);
            defensiveResources += defensiveResources * (float) (Globals.Rng.Next(-1, 1) * (Globals.Settings.ResourceSpread));
            var startingDefensiveResources = defensiveResources;
            var duplicateDefenseTargets = new List<string>(warFaction.defenseTargets);

            // spend and decrement defensiveResources
            while (defensiveResources > float.Epsilon)
            {
                //LogDebug(spendDR);
                var highest = 0f;
                var highestFaction = faction;
                var spendDr = 1.0f;
                string system;
                if (duplicateDefenseTargets.Count != 0)
                {
                    system = duplicateDefenseTargets.GetRandomElement();
                    duplicateDefenseTargets.Remove(system);
                }
                else
                {
                    system = warFaction.defenseTargets.GetRandomElement();
                    var drFactor = UnityEngine.Random.Range(Globals.Settings.MinimumResourceFactor, Globals.Settings.MaximumResourceFactor);
                    spendDr = Mathf.Min(startingDefensiveResources * drFactor, defensiveResources);
                    spendDr = spendDr < 1 ? 1 : Math.Max(1, spendDr) * Globals.SpendFactor;
                }

                // fastest loop possible?
                SystemStatus systemStatus = default;
                for (var i = 0; i < Globals.WarStatusTracker.SystemStatuses.Count; i++)
                {
                    if (Globals.WarStatusTracker.SystemStatuses[i].name == system)
                    {
                        systemStatus = Globals.WarStatusTracker.SystemStatuses[i];
                        break;
                    }
                }

                if (systemStatus == null)
                {
                    LogDebug("NULL SystemStatus at AllocateDefensiveResources");
                    return;
                }

                if (systemStatus.Contended || Globals.WarStatusTracker.HotBox.Contains(systemStatus.name))
                {
                    warFaction.defenseTargets.Remove(systemStatus.starSystem.Name);
                    if (warFaction.defenseTargets.Count == 0 || warFaction.defenseTargets == null)
                    {
                        break;
                    }

                    continue;
                }

                var total = systemStatus.influenceTracker.Values.Sum();
                var sequence = systemStatus.influenceTracker
                    .Where(x => x.Value != 0)
                    .Select(x => x.Key);
                foreach (var factionStr in sequence)
                {
                    if (systemStatus.influenceTracker[factionStr] > highest)
                    {
                        highest = systemStatus.influenceTracker[factionStr];
                        highestFaction = factionStr;
                    }

                    if (highest / total >= 0.5)
                        break;
                }

                if (highestFaction == faction)
                {
                    if (defensiveResources > 0)
                    {
                        systemStatus.influenceTracker[faction] += spendDr;
                        defensiveResources -= spendDr;
                    }
                    else
                    {
                        systemStatus.influenceTracker[faction] += defensiveResources;
                        defensiveResources = 0;
                    }
                }
                else
                {
                    var diffRes = systemStatus.influenceTracker[highestFaction] / total - systemStatus.influenceTracker[faction] / total;
                    var bonusDefense = spendDr + (diffRes * total - (Globals.Settings.TakeoverThreshold / 100) * total) / (Globals.Settings.TakeoverThreshold / 100 + 1);
                    //LogDebug(bonusDefense);
                    if (100 * diffRes > Globals.Settings.TakeoverThreshold)
                        if (defensiveResources >= bonusDefense)
                        {
                            systemStatus.influenceTracker[faction] += bonusDefense;
                            defensiveResources -= bonusDefense;
                        }
                        else
                        {
                            systemStatus.influenceTracker[faction] += Math.Min(defensiveResources, 50);
                            defensiveResources -= Math.Min(defensiveResources, 50);
                        }
                    else
                    {
                        systemStatus.influenceTracker[faction] += Math.Min(defensiveResources, 50);
                        defensiveResources -= Math.Min(defensiveResources, 50);
                    }
                }

                //Log("After Defense");
                //foreach (var foo in systemStatus.influenceTracker.Keys)
                //    Log("    " + foo + ": " + systemStatus.influenceTracker[foo]);
            }
        }
    }
}

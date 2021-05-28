using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GalaxyatWar.Logger;
using Random = UnityEngine.Random;

// ReSharper disable ClassNeverInstantiated.Global

namespace GalaxyatWar
{
    public class Resource
    {
        public static void DivideAttackResources(WarFaction warFaction, bool useFullSet)
        {
            //Log("Attacking");
            var deathList = warFaction.DeathListTracker;
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
            if (warFaction.warFactionAttackResources.Count == 0)
                return;
            var warFactionAR = warFaction.warFactionAttackResources;
            //Go through the different resources allocated from attacking faction to spend against each targetFaction
            var deathListTracker = warFaction.DeathListTracker;
            foreach (var targetFaction in warFactionAR.Keys.Intersect(warFaction.attackTargets.Keys))
            {
                var targetFar = warFactionAR[targetFaction];
                var startingTargetFar = targetFar;
                var attackTargets = warFaction.attackTargets[targetFaction];
                var map = new Dictionary<string, SystemStatus>();
                foreach (var targetName in attackTargets)
                {
                    map.Add(targetName, Globals.WarStatusTracker.systems.Find(x => x.name == targetName));
                }

                var hatred = deathListTracker.deathList[targetFaction];
                var targetWarFaction = Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == targetFaction);
                while (targetFar > 0 && attackTargets.Count > 0)
                {
                    // DE-CONSTRUCTOR
                    var foo = map.GetRandomElement();
                    var target = foo.Key;
                    var system = foo.Value;
                    //var (target, system) = map.GetRandomElement<KeyValuePair<string, SystemStatus>>();

                    if (system == null)
                    {
                        LogDebug("CRITICAL:  No system found at AllocateAttackResources, aborting processing.");
                        return;
                    }

                    if (system.owner == warFaction.faction || Globals.WarStatusTracker.FlashpointSystems.Contains(system.name))
                    {
                        attackTargets.Remove(target);
                        return;
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
                        attackTargets.Remove(system.starSystem.Name);
                        if (warFaction.attackTargets[targetFaction].Count == 0 || !warFaction.attackTargets.Keys.Contains(targetFaction))
                        {
                            break;
                        }

                        continue;
                    }

                    var arFactor = Random.Range(Globals.Settings.MinimumResourceFactor, Globals.Settings.MaximumResourceFactor);
                    var spendAR = Mathf.Min(startingTargetFar * arFactor, targetFar);
                    spendAR = spendAR < 1 ? 1 : Math.Max(1 * Globals.SpendFactor, spendAR * Globals.SpendFactor);
                    var maxValueList = system.InfluenceTracker.Values.OrderByDescending(x => x).ToList();
                    var pMaxValue = 200.0f;
                    if (maxValueList.Count > 1)
                        pMaxValue = maxValueList[1];

                    var itValue = system.InfluenceTracker[warFaction.faction];
                    var basicAR = (float) (11 - system.DifficultyRating) / 2;
                    var bonusAR = 0f;
                    if (itValue > pMaxValue)
                        bonusAR = (itValue - pMaxValue) * 0.15f;

                    var totalAR = basicAR + bonusAR + spendAR;
                    if (targetFar > totalAR)
                    {
                        system.InfluenceTracker[warFaction.faction] += totalAR;
                        targetFar -= totalAR;
                        targetWarFaction.defenseTargets.Add(system.name);
                    }
                    else
                    {
                        system.InfluenceTracker[warFaction.faction] += targetFar;
                        targetFar = 0;
                        targetWarFaction.defenseTargets.Add(system.name);
                    }
                }
            }
        }


        public static void AllocateDefensiveResources(WarFaction warFaction, bool useFullSet)
        {
            if (warFaction.defenseTargets.Count == 0)
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
            defensiveResources += defensiveResources * (float) (Globals.Rng.Next(-1, 1) * Globals.Settings.ResourceSpread);
            var startingDefensiveResources = defensiveResources;
            var map = new Dictionary<string, SystemStatus>();
            foreach (var defenseTarget in warFaction.defenseTargets.Distinct())
            {
                map.Add(defenseTarget, Globals.WarStatusTracker.systems.Find(x => x.name == defenseTarget));
            }

            // spend and decrement defensiveResources
            while (defensiveResources > float.Epsilon)
            {
                var highest = 0f;
                var highestFaction = faction;
                var drFactor = Random.Range(Globals.Settings.MinimumResourceFactor, Globals.Settings.MaximumResourceFactor);
                var spendDr = Globals.Settings.MinimumSpendDR;
                //var spendDr = Mathf.Min(startingDefensiveResources * drFactor, defensiveResources);
                //spendDr = spendDr < 1 ? 1 : Math.Max(1 * Globals.SpendFactor, spendDr * Globals.SpendFactor);

                var systemStatus = map.GetRandomElement().Value;
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

                var total = systemStatus.InfluenceTracker.Values.Sum();
                var sequence = systemStatus.InfluenceTracker
                    .Where(x => x.Value != 0)
                    .Select(x => x.Key);
                foreach (var factionStr in sequence)
                {
                    if (systemStatus.InfluenceTracker[factionStr] > highest)
                    {
                        highest = systemStatus.InfluenceTracker[factionStr];
                        highestFaction = factionStr;
                    }

                    if (highest / total >= 0.5)
                        break;
                }

                if (highestFaction == faction)
                {
                    if (defensiveResources > 0)
                    {
                        systemStatus.InfluenceTracker[faction] += spendDr;
                        defensiveResources -= spendDr;
                    }
                    else
                    {
                        systemStatus.InfluenceTracker[faction] += defensiveResources;
                        defensiveResources = 0;
                    }
                }
                else
                {
                    var diffRes = systemStatus.InfluenceTracker[highestFaction] / total - systemStatus.InfluenceTracker[faction] / total;
                    var bonusDefense = spendDr + (diffRes * total - Globals.Settings.TakeoverThreshold / 100 * total) / (Globals.Settings.TakeoverThreshold / 100 + 1);
                    //LogDebug(bonusDefense);
                    if (100 * diffRes > Globals.Settings.TakeoverThreshold)
                        if (defensiveResources >= bonusDefense)
                        {
                            systemStatus.InfluenceTracker[faction] += bonusDefense;
                            defensiveResources -= bonusDefense;
                        }
                        else
                        {
                            systemStatus.InfluenceTracker[faction] += Math.Min(defensiveResources, 10 * spendDr);
                            defensiveResources -= Math.Min(defensiveResources, 10 * spendDr);
                        }
                    else
                    {
                        systemStatus.InfluenceTracker[faction] += Math.Min(defensiveResources, 10 * spendDr);
                        defensiveResources -= Math.Min(defensiveResources, 10 * spendDr);
                    }
                }
            }
        }
    }
}

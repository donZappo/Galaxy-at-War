using System;
using System.Collections.Generic;
using System.Linq;
using Harmony;
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
            var warFar = warFaction.WarFactionAttackResources;
            warFar.Clear();
            var tempTargets = new Dictionary<string, float>();
            foreach (var fact in warFaction.AttackTargets.Keys)
            {
                tempTargets.Add(fact, deathList.DeathList[fact]);
            }

            var total = tempTargets.Values.Sum();
            var attackResources = warFaction.AttackResources - warFaction.AR_Against_Pirates;
            if (warFaction.ComstarSupported)
                attackResources += Globals.Settings.GaW_Police_ARBonus;
            warFaction.AR_Against_Pirates = 0;
            if (Globals.Settings.AggressiveToggle && !Globals.Settings.DefensiveFactions.Contains(warFaction.FactionName))
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
            int factionRep = -1;
            try
            {
                factionRep = Globals.Sim.GetRawReputation(Globals.FactionValues.Find(value => value.Name == warFaction.FactionName));
            }
            catch (Exception ex)
            {
                LogDebug("warFaction.FactionName: " + warFaction.FactionName);
                Error(ex);
            }

            var maxContracts = HotSpots.ProcessReputation(factionRep);
            if (warFaction.WarFactionAttackResources.Count == 0)
                return;
            var warFactionAR = warFaction.WarFactionAttackResources;
            //Go through the different resources allocated from attacking faction to spend against each targetFaction
            var deathListTracker = warFaction.DeathListTracker;
            var attackFactions = warFactionAR.Keys.Intersect(warFaction.AttackTargets.Keys);
            foreach (var targetFaction in attackFactions)
            {
                var targetFar = warFactionAR[targetFaction];
                var startingTargetFar = targetFar;
                var attackTargets = warFaction.AttackTargets[targetFaction];
                var hatred = deathListTracker.DeathList[targetFaction];
                var targetWarFaction = Globals.WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == targetFaction);
                while (targetFar > 0 && attackTargets.Count > 0)
                {
                    var systemStatus = attackTargets.GetRandomElement();
                    if (systemStatus.Owner == warFaction.FactionName || Globals.WarStatusTracker.FlashpointSystems.Contains(systemStatus.Name))
                    {
                        attackTargets.Remove(systemStatus);
                        return;
                    }

                    //Find most valuable target for attacking for later. Used in HotSpots.
                    if (hatred >= Globals.Settings.PriorityHatred &&
                        systemStatus.DifficultyRating <= maxContracts &&
                        systemStatus.DifficultyRating >= maxContracts - 4)
                    {
                        systemStatus.PriorityAttack = true;
                        if (!systemStatus.CurrentlyAttackedBy.Contains(warFaction.FactionName))
                        {
                            systemStatus.CurrentlyAttackedBy.Add(warFaction.FactionName);
                        }

                        if (!Globals.WarStatusTracker.PrioritySystems.Contains(systemStatus.StarSystem.Name))
                        {
                            Globals.WarStatusTracker.PrioritySystems.Add(systemStatus.StarSystem.Name);
                        }
                    }

                    //Distribute attacking resources to systems.
                    if (systemStatus.Contested || Globals.WarStatusTracker.HotBox.IsHot(systemStatus.Name))
                    {
                        attackTargets.Remove(systemStatus);
                        if (warFaction.AttackTargets[targetFaction].Count == 0 || !warFaction.AttackTargets.Keys.Contains(targetFaction))
                        {
                            break;
                        }

                        continue;
                    }

                    var arFactor = Random.Range(Globals.Settings.MinimumResourceFactor, Globals.Settings.MaximumResourceFactor);
                    var spendAR = Mathf.Min(startingTargetFar * arFactor, targetFar);
                    spendAR = spendAR < 1 ? 1 : Math.Max(1 * Globals.SpendFactor, spendAR * Globals.SpendFactor);
                    var maxValueList = systemStatus.InfluenceTracker.Values.OrderByDescending(x => x).ToList();
                    var pMaxValue = 200.0f;
                    if (maxValueList.Count > 1)
                        pMaxValue = maxValueList[1];

                    var itValue = systemStatus.InfluenceTracker[warFaction.FactionName];
                    var basicAR = (float) (11 - systemStatus.DifficultyRating) / 2;
                    var bonusAR = 0f;
                    if (itValue > pMaxValue)
                        bonusAR = (itValue - pMaxValue) * 0.15f;

                    var totalAR = basicAR + bonusAR + spendAR;
                    if (targetFar > totalAR)
                    {
                        systemStatus.InfluenceTracker[warFaction.FactionName] += totalAR;
                        targetFar -= totalAR;
                        targetWarFaction.DefenseTargets.Add(systemStatus);
                    }
                    else
                    {
                        systemStatus.InfluenceTracker[warFaction.FactionName] += targetFar;
                        targetFar = 0;
                        targetWarFaction.DefenseTargets.Add(systemStatus);
                    }
                }
            }
        }


        public static void AllocateDefensiveResources(WarFaction warFaction, bool useFullSet)
        {
            if (warFaction.DefenseTargets.Count == 0)
                return;

            //LogDebug("========= Allocate Defensive Resources =========");
            var faction = warFaction.FactionName;
            //LogDebug("Faction: " + faction);
            //LogDebug("Defensive Resources: " + warFaction.DefensiveResources + "; DR Against Pirates: " + warFaction.DR_Against_Pirates);
            var defensiveResources = warFaction.DefensiveResources + warFaction.DR_Against_Pirates;
            //LogDebug("Comstar Supported: " + warFaction.ComstarSupported);
            if (warFaction.ComstarSupported)
                defensiveResources += Globals.Settings.GaW_Police_DRBonus;
            warFaction.DR_Against_Pirates = 0;
            if (Globals.Settings.AggressiveToggle && Globals.Settings.DefensiveFactions.Contains(warFaction.FactionName))
                defensiveResources += Globals.Sim.Constants.Finances.LeopardBaseMaintenanceCost;
            var defensiveCorrection = defensiveResources * (100 * Globals.Settings.GlobalDefenseFactor -
                                                            Globals.Settings.DResourceAdjustmentPerCycle * warFaction.DaysSinceSystemLost) / 100;
            //LogDebug("Defensive Correction: " + defensiveCorrection);
            defensiveResources = Math.Max(defensiveResources, defensiveCorrection);
            defensiveResources += defensiveResources * (float) (Globals.Rng.Next(-1, 1) * Globals.Settings.ResourceSpread);
            //LogDebug("New Defensive Resources: " + defensiveResources);
            var startingDefensiveResources = defensiveResources;
            var defenseTargets = warFaction.DefenseTargets.Distinct().ToList();
            // reset the cached state for the next tick
            Globals.WarStatusTracker.Systems.Do(systemStatus => systemStatus.TrackerSum = -1);
            // spend and decrement defensiveResources
            while (defensiveResources > 0)
            {
                //LogDebug("****DEFENSIVE RESOURCES****");
                //LogDebug("defensive Resources: " + defensiveResources);
                var highest = 0f;
                var highestFaction = faction;
                var drFactor = Random.Range(Globals.Settings.MinimumResourceFactor, Globals.Settings.MaximumResourceFactor);
                var spendDr = Globals.Settings.MinimumSpendDR;
                //var spendDr = Mathf.Min(startingDefensiveResources * drFactor, defensiveResources);
                //spendDr = spendDr < 1 ? 1 : Math.Max(1 * Globals.SpendFactor, spendDr * Globals.SpendFactor);

                var systemStatus = defenseTargets.GetRandomElement();
                if (systemStatus == null)
                {
                    LogDebug("NULL SystemStatus at AllocateDefensiveResources");
                    return;
                }

                if (systemStatus.Contested || Globals.WarStatusTracker.HotBox.IsHot(systemStatus.Name))
                {
                    warFaction.DefenseTargets.Remove(systemStatus);
                    if (warFaction.DefenseTargets.Count == 0 || warFaction.DefenseTargets == null)
                    {
                        break;
                    }

                    continue;
                }

                var total = systemStatus.TrackerSum;
                var factionTrackers = systemStatus.InfluenceTracker.Where(x => x.Value != 0);
                foreach (var factionKVP in factionTrackers)
                {
                    var factionStr = factionKVP.Key;
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
                    //LogDebug("Bonus Defense: " + bonusDefense);
                    //LogDebug("   Highest Faction: " + systemStatus.InfluenceTracker[highestFaction]);
                    //LogDebug("   Current Faction: " + systemStatus.InfluenceTracker[faction]);
                    //LogDebug("   Total: " + total);
                    //LogDebug("   diffRes: " + diffRes);
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

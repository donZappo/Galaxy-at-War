using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BattleTech;
using static GalaxyatWar.Logger;
using Random = UnityEngine.Random;
using static GalaxyatWar.Helpers;

// ReSharper disable ClassNeverInstantiated.Global

namespace GalaxyatWar
{
    //The random Number generator should more then likely be Instanciated In WarStatus
    //Being Called here would only mean it keeps being regenerated right ?
    //using a single instance of random that gets used against all processes that call it
    //would give much better random number results, or even initiating it in globals.

    //Resource should probably be restructured to be an internal class of SystemStatus Class.
    //TODO check viability.
    public class Resource
    {
        internal float BaseSystemAttackResources;
        internal float BaseSystemDefenceResources;
        public float AttackResources;
        public float DefenceResources;

        
        public Resource()
        {

        }

        public Resource(StarSystem system)
        {
            BaseSystemAttackResources = Helpers.GetTotalAttackResources(system);
            BaseSystemDefenceResources = Helpers.GetTotalDefensiveResources(system);
        }


        //this will more then likely get changed when influence gets re-introduced
        public void AddBaseWarTickResourcesForSystem()
        {
            AttackResources += BaseSystemAttackResources;
            DefenceResources += BaseSystemDefenceResources;
            /*foreach (SystemStatus system in Globals.WarStatusTracker.systems)
            {
                system.AttackResources += GetTotalAttackResources(system.starSystem);
                system.DefenseResources += GetTotalDefensiveResources(system.starSystem);
            }*/
        }

        public static void DistributeResourcesBetweenFactionSystems(WarFaction warFaction, bool useFullSet)
        {
            //TODO make a function in SystemStatus, that processess and stores Neighbor Systems As a list of SystemStatus
            List<SystemStatus> localSystems = new List<SystemStatus>();

            foreach (SystemStatus starSystem in Globals.WarStatusTracker.systems)
            {                 
                if (starSystem.owner == warFaction.faction)
                {

                }
            }
        }

        /*public static void DivideAttackResources(WarFaction warFaction, bool useFullSet)
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
            var attackResources =  warFaction.AttackResources - warFaction.AR_Against_Pirates;
            attackResources = Helpers.Clamp(attackResources, Globals.ResourceGenericMax);
            if (warFaction.ComstarSupported)
                attackResources += Globals.Settings.GaW_Police_ARBonus;
            warFaction.AR_Against_Pirates = 0;
            if (Globals.Settings.AggressiveToggle && !Globals.Settings.DefensiveFactions.Contains(warFaction.faction))
                attackResources += Globals.Sim.Constants.Finances.LeopardBaseMaintenanceCost;

            attackResources = attackResources * (1 + warFaction.DaysSinceSystemAttacked * Globals.Settings.AResourceAdjustmentPerCycle / 100);
            attackResources = Helpers.Clamp(attackResources, Globals.ResourceGenericMax);
            attackResources += attackResources * (float) (Globals.Rng.Next(-1, 1) * Globals.Settings.ResourceSpread);
            attackResources = Helpers.Clamp(attackResources, Globals.ResourceGenericMax);

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

                    // DE-CONSTRUCTOR!
                    (string target, SystemStatus system) = map.GetRandomElement();

                    if (system == null)
                    {
                        Log("CRITICAL:  No system found at AllocateAttackResources, aborting processing.");
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
                    spendAR = Helpers.Clamp(spendAR, Globals.ResourceGenericMax);
                    var maxValueList = system.influenceTracker.Values.OrderByDescending(x => x).ToList();
                    var pMaxValue = 200.0f;
                    if (maxValueList.Count > 1)
                        pMaxValue = maxValueList[1];

                    var itValue = system.influenceTracker[warFaction.faction];
                    var basicAR = (float) (11 - system.DifficultyRating) / 2;
                    var bonusAR = 0f;
                    if (itValue > pMaxValue)
                        bonusAR = (itValue - pMaxValue) * 0.15f;

                    var totalAR = basicAR + bonusAR + spendAR;
                    if (targetFar > totalAR)
                    {
                        system.influenceTracker[warFaction.faction] += totalAR;
                        targetFar -= totalAR;
                        targetWarFaction.defenseTargets.Add(system.name);
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
            if (warFaction.defenseTargets.Count == 0)
                return;

            var faction = warFaction.faction;
            var defensiveResources = warFaction.DefensiveResources + warFaction.DR_Against_Pirates;
            defensiveResources = Helpers.Clamp(defensiveResources, Globals.ResourceGenericMax);

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
                var spendDr = Mathf.Min(startingDefensiveResources * drFactor, defensiveResources);
                spendDr =  spendDr < 1 ? 1 : Math.Max(1 * Globals.SpendFactor, spendDr * Globals.SpendFactor);
                spendDr = Helpers.Clamp(spendDr, Globals.ResourceGenericMax);

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
                    var bonusDefense = spendDr + (diffRes * total - Globals.Settings.TakeoverThreshold / 100 * total) / (Globals.Settings.TakeoverThreshold / 100 + 1);
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
            }
        }*/  
   
    }
}

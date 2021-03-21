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
        public bool hasDoneDistrobution;               //used as a check to make sure the system does not distribute resources more then once per tick.
        public float AttackResources;
        public float DefenceResources;
        public float TotalResources;
        
        public Resource()
        {

        }

        public Resource(StarSystem system)
        {
            BaseSystemAttackResources = Helpers.GetTotalAttackResources(system);
            BaseSystemDefenceResources = Helpers.GetTotalDefensiveResources(system);
            hasDoneDistrobution = false;
        }

        //TODO Remember to edit this code block, when re-introducing Influence.
        //this will more then likely get changed when influence gets re-introduced.
        public void AddBaseWarTickResourcesForSystem()
        {
            AttackResources += BaseSystemAttackResources;
            DefenceResources += BaseSystemDefenceResources;
            TotalResources = AttackResources + DefenceResources;
        }


        // TODO go through this code.
        // putting in notes, there are somethings that i don't quiet understand in it
        // Will repurpose modify/take parts that are needed
        // So far my understanding is this takes the factions total attack resources
        // divides it all up. Then stores the total amount that is going to be used agaisnt each other faction it is currently
        // in combat with. Assuming this gets carried on in the next method AllocateAttackResources.
        public static void DivideAttackResources(WarFaction warFaction, bool useFullSet)
        {
            //Log("Attacking");
            var deathList = warFaction.DeathListTracker;  // Still really need to properly figure out what this is (assumed systems faction is attacking)
            var warFar = warFaction.warFactionAttackResources; //is this attack resources spent against each faction ? key == faction value==attackresources
            warFar.Clear();

            var tempTargets = new Dictionary<string, float>(); //targets current faction is attacking

            //gets all systems on the factions deathlist
            foreach (var fact in warFaction.attackTargets.Keys)
            {
                tempTargets.Add(fact, deathList.deathList[fact]);
            }

            var total = tempTargets.Values.Sum();  //total amount of systems in combat with


            // this one line wouldve been making some pretty heafty negative values, while the pirates math was going awol
            var attackResources =  warFaction.AttackResources - warFaction.AR_Against_Pirates;  //total amount of attack resources

            //attackResources = Helpers.Clamp(attackResources, Globals.ResourceGenericMax);
            //---look---at-this-some-more-
            if (warFaction.ComstarSupported)
                attackResources += Globals.Settings.GaW_Police_ARBonus;

            warFaction.AR_Against_Pirates = 0;

            if (Globals.Settings.AggressiveToggle && !Globals.Settings.DefensiveFactions.Contains(warFaction.faction))
                attackResources += Globals.Sim.Constants.Finances.LeopardBaseMaintenanceCost;

            //not sure why this is necessary.
            attackResources = attackResources * (1 + warFaction.DaysSinceSystemAttacked * Globals.Settings.AResourceAdjustmentPerCycle / 100);

            //attackResources = Helpers.Clamp(attackResources, Globals.ResourceGenericMax);

            //potential negative value -1,1
            attackResources += attackResources * (float) (Globals.Rng.Next(-1, 1) * Globals.Settings.ResourceSpread); 
            //---end--looking--------------

            ///attackResources = Helpers.Clamp(attackResources, Globals.ResourceGenericMax);

            //warFar changes never do anything, unless DIctionarys are passed by ref nativly.
            //TODO check how dictionarys are passed
            //some of this doesn't add up sit right with me, more code to follow, but since i will be 
            //doing processing differently, by a system calculating it's own resources do not believe much of the code
            //in here will be used.
            foreach (var rfact in tempTargets.Keys)
            {
                warFar.Add(rfact, tempTargets[rfact] * attackResources / total); //ahh warfar is pretty much just a label/pointer that refers to
            }                                                                    //warFaction.warFactionAttackResources
        }

        //TODO go through this code.
        public static void AllocateAttackResources(WarFaction warFaction)
        {
            //contracts--code-------
            var factionRep = Globals.Sim.GetRawReputation(Globals.FactionValues.Find(x => x.Name == warFaction.faction));
            var maxContracts = HotSpots.ProcessReputation(factionRep);
            //end------contracts---code----- 


            if (warFaction.warFactionAttackResources.Count == 0)
                return;

            var warFactionAR = warFaction.warFactionAttackResources;
            //Go through the different resources allocated from attacking faction to spend against each targetFaction
            var deathListTracker = warFaction.DeathListTracker;
            foreach (var targetFaction in warFactionAR.Keys.Intersect(warFaction.attackTargets.Keys))
            {
                var targetFar = warFactionAR[targetFaction]; //gets resources allocated to swing at other faction
                var startingTargetFar = targetFar;
                var attackTargets = warFaction.attackTargets[targetFaction];  //gets the list of systems names in combat with, of the currently selected faction

                // makes a list of systemStatus of the currents systems of the current faction this faction is at war with
                var map = new Dictionary<string, SystemStatus>();
                foreach (var targetName in attackTargets)
                {
                    map.Add(targetName, Globals.WarStatusTracker.systems.Find(x => x.name == targetName));
                }

                var hatred = deathListTracker.deathList[targetFaction];  // I dont actualy know what the float is refering to for this, AR or DR ?
                                                                         // or something else entirely

                var targetWarFaction = Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == targetFaction); //gets opposong factions warfaction info

                // this loop keeps going untill the total amount of resources to use against the current enemy faction is exausted
                // targetFar is the total allocated resource
                while (targetFar > 0 && attackTargets.Count > 0)
                {

                    // DE-CONSTRUCTOR!
                    (string target, SystemStatus system) = map.GetRandomElement(); // gets a random system or a return value of null

                    if (system == null)
                    {
                        Log("CRITICAL:  No system found at AllocateAttackResources, aborting processing.");
                        return;
                    }

                    //this looks like a filter check to catch anything that should not have gotten through, or is currently immune to war.
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
                        system.PriorityAttack = true; //another item i'm not so sure on, have to dig some to find out.


                        if (!system.CurrentlyAttackedBy.Contains(warFaction.faction)) //does a check to see if the opposing system is currently beiing attacked
                        {                                                             //by this faction
                            system.CurrentlyAttackedBy.Add(warFaction.faction);
                        }

                        if (!Globals.WarStatusTracker.PrioritySystems.Contains(system.starSystem.Name)) 
                        {
                            Globals.WarStatusTracker.PrioritySystems.Add(system.starSystem.Name);
                        }
                    }

                    //Distribute attacking resources to systems.
                    //TODO find out what triggers/flags the Contended status
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

                    // this calculates the total amount of resources the current system will get upto the the total amount that can potentially be
                    // allocated to it. 
                    var spendAR = Mathf.Min(startingTargetFar * arFactor, targetFar);

                    spendAR = spendAR < 1 ? 1 : Math.Max(1 * Globals.SpendFactor, spendAR * Globals.SpendFactor); //Spend factor could inflate value considerably

                    //clamp that had been put in many places to try get rid of the crazy negative values.
                    spendAR = Helpers.Clamp(spendAR, Globals.ResourceGenericMax);

                    var maxValueList = system.influenceTracker.Values.OrderByDescending(x => x).ToList();

                    var pMaxValue = 200.0f;          //What is P ?

                    if (maxValueList.Count > 1)
                        pMaxValue = maxValueList[1];  //why 1 instead of 0

                    var itValue = system.influenceTracker[warFaction.faction]; //should be a value between or equal to 0 and 100

                    var basicAR = (float) (11 - system.DifficultyRating) / 2; //I changed this from memory it was creating a much larger value and wasnt / 2
                                                                              //just seemed like there was way to much inflation of values
                                                                              //almost like numbers were inflated so more weird dodgy math could be added to
                                                                              //try fix a problem.
                    var bonusAR = 0f;

                    if (itValue > pMaxValue)                                  //either ive missed something or this code should never have to be called
                        bonusAR = (itValue - pMaxValue) * 0.15f;

                    var totalAR = basicAR + bonusAR + spendAR;                // actuall amount of AR being swung at the current enemy system                

                    //looking back at things I modified this block of code originally not realising what it did.
                    //from memory this one block, affects overaul influence way more then you would expect.
                    if (targetFar > totalAR)
                    {
                        system.influenceTracker[warFaction.faction] += totalAR; //what i don't get is why boost the enemys influence by the amount of resources
                                                                                //they lose.  It is possible that at the time i may have changed them to +
                                                                                //while trying to get rid of negative values. ahh yes because of the resource
                                                                                //system getting mangled and throwing massive negative values everywhere
                                                                                //influence was all over the place as well.
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

        //TODO go through this code.
        public static void AllocateDefensiveResources(WarFaction warFaction, bool useFullSet)
        {
            if (warFaction.defenseTargets.Count == 0)                  // not going to change it, but systems defending would be easier to understand
                                                                       // or defendingSystems. just checks if the current faction is defending itself from
                                                                       // another faction or not.
                return;

            var faction = warFaction.faction;
            var defensiveResources = warFaction.DefensiveResources + warFaction.DR_Against_Pirates; // calculates the total amount of the current factions pool
                                                                                                    // of defence resources.
            // was clamped to make sure the value was not negative.
            defensiveResources = Helpers.Clamp(defensiveResources, Globals.ResourceGenericMax);

            if (warFaction.ComstarSupported)
                defensiveResources += Globals.Settings.GaW_Police_DRBonus;

            warFaction.DR_Against_Pirates = 0;              // not sure why this gets set to 0, the resources have already been applied to the factions
                                                            // total defence resources. Maybe i made that change last year ?
                                       
            if (Globals.Settings.AggressiveToggle && Globals.Settings.DefensiveFactions.Contains(warFaction.faction))
                defensiveResources += Globals.Sim.Constants.Finances.LeopardBaseMaintenanceCost;   // gonna take a stab in the dark and assume that is adding
                                                                                                   // a negative value.

            // TODO follow the Globals
            // Currently not sure why this exists, player added difficulty ?
            var defensiveCorrection = defensiveResources * (100 * Globals.Settings.GlobalDefenseFactor -
                                                            Globals.Settings.DResourceAdjustmentPerCycle * warFaction.DaysSinceSystemLost) / 100;

            defensiveResources = Math.Max(defensiveResources, defensiveCorrection);
            defensiveResources += defensiveResources * (float) (Globals.Rng.Next(-1, 1) * Globals.Settings.ResourceSpread);


            var startingDefensiveResources = defensiveResources;
            var map = new Dictionary<string, SystemStatus>();

            // makes a dictionary of SystemStatus of this factions defending systems.
            foreach (var defenseTarget in warFaction.defenseTargets.Distinct())
            {
                map.Add(defenseTarget, Globals.WarStatusTracker.systems.Find(x => x.name == defenseTarget));
            }


            // spend and decrement defensiveResources
            while (defensiveResources > 0.0f)  
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

                var total = systemStatus.influenceTracker.Values.Sum();  // Influence code, gets total influence, should equal 100 

                // sorts influence by faction name and influence is greater then 0, I think?
                var sequence = systemStatus.influenceTracker
                    .Where(x => x.Value != 0)
                    .Select(x => x.Key);

                foreach (var factionStr in sequence)                                // finds the first faction with influence greater then or equal to 50 
                                                                                    // and exits loop imidiately
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
                        systemStatus.influenceTracker[faction] += spendDr;         // gives the owner faction in the system more influence if they are still
                                                                                   // the main influence holder of the system.
                        defensiveResources -= spendDr;
                    }
                    else
                    {
                        systemStatus.influenceTracker[faction] += defensiveResources; // this should probably just be commented out, or removed.
                        defensiveResources = 0;
                    }
                }
                else
                {
                    // is processed when the owner faction no longer has majority Influence
                    // There is one thing that confuses me most, shouldnt someone who is defending be reducing an attackers attack points with defence points
                    // in both the attack and defence methods, atk reduces atk and def reduces def. You may have atk vs atk posibly, but def vs def makes no sense at all.
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
        }   
    }
}

using System;
using System.Collections.Generic;

namespace GalaxyatWar
{
    /***************************************************************************
     * 
     * The intended purpose of this class is to resolve combat between systems.
     * It will use the trackers that are already implimented in WarStatus.
     * It will also resolve the change in resources from combat.
     * 
     * It will not generate resources.
     * It will not change influence in anyway.
     *     
     * The way it works should be Resource sets systems base resources and current usable resources.
     * Combat manages trackers and Resource loss between systems
     * Influence, does Influence processing and nothing else.
     *     
     * Order of use, would reliably be; Resource > Combat > Influence 
     * 
     ***************************************************************************/    
    public class Combat
    {
        //class variables here
        internal string sysName;
        internal string sysFaction;
        internal WarFaction warFaction;
        readonly Resource sysResource;

        public Combat()
        {
        }

        public Combat(string systemName, string faction, SystemStatus nSystems, Resource resource)
        {
            sysName = systemName;
            sysFaction = faction;
            sysResource = resource;
            warFaction = Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == sysFaction);
        }

        /*
         * Not feeling too spectaculary well today, want to get this started though.
         * Will have to come back and do a lot of fixing at a guess. 03-24-2021
         */

        /******************************************************************************
         * Made a method for this as it was sidelining me a bit.
         *         
         * Finds the next most valuable system/target to attack
         * It is part of the old combat code that is buried in resources.
         * Yet it is pretty important.
         * 
         ******************************************************************************/
        internal void FindMostValubleTarget(SystemStatus system, DeathListTracker deathListTracker)
        {
            //contracts--code-------
            var factionRep = Globals.Sim.GetRawReputation(Globals.FactionValues.Find(x => x.Name == warFaction.faction));
            var maxContracts = HotSpots.ProcessReputation(factionRep);
            //end------contracts---code----- 

            //Hatred--------------------------this could almost just be a segment on it's own
            var hatred = deathListTracker.deathList[system.owner];
            var targetWarFaction = Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == system.owner);
            //Continue from here have to go out.

            //Find most valuable target for attacking for later. Used in HotSpots.----------------------
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
            //end hatred--------------------------------------------------------------------------------
        }

        /***********************************************************************************************
         *        
         * Going to rename this.
         *         
         * Will eventually do the attack combat related stuff.
         * This will be better described when i don't have a headache.
         *         
         ************************************************************************************************/
         public void AttackResolve(List<SystemStatus> nSystems)
         {
            //TODO (Check this) May just move this into FindMostValubleTarget, Havent checked if it is even used outside of it.
            var deathListTracker = warFaction.DeathListTracker;
            //(n) means Neighbor
            List<SystemStatus> nSystemsThatCanBeAttacked = new List<SystemStatus>();

            // Determines what neighbor systems can actually be attacked
            foreach (SystemStatus systemStatus in nSystems)
            {
                // TODO keep an eye on that Globals.WarStatusTracker.HotBox.Contains(systemStatus.name) condition
                if (warFaction.attackTargets[systemStatus.owner].Contains(systemStatus.name) && !(systemStatus.owner == sysFaction && 
                    Globals.WarStatusTracker.FlashpointSystems.Contains(systemStatus.name) && Globals.WarStatusTracker.HotBox.Contains(systemStatus.name)))
                {
                    FindMostValubleTarget(systemStatus, deathListTracker);
                    nSystemsThatCanBeAttacked.Add(systemStatus);
                }
            }

            int numSystemsToAtk = nSystemsThatCanBeAttacked.Count;
            float aRToHitEachNSystemWith = (sysResource.AttackResources - sysResource.BaseSystemAttackResources) / numSystemsToAtk;

            if (aRToHitEachNSystemWith > 0)
            {
                foreach (SystemStatus system in nSystemsThatCanBeAttacked)
                {
                    SpendAttackResources(system, nSystemsThatCanBeAttacked.Count);
                }
            }
        }

        /*
         * wasn't sure how resource information should be passed through
         * so currently as a read only field.
         * 
         * There are people who would change it, but if you do beware, it may cause
         * you problems if your no good at tracking issues (know I definantly do).
         * May just end up changing it purely because of the already stated reason.
         * But for now, it is what it is.
         * 
         * Thinking may just let systems do a full swing (everything they have atk
         * wise) above their base attack.
         * 
         * Just to see what happens, im assumeing a lot of systems are going to have no defence afterwards.
         * Going to try a different approach to others being when a system attacks another
         * The attacking system loses attack resources and the defending system loses defence reources.
         * 
         * atk vs atk could happen but def vs def makes no sense to me. atk vs def would be the most consistant.
         * best case scenario if the system swinging hits for more then the enemies def, check if the enemy has attack
         * and reduce that value first. or just do a check if TAr == 0 then go ham on the TDr
         * 
         * Was thinking of having an overflow, havent thought of how to impliment it though.
         * But the idea is, when the enemy system has no resources if someone attacks it they get an overflow value 
         * for their faction towards that system and that may get added 
         * to the influence calculations in the influence class, when influence is processed.
         * 
         * ^^ may not happen it is just an idea, currently just wiring everything to work.
         *         
         */        
        internal void SpendAttackResources(SystemStatus system, float aRToAttackNeighborWith)
        {
            float excess = system.systemResources.AttackResources - aRToAttackNeighborWith;
            if (excess >= 0f)
            {
                 //remove attack from neighbor systems attack.
            }
            else if (excess < 0 && system.systemResources.DefenceResources >= Math.Abs(excess))
            {
                // set neighbors atk to 0
                // subtract excess from neighbors defence resources
            }
            else
            {
                // set both neighbor atk and def values to zero
                // pretty sure im missing something here, need to have a break for a bit.

                //----------------Potential--------------influence-----idea-------will-not-be-coded-here----
                // potential excess to mess with not sure if this will be used.
                // maybe excess from factions gets compaired to each other (if there is more then one faction with excess)
                // the faction that has excess remaining gets an influence boost (remaining opposing factions in system duke it out)
                //-------------end------potential-----influence-----idea-------------------------------------
            }
            //remove attack from self and selfs total resources
        }

        //not even sure if this will end up being needed
        public void DefenseResolve()
        {

        }

        /*public void CodeDump()
        {
            var deathListTracker = warFaction.DeathListTracker;

            //sinse a system is checking itself no loop is needed to determin faction.
            //this code will need to be adjusted to reflect that.
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
        }*/
    }
}

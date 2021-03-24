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

        public Combat()
        {
        }

        public Combat(string systemName, string faction, SystemStatus nSystems)
        {
            sysName = systemName;
            sysFaction = faction;
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

            foreach (SystemStatus systemStatus in nSystems)
            {
                if (warFaction.attackTargets[systemStatus.owner].Contains(systemStatus.name) && !(systemStatus.owner == sysFaction && 
                    Globals.WarStatusTracker.FlashpointSystems.Contains(systemStatus.name) && Globals.WarStatusTracker.HotBox.Contains(systemStatus.name)))
                {
                    FindMostValubleTarget(systemStatus, deathListTracker);

                    //Actual resource code starts here, think this may all need a few go overs at some point
                    //Going to make another method to put inside here that does the math for combat.
                    //TODO Starting point for tomorrow. Thought I'd be a bit further along then this today, but it is what it is.
                    
                }
            }
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

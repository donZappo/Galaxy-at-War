Focussed a bit of time recently, to look at how the old resource code works.
To keep the code in line with as much of the old code as possible, so that a lot of the helper code doesn't break.
Need to make sure, the correct trackers are being used in the new code base.
Only realy at the stage, where correct trackers need to be applied and testing of resource distrobution, both adding and removing is working at least resonably well.
Without using influence.

Edit:
Will be overhauling resource distrobution and if necessary influence processing as well.
Influence should more then likely have a class of it's own so it is easier to track, as it seems to be tied into many things.
In it's current state, it is not easy to trace problems. If required will be stripping things back and re-doing them in a modular fasion.
To make sure each facet works properly before re-adding the next one.
end edit:

# Galaxy At War

In Galaxy at War, the Great Houses of the Inner Sphere will not simply wait for a wedding invitation to show their disdain for each other. To that end, war will break out as petty bickering turns into all out conflict. Your reputation with the factions is key - the more they like you, the more they'll bring you to the front lines and the greater the rewards. Perhaps an enterprising mercenary could make their fortune changing the tides of battle and helping a faction dominate the Inner Sphere.


### New features in Galaxy at War:

- Each planet generates Attack Resources and Defensive Resources that they will be constantly spending to spread their influence and protect their own systems.
- Planetary Resources and Faction Influence can be seen on the Star Map by hovering over any system.
- Successfully completing missions will swing the influence towards the Faction granting the contract.
- Target Acquisition Missions & Attack and Defend Missions will give a permanent bonus to the winning faction's Attack Resources and a permanent deduction to the losing faction's Defensive Resources.
- If you accept a travel contract the Faction will blockade the system for 30 days. A bonus will be granted for every mission you complete within that system during that time.
- Pirates are active and will reduce Resources in a system. High Pirate activity will be highlighted in red.
- Sumire will flag the systems in purple on the Star Map that are the most valuable local targets.
- Sumire will also highlight systems in yellow that have changed ownership during the previous month.
- Hitting Control-R will bring up a summary of the Faction's relationships and their overall war status.
- Shift-Click on any item on the Timeline to accelerate the movement of time until that item is reached.


### Installation Notes:

- Requires ModTek. 
- Copy the GalaxyAtWar folder into your BATTLETECH\Mods directory. 


### Acknowledgements

This mod takes inspiration from Morphyum's WarTech mod, the previous standard in Battletech sandbox play. Parts of Morphyum's code was also utilized from WarTech, ContractSort, and RandomTravelContracts. 

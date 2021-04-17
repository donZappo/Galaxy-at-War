A pretty big snag was hit so it wasn't pushed to this fork.

Whenever removing resources happened, resources went up, there was no reason for it to be doing that.

Adding resources before removal being added, was working perfectly fine.

Figured, either my removal code was bad (possible), something that I havent touched changes it again (possible), or there is something wrong with serialization (no idea, also possible?).

Serialization being the toughest one to do anything with, figured will focus on that for a while.

After doing some reading on how some mobile app devs deal with saving and loading using sqlight, I believe sqlight could be a better alternative to save and load mod data for Battletech.

So for now, this will be dorment.
Another repo will be made, to do with adding save/load features with sqlight(if possible).
Would rather try and possibly fail, then not try at all.

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

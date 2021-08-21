# CustomRegionSaves  v0.1.1     

Author: Deltatime

CustomRegionSaves is a mod that allows the game to remain playable after adding/removing custom regions!
Objects, spawns, sticks, and consumables should remain in their correct locations/states after adding/removing custom regions!
Creatures that spawn/have a den in a pipe that has been removed/made invalid will attempt to move into a nearby valid pipe!
The mod should work with both CRS and merge regions.
*Note that this mod cannot restore an already corrupted file, and should only prevent the future corruption of files that already work.

To add custom regions to a vanilla playthrough or add/remove custom regions from a modded playthrough: 
  Enable CusotmRegionSaves without adding/removing the custom region
  Save the game at least once
  Once the game has been saved (usually by ending a cycle) it is safe to add/remove your new custom regions!

On this version things such as respawns, lineages, discoveredShelters and maps are not synced so they will probably not work as well after adding/removing a region.

#### IT IS RECOMMENDED THAT THIS MOD IS ALWAYS ACTIVE!
-------------------------------------------------
### Installation

#### Using Bepinex + BOI:
  1) Move CustomRegionsSaves.dll into "Rainworld/Mods"
  2) Enable mod and launch game through BOI
#### Using Bepinex ONLY:
  1) Move CustomRegionsSaves.dll into "Rainworld/bepinex/plugins"
  2) Run the game

-------------------------------------------------
### ChangeLog

#### Version 0.1.1 : Bugfix
 - Fixed bug where the game crashes when a creature den had a creature type that's not in the vanilla enum while being outside of a region.
#### Version 0.1.0 : initial release
 - Syncs the location of objects, sticks, consumedItems, and population
 - Fixes the location of any spawners and population that are in a pipe that is not a den.
 - Prevents crash from having a custom region in a save file after removing it.
 * Does not sync creatures to their spawners, so killing a creature may not trigger it to respawn/lineage.
 * Does not sync lineage progress
 * Does not sync map progress, so maps may still be messed up

-------------------------------------------------
### How (Den/Spawn) fix works 

  All spawners/creatures that have a valid abstract node position will go to their corresponding abstract nodes.
  if a spawner/creature's abstractnode is not a den, then this mod will move that spawner to nearest empty den index. 
  This means that if a spawner is in den 4 and the only other dens in a room are dens 6 and 1, then that spawner would be moved to den 1.
  If there is a tie for the nearest empty den, then the den with the lower index will be taken
  If there are no empty dens for spawners/creatures, the spawner/creature will take the closest valid den.
  If there are no dens in a room the creature/spawner will remain in its original location

using System.Collections.Generic;
//TODO - Creature spawner translation (hopefully this fixes any creatures in the respawn list)

//Note: This might have to split into 2 parts, but how will I get them across the function (most likely a static class/value...)

//Respawns have two parts: The translation of the rooms and the actual respawn correcting?
//ISSUE : You cannot go back and change creature respawns once they are made..?
/* Notes
 * 1st - Room translations
 * 2nd - Spanwers created
*/


//TODO - Out of region/Invalid respawns

namespace CustomRegionSaves {

    public class RespawnCorrector {
        List<Respawn> respawns;

        //sets up the respawn corrector for this save state.

        public RespawnCorrector() {
            respawns = new List<Respawn>();
        }

        /* 
         * Strings need to be in the following format (The example uses 3 elements)
         * "RespawnId:Den|RespawnId:Den|RespawnId:Den"
        */


        public void FixRespawns(SFRegionState regionData) {
            SFLogSource log = new SFLogSource("RespawnCorrector::FixRespawns");
            if (regionData == null) {
                //ERROR
                return;
            }
            int lostRespawns = 0;
            int fixedRespawns = 0;
            int failedRespawns = 0;
            foreach (Respawn r in respawns) {
                foreach (SFRegionState.RoomTranslation roomCorrection in regionData.roomTranslations) {
                    if (r.respawnDen.room == roomCorrection.roomFrom) {
                        if (roomCorrection.roomTo != null) {
                            r.respawnDen.room = roomCorrection.roomTo ?? -1;
                            //Now check for a spawner corresponding to the den position in this room.
                            log.LogDebug($"Respawn {r.origRespawnId} is at worldCoordinate {r.respawnDen}");
                            foreach (World.CreatureSpawner spawner in regionData.regionState.world.spawners) {
                                if (spawner.den.room == r.respawnDen.room) {
                                    log.LogDebug($"Found spawner in the same room as respawn at worldCoordinate {spawner.den}");
                                    if (spawner.den.x == r.respawnDen.x && spawner.den.y == r.respawnDen.y) {
                                        if (spawner.den.abstractNode == r.respawnDen.abstractNode) {
                                            r.respawnDen.abstractNode = spawner.den.abstractNode;
                                            r.shiftedRespawnId = spawner.SpawnerID;
                                            log.LogDebug($"Found matching spawner for respawn! Translation created: {r.origRespawnId} >> {r.shiftedRespawnId}");
                                            ++fixedRespawns;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (r.shiftedRespawnId == null) {
                                log.LogWarning("Could not find corresponding spawner for respawn");
                                ++lostRespawns;
                            }
                            //Eventually save these so that they can be brought back (lost respawns)
                        } else {
                            ++failedRespawns;
                            //Failure to fix respawn position. (make into lost respawn)
                        }
                    }
                }
            }
            log.LogInfo($"Finished fixing respawns. Completed:{fixedRespawns}, Lost:{lostRespawns}, Failed:{failedRespawns}");
        }

        public void RespawnsFromGame(RegionState gameState) {
            SFLogSource l = new SFLogSource("RespawnCorrector::RespawnsFromGame");
            if (gameState == null) { l.LogError("GameState is null"); return;  }
            if (gameState.saveState == null) { l.LogError("SaveState is null"); return; }
            if (gameState.world == null) { l.LogError("World is null"); return; }
            l.LogDebug("Respawns being loaded from game");
            if (gameState.saveState.respawnCreatures == null || gameState.saveState.waitRespawnCreatures == null) { l.LogError("Respawn/WaitRespawn creatures is null."); return; }
            respawns.Clear();
            foreach (int respawnId in gameState.saveState.respawnCreatures) {
                World.CreatureSpawner spawner = gameState.world.GetSpawner(new EntityID(respawnId, -1));
                if (spawner != null) {
                    Respawn r = new Respawn(respawnId, spawner.den, false);
                    l.LogDebug($"Adding respawn : {r}");
                    respawns.Add(r);
                } else {
                    l.LogError($"Spawner {respawnId}  is invalid!");
                }
            }
            foreach (int respawnId in gameState.saveState.waitRespawnCreatures) {
                World.CreatureSpawner spawner = gameState.world.GetSpawner(new EntityID(respawnId, -1));
                if (spawner != null) {
                    Respawn r = new Respawn(respawnId, spawner.den, true);
                    l.LogDebug($"Adding respawn : {r}");
                    respawns.Add(r);
                } else {
                    l.LogError("Spawner is invalid!");
                }
            }
            l.LogDebug($"Loaded {gameState.saveState.respawnCreatures.Count + gameState.saveState.waitRespawnCreatures.Count} respawns");
        }

        public void RespawnsFromString(string str) {
            SFLogSource l = new SFLogSource("RespawnCorrector::RespawnsFromString");
            l.LogDebug("Respawns being loaded from string");
            if (str == null) {
                l.LogError("Input string is null.");
                return;
            }
            respawns.Clear();
            string[] allRespawnsAsStrings = str.Split('|');
            int respawnsCompleted = 0;
            foreach (string respawnString in allRespawnsAsStrings) {
                string[] respawnElements = respawnString.Split(':');
                if (respawnElements.Length >= 3) {
                    if (respawnElements.Length > 3) {
                        l.LogWarning("Respawn string has more than 3 elements, there may be file corruption present.");
                    }
                    if (int.TryParse(respawnElements[0], out int respawnid) && bool.TryParse(respawnElements[2], out bool isWaitRespawn)) {
                        try {
                            WorldCoordinate respawnDen = WorldCoordinate.FromString(respawnElements[1]);
                            Respawn r = new Respawn(respawnid, respawnDen, isWaitRespawn);
                            respawns.Add(r);
                            ++respawnsCompleted;
                            l.LogDebug($"Adding respawn : {r}");
                        } catch (System.FormatException ex) {
                            l.LogError($"Could not parse worldCoordinate the following element string: [{respawnElements[1]}] in respawn [{respawnString}].");
                        }
                    } else {
                        l.LogError($"Could not parse one of the elements for the following element strings: [{respawnElements[0]}] OR [{respawnElements[2]}] in respawn [{respawnString}].");
                    }
                } else {
                    l.LogError("Respawn string did not have enough elements, there may be file corruption present");
                }
            }
            l.LogDebug($"Completed loading respawns. Completed {respawnsCompleted}/{allRespawnsAsStrings.Length}");
        }

        public string GameRespawnsToString(RegionState region) {
            RespawnsFromGame(region);
            return CurrentRespawnsToString();
        }

        public string CurrentRespawnsToString() {
            SFLogSource l = new SFLogSource("RespawnCorrector::CurrentRespawnsToString");
            string result = "";
            for (int i = 0; i < respawns.Count; ++i) {
                result += $"{respawns[i].shiftedRespawnId ?? respawns[i].origRespawnId}:{respawns[i].respawnDen.SaveToString()}:{respawns[i].isWaitRespawn}";
                if (respawns[i].shiftedRespawnId == null) { l.LogDebug($"Respawn {i} was not shifted, using the original respawn id..."); }
                if (i != respawns.Count - 1) { //Not last element
                    result += "|";
                }
            }
            l.LogDebug($"Respawns to string result: [{result}]");
            return result;
        }

        public List<AbstractRoom> GetRespawnDenRooms(RegionState region) {
            SFLogSource l = new SFLogSource("RespawnCorrector::GetRespawnDenRooms");
            List<AbstractRoom> respawnRooms = new List<AbstractRoom>();
            if (region.world == null) {
                l.LogError("World for the regionstate is null!");
                return respawnRooms;
            }
            foreach (Respawn r in respawns) {
                World.CreatureSpawner spawner = region.world.GetSpawner(new EntityID(r.shiftedRespawnId ?? r.origRespawnId, -1));
                if (r.shiftedRespawnId == null) { l.LogWarning("Respawn is not shifted, using the orig respawn id instead..."); }
                if (spawner != null) {
                    AbstractRoom room = region.world.GetAbstractRoom(spawner.den.room);
                    if (room != null) {
                        respawnRooms.Add(room);
                    } else {
                        l.LogError($"AbstractRoom for spawner {spawner.inRegionSpawnerIndex} is null");
                    }
                } else {
                    l.LogError($"Spawner for id {r.shiftedRespawnId ?? r.origRespawnId} is null!");
                }
            }
            return respawnRooms;
        }

    }

    public class Respawn {

        public int origRespawnId;
        public int? shiftedRespawnId;
        //The den that the respawn is set to (Rather than using ID a location is used)
        //The location can be shifted, and will need to be fixed before reading.
        public WorldCoordinate respawnDen;
        public bool isWaitRespawn;

        public Respawn(int respawnId, WorldCoordinate origDen, bool waitRespawn) {
            origRespawnId = respawnId;
            shiftedRespawnId = null;
            respawnDen = origDen;
            isWaitRespawn = waitRespawn;
        }

        public override string ToString() {
            return $"[Respawn Data : OrigId.{origRespawnId} ShiftedId.{shiftedRespawnId} Den.{respawnDen} WaitRespawn.{isWaitRespawn}]";
        }
    }
}

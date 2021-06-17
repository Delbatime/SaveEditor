using System.Collections.Generic;

namespace SaveFixer {
    public class SpawnerTranslationManager {
        SpawnerTranslationManager(RegionState region) {
            this.region = region;
            translations = new List<SpawnerTranslation>();
        }

        public void SpawnersToTranslation() {
            SFLogSource log = new SFLogSource("SpawnersToTranslation");
            log.EmptyLine();
            log.LogMessage($"Creating spawnertranslations from spawners | count : {region.world.spawners.Length}");
            for (int i = 0; i < region.world.spawners.Length; ++i) {
                World.CreatureSpawner currentSpawner = region.world.spawners[i];
                if (currentSpawner != null) {
                    AbstractRoom spawnerRoom = region.world.GetAbstractRoom(currentSpawner.den.room);
                    if (spawnerRoom != null) {

                    } else {
                        log.LogError($"Spawner {currentSpawner.SpawnerID} is not in region!");
                    }
                } else {
                    log.LogError("Spawner is null");
                }
            }
        }

        private List<SpawnerTranslation> translations;
        private RegionState region;
    }

    //Representation for a spawner translation. Immutable type.
    //SpawnerType should either be "simple" or "lineage"
    public struct SpawnerTranslation {
        SpawnerTranslation(int spawnerID, int roomIndex, int nodeIndex, string spawnerType) {
            Spawner = spawnerID;
            Room = roomIndex;
            Node = nodeIndex;
            Type = spawnerType;
        }

        public string Type { get; }
        public int Room { get; }
        public int Node { get; }
        public int Spawner { get; }
    }
}

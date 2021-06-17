using System.Collections.Generic;
using System;
using UnityEngine;
#pragma warning disable IDE0020 // Use pattern matching

namespace SaveFixer {
    class SpawnerDenFix {
        //Not called as of yet
        public static void WorldLoader_GeneratePopulation(On.WorldLoader.orig_GeneratePopulation orig, object self, bool fresh) {
            WorldLoader instance = self as WorldLoader;
            SFLogSource log = new SFLogSource("SpawnerDenFix::WorldLoader_GeneratePopulation");
            log.LogMessage($"Correcting nodes for spawners in region: {instance.world.name}");
            if (instance == null) {
                log.LogError("Instance defined in hook does not have the WorldLoader Type! How did you even do this??");
            }
            //Make sure spawners end up in a valid den.
            for (int i = 0; i < instance.spawners.Count; ++i) {
                AbstractRoom spawnerRoom = instance.world.GetAbstractRoom(instance.spawners[i].den.room);
                if (spawnerRoom != null && instance.spawners[i].den.NodeDefined) {
                    bool notInRange = false;
                    if (!(!(spawnerRoom.nodes.Length <= instance.spawners[i].den.abstractNode || instance.spawners[i].den.abstractNode < 0))) {
                        notInRange = true;
                        log.LogWarning($"Spawner {instance.spawners[i].SpawnerID} has a node outside of range! {spawnerRoom.name} nodeTotal: {spawnerRoom.TotalNodes} | requested node: {instance.spawners[i].den.abstractNode}");
                    }
                    if (notInRange || instance.world.GetNode(instance.spawners[i].den).type != AbstractRoomNode.Type.Den) {
                        log.Log($"Found a spawner that is not in a den: {instance.spawners[i].SpawnerID} | {spawnerRoom.name} ({instance.spawners[i].den.room}) | node:{instance.spawners[i].den.abstractNode}");
                        //TODO6 Make something that will find a den that does not already have a spawner, or add it to the closest den if it's already occupied.
                        int[] filledIndexes = GetUsedDensInRoom(instance.spawners[i].den.room, instance.spawners, i);
                        bool closestDir = false; //False if left, true is right
                        int? closestDistance = null;
                        int? closestValidIndex = null;
                        //DEBUG
                        string s = "Nodes: ";
                        for (int l = 0; l < spawnerRoom.TotalNodes; ++l) {
                            s = string.Concat(new object[] { s, spawnerRoom.nodes[l].type.ToString(), " " });
                        }
                        log.LogDebug(s);
                        //-DEBUG
                        for (int d = 0; d < spawnerRoom.TotalNodes; ++d) {
                            if (spawnerRoom.nodes[d].type == AbstractRoomNode.Type.Den) {
                                bool alreadyFilled = false;
                                for (int f = 0; f < filledIndexes.Length; ++f) {
                                    alreadyFilled = alreadyFilled ? true : d == filledIndexes[f];
                                }
                                if (alreadyFilled) {
                                    int distance;
                                    distance = Mathf.Abs(instance.spawners[i].den.abstractNode - d);
                                    if (closestDistance != null) {
                                        if (distance < closestDistance) {
                                            closestDistance = distance;
                                            closestDir = d <= instance.spawners[i].den.abstractNode;
                                        }
                                    } else {
                                        closestDistance = distance;
                                        closestDir = d <= instance.spawners[i].den.abstractNode;
                                    }
                                    log.LogDebug($"Closest (invalidOrValid) index: {instance.spawners[i].den.abstractNode + (distance * (closestDir ? -1 : 1))}");
                                } else {
                                    int distance;
                                    distance = Mathf.Abs(instance.spawners[i].den.abstractNode - d);
                                    if (closestValidIndex != null) {
                                        if (distance < Mathf.Abs(instance.spawners[i].den.abstractNode - closestValidIndex ?? 0)) {
                                            closestValidIndex = instance.spawners[i].den.abstractNode + (distance * (d <= instance.spawners[i].den.abstractNode ? -1 : 1));
                                            log.LogDebug($"New closest valid index found {closestValidIndex}");
                                        }
                                    } else {
                                        closestValidIndex = instance.spawners[i].den.abstractNode + (distance * (d <= instance.spawners[i].den.abstractNode ? -1 : 1));
                                        log.LogDebug($"Closest valid index found: {closestValidIndex}");
                                    }
                                }
                            }
                        }
                        if (closestValidIndex != null) {
                            log.LogDebug($"Corrected abstractNode for spawner: {closestValidIndex}");
                            instance.spawners[i].den.abstractNode = closestValidIndex ?? 0;
                        } else {
                            if (closestDistance != null) {
                                log.LogWarning("Could not find an empty abstractNode for spawner, setting it to be the closest den");
                                instance.spawners[i].den.abstractNode = instance.spawners[i].den.abstractNode + ((closestDistance ?? 0) * (closestDir ? -1 : 1));
                                log.LogDebug($"New abstractNode for spawner: {instance.spawners[i].den.abstractNode}");
                            } else {
                                log.LogError("Could not find any dens to move spawner to! Spawner will remain with it's original node");
                            }
                        }
                    }
                } else {
                    if (spawnerRoom != null) {
                        log.LogError($"Spawner {instance.spawners[i].SpawnerID} has no defined Node! {spawnerRoom.name} | {instance.spawners[i].den.ToString()}");
                    } else {
                        string s = string.Empty;
                        if (instance.spawners[i] is World.SimpleSpawner) {
                            World.SimpleSpawner spawner = (World.SimpleSpawner)instance.spawners[i];
                            s = string.Concat(new object[] { "Simplespawner: ", spawner.creatureType.ToString()}); 
                        } else if (instance.spawners[i] is World.Lineage) {
                            World.Lineage spawner = (World.Lineage)instance.spawners[i];
                            s = "Lineage: ";
                            for (int a = 0; a < spawner.creatureTypes.Length; ++a) {
                                if (spawner.creatureTypes[a] < spawner.creatureTypes.Length) {
                                    if (a >= Enum.GetNames(typeof(CreatureTemplate.Type)).Length) {
                                        if (a != spawner.creatureTypes.Length - 1) {
                                            s += Enum.GetNames(typeof(CreatureTemplate.Type))[spawner.creatureTypes[a]] + ", ";
                                        } else {
                                            s += Enum.GetNames(typeof(CreatureTemplate.Type))[spawner.creatureTypes[a]];
                                        }
                                    } else {
                                        s += $"ERR: {spawner.creatureTypes[a]} ";
                                    }
                                }
                            }
                        } else {
                            s = "Spawner is not a simplespawner or linage";
                        }
                        log.LogError($"Spawner {instance.spawners[i].SpawnerID} room is not in region! | {instance.spawners[i].den.ToString()} | Info: {s}");
                    }
                }
            }
            log.Log("Finished Correcting spawner nodes");
            log.EmptyLine();
            orig.Invoke(instance, fresh);
        }

        public static int[] GetUsedDensInRoom(int roomIndex, List<World.CreatureSpawner> spawnerList, params int[] excludedIndexes) {
            SFLogSource log = new SFLogSource("SpawnerDenFix::GetSpawnersInRoom");
            if (spawnerList != null) {
                List<int> filledDens = new List<int>();
                for (int i = 0; i < spawnerList.Count; ++i) {
                    if (spawnerList[i].den.room == roomIndex) {
                        bool excluded = false;
                        for (int t = 0; t < excludedIndexes.Length; ++t) {
                            excluded = excluded ? true : excludedIndexes[t] == i;
                        }
                        if (!excluded) {
                            if (spawnerList[i].den.NodeDefined) {
                                filledDens.Add(spawnerList[i].den.abstractNode);
                                log.LogDebug($"Found spawner in room {roomIndex} occupying den {spawnerList[i].den.abstractNode} : {spawnerList[i].SpawnerID}");
                            } else {
                                log.LogError($"Spawner {spawnerList[i].SpawnerID} does not have a defined node");
                            }
                        }
                    }
                }
                return filledDens.ToArray();
            } else {
                log.LogError("SpawnerList is null!");
                return null;
            }
        }

    }
}

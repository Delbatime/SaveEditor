//Author:Deltatime
//Progress: NONE
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using UnityEngine;
//TODO0: make it so that files will write with a variable amount of outOfRegionObjects
//TODO2: Add whatever to invalid creatures
//TODO3: Make is so that this actually corrects the worldCoordinate
//TODO4: Make it so that creatures won't be put into the same den unless it is the only option!
//TODO5: Make it so that a newly loaded region won't destroy creature spawns (configurable)??
namespace CustomRegionSaves {
    public class SFRegionState {
        
        //None of the values in SFRegionState should be null.
        public SFRegionState(RegionState saveFixRegion) {
            respawnFixer = new RespawnCorrector();
            roomTranslations = new List<RoomTranslation>();
            outOfRegionObjects = new List<OutOfRegionEntity>();
            outOfRegionPopulation = new List<OutOfRegionEntity>();
            outOfRegionSticks = new List<OutOfRegionEntity>();
            outOfRegionConsumedItems = new List<OutOfRegionEntity>();
            regionState = saveFixRegion;
        }

        //Loads the CRsavedata for the region associated with the regionstate that the SFRegionState was created with.
        //Region data is divided by <aDiv>
        //Result from readRegion(): (things in [] are elements/values rather than separators)
        //[0] - Roomshifts in region
        //<aDiv>
        //[1] - Lost entities in region
        //<aDiv>
        //[2] - Respawns in region
        public void LoadCRsavedata() {
            SFLogSource log = new SFLogSource("SFRegionState::LoadCRSavedata");
            log.Log("Loading CRSave data for region...");
            string regionText = SFFile.ReadRegion(regionState);
            if (!string.IsNullOrEmpty(regionText)) {
                string[] regiondataSections = Regex.Split(regionText, "<aDiv>");
                for (int i = 0; i < regiondataSections.Length; ++i) {
                    string[] regiondataSectionIdSplit = Regex.Split(regiondataSections[i], "<idA>"); //The data in a section and the id of a section is divided by a '|' character
                    if (regiondataSectionIdSplit.Length >= 2) {
                        if (int.TryParse(regiondataSectionIdSplit[0], out int sectionId)) {
                            switch (sectionId) {
                                case 0:
                                    log.LogVerbose("Dumping what is being passed to adaptTranslationsFromString: " + regiondataSectionIdSplit[1]);
                                    LoadRoomShiftsFromString(regiondataSectionIdSplit[1], true);
                                    break;
                                case 1:
                                    log.LogVerbose("Dumping what is being passed to adaptOutOfRegionEntitiesFromString: " + regiondataSectionIdSplit[1]);
                                    LoadOutOfRegionEntitiesFromString(regiondataSectionIdSplit[1], true);
                                    break;
                                case 2:
                                    log.LogVerbose("Respawns as String: " + regiondataSectionIdSplit[1]);
                                    respawnFixer.RespawnsFromString(regiondataSectionIdSplit[1]);
                                    break;
                                //To add more elements, add a new case here!
                                //Don't forget to add one to the write function
                            }
                        } else {
                            log.LogWarning($"Expected first element of division '|' to be an integer in regionDataSection {i}");
                        }
                    } else {
                        log.LogWarning($"Could not read regiondataSection {i}, expected at least 2 elements divided by '|'");
                    }
                }
            } else {
                log.LogWarning($"Could not get region {regionState.regionName} from save file");
            }
        }
        
        //Takes in a string with the following formatting: (things in [] are elements/values rather than separators)
        // [0] - Unshifted room index (roomFrom)
        //  :
        // [1] - Name of the room for the above mentioned index.
        // <tDiv>
        //If the room name of a roomShift cannot be matched then it is counted as "lost" and not have a shift destinition. These translations are not saved in the next file.
        //If the formatting of the string is invalid that roomShift will be not be saved in the next file.
        public void LoadRoomShiftsFromString(string roomShiftDataString, bool clear) {
            SFLogSource log = new SFLogSource("SFRegionState::CreateRoomShiftsFromString");
            if (string.IsNullOrEmpty(roomShiftDataString)) {
                log.LogError("Input string is empty or null");
                return;
            }
            if (clear) {
                roomTranslations.Clear();
                log.Log("Cleared previous roomShifts.");
            }
            string[] roomTranslationStrings = Regex.Split(roomShiftDataString, "<tDiv>");
            log.Log("Loading room shifts from CRsaves file. Count:" + roomTranslationStrings.Length);
            int loadedShifts = 0;
            int lostShifts = 0;
            int failedShifts = 0;
            for (int i = 0; i < roomTranslationStrings.Length; ++i) {
                string[] stringSplit = roomTranslationStrings[i].Split(new char[] { ':' });
                if (stringSplit.Length >= 2) {
                    if (int.TryParse(stringSplit[0], out int roomFrom)) {
                        bool foundRoomTo = false;
                        for (int r = regionState.world.firstRoomIndex; r < regionState.world.firstRoomIndex + regionState.world.NumberOfRooms; ++r) {
                            AbstractRoom roomInRegion = regionState.world.GetAbstractRoom(r);
                            if (roomInRegion != null && stringSplit[1].Equals(roomInRegion.name)) {
                                RoomTranslation newTranslation = new RoomTranslation(roomFrom, stringSplit[1], r);
                                roomTranslations.Add(newTranslation);
                                foundRoomTo = true;
                                ++loadedShifts;
                                log.LogVerbose($"Loaded room shift {newTranslation}");
                            }
                        }
                        if (!foundRoomTo) {
                            RoomTranslation newTranslation = new RoomTranslation(roomFrom, stringSplit[1]);
                            roomTranslations.Add(newTranslation);
                            log.LogVerbose($"Loaded lost room shift: {newTranslation}");
                            ++lostShifts;
                        }
                    } else {
                        log.LogWarning($"Room shift {i} des not have a valid format (expected integer for first value of elements divided by ':'): skipping...");
                        shouldCreateBackup = true;
                        ++failedShifts;
                    }
                } else {
                    log.LogWarning($"Room shift {i} does not have a valid format (expected 2 elements divided by ':'): skipping...");
                    shouldCreateBackup = true;
                    ++failedShifts;
                }
            }
            log.Log($"Loaded room shifts from CRSave file | Completed: {loadedShifts} Lost: {lostShifts} Failed: {failedShifts} Total: {roomTranslationStrings.Length}");
        }

        //Requires a string without the name of the dataSection
        //There are 4 types of outOfRegionEntities
        //Takes in a string with the following formatting: (things in [] are elements/values rather than separators)
        //[] - ID number for out of region objects type (0 - Object, 1 - Population, 2 - sticks, 3 - Consumables)
        //<idB> - division between id and data
        //[] - LostRoomName
        //:
        //[] - EntitiyData
        //<eDiv> - out of region entity division
        //<bDiv> - out of region entity type division
        public void LoadOutOfRegionEntitiesFromString(string outOfRegionDataString, bool clear) {
            SFLogSource log = new SFLogSource("SFRegionState::AdaptOutOfRegionEntitiesFromString");
            log.Log($"Loading OutOfRegionEntities from CRsaves file");
            string[] loadedOutOfRegionTypes = Regex.Split(outOfRegionDataString, "<bDiv>");
            log.LogDebug($"{loadedOutOfRegionTypes.Length} categories of outOfRegionEntities in file");
            for (int i = 0; i < loadedOutOfRegionTypes.Length; ++i) {
                string[] outOfRegionTypeIdSplit = Regex.Split(loadedOutOfRegionTypes[i], "<idB>");
                if (outOfRegionTypeIdSplit.Length >= 3) {
                    if (int.TryParse(outOfRegionTypeIdSplit[0], out int outOfRegionTypeId)) {
                        List<OutOfRegionEntity> outOfRegionEntityList = null;
                        switch (outOfRegionTypeId) {
                            case 0:
                                outOfRegionEntityList = outOfRegionObjects;
                                log.LogDebug("Selecting object list");
                                break;
                            case 1:
                                outOfRegionEntityList = outOfRegionPopulation;
                                log.LogDebug("Selecting population list");
                                break;
                            case 2:
                                outOfRegionEntityList = outOfRegionSticks;
                                log.LogDebug("Selecting stick list");
                                break;
                            case 3:
                                outOfRegionEntityList = outOfRegionConsumedItems;
                                log.LogDebug("Selecting consumedItems list");
                                break;
                        }
                        if (outOfRegionEntityList != null) {
                            if (clear) {
                                outOfRegionEntityList.Clear();
                            }
                            string[] outOfRegionEntityStrings = Regex.Split(outOfRegionTypeIdSplit[1], "<eDiv>");
                            for (int a = 0; a < outOfRegionEntityStrings.Length; ++a) {
                                string[] outOfRegionEntityAsArray = outOfRegionEntityStrings[a].Split( new char[] { ':' });
                                if (outOfRegionEntityAsArray.Length >= 2) {
                                    outOfRegionEntityList.Add(new OutOfRegionEntity(outOfRegionEntityAsArray[0], outOfRegionEntityAsArray[1]));
                                    log.LogVerbose($"Added outOfRegionEntity to list{outOfRegionTypeId}, dumping - {outOfRegionEntityAsArray[0]} : {outOfRegionEntityAsArray[1]}");
                                } else {
                                    log.LogWarning($"Could not read outOfRegionEntity index {a} in type {i}, expected to have 2 elements divided by ':'");
                                    shouldCreateBackup = true;
                                }
                            }
                        } else {
                            log.LogWarning($"There is no support for outOfRegionEntityType with an id of {outOfRegionTypeId}. Potential file corruption or invalid version.");
                            shouldCreateBackup = true;
                        }
                    } else {
                        log.LogWarning($"Expected value 0 in outOfRegionData {i} split by <idB>  to be an integer. Potential file corruption.");
                        shouldCreateBackup = true;
                    }
                } else {
                    log.LogError($"could not read outOfRegionType {i}, expected to have 2 elements divived by <idB>. Potential file corruption");
                    shouldCreateBackup = true;
                }
            }

            for (int i = 0; i < outOfRegionObjects.Count; ++i) {
                log.LogVerbose($"Dumping outOfRegionObjects{i}: {outOfRegionObjects[i]}");
            }
            for (int i = 0; i < outOfRegionPopulation.Count; ++i) {
                log.LogVerbose($"Dumping outOfRegionPopulation{i}: {outOfRegionPopulation[i]}");
            }
            for (int i = 0; i < outOfRegionSticks.Count; ++i) {
                log.LogVerbose($"Dumping outOfRegionSticks{i}: {outOfRegionSticks[i]}");
            }
            for (int i = 0; i < outOfRegionConsumedItems.Count; ++i) {
                log.LogVerbose($"Dumping outOfRegionConsumedItems{i}: {outOfRegionConsumedItems}");
            }
        }

        //Fixes RoomShifts, objects, population, sticks, and consumables, and attempts to restore out of region objects.
        //Fixes respawns
        //TODO: Restore invalid objects
        public void FixRegion() {
            SFLogSource log = new SFLogSource("SFRegionState::FixRegion");
            log.Log($"Fixing region (name:{regionState.regionName} firstroom:{regionState.world.firstRoomIndex} size:{regionState.world.NumberOfRooms})");
            ApplyRoomShiftsToObjects();
            ApplyRoomShiftsToPopulation();
            ApplyRoomTranslationsToSticks();
            ApplyRoomTranslationsToConsumables();
            log.Log($"Finished Fixing region. Out of region (objects | population | sticks | consumables) total count : ({outOfRegionObjects.Count} | {outOfRegionPopulation.Count} | {outOfRegionSticks.Count} | {outOfRegionConsumedItems.Count})");
        }

        //TODO : Add more documentation to the ApplyRoomShift functions (and maybe encapsulate them into their own file).

        void ApplyRoomShiftsToObjects() {
            SFLogSource log = new SFLogSource("SFRegionState::ApplyRoomShiftsToObjects");
            log.Log($"savedObjects count: {regionState.savedObjects.Count}");
            for (int i = 0; i < regionState.savedObjects.Count; ++i) {
                log.LogVerbose($"Dumping original object data: {regionState.savedObjects[i]}");
                AbstractPhysicalObject abstractObject = SaveState.AbstractPhysicalObjectFromString(regionState.world, regionState.savedObjects[i]);
                if (abstractObject != null) {
                    bool didAction = false;
                    for (int rt = 0; rt < roomTranslations.Count; ++rt) {
                        if (abstractObject.pos.room == roomTranslations[rt].roomFrom) {
                            if (roomTranslations[rt].roomTo != null) {
                                abstractObject.pos.room = roomTranslations[rt].roomTo ?? -1;
                                regionState.savedObjects[i] = abstractObject.ToString();
                                log.LogVerbose($"Found matching translation for object! Dumping edited object data: {regionState.savedObjects[i]}");
                            } else {
                                log.LogInfo($"Object matches with lost roomShift {roomTranslations[rt].roomName} ({roomTranslations[rt].roomFrom}), adding to invalid objects!");
                                outOfRegionObjects.Add(new OutOfRegionEntity(roomTranslations[rt].roomName, regionState.savedObjects[i]));
                                regionState.savedObjects[i] = null;
                            }
                            didAction = true;
                            break;
                        }
                    }
                    if (!didAction) {
                        log.LogInfo($"Could not find any matching translations, object {i} will stay at the original value");
                    }
                } else {
                    log.LogWarning($"Failed to create object from savedString: {i}");
                    shouldCreateBackup = true;
                }
            }
            //Remove any null savedObjects
            regionState.savedObjects.RemoveAll(x => { return x == null; });
        }

        void ApplyRoomShiftsToPopulation() {
            SFLogSource log = new SFLogSource("SFRegionState::ApplyRoomShiftsToPopulation");
            log.Log($"savedPopulation amount: {regionState.savedPopulation.Count}");
            //What is this part for?
            for (int i = 0; i < regionState.savedPopulation.Count; ++i) {
                log.LogVerbose($"orig creature data: {regionState.savedPopulation[i]}");
                string[] creatureData = Regex.Split(regionState.savedPopulation[i], "<cA>");
                if (creatureData.Length >= 3) {
                    string[] creatureWorldCoordinateSplit = creatureData[2].Split(new char[] { '.' });
                    if (creatureWorldCoordinateSplit.Length >= 2) {
                        if (int.TryParse(creatureWorldCoordinateSplit[0], out int creatureRoom) && int.TryParse(creatureWorldCoordinateSplit[1], out int creatureAbstractNode)) {
                            WorldCoordinate den = new WorldCoordinate(creatureRoom, -1, -1, creatureAbstractNode);
                            bool didAction = false;
                            for (int rt = 0; rt < roomTranslations.Count; ++rt) {
                                if (den.room == roomTranslations[rt].roomFrom) {
                                    if (roomTranslations[rt].roomTo != null) {
                                        den.room = roomTranslations[rt].roomTo ?? -1;
                                        string correctedCreatureString = string.Empty;
                                        creatureData[2] = string.Concat(new object[] { den.room, ".", den.abstractNode });
                                        for (int a = 0; a < creatureData.Length; ++a) {
                                            correctedCreatureString = string.Concat(new object[] { correctedCreatureString, creatureData[a] });
                                            if (a != creatureData.Length - 1) {
                                                correctedCreatureString = string.Concat(new object[] { correctedCreatureString, "<cA>" });
                                            }
                                        }
                                        regionState.savedPopulation[i] = correctedCreatureString;
                                        log.LogVerbose($"-new creature data: {regionState.savedPopulation[i]}");
                                    } else { //Add an out of region creature
                                        OutOfRegionEntity invalidCreature = new OutOfRegionEntity(roomTranslations[rt].roomName, regionState.savedPopulation[i]);
                                        outOfRegionPopulation.Add(invalidCreature);
                                        regionState.savedPopulation[i] = null;
                                        log.Log($"Adding new out of region population entry: {invalidCreature}");
                                    }
                                    didAction = true;
                                    break;
                                }
                            }
                            if (!didAction) {
                                log.LogInfo($"Unable to find any matching translations for creature, no actions will be done");
                            }
                        } else {
                            log.LogWarning("Failed reading creature worldCoordinate, expected integer value");
                            shouldCreateBackup = true;
                        }
                    } else {
                        log.LogWarning("Failed reading creature worldcoordinate: expected at least 2 values to be split by '.'");
                        shouldCreateBackup = true;
                    }
                } else {
                    log.LogWarning("Failed creature read: Not enough indexes to read room at index [2]");
                    shouldCreateBackup = true;
                }
            }
            //Remove any null savedPopulation
            regionState.savedPopulation.RemoveAll(x => { return x == null; });

            //Now fix creature den positions (this is in case a creature has a spawn in a den that doesnt exist?
            log.Log("Correcting Abstract node for creature dens:");
            for (int i = 0; i < regionState.savedPopulation.Count; ++i) {
                string[] creatureData = Regex.Split(regionState.savedPopulation[i], "<cA>");
                if (creatureData.Length >= 3) {
                    string[] creatureWorldCoordinateSplit = creatureData[2].Split(new char[] { '.' });
                    if (creatureWorldCoordinateSplit.Length >= 2) {
                        if (int.TryParse(creatureWorldCoordinateSplit[0], out int creatureRoom) && int.TryParse(creatureWorldCoordinateSplit[1], out int creatureAbstractNode)) {
                            WorldCoordinate den = new WorldCoordinate(creatureRoom, -1, -1, creatureAbstractNode);
                            CorrectCreatureAbstractNode(ref den);
                            string correctedCreatureString = string.Empty;
                            creatureData[2] = string.Concat(new object[] { den.room, ".", den.abstractNode });
                            for (int a = 0; a < creatureData.Length; ++a) {
                                correctedCreatureString = string.Concat(new object[] { correctedCreatureString, creatureData[a] });
                                if (a != creatureData.Length - 1) {
                                    correctedCreatureString = string.Concat(new object[] { correctedCreatureString, "<cA>" });
                                }
                            }
                            regionState.savedPopulation[i] = correctedCreatureString;
                        } else {
                            log.LogError("Failed reading creature worldCoordinate (abstractNodes), expected integer value");
                        }
                    } else {
                        log.LogError("Failed reading creature worldcoordinate (abstractNodes): expected at least 2 values to be split by '.'");
                    }
                } else {
                    log.LogError("Failed creature read (abstractNodes): Not enough indexes to read room at index [2]");
                }
            }
        }

        void ApplyRoomTranslationsToSticks() {
            SFLogSource log = new SFLogSource("SFRegionState::ApplyRoomTranslationsToSticks");
            log.Log($"savedSticks amount: {regionState.savedSticks.Count}");
            for (int i = 0; i < regionState.savedSticks.Count; ++i) {
                log.Log($"orig stick data: {regionState.savedSticks[i]}");
                string[] stickData = Regex.Split(regionState.savedSticks[i], "<stkA>");
                if (stickData.Length >= 1) {
                    if (int.TryParse(stickData[0], out int StickRoom)) {
                        bool didAction = false;
                        for (int rt = 0; rt < roomTranslations.Count; ++rt) {
                            if(StickRoom == roomTranslations[rt].roomFrom) {
                                if (roomTranslations[rt].roomTo != null) {
                                    string correctedStickData = string.Empty;
                                    stickData[0] = roomTranslations[rt].roomTo.ToString();
                                    for (int a = 0; a < stickData.Length; ++a) {
                                        correctedStickData = string.Concat(new object[] { correctedStickData, stickData[a] });
                                        if (a != stickData.Length - 1) {
                                            correctedStickData = string.Concat(new object[] { correctedStickData, "<stkA>" });
                                        }
                                    }
                                    regionState.savedSticks[i] = correctedStickData;
                                    log.Log($"Found matching translation for stick! Edited stick data: {regionState.savedSticks[i]}");
                                } else {
                                    OutOfRegionEntity invalidStick = new OutOfRegionEntity(roomTranslations[rt].roomName, regionState.savedPopulation[i]);
                                    outOfRegionSticks.Add(invalidStick);
                                    regionState.savedSticks[i] = null;
                                    log.Log($"Adding new invalid Stick entry: {invalidStick}");
                                }
                                didAction = true;
                                break;
                            }
                        }
                        if (!didAction) {
                            log.LogWarning("Unable to find any matching room translations for stick, no actions will be done");
                        }
                    } else {
                        log.LogError("Unable to read stick room, expected integer at index [0]");
                    }
                } else {
                    log.LogError("Not enough elements in stick data to read room");
                }
            }
            //Remove any null sticks
            regionState.savedSticks.RemoveAll(x => { return x == null; });
        }

        void ApplyRoomTranslationsToConsumables() {
            SFLogSource log = new SFLogSource("SFRegionState::ApplyRoomTranslationsToConsumables");
            log.Log($"SavedConsumables amount: {regionState.consumedItems.Count}");
            for (int i = 0; i < regionState.consumedItems.Count; ++i) {
                log.Log($"Orig consumable data: {regionState.consumedItems[i].ToString()}");
                if (regionState.consumedItems[i] != null) {
                    bool didAction = false;
                    for (int rt = 0; rt < roomTranslations.Count; ++rt) {
                        if (regionState.consumedItems[i] != null && regionState.consumedItems[i].originRoom == roomTranslations[rt].roomFrom) {
                            if (roomTranslations[rt].roomTo != null) {
                                regionState.consumedItems[i].originRoom = roomTranslations[rt].roomTo ?? -1;
                                log.Log($"Found matching translation for consumable! Edited consumable data: {regionState.consumedItems[i].ToString()}");
                            } else {
                                log.Log($"Found consumable matching with invalid translation {roomTranslations[rt].roomName} ({roomTranslations[rt].roomFrom}), adding to invalid consumables!");
                                outOfRegionConsumedItems.Add(new OutOfRegionEntity(roomTranslations[rt].roomName, regionState.consumedItems[i].ToString()));
                                regionState.consumedItems[i] = null;
                            }
                            didAction = true;
                            break;
                        }
                    }
                    if (!didAction) {
                        log.LogWarning("Unable to find any matching room translations for consumable, no actions will be done");
                    }
                } else {
                    log.LogError("ConsumedItem is null");
                }
            }
            regionState.consumedItems.RemoveAll(x => { return x == null; });
        }

        public bool IsDuplicateInRoomTranslation(int roomNumber) {
            for (int i = 0; i < roomTranslations.Count; ++i) {
                if (roomTranslations[i].roomFrom == roomNumber) {
                    return true;
                }
            }
            return false;
        }

        //    <tDiv>    ':'
        public string TranslationsToString() {
            SFLogSource log = new SFLogSource("SFRegionState::TranslationsToString");
            string output = string.Empty;
            for (int i = 0; i < roomTranslations.Count; ++i) {
                if (i != roomTranslations.Count - 1) {
                    output = string.Concat(new object[] { output, roomTranslations[i].SaveToString(), "<tDiv>" });
                } else {
                    output = string.Concat(new object[] { output, roomTranslations[i].SaveToString() });
                }
            }
            log.Log($"Translations to string: [{output}]");
            return output;
        }
        //    <bDiv>    <idB>    <eDiv>    ':'
        public string OutOfRegionEntitiesToString() {
            SFLogSource log = new SFLogSource("SFRegionState::OutOfRegionObjectsToString");
            string output = string.Empty;
            for (int listSelect = 0; listSelect < 4; ++listSelect) {
                List<OutOfRegionEntity> outOfRegionEntityList = null;
                switch (listSelect) {
                    case 0:
                        outOfRegionEntityList = outOfRegionObjects;
                        break;
                    case 1:
                        outOfRegionEntityList = outOfRegionPopulation;
                        break;
                    case 2:
                        outOfRegionEntityList = outOfRegionSticks;
                        break;
                    case 3:
                        outOfRegionEntityList = outOfRegionConsumedItems;
                        break;
                }
                if (outOfRegionEntityList != null && outOfRegionEntityList.Count > 0) {
                    output = string.Concat(new object[] { output, listSelect, "<idB>" }); //Create ID division
                    for (int b = 0; b < outOfRegionEntityList.Count; ++b) {
                        if (b != outOfRegionEntityList.Count - 1) {
                            output = string.Concat(new object[] { output, outOfRegionEntityList[b].SaveToString(), "<eDiv>" });
                        } else {
                            output = string.Concat(new object[] { output, outOfRegionEntityList[b].SaveToString() });
                        }
                    }
                    if (listSelect != 3) {
                        output = string.Concat(new object[] { output, "<bDiv>" });
                    }
                }
            }
            log.Log($"outOfRegionEntities to string: [{output}]");
            return output;
        }

        //    <nDiv>    <aDiv>    <idA>
        public string SaveToString() {
            SFLogSource log = new SFLogSource("SFRegionState::SaveToString");
            log.Log("Saving SFRegionState to string...");
            string output = string.Empty;
            output = string.Concat(new object[] { regionState.regionName, "<nDiv>"});
            List<string> validAddedStrings = new List<string>();
            for (int i = 0; i < 3; ++i) {
                string addedString = string.Empty;
                switch(i) {
                    case 0:
                        addedString = TranslationsToString();
                        break;
                    case 1:
                        addedString = OutOfRegionEntitiesToString();
                        break;
                    case 2:
                        addedString = respawnFixer.GameRespawnsToString(regionState);
                        break;
                }
                if (!string.IsNullOrEmpty(addedString)) {
                    validAddedStrings.Add(string.Concat(new object[] { i, "<idA>", addedString }));
                    log.Log("Added valid string!");
                } else {
                    log.Log($"added string {i} is empty, skipping...");
                }
            }
            for (int i = 0; i < validAddedStrings.Count; ++i) {
                if (i != validAddedStrings.Count -1) {
                    output = string.Concat(new object[] { output, validAddedStrings[i], "<aDiv>" });
                } else {
                    output = string.Concat(new object[] { output, validAddedStrings[i] });
                }
            }
            log.Log($"SFRegionState as string: {output}");
            return output;
        }

        //Does not do anything with out of region entities, assumes the region save has already been fixed.
        public void AdaptTranslationsToWorld() {
            SFLogSource log = new SFLogSource("SFRegionState::AdaptTranslationsToWorld");
            roomTranslations.Clear();
            log.Log($"Updating saveFixState roomTranslations to match with the current region data in region ({regionState.regionName})");
            log.Log("Number of savedObjects: " + regionState.savedObjects.Count);
            log.Log("Number of savedPopulations: " + regionState.savedPopulation.Count);
            log.Log("Number of savedSticks: " + regionState.savedSticks.Count);
            log.Log("Number of consumeables: " + regionState.consumedItems.Count);
            log.LogInfo("Number of respawns: " + (regionState.saveState.respawnCreatures.Count + regionState.saveState.waitRespawnCreatures.Count));
            int? lastAddedRoom = null;
            for (int listSelect = 0; listSelect < 4; ++listSelect) {
                ReadOnlyCollection<string> savedEntityList = null;
                int? nonStringListCount = null;
                switch (listSelect) {
                    case 0:
                        savedEntityList = regionState.savedObjects.AsReadOnly();
                        break;
                    case 1:
                        savedEntityList = regionState.savedPopulation.AsReadOnly();
                        break;
                    case 2:
                        savedEntityList = regionState.savedSticks.AsReadOnly();
                        break;
                    case 3:
                        nonStringListCount = regionState.consumedItems.Count;
                        break;
                }
                log.Log($"Iteration count: {(nonStringListCount == null ? savedEntityList.Count : nonStringListCount ?? -1)}");
                for (int i = 0; i < (nonStringListCount == null ? savedEntityList.Count : nonStringListCount ?? -1); ++i) {
                    int? entityRoomNumber = null;
                    if (nonStringListCount == null) {
                        if (savedEntityList[i] != null) {
                            log.Log($"Entity {i} in list {listSelect} dump: {savedEntityList[i]}");
                            //Search for the roomNumber of the savedEntity
                            switch (listSelect) {
                                case 0: //Object
                                    AbstractPhysicalObject tempObject = SaveState.AbstractPhysicalObjectFromString(regionState.world, savedEntityList[i]);
                                    if (tempObject != null) {
                                        entityRoomNumber = tempObject.pos.room;
                                    } else {
                                        log.LogError($"Element {i} in savedObjects is not a valid object string");
                                    }
                                    break;
                                case 1: //Population
                                    string[] creatureArray = Regex.Split(savedEntityList[i], "<cA>");
                                    if (creatureArray.Length > 2) {
                                        if (int.TryParse(creatureArray[2].Split(new char[] { '.' })[0], out int creatureRoom)) {
                                            entityRoomNumber = creatureRoom;
                                        } else {
                                            log.LogError($"Unable toread room in element {i} of savedPopulation: invalid value type in segment <ca>: 2, '.' : 0");
                                        }
                                    } else {
                                        log.LogError($"Unable to read room in element {i} of savedPopulation: invalid size");
                                    }
                                    break;
                                case 2: //Sticks
                                    string[] stickArray = Regex.Split(savedEntityList[i], "<stkA>");
                                    if (stickArray.Length > 0) {
                                        if (int.TryParse(stickArray[0], out int stickRoom)) {
                                            entityRoomNumber = stickRoom;
                                        } else {
                                            log.LogError($"Unable to read room in element {i} of savedSticks: invalid value type in segment <stkA>: 0 ");
                                        }
                                    } else {
                                        log.LogError($"Unable to read room in element {i} of savedSticks: invalid size");
                                    }
                                    break;
                            }
                        } else {
                            log.LogWarning($"saved entity in list {listSelect} on index {i} is invalid");
                        }
                    } else { //Non-string lists
                        switch (listSelect) {
                            case 3: //Consumables
                                log.Log($"Entity {i} in list {listSelect} dump: {regionState.consumedItems[i].ToString()}");
                                entityRoomNumber = regionState.consumedItems[i].originRoom;
                                break;
                        }
                    }
                    if (entityRoomNumber != null) {
                        AbstractRoom entityRoom = regionState.world.GetAbstractRoom(entityRoomNumber ?? -1);
                        if (entityRoom != null) {
                            if (lastAddedRoom != entityRoomNumber && !IsDuplicateInRoomTranslation(entityRoomNumber ?? -1)) {
                                log.Log($"Creating room translation for room {entityRoom.name} ({entityRoomNumber})");
                                roomTranslations.Add(new RoomTranslation(entityRoomNumber ?? -1, entityRoom.name));
                                lastAddedRoom = entityRoomNumber;
                            } else {
                                log.Log($"Entity {i} in list {listSelect} is has a duplicate room number, not creating translation for this entity...");
                            }
                        } else {
                            log.LogWarning($"Entity {i} in list {listSelect} is outside of region {regionState.regionName} (#{entityRoomNumber}), not creating translation for this entity...");
                        }
                    } else {
                        log.LogWarning($"could not get room for entity {i} in list {listSelect}, not creating translation for this entity...");
                    }
                }
            }
            //For respawns
            respawnFixer.GameRespawnsToString(regionState);
            List<AbstractRoom> rooms = respawnFixer.GetRespawnDenRooms(regionState);
            foreach (AbstractRoom r in rooms) {
                if (!IsDuplicateInRoomTranslation(r.index)) {
                    roomTranslations.Add(new RoomTranslation(r.index, r.name));
                    log.LogDebug($"Added room from respawn {r.name}");
                } else {
                    log.LogDebug($"Skipped adding room from respawn because of duplication: {r.name}");
                }
                
            }
            //DEBUG:
            log.Log("Dumping new room translations list");
            for (int i = 0; i < roomTranslations.Count; ++i) {
                log.Log("{i}: " + roomTranslations[i].ToString());
            }
        }

        public void RecoverOutOfRegionObjects() {
            SFLogSource log = new SFLogSource("SFRegionState::RecoverOutOfRegionObjects");
            for (int i = 0; i < outOfRegionObjects.Count; ++i) {
                log.Log($"Invalid Object data: {outOfRegionObjects[i].entityData}");
                for (int r = regionState.world.firstRoomIndex; r < regionState.world.firstRoomIndex + regionState.world.NumberOfRooms; ++r) {
                    AbstractRoom abstractRoom = regionState.world.GetAbstractRoom(r);
                    if (abstractRoom != null && outOfRegionObjects[i].roomName != null && abstractRoom.name == outOfRegionObjects[i].roomName) {
                        AbstractPhysicalObject abstractObject = SaveState.AbstractPhysicalObjectFromString(regionState.world, outOfRegionObjects[i].entityData);
                        string restoredObject = null;
                        if (abstractObject != null) {
                            abstractObject.pos.room = r;
                            restoredObject = abstractObject.ToString();
                        }
                        if (restoredObject != null) {
                            regionState.savedObjects.Add(restoredObject);
                            outOfRegionObjects[i] = null;
                            log.Log($"Invalid object made valid again in room {abstractRoom.name} : {restoredObject}");
                            break;
                        } else {
                            log.LogWarning("Failed to restore invalid object");
                        }
                    }
                }
            }
            outOfRegionObjects.RemoveAll(x => { return x == null; });
        }

        public void RecoverOutOfRegionPopulation() {
            SFLogSource log = new SFLogSource("SFRegionState::RecoverOutOfRegionPopulation");
            for (int i = 0; i < outOfRegionPopulation.Count; ++i) {
                log.Log($"Invalid Population data: {outOfRegionPopulation[i].entityData}");
                for (int r = regionState.world.firstRoomIndex; r < regionState.world.firstRoomIndex + regionState.world.NumberOfRooms; ++r) {
                    AbstractRoom abstractRoom = regionState.world.GetAbstractRoom(r);
                    if (abstractRoom != null && outOfRegionPopulation[i].roomName != null && abstractRoom.name == outOfRegionPopulation[i].roomName) {
                        string[] creatureData = Regex.Split(outOfRegionPopulation[i].entityData, "<cA>");
                        if (creatureData.Length >= 3) {
                            string[] creatureDenString = creatureData[2].Split(new char[] { '.' });
                            if (int.TryParse(creatureDenString[1], out int creatureAbstractNode)) {
                                string restoredCreature = string.Empty;
                                creatureData[2] = string.Concat(new object[] { r, ".", creatureAbstractNode });
                                //Compile string together
                                for (int a = 0; a < creatureData.Length; ++a) {
                                    restoredCreature = string.Concat(new object[] { restoredCreature, creatureData[a] });
                                    if (a != creatureData.Length - 1) {
                                        restoredCreature = string.Concat(new object[] { restoredCreature, "<cA>" });
                                    }
                                }
                                log.Log($"Invalid creature made valid again in room {abstractRoom.name} : {restoredCreature}");
                                regionState.savedPopulation.Add(restoredCreature);
                                outOfRegionPopulation[i] = null;
                            } else {
                                log.LogError($"Failed to restore creature: could not get abstract node on index {i}");
                            }
                        } else {
                            log.LogError($"Failed to restore creature: could not access room number on section 2 of index {i}");
                        }
                    }
                }
            }
            outOfRegionPopulation.RemoveAll(x => { return x == null; });
        }

        public void RecoverOutOfRegionSticks() {
            SFLogSource log = new SFLogSource("SFRegionState::RecoverOutOfRegionSticks");
            for (int i = 0; i < outOfRegionSticks.Count; ++i) {
                log.Log($"Invalid Stick data: {outOfRegionSticks[i].entityData}");
                for (int r = regionState.world.firstRoomIndex; r < regionState.world.firstRoomIndex + regionState.world.NumberOfRooms; ++r) {
                    AbstractRoom abstractRoom = regionState.world.GetAbstractRoom(r);
                    if (abstractRoom != null && outOfRegionPopulation[i].roomName != null && abstractRoom.name == outOfRegionPopulation[i].roomName) {
                        string[] stickData = Regex.Split(outOfRegionSticks[i].entityData, "<stkA>");
                        if (stickData.Length >= 1) {
                            string restoredStick = string.Empty;
                            stickData[0] = r.ToString();
                            for (int a = 0; a < stickData.Length; ++a) {
                                if (a != stickData.Length - 1) {
                                    restoredStick = string.Concat(new object[] { restoredStick, stickData[a], "<stkA>" });
                                } else {
                                    restoredStick = string.Concat(new object[] { restoredStick, stickData[a] });
                                }
                            }
                            log.Log($"Invalid stick made valid again in room {abstractRoom.name} : {restoredStick}");
                            regionState.savedSticks.Add(restoredStick);
                            outOfRegionSticks[i] = null;
                        } else {
                            log.LogWarning($"Failed to restore creature could not access room number on section 0 of index {i}");
                        }
                    }
                }
            }
            outOfRegionSticks.RemoveAll(x => { return x == null; });
        }

        public void RecoverOutOfRegionConsumables() {
            SFLogSource log = new SFLogSource("SFRegionState::RecoverOutOfRegionConsumables");
            for (int i = 0; i < outOfRegionConsumedItems.Count; ++i) {
                log.Log($"Invalid Consumable data: {outOfRegionConsumedItems[i].entityData}");
                for (int r = regionState.world.firstRoomIndex; r < regionState.world.firstRoomIndex + regionState.world.NumberOfRooms; ++r) {
                    AbstractRoom abstractRoom = regionState.world.GetAbstractRoom(r);
                    if (abstractRoom != null && outOfRegionConsumedItems[i].roomName != null && abstractRoom.name == outOfRegionConsumedItems[i].roomName) {
                        RegionState.ConsumedItem restoredConsumable = new RegionState.ConsumedItem(0, 0, 0);
                        restoredConsumable.FromString(outOfRegionConsumedItems[i].entityData);
                        regionState.consumedItems.Add(restoredConsumable);
                        outOfRegionConsumedItems[i] = null;
                        log.Log($"Invalid consumable made valid again in room {abstractRoom.name} : {restoredConsumable.ToString()}");
                        break;
                    }
                }
            }
            outOfRegionConsumedItems.RemoveAll(x => { return x == null; });
        }

        public void RecoverOutOfRegionEntities() {
            SFLogSource log = new SFLogSource("SFRegionState::RestoreOutOfRegionEntities");
            log.Log("Attempting to restore out of region entities");
            RecoverOutOfRegionObjects();
            RecoverOutOfRegionPopulation();
            RecoverOutOfRegionSticks();
            RecoverOutOfRegionConsumables();
        }

        //Will convert the coordinate
        public void CorrectCreatureAbstractNode(ref WorldCoordinate coord) {
            SFLogSource log = new SFLogSource("SFRegionState::CorrectCreatureAbstractNode");
            if (coord != null) {
                if (coord.NodeDefined) {
                    AbstractRoom denRoom = regionState.world.GetAbstractRoom(coord);
                    if (denRoom != null) {
                        bool notInRange = false;
                        if (!(!(denRoom.nodes.Length <= coord.abstractNode || coord.abstractNode < 0))) {
                            notInRange = true;
                            log.LogWarning($"Creature at coordinate ({coord}) has a node outside of range! {denRoom.name} nodeTotal: {denRoom.TotalNodes} | requested node: {coord.abstractNode}");
                        }
                        if (notInRange || regionState.world.GetNode(coord).type != AbstractRoomNode.Type.Den) {
                            log.Log($"orig coordinate {coord.ToString()} | {denRoom.name}");
                            if (denRoom != null) {
                                int[] filledIndexes = GetCreatureUsedDensInRoom(coord.room, regionState.savedPopulation, coord.abstractNode);
                                bool closestDir = false; //True is left, false is right. (if the distance is 0 this value should be true)
                                int? closestValueDistance = null;
                                int? closestValidIndex = null;
                                for (int i = 0; i < denRoom.TotalNodes; ++i) {
                                    if (denRoom.nodes[i].type == AbstractRoomNode.Type.Den) {
                                        bool alreadyFilled = false;
                                        for (int f = 0; f < filledIndexes.Length; ++f) {
                                            alreadyFilled = alreadyFilled ? true : i == filledIndexes[f];
                                        }
                                        if (alreadyFilled) {
                                            int distance;
                                            distance = Mathf.Abs(coord.abstractNode - i);
                                            if (closestValueDistance != null) {
                                                if (distance < closestValueDistance) {
                                                    closestValueDistance = distance;
                                                    closestDir = i <= coord.abstractNode;
                                                }
                                            } else {
                                                closestValueDistance = distance;
                                                closestDir = i <= coord.abstractNode;
                                            }
                                        } else {
                                            int distance;
                                            distance = Mathf.Abs(coord.abstractNode - i);
                                            if (closestValidIndex != null) {
                                                if (distance < Mathf.Abs(coord.abstractNode - closestValidIndex ?? 0)) {
                                                    closestValidIndex = coord.abstractNode + (distance * (i <= coord.abstractNode ? -1 : 1));
                                                }
                                            } else {
                                                closestValidIndex = coord.abstractNode + (distance * (i <= coord.abstractNode ? -1 : 1));
                                            }
                                        }
                                    }
                                }
                                if (closestValidIndex != null) {
                                    log.LogDebug($"Corrected abstractNode for creature: {closestValidIndex}");
                                    coord.abstractNode = closestValidIndex ?? 0;
                                } else {
                                    if (closestValueDistance != null) {
                                        log.LogWarning("Could not find an empty abstractNode for creature, setting it to be the closest den");
                                        coord.abstractNode = coord.abstractNode + ((closestValueDistance ?? 0) * (closestDir ? -1 : 1));
                                        log.LogDebug($"New abstractNode for creature: {coord.abstractNode}");
                                    } else {
                                        log.LogError("Could not find any dens to move creature to! creature will remain with it's original node");
                                    }
                                }
                            } else {
                                log.LogError($"Den room for coordinate is null! {coord.room}");
                            }
                        }
                    } else {
                        log.LogError($"DenRoom is null! : {coord.ToString()}");
                    }
                } else {
                    log.LogWarning("WorldCoordinate does not have a defined node!");
                }
            } else {
                log.LogError("WorldCoordinate parameter is null!");
            }
        }

        public int[] GetCreatureUsedDensInRoom(int roomIndex, List<string> creatureList, params int[] excludedIndexes) {
            SFLogSource log = new SFLogSource("SFRegionState::GetCreatureUsedDensInRoom");
            if (creatureList != null) {
                List<int> filledDens = new List<int>();
                for (int i = 0; i < creatureList.Count; ++i) {
                    AbstractCreature creature = SaveState.AbstractCreatureFromString(regionState.world, creatureList[i], true);
                    if (creature != null) {
                        if (creature.pos.room == roomIndex) {
                            bool excluded = false;
                            for (int t = 0; t < excludedIndexes.Length; ++t) {
                                excluded = excluded ? true : excludedIndexes[t] == i;
                            }
                            if (!excluded) {
                                if (creature.pos.NodeDefined) {
                                    filledDens.Add(creature.pos.abstractNode);
                                } else {
                                    log.LogError($"Creature ({creatureList[i]}) does not have a defined node");
                                }
                            }
                        }
                    } else {
                        log.LogError($"Creature {i} is not a valid string in savedPopulation!");
                    }
                }
                return filledDens.ToArray();
            } else {
                log.LogError("Creature list is null!");
                return null;
            }
        }
        public RespawnCorrector respawnFixer;
        public List<RoomTranslation> roomTranslations;
        public List<OutOfRegionEntity> outOfRegionObjects;
        public List<OutOfRegionEntity> outOfRegionPopulation;
        public List<OutOfRegionEntity> outOfRegionSticks;
        public List<OutOfRegionEntity> outOfRegionConsumedItems;
        public RegionState regionState { get; }
        //TODO implement this
        bool shouldCreateBackup = true;

        public class RoomTranslation {
            public RoomTranslation(int nRoomFrom, string nRoomName) {
                roomFrom = nRoomFrom;
                roomName = nRoomName;
                roomTo = null;
            }
            public RoomTranslation(int nRoomFrom, string nRoomName, int nRoomTo) {
                roomFrom = nRoomFrom;
                roomName = nRoomName;
                roomTo = nRoomTo;
            }

            public override string ToString() {
                return string.Concat(new object[] { "F:", roomFrom, " N:", roomName, " T:", (roomTo != null ? roomTo.ToString() : "null") });
            }

            public string SaveToString() {
                return string.Concat(new object[] { roomFrom, ":", roomName });
            }

            public int roomFrom;
            public int? roomTo;
            public string roomName;
        }

        public class OutOfRegionEntity {
            public OutOfRegionEntity(string nRoomName, string nEntityData) {
                roomName = nRoomName;
                entityData = nEntityData;
            }
            public override string ToString() {
                return string.Concat(new object[] { "outOfRegionEntity{ ", "roomName:", roomName, " data:", entityData });
            }
            public string SaveToString() {
                return string.Concat(roomName, ":", entityData);
            }

            public string roomName;
            public string entityData;
        }

    }
}
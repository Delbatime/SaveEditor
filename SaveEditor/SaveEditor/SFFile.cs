//Author:Deltatime
using System.Text.RegularExpressions;
using System.IO;

namespace SaveFixer {
    //Represents the actual save file that the mod reads/writes to
    class SFFile {
        //Path of saveFix.txt for the specififed save slot
        public static string GetFilePath(int slot) {
            return string.Concat(new object[] {
                RWCustom.Custom.RootFolderDirectory(),
                "UserData", Path.DirectorySeparatorChar,
                "CustomRegionSaveData", Path.DirectorySeparatorChar, "savFix",
                slot + 1, ".txt"
                });
        }
        //Path of saveFix.txt for the save slot in rw.options
        public static string GetFilePath(RainWorld rw) {
            return GetFilePath(rw.options.saveSlot);
        }
        //Gets all the text inside of the saveFix.txt as a single string. Creates a new file if one is not there before.
        public static string ReadFile(RainWorld rw) {
            SFLogSource log = new SFLogSource("SFFile::ReadFile");
            string filePath = GetFilePath(rw);
            if (!File.Exists(filePath)) {
                File.Create(filePath).Dispose();
                log.Log("SavFix file does not exist! Creating a new empty file...");
                return string.Empty;
            }
            string[] verSplit = null;
            verSplit = Regex.Split(File.ReadAllText(filePath), "<ver>");
            if (verSplit.Length > 1) {
                return verSplit[1];
            } else {
                if (verSplit.Length > 0) {
                    log.LogWarning("SaveFix file does not have a valid version split! (split by <ver>)");
                    return verSplit[0];
                } else {
                    log.LogError("Versplit for some reason is 0! Acting as if file is empty.");
                    return string.Empty;
                }
            }
        }
        //Gets the index of the current save state in string[]
        //Save states are divided into their (ID | Data) by <iDiv>
        public static bool GetSaveStateIndex(string[] saveStates, int saveStateSlot, out int index) {
            SFLogSource log = new SFLogSource("SFFile::GetSaveStateIndex");
            int? output = null;
            for (int i = 0; i < saveStates.Length; ++i) {
                string[] iDiv = Regex.Split(saveStates[i], "<iDiv>");
                if (iDiv.Length >= 2) {
                    if (int.TryParse(iDiv[0], out int loadedStateNumber)) {
                        if (loadedStateNumber == saveStateSlot) {
                            output = i;
                            break;
                        }
                    } else {
                        log.LogWarning($"Expected integer for element 0  of saveslot on division <iDiv>, skipping saveSlot index {i}...");
                    }
                } else {
                    log.LogWarning($"Expected saveSlot to have at least 2 elements to be divided by <iDiv>, skipping saveSlot index {i}...");
                }
            }
            index = output ?? -1;
            return output != null ? true : false;
        }
        //Returns a string containing the save state data specificed in (saveStateSlot), without the ID and <iDiv>
        //Save states are divived by <SavDiv>, then divided into (ID | Data) by <iDiv>
        public static string SaveStateTextFromString(string fileText, int saveStateSlot) {
            SFLogSource log = new SFLogSource("SFFile::SaveStateTextFromString");
            string[] saveStateArray = Regex.Split(fileText, "<SavDiv>");
            if (GetSaveStateIndex(saveStateArray, saveStateSlot, out int index)) {
                return Regex.Split(saveStateArray[index], "<iDiv>")[1];
            }
            log.LogWarning($"Save state number {saveStateSlot} is not in file");
            return string.Empty;
        }
        //Same as SaveStateTextFromString, but reads from the file and saveslot specified in the game.
        public static string ReadSaveState(RainWorldGame game) {
            SFLogSource log = new SFLogSource("SFFile::ReadSaveState");
            string fileText = ReadFile(game.rainWorld);
            if (!string.IsNullOrEmpty(fileText)) {
                return SaveStateTextFromString(fileText, game.StoryCharacter);
            } else {
                log.LogWarning("Save file is empty!");
                return string.Empty;
            }
        }
        //Returns the index of the region with the same name as (regionName)
        //Regions are divived into (name | Data) by <nDiv>
        public static bool GetRegionIndex(string[] regions, string regionName, out int index) {
            SFLogSource log = new SFLogSource("SFFile::GetRegionIndex");
            int? output = null;
            if (regions != null && regionName != null) {
                for (int i = 0; i < regions.Length; ++i) {
                    log.Log("Dumping full region text: " + regions[i]);
                    string[] regionNameDiv = Regex.Split(regions[i], "<nDiv>");
                    if (regionNameDiv.Length >= 2) {
                        if (regionNameDiv[0].Equals(regionName)) {
                            output = i;
                        }
                    } else {
                        log.LogWarning($"Invalid formatting, expected region to have 2 elements divided by <nDiv>! Skipping element {i}...");
                    }
                }
            } else {
                log.LogWarning("A Parameter is/are null, region cannot be found");
            }
            index = output ?? -1;
            return (output != null ? true : false);
        }
        //Returns a string containing the region with the matching name (regionName) without the name and <nDiv>
        //Regions in a save state's data are divided by <rDiv>, and then (name | data) by <nDiv>
        public static string RegionTextFromString(string saveStateText, string regionName) {
            SFLogSource log = new SFLogSource("SFFile::GetRegion");
            log.Log("Dumping savestateText: " + saveStateText);
            string[] regionsArray = Regex.Split(saveStateText, "<rDiv>");
            if (GetRegionIndex(regionsArray, regionName, out int regionIndex)) {
                return Regex.Split(regionsArray[regionIndex], "<nDiv>")[1];
            }
            log.Log($"Region {regionName} is not in saveState!");
            return string.Empty;
        }
        //Same as RegionTextFromString, but reads from the file and region specified in the regionstate. (calls readSaveState)
        public static string ReadRegion(RegionState region) {
            SFLogSource log = new SFLogSource("SFFile::ReadRegion");
            string saveStateText = ReadSaveState(region.world.game);
            if (!string.IsNullOrEmpty(saveStateText)) {
                return RegionTextFromString(saveStateText, region.regionName);
            } else {
                log.LogWarning("Save State (data) is empty");
                return string.Empty;
            }
        }
        //Writes all of the region data to the saveFix file
        //Contents of regionData is irrelevant to this function, although trying to load something incorrect will prevent the SFRegionState from loading
        public static void WriteRegion(string regionData, RegionState region) {
            SFLogSource log = new SFLogSource("SFFile::WriteRegion");
            string[] saveStateDiv = Regex.Split(ReadFile(region.world.game.rainWorld), "<SavDiv>"); //large division
            string[] regionDiv = null;
            bool makeNewSaveState = false;
            if (GetSaveStateIndex(saveStateDiv, region.world.game.StoryCharacter, out int savDivIndex)) {
                regionDiv = Regex.Split(Regex.Split(saveStateDiv[savDivIndex], "<iDiv>")[1], "<rDiv>"); //Region division
            } else {
                log.Log("Could not find current saveslot! Making a new save slot...");
                makeNewSaveState = true;
            }
            //Edit the region
            string saveStateData = string.Empty;
            if (GetRegionIndex(regionDiv, region.regionName, out int regionIndex)) {
                log.Log($"Editing region {region.regionName} in save slot {savDivIndex}");
                regionDiv[regionIndex] = regionData;
                log.Log("regionDiv.Length: " + regionDiv.Length);
                for (int i = 0; i < regionDiv.Length; ++i) {
                    saveStateData = string.Concat(new object[] { saveStateData, regionDiv[i] });
                    if (i != regionDiv.Length - 1 && regionDiv[i] != string.Empty) {
                        saveStateData += "<rDiv>";
                    }
                }
            } else {
                log.LogWarning($"Could not find region to save to, adding region to savestate {savDivIndex}");
                if (!makeNewSaveState) {
                    log.Log("Creating new region in SaveFix file!");
                    saveStateData = ReadSaveState(region.world.game);
                    if (saveStateData != null) {
                        saveStateData = string.Concat(new object[] { saveStateData, "<rDiv>", regionData });
                    } else {
                        saveStateData = regionData;
                    }
                } else {
                    saveStateData = regionData;
                }
            }
            saveStateData = string.Concat(region.world.game.StoryCharacter, "<iDiv>", saveStateData);
            log.Log("Dumping saveStateText: " + saveStateData);
            if (!makeNewSaveState) {
                saveStateDiv[savDivIndex] = saveStateData;
            }
            //Put all of the savestates back together.
            string outputText = string.Empty;
            log.Log("saveStateDivLength: " + saveStateDiv.Length);
            for (int i = 0; i < saveStateDiv.Length; ++i) {
                outputText = string.Concat(outputText, saveStateDiv[i]);
                if (i != saveStateDiv.Length - 1 && saveStateDiv[i] != string.Empty) {
                    outputText += "<SavDiv>";
                }

            }
            if (makeNewSaveState) {
                if (outputText != string.Empty) {
                    outputText = string.Concat(outputText, "<SavDiv>", saveStateData);
                } else {
                    outputText = saveStateData;
                }
            }
            outputText = string.Concat(new object[] { SaveFixer.versionString, "<ver>", outputText });
            log.Log("Dumping entire text file output! : " + outputText);
            using (StreamWriter streamWriter = File.CreateText(SFFile.GetFilePath(region.world.game.rainWorld))) {
                streamWriter.Write(outputText);
            }
        }
    }
}

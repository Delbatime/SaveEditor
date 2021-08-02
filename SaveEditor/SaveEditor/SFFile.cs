//Author:Deltatime
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;

namespace CustomRegionSaves {
    //Methods for accessing and managing the SaveFix files
    static class SFFile {

        #region getPaths
        //Path of saveFix.txt for the specififed save slot
        public static string GetSFPath(int saveSlot) {
            return string.Concat(new object[] {
                RWCustom.Custom.RootFolderDirectory(),
                "UserData", Path.DirectorySeparatorChar,
                "CustomRegionSaveData", Path.DirectorySeparatorChar, "savFix_",
                saveSlot + 1, ".txt"
                });
        }
        //Path of saveFix.txt for the save slot in rw.options
        public static string GetSFPath(RainWorld rw) {
            return GetSFPath(rw.options.saveSlot);
        }
        //Path of saveFix_Backup.txt for the specified save slot
        public static string GetSFBackupPath(int saveSlot) {
            return string.Concat(new object[] {
                RWCustom.Custom.RootFolderDirectory(),
                "UserData", Path.DirectorySeparatorChar,
                "CustomRegionSaveData", Path.DirectorySeparatorChar, "savFix_",
                saveSlot + 1, "_Backup", ".txt"
                });
        }
        //Path of saveFix_Backup.txt for the save slot in rw.options
        public static string GetSFBackupPath(RainWorld rw) {
            return GetSFBackupPath(rw.options.saveSlot);
        }
        #endregion getPaths


        #region Version stuff
        //Function that is supposed to change the version of a SaveFix file
        private delegate string VersionChangeFunc(string fileText);
        //Strucutre used for defining a version conversion
        private readonly struct VersionConversion {
            public readonly VersionChangeFunc func;
            public readonly uint majorVer;
            public readonly uint minorVer;
            public readonly uint hotFix;
            public VersionConversion(VersionChangeFunc _func, uint _majorVer, uint _minorver, uint _hotFix) { func = _func; majorVer = _majorVer; minorVer = _minorver; hotFix = _hotFix; }
        }
        //Converts fileText to the latest version's protocol if there is a difference between the SFFile's version and the current version.
        //Will return true/false on whenever the version can sucessfully be read and the file is converted.
        public static bool ProcessVersion(string verString, ref string fileText) {
            //throw new System.NotImplementedException("ProcessVersion is still incomplete!");
            SFLogSource log = new SFLogSource("SFFile::ProcessVersion");

            //Checking that the SFFile version and the current version is in the correct format (Major.Minor.Hotfix)
            uint?[] FileVersionNumber = { null, null, null }; //Version numbers found on the SFFile
            uint?[] CurrentVersionNumber = { null, null, null }; //Version numbers for the current mod.
            for (int v = 0; v < 2; ++v) {
                ref uint?[] temp = ref ((v == 1) ? ref FileVersionNumber : ref CurrentVersionNumber); //Passed by reference so I don't have to write the same code twice.
                string[] verNumberSplit = (v == 1) ? verString.Split('.') : SaveFixer.versionString.Split('.');
                if (verNumberSplit.Length < 3) {
                    log.LogError(string.Concat(new object[] { "Expected at least 3 elements divided by '.' for the ", (v == 1 ? "file" : "current"), "version" }));
                    return false;
                }
                for (int i = 0; i < 3; ++i) {
                    if (!uint.TryParse(verNumberSplit[i], out uint ver)) {
                        log.LogError(string.Concat(new object[] { $"Expected integer in element >> .[{i}]", (v == 1 ? "file" : "current"), "version" }));
                        return false;
                    }
                    temp[i] = ver;
                }
            }
            //Check whenever the file is on the same version as the current one, or whenever it is on a older version or a newer version
            for (int i = 0; i < 4; ++i) {
                if (i >= 3) { //This block only happens if the loop is passed through 3 times previously, which means that all version numbers are the same.
                    return true;
                }
                if (CurrentVersionNumber[i] != FileVersionNumber[i]) {
                    if (CurrentVersionNumber[i] < FileVersionNumber[i]) {
                        log.LogError($"The file is on a newer version than the mod! If the future version is compatible revert the version by manually editing the version in the backup file and replacing the current file with it. Info: Index{i}, Current{CurrentVersionNumber[i]}, File{FileVersionNumber[i]}");
                        return false;
                    }
                    log.LogWarning("The file is on an older version!");
                    break;
                }
            }

            //The area below only executes if the SFfile is an older version than the current mod

            //THIS NEEDS TO BE IN THE ORDER OF THE VERSIONS, LATEST VERSIONS COME LAST.
            List<VersionConversion> verChanges = new List<VersionConversion> {
                new VersionConversion(VerChange_TO_1_0_0, 1, 0, 0)
                //ADD NEW VERSIONS HERE
            };
            foreach (VersionConversion v in verChanges) {
                if (FileVersionNumber[0] <= v.majorVer && FileVersionNumber[1] <= v.minorVer && FileVersionNumber[2] < v.hotFix) { //If the version number is below the conversion
                    string temp = v.func.Invoke(fileText);
                    if (temp == null) { //Conversions should return null on failure.
                        log.LogError($"Failed to make conversion from {verString} to {FileVersionNumber[0]}.{FileVersionNumber[1]}.{FileVersionNumber[2]}");
                        return false;
                    }
                    fileText = temp; //Replace the fileText with the converted text on success.
                    FileVersionNumber[0] = v.majorVer; FileVersionNumber[1] = v.minorVer; FileVersionNumber[2] = v.hotFix;
                }
            }
            log.Log($"Sucessfully converted file from version {verString} to {SaveFixer.versionString}");
            return true;
        }

        //TODO: Actually implement this if there ever ends up being a significant change to the file formatting.
        public static string VerChange_TO_1_0_0(string fileText) {
            return fileText;
        }
        #endregion Version stuff


        //Returns the contents of <ver>[1] if this file is split by a version. Will back up the file and return an empty string if <ver> div is less than 2 or if the version number is invalid.
        //Returns an empty string if the file does not exist (after creating a new file)
        public static string ReadSFFile(RainWorld rw) {
            SFLogSource log = new SFLogSource("SFFile::ReadSFFile");

            string filePath = SFFile.GetSFPath(rw);

            //Attempt to create a new empty file if the file cannot be found
            if (!File.Exists(filePath)) {
                log.Log("SavFix file does not exist! Creating a new empty file.");
                try {
                    File.Create(filePath).Dispose();
                } catch (IOException e) {
                    log.LogError("Failed to create new file! Info: " + e.Message + "\nStackTrace:" + e.StackTrace);
                }
                return string.Empty;
            }

            string[] verSplit; //Split by <ver>
            verSplit = Regex.Split(File.ReadAllText(filePath), "<ver>");
            if (verSplit.Length > 1) {
                if (SFFile.ProcessVersion(verSplit[0], ref verSplit[1])) {
                    return verSplit[1];
                }
            }
            //If the above code fails to return then a version error occured.
            log.LogError("Version was invalid, creating a backup before clearing current file.");
            try {
                if (verSplit.Length > 1) {
                    File.WriteAllText(SFFile.GetSFBackupPath(rw), string.Concat(new object[] { verSplit[0], "<ver>", verSplit[1] }));
                } else {
                    log.LogError("Reason: Expected at least 2 elements divided by <ver>");
                    File.WriteAllText(SFFile.GetSFBackupPath(rw), verSplit[0]);
                }
            } catch (IOException e) {
                log.LogError($"Failed to create a backup file at {GetSFBackupPath(rw)}. Info: {e.Message}\nStackTrace: {e.StackTrace}");
            }
            return string.Empty;
        }

        //Gets the index of the current save state in string[]
        //Save states are divided into their (ID | Data) by <iDiv>
        //This function is needed because savestates are not always in their numerical order but instead in the order that they were added to the saveFix file.
        public static bool GetSaveStateIndex(string[] saveStates, int desiredSlot, out int index) {
            SFLogSource log = new SFLogSource("SFFile::GetSaveStateIndex");
            int? output = null;
            for (int i = 0; i < saveStates.Length; ++i) {
                string[] iDiv = Regex.Split(saveStates[i], "<iDiv>");
                if (iDiv.Length >= 2) {
                    if (int.TryParse(iDiv[0], out int loadedStateNumber)) {
                        if (loadedStateNumber == desiredSlot) {
                            output = i;
                            break;
                        }
                    } else {
                        log.LogWarning($"Expected integer for element 0 of saveslot on division <iDiv>, skipping saveSlot index {i}...");
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
            string[] saveStates = Regex.Split(fileText, "<SavDiv>");
            if (GetSaveStateIndex(saveStates, saveStateSlot, out int index)) {
                return Regex.Split(saveStates[index], "<iDiv>")[1];
            }
            log.LogWarning($"Save state number {saveStateSlot} is not in file");
            return string.Empty;
        }

        //Same as SaveStateTextFromString, but reads from the file and saveslot specified in the game.
        public static string ReadSaveState(RainWorldGame game) {
            SFLogSource log = new SFLogSource("SFFile::ReadSaveState");
            string fileText = ReadSFFile(game.rainWorld);
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
            string[] saveStateDiv = Regex.Split(ReadSFFile(region.world.game.rainWorld), "<SavDiv>"); //large division
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
            using (StreamWriter streamWriter = File.CreateText(SFFile.GetSFPath(region.world.game.rainWorld))) {
                streamWriter.Write(outputText);
            }
        }
    }
}

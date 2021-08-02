//Author: Deltatime
//Progress: Need to check : {RegionState_AdaptWorlldToRegion} {RegionState_AdaptRegionStateToWorld} {PlayerProgression_WipeSaveState}
using BepInEx;
using System.IO;
using System.Text.RegularExpressions;
using System;

//************* Bee's Magic code to make things public **************
#pragma warning disable CS0436 //Removes warning caused by something in here.
using System.Security;
using System.Security.Permissions;
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("Assembly-CSharp")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[module: UnverifiableCode]

namespace System.Runtime.CompilerServices {
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class IgnoresAccessChecksToAttribute : Attribute {
        public IgnoresAccessChecksToAttribute(string assemblyName) {
            AssemblyName = assemblyName;
        }

        public string AssemblyName { get; }
    }
}
#pragma warning restore CS0436
//*******************************************************************

//TODO - Add support for boolean arrays (SavedShelters, linages, etc.) in the save file
//TODO - Find a fix for loading creatures in regions after a region has been loaded are supposed to. (RELATED WITH THE PREVIOUS APPARENTLY)
//TODO - Allow for spawners to be corrected even if their node value is above or below the valid coordinates.
//TODO - When complete change !LATEST! in comments to the current version.
namespace CustomRegionSaves {
    [BepInPlugin("Deltatime.CustomRegionSaves", "CustomRegionSaves", SaveFixer.versionString)]
    public class SaveFixer : BaseUnityPlugin {
        //  Version number, used in fileAssemblyVersion: (majorversion.minorversion.hotfix)
        public const string versionString = "0.1.2";
        // Incremented for every revision/build
        public const string  buildNumber = "9";
        // Logger for CustomRegionSaves (outputs to SaveFixerLog.txt)
        //TODO Switch log file to CRSaveLog.txt and to remember to delete the old one (SaveFixerLog.txt)
        public static SFLog outputLog = new SFLog();

        public SaveFixer() {
            //Start of CustomRegionSaves log
            outputLog.LogString($"##################################################\n CustomRegionSaves log v{SaveFixer.versionString}.{SaveFixer.buildNumber}\n");
            //IL hooks
            IL.PlayerProgression.SaveToDisk += SaveToDiskFix.IL_PlayerProgression_SaveToDisk;
            //On hooks
            On.RegionState.AdaptWorldToRegionState += RegionState_AdaptWorldToRegionState;
            On.RegionState.AdaptRegionStateToWorld += RegionState_AdaptRegionStateToWorld;
            On.PlayerProgression.WipeAll += PlayerProgession_WipeAll;
            On.PlayerProgression.WipeSaveState += PlayerProgression_WipeSaveState;
            On.WorldLoader.GeneratePopulation += SpawnerDenFix.WorldLoader_GeneratePopulation;
        }

        // Called when the plugin is destoryed by Unity. <br></br>
        // Disposes of the outputLog stream and writes that this is the end of the log.
#pragma warning disable IDE0051 //This is an error caused since this function is not referenced by anything in the project directly.
        void OnDestroy() {
            outputLog.LogString(" End log\n##################################################");
            outputLog.Dispose();
        }
#pragma warning restore IDE0051
        //Should delete the CRsav file when you press the big "reset save" button from the options menu.
        public static void PlayerProgession_WipeAll(On.PlayerProgression.orig_WipeAll orig, PlayerProgression instance) {
            SFLogSource logger = new SFLogSource("PlayerProgession_WipeAll");

            string filePath = SFFile.GetSFPath(instance.rainWorld);
            try {
                if (File.Exists(filePath)) {
                    File.Delete(filePath);
                    logger.Log($"Completely removed file {filePath}");
                } else {
                    logger.Log($"Save file {filePath} is already removed");
                }
            } catch (Exception ex) {
                logger.LogError("Unable to delete saveFixFile, encountered exception: " + ex.Message);
            }
            logger.EmptyLine();

            orig(instance);
        }

        //Should wipe the character when you do "reset save" in the character menu (or when you win)
        public static void PlayerProgression_WipeSaveState(On.PlayerProgression.orig_WipeSaveState orig, PlayerProgression instance, int stateToDelete) {
            SFLogSource log = new SFLogSource("PlayerProgression_WipeSaveState");

            string[] fileSavDiv = Regex.Split(SFFile.ReadSFFile(instance.rainWorld), "<SavDiv>");
            if (SFFile.GetSaveStateIndex(fileSavDiv, stateToDelete, out int saveStateIndex)) {
                log.Log($"Clearing save state {stateToDelete}");
                //Construct a new string with the save state removed.
                string outText = string.Empty;
                for (int i = 0; i < fileSavDiv.Length; ++i) {
                    if (i != saveStateIndex) {
                        outText += fileSavDiv[i] + ((i == saveStateIndex - 1 || i == fileSavDiv.Length - 1 || fileSavDiv.Equals(string.Empty)) ? string.Empty : "<SavDiv>");
                    }
                }
                //Write to save
                using (StreamWriter streamWriter = File.CreateText(SFFile.GetSFPath(instance.rainWorld))) {
                    streamWriter.Write(outText);
                }
            } else {
                log.LogWarning($"Could not find a save state to wipe for save state {saveStateIndex}");
            }
            log.EmptyLine();
            orig(instance, stateToDelete);
        }

        //Fix room translations when adapting the save file to world (sav >> game)
        public static void RegionState_AdaptWorldToRegionState(On.RegionState.orig_AdaptWorldToRegionState orig, RegionState instance) {
            SFLogSource l = new SFLogSource("RegionState_AdaptWorldToRegionState");
            l.Log($"Adapting world to save data: {instance.regionName} | cycle: {instance.saveState.cycleNumber}");
            //Check the amount of additional time CustomRegionSaves adds to the saving/loading process.
            var watch = new System.Diagnostics.Stopwatch(); watch.Start();

            //NOTE : This is where the tricky stuff starts
            SFRegionState savFix = new SFRegionState(instance);
            savFix.AdaptToSaveFile();
            savFix.ApplyRoomTranslationsToRegion();
            savFix.RecoverOutOfRegionEntities();
            string regionData = savFix.SaveToString();
            SFFile.WriteRegion(regionData, instance);

            watch.Stop();
            l.Log($"Finished adapting world to save data : {instance.regionName} in {watch.ElapsedMilliseconds}ms");
            l.EmptyLine();

            orig(instance); //Room translations are applied to objects before the Save data is adapted into the game
        }

        //Save room translations to the save file (game >> sav)
        public static void RegionState_AdaptRegionStateToWorld(On.RegionState.orig_AdaptRegionStateToWorld orig, RegionState instance, int playerShelter, int activeGate) {
            orig(instance, playerShelter, activeGate); //Orig is called first since this gets saved after everything else.

            SFLogSource log = new SFLogSource("RegionState_AdaptRegionStateToWorld");
            log.Log($"Adapting save data to world : {instance.regionName} | cycle: {instance.saveState.cycleNumber}");
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            SFRegionState savFix = new SFRegionState(instance);
            savFix.AdaptToSaveFile(); //This will load any previously saved outOfRegionObjects, translations will be immedietly overwritten
            savFix.AdaptTranslationsToWorld();
            string regionData = savFix.SaveToString();
            SFFile.WriteRegion(regionData, instance);
            watch.Stop();
            log.Log($"Finished adapting save data to world : {instance.regionName} in {watch.ElapsedMilliseconds}ms");
            log.EmptyLine();
        }

        // ------------------------------------------------
        // Code for AutoUpdate support
        // Should be put in the main PartialityMod class.
        // Comments are optional.

        // Update URL - don't touch!
        // You can go to this in a browser (it's safe), but you might not understand the result.
        // This URL is specific to this mod, and identifies it on AUDB.
        public string updateURL = "http://beestuff.pythonanywhere.com/audb/api/mods/7/1";
        // Version - increase this by 1 when you upload a new version of the mod.
        // The first upload should be with version 0, the next version 1, the next version 2, etc.
        // If you ever lose track of the version you're meant to be using, ask Pastebin.
        public int version = 3;
        // Public key in base64 - don't touch!
        public string keyE = "AQAB";
        public string keyN = "pQTSWONMkz/+cDljDGQPVe33mzBTAjabsB8++ZF7h+5rx65KSpvqviESF8X6tKFZPQBxaQD+JwLK05kSt9lopcUsLe8T+Vxia4HXDnEGmAMuZg477vpib+JCgKP0pAMjwtLiD8GpvbI3kUcxD8qJ3+l6ULCTbT8Z120U2lae22AzMU5Tpz0Yvl/vATv3472roBYe7N9LA5mFaACPT+E+U36/hSoIhtVmIxtbOXCmCod/k4L3/CPDs4w34gb1Vo43GiLLo9jOSXVPhMTMkWHrYWnEWy4tu9Ujcj0KZcuHGylO6MYfV+dSJwdAgkcFuq4plNRHt+pmAnwbI0U5kcd2FlpkI2ihqVShvDyj4v3mFNmd/0YighTcBXmYQj3h06NKup9cPfNCPRdwP9CTNjtLljA+SWkl7z/j+z29lWFuE6a1xNiYZb+GGj4UbExUDgcZ1YFOqSgPeQPeoFGqY5nGBQN0UOv/9GmrdaxxWGDrgkbRL2+L/NwKV2uH8HVBzSu0VBRnTjz3JTzkKTBR+ai0LDfmez7BBvG8giTvgNrHi3LxxvVGUugj9GnbRxnTSY6By8JKSwKkgPztVb+irUPW+1lQv76Gyx6fh/8V/+EbrgKSVUEH/mF/Yg8MDQseRbF7X697ZNcfiHm/dGjV+zNcR8CL0Tvtj2mqdNPH4Eib/qE=";
        // ------------------------------------------------
    }
}
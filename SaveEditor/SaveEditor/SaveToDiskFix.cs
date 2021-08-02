//Author:Deltatime
//Progress:Complete
using System;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using BepInEx.Logging;

namespace CustomRegionSaves {
    //Fixes the crash caused by the game attempting to access an out of bounds position in an array when a region is in the save file but not loaded in the game.
    class SaveToDiskFix {
        public static void IL_PlayerProgression_SaveToDisk(ILContext il) {
            //Outputs to SaveFixerLog.txt
            SFLogSource l2 = new SFLogSource("IL_PlayerProgression_SaveToDisk");
            //Outputs to Bepinex log
            using (ManualLogSource l = Logger.CreateLogSource("IL_PlayerProgression_SaveToDisk")) {
                { string temp = "Seeking IL code to edit..."; l.LogInfo(temp); l2.Log(temp); }
                //IL Edit watch (TODO:Remove when this is no longer needed)
                var watch = new System.Diagnostics.Stopwatch(); watch.Start();
                //Number of IL edits made (should normally be only 1)
                int editedLocations = 0;
                try {
                    bool exit = false;
                    List<ILCursor> cc = new List<ILCursor>();
                    ILCursor c = new ILCursor(il);
                    while (!exit) {
                        //Cursor will try to find the next position of these IL instructions, which corresponds to the code
                        //array[num] = true; (from line 307 of decompiled assembly-csharp)
                        //{PlayerProgression::SaveToDisk::array} is an array of booleans with the size of {PlayerProgression::mapDiscoveryTextures}.
                        //{PlayerProgression::SaveToDisk::num} is an integer that is set to the index of a region, if the region can be found in the game but not the save file this will be -1.
                        if (c.TryGotoNext(i => i.MatchLdloc(1), i => i.MatchLdloc(8), i => i.MatchLdcI4(1), i => i.MatchStelemI1())) {
                            cc.Add(c);
                        } else {
                            exit = true;
                        }
                    }
                    //This adds a branch around the line causing the crash
                    //if (num > -1) { array[num] = true; }
                    for (int i = 0; i < cc.Count; ++i) {
                        ILCursor tempCursor = cc[i].Clone();
                        tempCursor.Index += 4;
                        ILLabel branchEnd = il.DefineLabel();
                        tempCursor.MarkLabel(branchEnd);
                        cc[i].Emit(OpCodes.Ldloc_S, (byte)8);
                        cc[i].Emit(OpCodes.Ldc_I4_M1);
                        cc[i].Emit(OpCodes.Ble_S, branchEnd); //The jump over the line happens on the true condition
                        ++editedLocations;
                    }
                } catch (Exception e) {
                    { string temp = "EXCEPTION BREAK: " + e.Message + "\nAt: " + e.StackTrace; l.LogInfo(temp); l2.Log(temp); }
                }
                watch.Stop();
                { string temp = $"Complete! Edited {editedLocations} locations after {watch.ElapsedMilliseconds} ms"; l.LogInfo(temp); l2.Log(temp); }
                l2.EmptyLine();
            }
        }
    }
}
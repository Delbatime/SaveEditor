//Author:Deltatime
using System;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using BepInEx.Logging;

namespace SaveFixer {
    class SaveToDiskFix {
        public static void IL_PlayerProgression_SaveToDisk(ILContext il) {
            using (ManualLogSource l = Logger.CreateLogSource("IL_PlayerProgression_SaveToDisk")) {
                l.LogInfo("Seeking IL code to edit...");
                var watch = new System.Diagnostics.Stopwatch();
                int editedLocations = 0;
                watch.Start();
                try {
                    bool exit = false;
                    List<ILCursor> cc = new List<ILCursor>();
                    ILCursor c = new ILCursor(il);
                    while (!exit) {
                        if (c.TryGotoNext(i => i.MatchLdloc(1), i => i.MatchLdloc(8), i => i.MatchLdcI4(1), i => i.MatchStelemI1())) {
                            cc.Add(c);
                        } else {
                            exit = true;
                        }
                    }
                    for (int i = 0; i < cc.Count; ++i) {
                        ILCursor tempCursor = cc[i].Clone();
                        tempCursor.Index += 4;
                        ILLabel branchEnd = il.DefineLabel();
                        tempCursor.MarkLabel(branchEnd);
                        cc[i].Emit(OpCodes.Ldloc_S, (byte)8);
                        cc[i].Emit(OpCodes.Ldc_I4_M1);
                        cc[i].Emit(OpCodes.Ble_S, branchEnd);
                        ++editedLocations;
                    }
                } catch (Exception e) {
                    l.LogError("EXCEPTION BREAK: " + e.Message);
                }
                watch.Stop();
                l.LogInfo($"Complete! Edited {editedLocations} locations after {watch.ElapsedMilliseconds} ms");
            }
        }
    }
}

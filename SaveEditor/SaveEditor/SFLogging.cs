//Author:Deltatime
using System;
using System.IO;
using RWCustom;

namespace SaveFixer {
    public class SFLog : System.IDisposable {
        public SFLog() {
            string filePath = string.Concat(new object[] { Custom.RootFolderDirectory(), "SaveFixerLog.txt" });
            try {
                logStream = new FileStream(filePath, FileMode.Create);
                logStreamWriter = new StreamWriter(logStream);
            } catch (Exception ex) {
                BepInEx.Logging.Logger.CreateLogSource("SaveFixer::SFLog").LogError($"Failed to create filestream, Exception thrown: {ex.Message}");
                this.Dispose();
            }
        }
        public void LogString(string s) {
            if (logStreamWriter != null) {
                if (s == null) {
                    s = string.Empty;
                }
                logStreamWriter.WriteLine(s);
                logStreamWriter.Flush();
            }
        }

        public void LogMessage(string message, string source, string state) {
            if (source == null) {
                source = string.Empty;
            }
            if (state == null) {
                state = string.Empty;
            }
            LogString(string.Concat(new object[] { "[", state,  "]", source, " - ", message }));
        }
        public void Dispose() {
            logStreamWriter.Dispose();
            logStream.Dispose();
            logStream = null;
            logStreamWriter = null;
        }

        private FileStream logStream;
        private StreamWriter logStreamWriter;
    }

    public struct SFLogSource {
        public SFLogSource(string source) {
            if (source != null) {
                _source = source;
            } else {
                _source = string.Empty;
            }
        }
        public void Log(string text) { SaveFixer.outputLog.LogString(text); }
        public void LogMessage(string message) { SaveFixer.outputLog.LogString(string.Concat(new object[] { _source, " - ", message })); }
        public void LogDebug(string message) { SaveFixer.outputLog.LogMessage(message, _source, " debug "); }
        public void LogWarning(string message) { SaveFixer.outputLog.LogMessage(message, _source, "Warning"); }
        public void LogError(string message) { SaveFixer.outputLog.LogMessage(message, _source, " ERROR "); }
        public void EmptyLine() { SaveFixer.outputLog.LogString(string.Empty); }

        private readonly string _source;
    }
}

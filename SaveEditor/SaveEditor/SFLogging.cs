//Author:Deltatime
//Progress:Complete
using System;
using System.IO;
using RWCustom;

namespace CustomRegionSaves {
    // Log for CustomRegionSaves. Outputs to SaveFixerLog.txt. <br></br>
    // Creates a new file if SaveFixerLog.txt does not exist already.
    public class SFLog : System.IDisposable {
        //Ctor
        public SFLog() {
            Init();
        }
        //Added in Ver !LATEST!
        // Initializes the file streams for the CustomRegionSaves log. <br></br>
        // Streams output to SaveFixerLog.txt, and a new file is created if SaveFixerLog.txt does not already exist. If SavefixerLog.txt does exist it will be overwritten.<br></br>
        // Does nothing if logStream or logStreamWriter are not null.
        public void Init() {
            if (logStream != null || logStreamWriter != null) {
                return;
            }
            string filePath = string.Concat(new object[] { Custom.RootFolderDirectory(), "SaveFixerLog.txt" });
            try {
                logStream = new FileStream(filePath, FileMode.Create);
                logStreamWriter = new StreamWriter(logStream);
            } catch (Exception ex) {
                BepInEx.Logging.Logger.CreateLogSource("CustomRegionSaves::SFLog").LogError($"Failed to create filestream, Exception thrown: {ex.Message}");
                this.Dispose();
            }

        }
        // Disposes of the file streams associated with the CustomRegionSaves log.
        // When disposed, streams should be null.
        public void Dispose() {
            logStreamWriter.Dispose();
            logStream.Dispose();
            logStream = null;
            logStreamWriter = null;
            BepInEx.Logging.Logger.CreateLogSource("CustomRegionSaves::SFLog").LogInfo($"Disposing log.");
        }
        // Writes a string as a single line to the file stream.
        // string s - The String to write to the file stream.
        public void LogString(string s) {
            if (logStreamWriter != null) {
                if (s == null) {
                    s = string.Empty;
                }
                logStreamWriter.WriteLine(s);
                logStreamWriter.Flush();
            }
        }
        // Log a message with additional formatting for the source and type (Error/Warning/Debug) of the log message.
        // Message format: [STATE]SOURCE - MESSAGE
        // string message -  Main message of the log.<br></br> If null then this is treated as if it is an empty string.
        // string source -  Where the log message originated from.<br></br> If null then this is treated as if it is an empty string. 
        // string state - Type of the log message (Error/Warning/Debug).<br></br> If null then this is treated as if it is an empty string.
        public void LogMessage(string message, string source, string state) {
            if (source == null) {
                source = string.Empty;
            }
            if (state == null) {
                state = string.Empty;
            }
            LogString(string.Concat(new object[] { "[", state,  "]", source, " - ", message }));
        }
        // FileStream for the log file.
        private FileStream logStream;
        // Used to write strings to logStream.
        private StreamWriter logStreamWriter;
    }

    public struct SFLogSource {
        public SFLogSource(string source) {
            if (source != null) {
                _source = source;
            } else {
                _source = string.Empty;
            }
            _log = SaveFixer.outputLog;
        }
        public void Log(string text) { _log.LogString(text); }
        public void LogMessage(string message) { _log.LogString(string.Concat(new object[] { _source, " - ", message })); }
        public void LogDebug(string message) { _log.LogMessage(message, _source, " debug "); }
        public void LogWarning(string message) { _log.LogMessage(message, _source, "Warning"); }
        public void LogError(string message) { _log.LogMessage(message, _source, " ERROR "); }
        public void LogInfo(string message) { _log.LogMessage(message, _source, " info  "); }
        public void LogVerbose(string message) { _log.LogMessage(message, _source, "-extra"); }
        public void EmptyLine() { _log.LogString(string.Empty); }

        private readonly string _source;
        private readonly SFLog _log;
    }
}

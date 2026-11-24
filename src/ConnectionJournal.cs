using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Callrift {
    public static class ConnectionJournal {
        static readonly object gate=new object();
        // Only event identifiers, timestamps and engine exit codes; never raw engine output,
        // profiles, addresses, credentials, application paths or browsing destinations.
        public static void Record(string eventId) {
            if(!Regex.IsMatch(eventId??"","^[A-Za-z0-9_-]{1,80}$")) return;
            try { lock(gate) {
                string folder=Files.At("diagnostics"); Files.PrivateDirectory(folder);
                string path=Path.Combine(folder,"vpn-events.log");
                if(File.Exists(path) && new FileInfo(path).Length>65536) {
                    string previous=Path.Combine(folder,"vpn-events.previous.log");
                    if(File.Exists(previous)) File.Delete(previous);
                    File.Move(path,previous);
                }
                File.AppendAllText(path,DateTimeOffset.Now.ToString("o")+" "+eventId+Environment.NewLine,new UTF8Encoding(false));
            }} catch { }
        }
    }
}

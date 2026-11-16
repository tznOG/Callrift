using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Callrift {
    public class DiscordProcess {
        public int Id;
        public long Started;
        public string Channel;
    }
    public static class DiscordSession {
        public static readonly string[] Channels = { "Discord", "DiscordPTB", "DiscordCanary", "DiscordDevelopment" };
        public static string Protocol(string channel) {
            switch(channel) {
                case "Discord": return "discord://-/";
                case "DiscordPTB": return "discord-ptb://-/";
                case "DiscordCanary": return "discord-canary://-/";
                case "DiscordDevelopment": return "discord-development://-/";
                default: throw new ArgumentException("Cliente Discord desconhecido.");
            }
        }
        public static List<DiscordProcess> Capture() {
            var result = new List<DiscordProcess>();
            foreach(string channel in Channels) foreach(var process in Process.GetProcessesByName(channel)) {
                using(process) { try { result.Add(new DiscordProcess { Id=process.Id, Started=process.StartTime.ToUniversalTime().Ticks, Channel=channel }); } catch(InvalidOperationException) { } catch(System.ComponentModel.Win32Exception) { result.Add(new DiscordProcess { Id=process.Id,Started=0,Channel=channel }); } }
            }
            return result;
        }
        static Process Match(DiscordProcess snapshot) {
            if(!Channels.Contains(snapshot.Channel) || snapshot.Started == 0) throw new Exception("Não foi possível verificar os processos do Discord. Feche-o manualmente ou confira as permissões do Windows.");
            try {
                var process=Process.GetProcessById(snapshot.Id);
                if(process.ProcessName == snapshot.Channel && process.StartTime.ToUniversalTime().Ticks == snapshot.Started) return process;
                process.Dispose(); return null;
            } catch(ArgumentException) { return null; } catch(InvalidOperationException) { return null; }
        }
        // Call only after explicit consent in the app. No restart happens during VPN tests.
        public static async Task Stop(List<DiscordProcess> approved) {
            foreach(var snapshot in approved) using(var process=Match(snapshot)) { if(process!=null) process.CloseMainWindow(); }
            await Task.Delay(1000);
            await Task.Run(() => {
                foreach(var snapshot in approved) using(var process=Match(snapshot)) {
                    if(process==null) continue;
                    if(!process.HasExited) process.Kill();
                    if(!process.WaitForExit(3000)) throw new Exception("O Discord não encerrou a tempo. Feche-o pela bandeja e abra novamente.");
                }
            });
            var channels=approved.Select(x=>x.Channel).Distinct().ToArray();
            if(Capture().Any(x=>channels.Contains(x.Channel))) throw new Exception("Ainda há processos do Discord abertos. Feche-o pela bandeja antes de abrir novamente.");
        }
    }
}

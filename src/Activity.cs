using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Media;

namespace Callrift {
    public class GameExecutable {
        public string name { get; set; }
        public string os { get; set; }
        public override string ToString() { return name; }
    }
    public class Game : INotifyPropertyChanged {
        public string id { get; set; }
        public string name { get; set; }
        public string icon_hash { get; set; }
        public List<GameExecutable> executables { get; set; }
        public string[] aliases { get; set; }
        [ScriptIgnore] public bool Favorite { get; set; }
        [ScriptIgnore] public bool Running { get; set; }
        [ScriptIgnore] public DateTime Started { get; set; }
        [ScriptIgnore] public ImageSource Icon { get { return GameIcons.Get(this); } }
        [ScriptIgnore]
        public string ActionLabel { get { return Running ? I18n.T("Stop") : I18n.T("Run"); } }
        [ScriptIgnore]
        public string Star { get { return Favorite ? "★" : "☆"; } }
        [ScriptIgnore]
        public string Elapsed { get { return Running ? (DateTime.UtcNow - Started).ToString(@"hh\:mm\:ss") : ""; } }
        [ScriptIgnore]
        public double Progress { get { return Running ? Math.Min(100, (DateTime.UtcNow - Started).TotalSeconds / 9) : 0; } }
        [ScriptIgnore]
        public string Details { get { return "ID: " + id + "\n" + String.Join("\n", (executables ?? new List<GameExecutable>()).Select(x => x.name + " (" + x.os + ")")); } }
        [ScriptIgnore]
        public List<GameExecutable> WindowsExecutables { get { return (executables ?? new List<GameExecutable>()).Where(x => x.os == "win32" && !String.IsNullOrEmpty(x.name) && !x.name.StartsWith(">")).OrderBy(x => x.name.Count(c => c == '/' || c == '\\')).ThenBy(x => x.name.Length).ToList(); } }
        [ScriptIgnore]
        public bool CanRun { get { return Running || WindowsExecutables.Count > 0; } }
        public event PropertyChangedEventHandler PropertyChanged;
        public void Notify() { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("")); }
    }
    public class GameCache { public DateTime Timestamp { get; set; } public List<Game> Games { get; set; } }
    public class RunningGame { public Game Game; public Process Process; public string Directory; }

    public sealed class ActivityService : IDisposable {
        public List<Game> Games = new List<Game>();
        public Dictionary<string,RunningGame> Running = new Dictionary<string,RunningGame>();
        HashSet<string> favorites = new HashSet<string>();
        ChildJob job;
        public DateTime CacheTime;
        public bool FromCache, Offline;
        static readonly HttpClient client = MakeClient();
        readonly HttpClient transport;
        static HttpClient MakeClient() { var c = new HttpClient(); c.Timeout = TimeSpan.FromSeconds(25); c.DefaultRequestHeaders.UserAgent.ParseAdd("Callrift-Presence/1.2"); return c; }
        public ActivityService() : this(null) { }
        public ActivityService(HttpClient transport) {
            this.transport = transport ?? client;
            try { if (File.Exists(Files.At("favorites.json"))) favorites = new HashSet<string>(Json.Read<string[]>(File.ReadAllText(Files.At("favorites.json")))); } catch { }
        }
        public async Task Load(bool force) {
            string path = Files.At("games-cache.json"); GameCache cache = null;
            try { if (File.Exists(path)) cache = await Task.Run(() => Json.Read<GameCache>(File.ReadAllText(path))); } catch { }
            Offline = false;
            if (!force && cache != null && DateTime.UtcNow - cache.Timestamp < TimeSpan.FromDays(2)) { Games = cache.Games; CacheTime = cache.Timestamp; FromCache = true; }
            else {
                try {
                    var requests = new [] { transport.GetStringAsync("https://discord.com/api/v9/games/detectable"), transport.GetStringAsync("https://discord.com/api/v9/applications/non-games/detectable") };
                    // Keep the main catalogue usable even if the optional non-games endpoint fails.
                    string gamesJson = await requests[0];
                    var data = await Task.Run(() => Json.Read<List<Game>>(gamesJson));
                    try { string extra = await requests[1]; data.AddRange(await Task.Run(() => Json.Read<List<Game>>(extra))); } catch { }
                    Games = data.Where(x => x.executables != null && x.executables.Count > 0).GroupBy(x => x.id).Select(x => x.First()).OrderBy(x => x.name, StringComparer.CurrentCultureIgnoreCase).ToList();
                    CacheTime = DateTime.UtcNow; FromCache = false;
                    await Task.Run(() => Files.AtomicText(path, Json.Write(new GameCache { Timestamp = CacheTime, Games = Games })));
                } catch (Exception error) {
                    if (cache == null) throw new Exception("Não foi possível carregar o catálogo do Discord. Confira sua conexão e tente atualizar. " + error.Message, error);
                    Games = cache.Games; CacheTime = cache.Timestamp; FromCache = true; Offline = true;
                }
            }
            foreach (var g in Games) {
                g.Favorite = favorites.Contains(g.id);
                RunningGame active;
                if (Running.TryGetValue(g.id, out active)) { g.Running = true; g.Started = active.Game.Started; active.Game = g; }
                else g.Running = false;
            }
        }
        public List<Game> Filter(string query) {
            var terms = (query ?? "").Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToArray();
            return Games.Where(g => terms.Length == 0 || terms.Any(t => Contains(g.name,t) || Contains(g.id,t) || (g.aliases != null && g.aliases.Any(a => Contains(a,t))))).ToList();
        }
        static bool Contains(string text, string term) { return text != null && text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0; }
        public void Favorite(Game game) {
            if (favorites.Contains(game.id)) favorites.Remove(game.id); else favorites.Add(game.id);
            Files.AtomicText(Files.At("favorites.json"), Json.Write(favorites.ToArray()));
            game.Favorite = favorites.Contains(game.id); game.Notify();
        }
        public static string SafePath(string root, string id, string executable) {
            if (!Regex.IsMatch(id ?? "", "^[0-9]{1,30}$")) throw new Exception("ID de aplicativo inválido.");
            if (String.IsNullOrWhiteSpace(executable) || executable.Length > 220) throw new Exception("Nome de executável inválido.");
            executable = executable.Replace('/', '\\');
            if (Path.IsPathRooted(executable) || executable.Contains(':') || executable.Split('\\').Any(x => x.Length == 0 || x == "." || x == ".." || x.EndsWith(".") || x.EndsWith(" ") || x.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)) throw new Exception("Caminho de executável inseguro no catálogo.");
            if (!executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new Exception("Este executável não é um aplicativo .exe do Windows.");
            string directory = Path.GetFullPath(Path.Combine(root, id)) + Path.DirectorySeparatorChar;
            string destination = Path.GetFullPath(Path.Combine(directory, executable));
            if (!destination.StartsWith(directory, StringComparison.OrdinalIgnoreCase)) throw new Exception("Caminho fora da pasta da atividade.");
            return destination;
        }
        public void Start(Game game, string selected) {
            if (Running.ContainsKey(game.id)) return;
            var options = game.WindowsExecutables;
            string executable = selected ?? options.Select(x => x.name).FirstOrDefault();
            if (executable == null || !options.Any(x => x.name == executable)) throw new Exception("Nenhum executável Windows compatível para este aplicativo.");
            string root = Files.At("activities"); Directory.CreateDirectory(root);
            string destination = SafePath(root, game.id, executable);
            string directory = Path.Combine(root, game.id);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ActivityHost.exe");
            File.Copy(source, destination, true);
            if (job == null) job = new ChildJob();
            Process process = null;
            try {
                process = Process.Start(new ProcessStartInfo(destination, Process.GetCurrentProcess().Id.ToString()) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(destination) });
                job.Add(process);
                game.Running = true; game.Started = DateTime.UtcNow;
                Running.Add(game.id, new RunningGame { Game = game, Process = process, Directory = directory }); game.Notify();
            } catch { if (process != null) { try { process.Kill(); } catch { } process.Dispose(); } throw; }
        }
        public void Stop(string id) {
            RunningGame item; if (!Running.TryGetValue(id, out item)) return;
            if (!item.Process.HasExited) { item.Process.Kill(); item.Process.WaitForExit(1500); }
            item.Process.Dispose(); Running.Remove(id); item.Game.Running = false; item.Game.Notify();
            try { Directory.Delete(item.Directory, true); } catch { }
        }
        public bool Reap() {
            var dead = Running.Where(x => x.Value.Process.HasExited).Select(x => x.Key).ToArray();
            foreach (string id in dead) Stop(id); return dead.Length > 0;
        }
        public void StopAll() { foreach (string id in Running.Keys.ToArray()) Stop(id); }
        public void Dispose() { try { StopAll(); } finally { if (job != null) job.Dispose(); } }
    }
}

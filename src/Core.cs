using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.ComponentModel;

namespace Callrift {
    public static class Json {
        public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 100 }; }
        public static string Write(object value) { return Serializer().Serialize(value); }
        public static T Read<T>(string text) { return Serializer().Deserialize<T>(text); }
    }

    public static class Files {
        // Keep the original data directory so existing DPAPI profiles and favorites remain available.
        public static string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiscordVPN");
        public static string At(string name) { Directory.CreateDirectory(Root); return Path.Combine(Root, name); }
        public static void PrivateDirectory(string path) {
            Directory.CreateDirectory(path);
            var acl = new DirectorySecurity();
            acl.SetAccessRuleProtection(true, false);
            acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(path).SetAccessControl(acl);
        }
        public static void AtomicText(string path, string value) {
            string temp = path + ".new";
            File.WriteAllText(temp, value, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        public static void Protect(string path, string value) {
            var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, data);
        }
        public static string Unprotect(string path) { return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser)); }
    }

    public class Settings {
        public string SelectedProfile { get; set; }
        public bool StartWithWindows { get; set; }
        public bool AutoConnect { get; set; }
        public bool OpenDiscord { get; set; }
        public bool MinimizeToTray { get; set; }
        public bool LightTheme { get; set; }
        public bool CheckUpdatesOnStart { get; set; }
        public string Language { get; set; }
        public Settings() { CheckUpdatesOnStart = true; MinimizeToTray = true; AutoConnect = true; OpenDiscord = true; Language = "pt-BR"; }
        public static Settings Load() {
            try { if (File.Exists(Files.At("settings.json"))) return Json.Read<Settings>(File.ReadAllText(Files.At("settings.json"))); } catch { }
            return new Settings();
        }
        public void Save() { Files.AtomicText(Files.At("settings.json"), Json.Write(this)); }
    }

    public class WireGuardProfile : INotifyPropertyChanged {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime Imported { get; set; }
        public string Configuration { get; set; }
        public string Endpoint { get { return WireGuard.Parse(Configuration).Endpoint; } }
        public string Modified { get { return Imported.ToLocalTime().ToString("dd/MM/yyyy HH:mm"); } }
        [ScriptIgnore] public ProfileMeasurement Measurement { get; set; }
        [ScriptIgnore] public bool Best { get; set; }
        [ScriptIgnore] public bool Testing { get; set; }
        [ScriptIgnore] public string LatencyLabel { get { return Testing ? I18n.T("Testing") : Measurement == null ? "—" : Measurement.MedianMs.HasValue ? Math.Round(Measurement.MedianMs.Value) + " ms" : I18n.T("TestFailed"); } }
        [ScriptIgnore] public string TestLabel { get { return Testing ? I18n.T("PleaseWait") : Best ? I18n.T("BestProfile") : Measurement == null ? I18n.T("NotTested") : Measurement.Successes + "/3 HTTPS"; } }
        [ScriptIgnore] public string TestTooltip { get { return Measurement == null ? I18n.T("NotTested") : Measurement.Summary; } }
        public event PropertyChangedEventHandler PropertyChanged;
        public void Notify() { if (PropertyChanged != null) PropertyChanged(this,new PropertyChangedEventArgs("")); }
    }

    public class ProfileStore {
        public string DirectoryPath { get { string p = Files.At("profiles"); Directory.CreateDirectory(p); return p; } }
        public List<WireGuardProfile> Load(Action<string> warning) {
            var list = new List<WireGuardProfile>();
            foreach (string path in Directory.GetFiles(DirectoryPath, "*.profile")) {
                try { var p = Json.Read<WireGuardProfile>(Files.Unprotect(path)); WireGuard.Parse(p.Configuration); list.Add(p); }
                catch { if (warning != null) warning("Um perfil não pôde ser lido. Importe novamente o arquivo .conf."); }
            }
            return list.OrderBy(p => p.Name).ToList();
        }
        public WireGuardProfile Import(string path) {
            if (!String.Equals(Path.GetExtension(path), ".conf", StringComparison.OrdinalIgnoreCase)) throw new Exception("Selecione um arquivo WireGuard .conf da Proton VPN.");
            if (new FileInfo(path).Length > 65536) throw new Exception("Arquivo .conf maior que o limite de 64 KB.");
            string conf = File.ReadAllText(path); WireGuard.Parse(conf);
            var p = new WireGuardProfile { Id = Guid.NewGuid().ToString("N"), Name = Path.GetFileNameWithoutExtension(path), Imported = DateTime.UtcNow, Configuration = conf };
            Files.Protect(Path.Combine(DirectoryPath, p.Id + ".profile"), Json.Write(p));
            return p;
        }
        public void Remove(WireGuardProfile p) {
            if (!Regex.IsMatch(p.Id, "^[a-f0-9]{32}$")) throw new Exception("Perfil inválido.");
            File.Delete(Path.Combine(DirectoryPath, p.Id + ".profile"));
        }
    }

    public class WireGuard {
        public string PrivateKey, PublicKey, PreSharedKey, Host, Endpoint;
        public string[] Address, AllowedIPs;
        public int Port, MTU = 1420, KeepAlive = 25;
        static string Need(Dictionary<string,string> values, string key) {
            string v; if (!values.TryGetValue(key, out v) || String.IsNullOrWhiteSpace(v)) throw new Exception("Campo obrigatório ausente: " + key + "."); return v;
        }
        static void Key(string value) {
            try { if (Convert.FromBase64String(value).Length != 32) throw new Exception(); }
            catch { throw new Exception("Chave WireGuard inválida. Gere um novo .conf na Proton VPN."); }
        }
        public static bool IsCidr(string value) {
            var bits = value.Split('/'); IPAddress ip; int prefix;
            return bits.Length == 2 && IPAddress.TryParse(bits[0], out ip) && Int32.TryParse(bits[1], out prefix) && prefix >= 0 && prefix <= (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128);
        }
        static string[] Cidrs(string value) {
            var list = value.Split(',').Select(x => x.Trim()).ToArray();
            if (list.Length == 0 || list.Any(x => !IsCidr(x))) throw new Exception("Endereço ou máscara de rede inválidos no .conf.");
            return list;
        }
        public static WireGuard Parse(string text) {
            var face = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            var peer = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string,string> section = null; int peers = 0; bool hasInterface = false;
            foreach (string raw in text.Split('\n')) {
                string line = raw.Split('#')[0].Trim();
                if (line.Length == 0 || line.StartsWith(";")) continue;
                if (line.StartsWith("[")) {
                    if (line.Equals("[Interface]", StringComparison.OrdinalIgnoreCase) && !hasInterface) { section = face; hasInterface = true; }
                    else if (line.Equals("[Peer]", StringComparison.OrdinalIgnoreCase) && ++peers == 1) section = peer;
                    else throw new Exception("Use uma configuração Proton WireGuard com uma Interface e um Peer.");
                    continue;
                }
                int split = line.IndexOf('=');
                if (section == null || split <= 0) throw new Exception("Formato .conf inválido.");
                string k = line.Substring(0, split).Trim(), v = line.Substring(split + 1).Trim();
                if (new [] { "PreUp", "PostUp", "PreDown", "PostDown" }.Contains(k, StringComparer.OrdinalIgnoreCase)) throw new Exception("Comandos de sistema no .conf não são aceitos. Use o arquivo original da Proton.");
                if (section.ContainsKey(k)) throw new Exception("Campo duplicado no .conf: " + k);
                section[k] = v;
            }
            if (!hasInterface || peers != 1) throw new Exception("O arquivo precisa ter uma Interface e um Peer.");
            var w = new WireGuard();
            w.PrivateKey = Need(face, "PrivateKey"); w.PublicKey = Need(peer, "PublicKey"); Key(w.PrivateKey); Key(w.PublicKey);
            w.Address = Cidrs(Need(face, "Address")); w.AllowedIPs = Cidrs(Need(peer, "AllowedIPs"));
            w.Endpoint = Need(peer, "Endpoint");
            var match = Regex.Match(w.Endpoint, @"^(?:\[([^\]]+)\]|([^:\s/]+)):(\d+)$");
            if (!match.Success || !Int32.TryParse(match.Groups[3].Value, out w.Port) || w.Port < 1 || w.Port > 65535) throw new Exception("Endpoint WireGuard inválido.");
            w.Host = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            IPAddress endpointIp;
            if (!IPAddress.TryParse(w.Host, out endpointIp) && Uri.CheckHostName(w.Host) != UriHostNameType.Dns) throw new Exception("Servidor WireGuard inválido.");
            string number;
            if (face.TryGetValue("MTU", out number) && (!Int32.TryParse(number, out w.MTU) || w.MTU < 1280 || w.MTU > 1500)) throw new Exception("MTU deve estar entre 1280 e 1500.");
            if (peer.TryGetValue("PersistentKeepalive", out number) && (!Int32.TryParse(number, out w.KeepAlive) || w.KeepAlive < 0 || w.KeepAlive > 65535)) throw new Exception("PersistentKeepalive inválido.");
            if (peer.TryGetValue("PresharedKey", out w.PreSharedKey)) Key(w.PreSharedKey);
            return w;
        }
        public object Config(int probePort, string password) { return Config(probePort,password,false); }
        public object Config(int probePort, string password, bool probeOnly) {
            var peer = new Dictionary<string,object> { { "address", Host }, { "port", Port }, { "public_key", PublicKey }, { "allowed_ips", AllowedIPs }, { "persistent_keepalive_interval", KeepAlive } };
            if (!String.IsNullOrEmpty(PreSharedKey)) peer["pre_shared_key"] = PreSharedKey;
            var inbounds = new List<object>();
            if (!probeOnly) inbounds.Add(new { type = "tun", tag = "discord-tun", interface_name = AppNetworkIsolation.AdapterName, address = new [] { AppNetworkIsolation.IPv4+"/30", AppNetworkIsolation.IPv6+"/126" }, mtu = MTU, auto_route = false, strict_route = false, dns_mode = "disabled", stack = "gvisor" });
            inbounds.Add(new { type = "http", tag = "vpn-probe", listen = "127.0.0.1", listen_port = probePort, users = new [] { new { username = "probe", password = password } } });
            var rules = new List<object> { new { inbound = new [] { "vpn-probe" }, action = "route", outbound = "proton" } };
            // WFP selects the signed executable before packets reach this inbound.
            // A second process-table lookup races the first SYN after WFP redirection.
            if (!probeOnly) rules.Add(new { inbound = new [] { "discord-tun" }, action = "route", outbound = "proton" });
            // No other inbound may forward traffic.
            if (!probeOnly) rules.Add(new { action = "reject" });
            return new {
                log = new { level = "warn", timestamp = true },
                dns = new { servers = new [] { new { type = "local", tag = "local-dns" } } },
                inbounds = inbounds,
                endpoints = new [] { new { type = "wireguard", tag = "proton", system = false, mtu = MTU, address = Address, private_key = PrivateKey, peers = new [] { peer }, workers = 2 } },
                outbounds = new [] { new { type = "direct", tag = "direct" } },
                route = new {
                    auto_detect_interface = true, default_domain_resolver = "local-dns",
                    rules = rules,
                    final = "direct"
                }
            };
        }
    }
}

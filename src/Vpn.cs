using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Callrift {
    // A job guarantees the engine dies if its privileged owner terminates.
    public sealed class ChildJob : IDisposable {
        IntPtr handle;
        [StructLayout(LayoutKind.Sequential)] struct Basic { public long PerProcessUserTimeLimit, PerJobUserTimeLimit; public uint LimitFlags; public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize; public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass, SchedulingClass; }
        [StructLayout(LayoutKind.Sequential)] struct Io { public ulong a,b,c,d,e,f; }
        [StructLayout(LayoutKind.Sequential)] struct Extended { public Basic BasicLimitInformation; public Io IoInfo; public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed; }
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr attributes, string name);
        [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job, int kind, ref Extended info, uint size);
        [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        public ChildJob() {
            handle = CreateJobObject(IntPtr.Zero, null);
            var info = new Extended(); info.BasicLimitInformation.LimitFlags = 0x2000;
            if (handle == IntPtr.Zero || !SetInformationJobObject(handle, 9, ref info, (uint)Marshal.SizeOf(info))) throw new Exception("Não foi possível criar o controle de processos do Windows.");
        }
        public void Add(Process p) { if (!AssignProcessToJobObject(handle, p.Handle)) throw new Exception("O Windows não permitiu supervisionar o processo."); }
        public void Dispose() { if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; } }
    }

    public static class Engine {
        public const string Version = "1.14.4";
        public const string Sha256 = "8f30682ee198855440d2a7fe070d5d3653793b2d01bb8c85eea88f93bbe7c8b9";
        public static string PathName { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engine", "sing-box.exe"); } }
        public static void Verify() {
            if (!File.Exists(PathName)) throw new Exception("Motor VPN não encontrado. Extraia a pasta completa do aplicativo.");
            using (var stream = File.OpenRead(PathName)) using (var sha = SHA256.Create()) {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (actual != Sha256) throw new Exception("A integridade do motor VPN não confere. Extraia uma cópia original.");
            }
            string driver = Path.Combine(Path.GetDirectoryName(PathName), "wintun.dll");
            if (!File.Exists(driver)) throw new Exception("Driver Wintun não encontrado. Extraia a pasta completa do aplicativo.");
            using (var stream = File.OpenRead(driver)) using (var sha = SHA256.Create()) {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (actual != "e5da8447dc2c320edc0fc52fa01885c103de8c118481f683643cacc3220dafce") throw new Exception("A integridade do driver Wintun não confere.");
            }
        }
    }

    public sealed class VpnController : IDisposable {
        NamedPipeServerStream pipe;
        StreamWriter writer;
        Process worker;
        bool disposing;
        int generation;
        Task monitor;
        public bool IsActive { get; private set; }
        public bool IsBusy { get; private set; }
        public string ExitIP { get; private set; }
        public event Action<string> Log;
        public event Action Lost;
        void Emit(string text) { if (Log != null) Log(text); }

        public async Task Connect(WireGuardProfile profile) {
            if (IsActive || IsBusy) throw new Exception("Já existe uma conexão em andamento.");
            IsBusy = true; disposing = false;
            int sessionGeneration=++generation;
            Exception failure = null;
            try {
                var config = WireGuard.Parse(profile.Configuration);
                await Task.Run((Action)Engine.Verify);
                string[] targets=await Task.Run((Func<string[]>)DiscordTargets.Discover);
                IsolationDriver.VerifyFile(Path.Combine(Path.GetDirectoryName(Engine.PathName),"ProtonVPN.CalloutDriver.sys"));
                string name = "DiscordVPN-" + Guid.NewGuid().ToString("N");
                var security = new PipeSecurity(); security.SetAccessRuleProtection(true, false);
                security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
                pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, security);
                var connected = Task.Factory.FromAsync(pipe.BeginWaitForConnection, pipe.EndWaitForConnection, null);
                var start = new ProcessStartInfo(Program.Executable, "--vpn-worker " + name) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
                if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) start.Verb = "runas";
                worker = Process.Start(start);
                if (await Task.WhenAny(connected, Task.Delay(45000)) != connected) throw new Exception("O componente VPN não respondeu. Tente conectar novamente.");
                await connected;
                writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
                var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true);
                int port; var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start(); port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
                string password = Guid.NewGuid().ToString("N");
                await writer.WriteLineAsync(Json.Write(new WorkerRequest { Configuration = profile.Configuration, ProbePort = port, Password = password, DiscordPaths=targets }));
                Task<string> ready = reader.ReadLineAsync();
                if (await Task.WhenAny(ready, Task.Delay(30000)) != ready) throw new Exception("O motor VPN não iniciou a tempo.");
                string response = await ready;
                if (response != "READY") throw new Exception(response != null && response.StartsWith("ERROR ") ? response.Substring(6) : "O motor VPN encerrou durante a inicialização.");
                Emit("Isolamento por executável configurado. Verificando acesso à internet pela Proton VPN...");
                monitor=Monitor(reader,pipe,sessionGeneration);
                // This probe is forced through the WireGuard outbound by its inbound tag.
                ExitIP = await Task.Run(() => Probe(port, password));
                if(monitor.IsCompleted || worker.HasExited) throw new Exception("O componente VPN encerrou durante a verificação.");
                IsActive = true;
                ConnectionJournal.Record("vpn_probe_ok");
                Emit("Túnel verificado. IP de saída: " + ExitIP + ". Regras WFP restritas aos executáveis do Discord; reinicie conexões já abertas.");
            } catch (Exception e) { failure = e; }
            if (failure != null) {
                try { await StopInternal(); } finally { IsBusy = false; }
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
            IsBusy = false;
        }
        static string Probe(int port, string password) {
            Exception last = null;
            for (int attempt = 0; attempt < 3; attempt++) {
                try {
                    var request = (HttpWebRequest)WebRequest.Create("https://api.ipify.org");
                    request.Proxy = new WebProxy("http://127.0.0.1:" + port) { Credentials = new NetworkCredential("probe", password) };
                    request.Timeout = 12000; request.ReadWriteTimeout = 12000; request.KeepAlive = false;
                    using (var response = request.GetResponse()) using (var stream = new StreamReader(response.GetResponseStream())) {
                        string address = stream.ReadToEnd().Trim(); IPAddress parsed;
                        if (!IPAddress.TryParse(address, out parsed)) throw new Exception("Resposta de verificação inválida.");
                        return address;
                    }
                } catch (Exception e) { last = e; }
            }
            throw new Exception("O túnel iniciou, mas não houve acesso à internet por ele. Confira o servidor e gere um novo .conf. " + (last is WebException ? "A verificação de rede expirou ou foi recusada." : ""));
        }
        async Task Monitor(StreamReader reader,NamedPipeServerStream sessionPipe,int sessionGeneration) {
            try {
                while (sessionPipe.IsConnected && sessionGeneration==generation) {
                    string line = await reader.ReadLineAsync(); if (line == null) break;
                    if(sessionGeneration!=generation) return;
                    if (line.StartsWith("LOG ")) Emit(line.Substring(4));
                    else if (line.StartsWith("ERROR ")) Emit(line.Substring(6));
                }
            } catch { }
            if (sessionGeneration==generation && !disposing && IsActive) { IsActive = false; ExitIP=null; ConnectionJournal.Record("worker_connection_lost"); Emit("O componente VPN encerrou. O isolamento do Discord foi removido."); if (Lost != null) Lost(); }
        }
        async Task StopInternal() {
            disposing = true; IsActive = false; ExitIP = null;
            ++generation;
            try { if (writer != null) await writer.WriteLineAsync("STOP"); } catch { }
            // Closing IPC also stops the worker if this app crashes or exits.
            try { if (pipe != null) pipe.Dispose(); } catch { }
            pipe = null; writer = null;
            if (worker != null) {
                var target = worker; worker = null;
                bool ended=await Task.Run(() => { try { return target.WaitForExit(10000); } catch(InvalidOperationException) { return true; } finally { target.Dispose(); } });
                if(!ended) throw new Exception("O componente VPN ainda está encerrando. Não foi possível confirmar a remoção da sessão.");
            }
        }
        public async Task Disconnect() { if (IsBusy) return; IsBusy = true; try { await StopInternal(); Emit("VPN desconectada. Componente de isolamento encerrado."); } finally { IsBusy = false; } }
        public void Dispose() {
            disposing = true;
            try { if (writer != null) writer.WriteLine("STOP"); } catch { }
            try { if (pipe != null) pipe.Dispose(); } catch { }
        }
    }

    public static class VpnWorker {
        [DllImport("kernel32.dll")] static extern bool AllocConsole();
        [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window, int command);
        delegate bool Handler(uint signal);
        [DllImport("kernel32.dll")] static extern bool SetConsoleCtrlHandler(Handler handler, bool add);
        [DllImport("kernel32.dll")] static extern bool GenerateConsoleCtrlEvent(uint signal, uint group);
        static Handler ignore = delegate { return true; };

        public static int Run(string name) {
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^DiscordVPN-[a-f0-9]{32}$")) return 1;
            string directory = null; Process engine = null; ChildJob job = null;
            AppNetworkIsolation isolation=null; SecondaryTunnelRoutes routes=null; IsolationDriver driver=null;
            Mutex ownership=null; bool owns=false;
            var gate = new object(); StreamWriter output = null;
            Action<string> send = message => { lock (gate) { try { if (output != null) output.WriteLine(message); } catch { } } };
            try {
                using (var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification)) {
                    pipe.Connect(15000);
                    output = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
                    var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true);
                    string requestText = reader.ReadLine();
                    if (requestText == null || requestText.Length > 100000) throw new Exception("Configuração de túnel inválida.");
                    // Never accept an arbitrary sing-box config at an elevated boundary.
                    var request = Json.Read<WorkerRequest>(requestText);
                    if (request.ProbePort < 1024 || request.ProbePort > 65535 || !System.Text.RegularExpressions.Regex.IsMatch(request.Password ?? "", "^[a-f0-9]{32}$")) throw new Exception("Parâmetros de verificação inválidos.");
                    string config = Json.Write(WireGuard.Parse(request.Configuration).Config(request.ProbePort, request.Password));
                    string[] targets=DiscordTargets.Validate(request.DiscordPaths);
                    ownership=new Mutex(false,"Global\\CallriftDiscordNetworkSession");
                    try { owns=ownership.WaitOne(0); } catch(AbandonedMutexException) { owns=true; }
                    if(!owns) throw new Exception("Outra sessão do Callrift já controla a VPN.");
                    SecondaryTunnelRoutes.CheckAvailable();
                    Engine.Verify();
                    ConnectionJournal.Record("worker_started");
                    directory = Path.Combine(Files.At("runtime"), Guid.NewGuid().ToString("N")); Files.PrivateDirectory(directory);
                    string path = Path.Combine(directory, "tunnel.json"); File.WriteAllText(path, config, new UTF8Encoding(false));
                    AllocConsole(); ShowWindow(GetConsoleWindow(), 0); SetConsoleCtrlHandler(ignore, true);
                    job = new ChildJob();
                    var start = new ProcessStartInfo(Engine.PathName, "run --disable-color -c \"" + path + "\"") {
                        UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true,
                        CreateNoWindow = false, WindowStyle = ProcessWindowStyle.Hidden,
                        WorkingDirectory = Path.GetDirectoryName(Engine.PathName)
                    };
                    start.EnvironmentVariables["GOGC"] = "50";
                    start.EnvironmentVariables["GOMEMLIMIT"] = "96MiB";
                    engine = Process.Start(start);
                    job.Add(engine);
                    var failed = new StringBuilder(); bool readySent = false;
                    engine.ErrorDataReceived += (s,e) => { if (e.Data != null) { lock (failed) { if (failed.Length < 4000) failed.AppendLine(e.Data); } if (readySent) send("LOG " + e.Data); } };
                    engine.OutputDataReceived += (s,e) => { if (e.Data != null && readySent) send("LOG " + e.Data); };
                    engine.BeginErrorReadLine(); engine.BeginOutputReadLine();
                    // The listener is checked here; successful internet access is checked in the UI.
                    int port = request.ProbePort;
                    bool listening = false;
                    for (int i = 0; i < 120 && !engine.HasExited; i++) {
                        try { using (var client = new TcpClient()) { client.Connect(IPAddress.Loopback, port); listening = true; break; } } catch { Thread.Sleep(75); }
                    }
                    if (!listening) { lock (failed) { throw new Exception("Falha ao criar o túnel. " + failed.ToString().Replace("\r", " ").Replace("\n", " ")); } }
                    // sing-box has loaded the key; minimize plaintext lifetime on disk.
                    File.Delete(path);
                    driver=new IsolationDriver(Path.Combine(Path.GetDirectoryName(Engine.PathName),"ProtonVPN.CalloutDriver.sys"));
                    isolation=new AppNetworkIsolation(targets);
                    routes=new SecondaryTunnelRoutes();
                    ConnectionJournal.Record("isolation_configured");
                    send("READY");
                    readySent = true;
                    var command = reader.ReadLineAsync();
                    while (!command.Wait(250)) { if (engine.HasExited) { ConnectionJournal.Record("engine_exited_"+engine.ExitCode); send("ERROR O motor VPN encerrou inesperadamente."); break; } }
                    ConnectionJournal.Record("shutdown_requested");
                }
                return 0;
            } catch (Exception e) { ConnectionJournal.Record("worker_failed_"+e.GetType().Name); send("ERROR " + e.Message.Replace("\r", " ").Replace("\n", " ")); return 1; }
            finally {
                // First remove only our routes, then the dynamic process filters, then the engine.
                if(routes!=null) routes.Dispose();
                if(isolation!=null) isolation.Dispose();
                try {
                    if (engine != null && !engine.HasExited) {
                        GenerateConsoleCtrlEvent(0, 0);
                        if (!engine.WaitForExit(3500)) { engine.Kill(); engine.WaitForExit(1500); }
                    }
                } catch { }
                if (job != null) job.Dispose();
                if (engine != null) engine.Dispose();
                if(driver!=null) driver.Dispose();
                // The only recursively deleted path is the specific session created above.
                if (directory != null) try { Directory.Delete(directory, true); } catch { }
                ConnectionJournal.Record("worker_stopped");
                if(owns && ownership!=null) ownership.ReleaseMutex();
                if(ownership!=null) ownership.Dispose();
            }
        }
    }
    public class WorkerRequest { public string Configuration { get; set; } public int ProbePort { get; set; } public string Password { get; set; } public string[] DiscordPaths { get; set; } }
}

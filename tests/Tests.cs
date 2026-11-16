using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using Callrift;

static class Tests {
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out NativeRect rect);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr window,IntPtr target,uint flags);
    static void Pump() { var frame=new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,(Action)(()=>frame.Continue=false)); Dispatcher.PushFrame(frame); }
    static void NativeCapture(Window window,string name) {
        Pump(); NativeRect rect; var handle=new System.Windows.Interop.WindowInteropHelper(window).Handle;
        Yes(GetWindowRect(handle,out rect));
        using(var bitmap=new System.Drawing.Bitmap(rect.Right-rect.Left,rect.Bottom-rect.Top)) using(var graphics=System.Drawing.Graphics.FromImage(bitmap)) {
            var dc=graphics.GetHdc(); try { Yes(PrintWindow(handle,dc,2)); } finally { graphics.ReleaseHdc(dc); }
            bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name),System.Drawing.Imaging.ImageFormat.Png);
            var top=bitmap.GetPixel(bitmap.Width/2,2); Yes(top.R+top.G+top.B<450);
        }
    }
    sealed class NoNetwork : System.Net.Http.HttpMessageHandler {
        protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,System.Threading.CancellationToken cancellation) {
            var result=new System.Threading.Tasks.TaskCompletionSource<System.Net.Http.HttpResponseMessage>(); result.SetException(new System.Net.Http.HttpRequestException("Simulated offline")); return result.Task;
        }
    }
    sealed class ReleaseReply : System.Net.Http.HttpMessageHandler {
        public System.Net.HttpStatusCode Status=System.Net.HttpStatusCode.OK;
        public string Body;
        public string Requested;
        protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken cancellation) {
            cancellation.ThrowIfCancellationRequested(); Requested=request.RequestUri.AbsoluteUri;
            Yes(request.Headers.Authorization==null && request.Headers.UserAgent.ToString().StartsWith("Callrift/"));
            return System.Threading.Tasks.Task.FromResult(new System.Net.Http.HttpResponseMessage(Status) { Content=new System.Net.Http.StringContent(Body ?? "{}") });
        }
    }
    static string ReleaseJson(string tag="v9.8.7",bool draft=false,bool prerelease=false) {
        return Json.Write(new GitHubRelease { tag_name=tag,draft=draft,prerelease=prerelease,assets=new List<ReleaseAsset> {
            new ReleaseAsset { name="Callrift-Setup-9.8.7.exe",state="uploaded",size=123,browser_download_url="https://github.com/example/callrift/releases/download/"+tag+"/Callrift-Setup-9.8.7.exe" },
            new ReleaseAsset { name="Callrift-9.8.7-Windows-x64.zip",state="uploaded",size=123,browser_download_url="https://github.com/example/callrift/releases/download/"+tag+"/Callrift-9.8.7-Windows-x64.zip" }
        }});
    }
    static int count, failures;
    static string root, engine;
    static string PrivateKey = Convert.ToBase64String(Enumerable.Range(1,32).Select(i => (byte)i).ToArray());
    static string PublicKey = Convert.ToBase64String(Enumerable.Range(33,32).Select(i => (byte)i).ToArray());
    static string Conf = "# Proton VPN test fixture, no live credentials\n[Interface]\nPrivateKey = " + PrivateKey + "\nAddress = 10.2.0.2/32\nDNS = 10.2.0.1\n\n[Peer]\nPublicKey = " + PublicKey + "\nAllowedIPs = 0.0.0.0/0, ::/0\nEndpoint = 192.0.2.1:51820\nPersistentKeepalive = 25\n";
    static void Test(string name,Action test) { count++; try { test(); Console.WriteLine("PASS " + name); } catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
    static void Yes(bool value,string message=null) { if (!value) throw new Exception(message ?? "Assertion failed"); }
    static void Reject(Action action) { try { action(); } catch { return; } throw new Exception("Should reject this input"); }
    static Game Fake(string id,string exe) { return new Game { id=id,name="Validation " + id,executables=new List<GameExecutable> { new GameExecutable { name=exe, os="win32" } } }; }
    static IEnumerable<DependencyObject> Visuals(DependencyObject parent) { for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) { var child=VisualTreeHelper.GetChild(parent,i); yield return child; foreach(var nested in Visuals(child)) yield return nested; } }
    [STAThread] static int Main(string[] args) {
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
        root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"state-" + Guid.NewGuid().ToString("N")));
        string originalDataRoot = Files.Root; Files.Root = root; engine = args[0]; Directory.CreateDirectory(root);
        try {
            Test("Callrift identity preserves existing profile storage", () => { Yes(originalDataRoot==Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DiscordVPN")); Yes(typeof(Program).Assembly.GetName().Name=="Callrift"); });
            Test("Release repository is embedded without account credentials",()=>Yes(Updates.ValidRepository(ReleaseConfiguration.Load().Repository)));
            Test("Release repository rejects hosts, query strings and traversal",()=>{
                foreach(string name in new[]{"https://github.com/example/callrift","example/../x","example/callrift?x=1","example/callrift#x","example/.."}) Yes(!Updates.ValidRepository(name));
            });
            Test("Stable release comparison is numeric and rejects prerelease tags",()=>{
                Yes(Updates.StableVersion("v1.10.0")>Updates.StableVersion("1.9.9"));
                foreach(string tag in new[]{"v1.2.0-beta","latest","1.2","1.2.0+build","v1.2.3/evil"}) Yes(Updates.StableVersion(tag)==null);
                Yes(Updates.Parse(ReleaseJson("v1.3.0"),"example/callrift",new Version(1,3,0,0),false).State==UpdateState.Current);
                Yes(Updates.Parse(ReleaseJson("v1.2.0"),"example/callrift",new Version(1,3,0,0),true).State==UpdateState.Current);
            });
            Test("Update selects installer or portable asset from the same repository",()=>{
                var a=Updates.Parse(ReleaseJson(),"example/callrift",new Version(1,3,0),true);
                var b=Updates.Parse(ReleaseJson(),"example/callrift",new Version(1,3,0),false);
                Yes(a.State==UpdateState.Available && a.Update.Download.AbsoluteUri.EndsWith("Callrift-Setup-9.8.7.exe"));
                Yes(b.State==UpdateState.Available && b.Update.Download.AbsoluteUri.EndsWith("Callrift-9.8.7-Windows-x64.zip"));
                Yes(a.Update.ReleasePage.AbsoluteUri=="https://github.com/example/callrift/releases/tag/v9.8.7");
            });
            Test("Draft, prerelease and incomplete uploads never produce an update",()=>{
                foreach(string json in new[]{ReleaseJson(draft:true),ReleaseJson(prerelease:true),ReleaseJson("v9.8.7-beta"),ReleaseJson().Replace("uploaded","new")}) Yes(Updates.Parse(json,"example/callrift",new Version(1,0,0),false).State==UpdateState.Unpublished);
            });
            Test("Release cannot redirect download to another repository or an executable scheme",()=>{
                foreach(string host in new[]{"https://evil.example/","file:///C:/","https://github.com/another/"}) {
                    string json=ReleaseJson().Replace("https://github.com/example/",host);
                    Yes(Updates.Parse(json,"example/callrift",new Version(1,0,0),true).State==UpdateState.Unpublished);
                }
            });
            Test("Update HTTP uses public latest release endpoint and handles missing releases and rate limits",()=>{
                using(var handler=new ReleaseReply { Body=ReleaseJson() }) using(var client=new System.Net.Http.HttpClient(handler)) {
                    var result=Updates.Check(client,"example/callrift",new Version(1,3,0),false,CancellationToken.None).GetAwaiter().GetResult();
                    Yes(result.State==UpdateState.Available && handler.Requested=="https://api.github.com/repos/example/callrift/releases/latest");
                    handler.Status=System.Net.HttpStatusCode.NotFound; Yes(Updates.Check(client,"example/callrift",new Version(1,3,0),false,CancellationToken.None).GetAwaiter().GetResult().State==UpdateState.Unpublished);
                    handler.Status=System.Net.HttpStatusCode.Forbidden; Yes(Updates.Check(client,"example/callrift",new Version(1,3,0),false,CancellationToken.None).GetAwaiter().GetResult().State==UpdateState.Unavailable);
                }
            });
            Test("Update parsing fails safely for malformed or oversized API responses",()=>{
                foreach(string body in new[]{"not json",new string('x',1024*1024+1)}) using(var handler=new ReleaseReply { Body=body }) using(var client=new System.Net.Http.HttpClient(handler))
                    Reject(()=>Updates.Check(client,"example/callrift",new Version(1,3,0),false,CancellationToken.None).GetAwaiter().GetResult());
            });
            Test("Update query cancellation leaves no request or worker",()=>{
                using(var cancel=new CancellationTokenSource()) using(var handler=new ReleaseReply { Body=ReleaseJson() }) using(var client=new System.Net.Http.HttpClient(handler)) {
                    cancel.Cancel(); Reject(()=>Updates.Check(client,"example/callrift",new Version(1,3,0),false,cancel.Token).GetAwaiter().GetResult()); Yes(handler.Requested==null);
                }
            });
            Test("Old settings enable update notices by default while an explicit opt-out persists",()=>{
                Yes(Json.Read<Settings>("{\"Language\":\"pt-BR\"}").CheckUpdatesOnStart);
                Yes(!Json.Read<Settings>("{\"CheckUpdatesOnStart\":false}").CheckUpdatesOnStart);
            });
            Test("Uninstaller startup cleanup matches the full executable path and rejects external XML entities",()=>{
                string executable=Path.Combine(root,"Callrift.exe");
                string xml="<Task xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\"><Actions><Exec><Command>"+System.Security.SecurityElement.Escape(executable)+"</Command></Exec></Actions></Task>";
                Yes(Startup.OwnsTask(xml,executable)); Yes(!Startup.OwnsTask(xml,Path.Combine(root,"other","Callrift.exe")));
                Reject(()=>Startup.OwnsTask("<!DOCTYPE Task [<!ENTITY e SYSTEM 'file:///C:/Windows/win.ini'>]><Task><Actions><Exec><Command>&e;</Command></Exec></Actions></Task>",executable));
            });
            Test("Proton WireGuard profile parses with comments and DNS", () => { var p=WireGuard.Parse(Conf); Yes(p.Port==51820 && p.Address[0]=="10.2.0.2/32" && p.AllowedIPs.Length==2); });
            Test("Private key retains trailing equals sign", () => Yes(WireGuard.Parse(Conf).PrivateKey==PrivateKey));
            Test("CRLF and whitespace", () => Yes(WireGuard.Parse(Conf.Replace("\n","\r\n")).Host=="192.0.2.1"));
            Test("IPv6 endpoint", () => Yes(WireGuard.Parse(Conf.Replace("192.0.2.1:51820","[2001:db8::1]:51820")).Host=="2001:db8::1"));
            Test("DNS endpoint", () => Yes(WireGuard.Parse(Conf.Replace("192.0.2.1","example.com")).Host=="example.com"));
            Test("Missing private key rejected", () => Reject(() => WireGuard.Parse(Conf.Replace("PrivateKey = " + PrivateKey,""))));
            Test("Malformed key rejected", () => Reject(() => WireGuard.Parse(Conf.Replace(PrivateKey,"secret"))));
            Test("Short decoded key rejected", () => Reject(() => WireGuard.Parse(Conf.Replace(PrivateKey,"YWJj"))));
            Test("Multiple peers rejected", () => Reject(() => WireGuard.Parse(Conf + "[Peer]\nPublicKey = " + PublicKey)));
            Test("Duplicate interface rejected", () => Reject(() => WireGuard.Parse(Conf + "[Interface]\n")));
            Test("Shell hooks rejected", () => Reject(() => WireGuard.Parse(Conf.Replace("DNS = 10.2.0.1","PostUp = arbitrary-command"))));
            Test("Duplicate keys rejected", () => Reject(() => WireGuard.Parse(Conf.Replace("DNS = 10.2.0.1","DNS = 10.2.0.1\nPrivateKey = " + PrivateKey))));
            Test("Invalid endpoint port rejected", () => Reject(() => WireGuard.Parse(Conf.Replace(":51820",":70000"))));
            Test("Invalid CIDR rejected", () => Reject(() => WireGuard.Parse(Conf.Replace("10.2.0.2/32","10.2.0.2/128"))));
            Test("IPv6 invalid prefix rejected", () => Reject(() => WireGuard.Parse(Conf.Replace("::/0","::/129"))));
            Test("Pre-shared key is supported", () => Yes(WireGuard.Parse(Conf + "PresharedKey = " + PublicKey).PreSharedKey==PublicKey));
            Test("Invalid MTU rejected", () => Reject(() => WireGuard.Parse(Conf.Replace("DNS = 10.2.0.1","MTU = 400"))));
            Test("Invalid keepalive rejected", () => Reject(() => WireGuard.Parse(Conf.Replace("PersistentKeepalive = 25","PersistentKeepalive = -1"))));
            Test("Process routes never match games or use VPN as final", () => {
                string json=Json.Write(WireGuard.Parse(Conf).Config(47833,"aabbcc"));
                Yes(json.Contains("\"final\":\"direct\"") && json.Contains("Discord.exe") && json.Contains("DiscordPTB.exe") && json.Contains("DiscordCanary.exe") && json.Contains("DiscordDevelopment.exe"));
                Yes(!json.Contains("domain_suffix") && !json.Contains("hijack-dns") && json.Contains("\"dns_mode\":\"disabled\""));
                Yes(json.Contains("\"listen\":\"127.0.0.1\"") && json.Contains("\"password\":\"aabbcc\""));
            });
            Test("Generated config validates with actual sing-box 1.14.4", () => {
                string path=Path.Combine(root,"validation.json"); File.WriteAllText(path,Json.Write(WireGuard.Parse(Conf).Config(47833,"aabbcc")),new UTF8Encoding(false));
                using (var p=Process.Start(new ProcessStartInfo(engine,"check --disable-color -c \""+path+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})) {
                    string error=p.StandardError.ReadToEnd(); p.WaitForExit(); if(p.ExitCode!=0) throw new Exception(error);
                }
            });
            Test("Benchmark config cannot create a TUN or route Discord",()=>{
                string json=Json.Write(WireGuard.Parse(Conf).Config(47834,"test-secret",true));
                Yes(!json.Contains("\"tun\"") && !json.Contains("auto_route") && !json.Contains("process_name") && !json.Contains("Discord.exe"));
                Yes(json.Contains("\"listen\":\"127.0.0.1\"") && json.Contains("test-secret") && json.Contains("\"system\":false"));
                string path=Path.Combine(root,"benchmark.json"); File.WriteAllText(path,json,new UTF8Encoding(false));
                using(var p=Process.Start(new ProcessStartInfo(engine,"check --disable-color -c \""+path+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true})) { string error=p.StandardError.ReadToEnd(); p.WaitForExit(); if(p.ExitCode!=0) throw new Exception(error); }
            });
            Test("Profile comparison prefers stable lower HTTPS times in the current batch",()=>{
                var fast=ProfileMeasurement.FromSamples("fast","new",new double[]{80,40,60});
                var slow=ProfileMeasurement.FromSamples("slow","new",new double[]{100,110,120});
                var unreliable=ProfileMeasurement.FromSamples("partial","new",new double[]{5});
                var old=ProfileMeasurement.FromSamples("old","old",new double[]{1,1,1});
                Yes(fast.MedianMs==60 && fast.JitterMs==40 && ProfileMeasurement.Best(new[]{slow,unreliable,old,fast},"new")==fast);
                Yes(ProfileMeasurement.Best(new[]{ProfileMeasurement.FromSamples("failed","new",new double[0])},"new")==null);
            });
            Test("Invalid benchmark samples do not produce a recommendation",()=>{
                Reject(()=>ProfileMeasurement.FromSamples("x","b",new[]{Double.NaN})); Reject(()=>ProfileMeasurement.FromSamples("x","b",new double[]{-1}));
            });
            Test("Profile test history contains no WireGuard private keys",()=>{
                var p=new WireGuardProfile{Id="history",Name="test",Configuration=Conf,Measurement=ProfileMeasurement.FromSamples("history","b",new double[]{123,125,124})};
                ProfileBenchmark.Save(new[]{p}); var saved=ProfileBenchmark.Load(); Yes(saved["history"].MedianMs==124 && !File.ReadAllText(Files.At("profile-tests.json")).Contains(PrivateKey));
            });
            Test("Cancelled profile benchmark leaves no process or runtime directory",()=>{
                using(var cts=new System.Threading.CancellationTokenSource()) using(var runner=new ProfileBenchmark()) {
                    cts.Cancel(); bool cancelled=false;
                    try { runner.Measure(new WireGuardProfile{Id="cancel",Configuration=Conf},"test",cts.Token).GetAwaiter().GetResult(); } catch(OperationCanceledException) { cancelled=true; }
                    Yes(cancelled && !Directory.Exists(Files.At("runtime")));
                }
            });
            Test("Benchmark cancels a live isolated engine and removes plaintext configuration",()=>{
                using(var cts=new System.Threading.CancellationTokenSource()) using(var runner=new ProfileBenchmark()) {
                    cts.CancelAfter(900); bool cancelled=false;
                    try { runner.Measure(new WireGuardProfile{Id="cancel-live",Configuration=Conf},"test",cts.Token).GetAwaiter().GetResult(); } catch(OperationCanceledException) { cancelled=true; }
                    Yes(cancelled && Directory.GetDirectories(Files.At("runtime")).Length==0);
                    foreach(var process in Process.GetProcessesByName("sing-box")) using(process) { try { Yes(!process.MainModule.FileName.StartsWith(AppDomain.CurrentDomain.BaseDirectory,StringComparison.OrdinalIgnoreCase)); } catch(System.ComponentModel.Win32Exception) { } }
                }
            });
            Test("Restart accepts only known Discord channels",()=>{
                Yes(DiscordSession.Protocol("Discord")=="discord://-/" && DiscordSession.Protocol("DiscordCanary").Contains("canary"));
                Reject(()=>DiscordSession.Protocol("notepad"));
                Reject(()=>DiscordSession.Stop(new List<DiscordProcess>{new DiscordProcess{Id=Process.GetCurrentProcess().Id,Started=0,Channel="notepad"}}).GetAwaiter().GetResult());
            });
            Test("Authorized restart stops only an approved fixture with a matching process identity",()=>{
                var present=DiscordSession.Capture(); string channel=DiscordSession.Channels.Reverse().First(c=>!present.Any(p=>p.Channel==c));
                string executable=Path.Combine(root,channel+".exe"); File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ActivityHost.exe"),executable);
                using(var p=Process.Start(new ProcessStartInfo(executable,Process.GetCurrentProcess().Id.ToString()){UseShellExecute=false})) {
                    try {
                        long timestamp=p.StartTime.ToUniversalTime().Ticks;
                        Reject(()=>DiscordSession.Stop(new List<DiscordProcess>{new DiscordProcess{Id=p.Id,Started=timestamp+1,Channel=channel}}).GetAwaiter().GetResult());
                        Yes(!p.HasExited);
                        DiscordSession.Stop(new List<DiscordProcess>{new DiscordProcess{Id=p.Id,Started=timestamp,Channel=channel}}).GetAwaiter().GetResult(); Yes(p.HasExited);
                    } finally { if(!p.HasExited) { p.Kill(); p.WaitForExit(1000); } }
                }
            });
            Test("DPAPI protects imported private key on disk", () => {
                string path=Path.Combine(root,"test.conf"); File.WriteAllText(path,Conf);
                var store=new ProfileStore(); var p=store.Import(path);
                byte[] bytes=File.ReadAllBytes(Path.Combine(store.DirectoryPath,p.Id+".profile"));
                Yes(!Encoding.UTF8.GetString(bytes).Contains(PrivateKey)); Yes(store.Load(null).Single().Configuration==Conf);
            });
            Test("Corrupted profile is skipped and reported", () => {
                var store=new ProfileStore(); File.WriteAllText(Path.Combine(store.DirectoryPath,"bad.profile"),"bad"); bool warning=false; Yes(store.Load(x=>warning=true).Count==1 && warning);
            });
            Test("Settings persist exactly", () => { var s=new Settings { SelectedProfile="sample",AutoConnect=true,OpenDiscord=true,Language="es-ES" }; s.Save(); var back=Settings.Load(); Yes(back.SelectedProfile=="sample" && back.Language=="es-ES" && back.OpenDiscord); });
            Test("Executable selection prefers shallow then short Windows path", () => { var g=Fake("1","long/path/app.exe"); g.executables.Add(new GameExecutable{name="short.exe",os="win32"}); g.executables.Add(new GameExecutable{name="x",os="linux"}); g.executables.Add(new GameExecutable{name=">abc",os="win32"}); Yes(g.WindowsExecutables.Count==2 && g.WindowsExecutables[0].name=="short.exe"); });
            Test("Safe game subdirectories preserved", () => Yes(ActivityService.SafePath(root,"12","bin/game.exe").EndsWith("12\\bin\\game.exe")));
            foreach(string bad in new [] { "../escape.exe", "..\\escape.exe", "C:\\evil.exe", "\\\\host\\evil.exe", "game.exe:stream", "game.bat", "bin/../../bad.exe", "./bad.exe", "a./bad.exe" }) {
                string path=bad; Test("Unsafe executable rejected: " + bad, () => Reject(() => ActivityService.SafePath(root,"12",path)));
            }
            Test("Unsafe game ID rejected", () => Reject(() => ActivityService.SafePath(root,"../evil","test.exe")));
            Test("Search supports alias, ID and comma separated OR terms", () => {
                using(var service=new ActivityService()) { var a=Fake("101","a.exe"); a.name="Alpha"; a.aliases=new []{"other"}; service.Games.Add(a); var b=Fake("202","b.exe"); b.name="Beta"; service.Games.Add(b); Yes(service.Filter("ALPHA,202").Count==2 && service.Filter("other").Count==1 && service.Filter("missing").Count==0); }
            });
            Test("Favorites survive service restart", () => { using(var s=new ActivityService()) s.Favorite(Fake("123","x.exe")); Yes(File.ReadAllText(Files.At("favorites.json")).Contains("123")); });
            Test("UI state does not bloat catalogue cache", () => { string j=Json.Write(Fake("1","x.exe")); Yes(!j.Contains("Details") && !j.Contains("WindowsExecutables") && !j.Contains("Icon") && !j.Contains("Running")); });
            Test("Expired catalogue is usable after actual network failure", () => {
                Files.AtomicText(Files.At("games-cache.json"),Json.Write(new GameCache{Timestamp=DateTime.UtcNow.AddDays(-3),Games=new List<Game>{Fake("123","cached.exe")}}));
                using(var offline=new System.Net.Http.HttpClient(new NoNetwork())) using(var service=new ActivityService(offline)) { service.Load(false).GetAwaiter().GetResult(); Yes(service.Offline && service.FromCache && service.Games.Single().id=="123" && service.Games.Single().Favorite); }
            });
            Test("Network failure without a cache is explicit", () => {
                File.Delete(Files.At("games-cache.json")); using(var offline=new System.Net.Http.HttpClient(new NoNetwork())) using(var service=new ActivityService(offline)) Reject(()=>service.Load(true).GetAwaiter().GetResult());
            });
            Test("Multiple activity processes start, reap and clean up", () => {
                using(var s=new ActivityService()) {
                    var a=Fake("4001","validation-a.exe"); var b=Fake("4002","bin/validation-b.exe");
                    s.Start(a,null); s.Start(b,null); Thread.Sleep(1200); Yes(s.Running.Count==2 && s.Running.Values.All(x=>!x.Process.HasExited));
                    var pid=s.Running[a.id].Process.Id; s.Running[a.id].Process.Kill(); s.Running[a.id].Process.WaitForExit(); Yes(s.Reap() && !a.Running && s.Running.Count==1);
                    s.StopAll(); Yes(!b.Running && s.Running.Count==0 && !Directory.Exists(Path.Combine(root,"activities","4002")));
                }
            });
            Test("Job object kills activity when supervisor job closes", () => {
                Process p=null; try { using(var j=new ChildJob()) { p=Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ActivityHost.exe"),Process.GetCurrentProcess().Id.ToString()){UseShellExecute=false}); j.Add(p); } Yes(p.WaitForExit(3000)); } finally { if(p!=null) p.Dispose(); }
            });
            Test("Three languages expose run and connection controls", () => { foreach(string language in new []{"pt-BR","en-US","es-ES"}) { I18n.Language=language; Yes(I18n.T("Run")!="Run" || language=="en-US"); Yes(I18n.T("Connect")!="Connect"); } I18n.Language="pt-BR"; });
            Test("Native XAML loads and all four pages render without a browser", () => {
                var app=new Application();
                using(var ui=new MainUI(false,true)) {
                    Yes(ui.Window.Title=="Callrift" && ((BitmapSource)((Image)ui.Window.FindName("BrandMark")).Source).PixelWidth==128);
                    ui.Navigate("activity");
                    var field=typeof(MainUI).GetField("activity",BindingFlags.Instance|BindingFlags.NonPublic);
                    var service=(ActivityService)field.GetValue(ui); var game=Fake("5001","ui-validation.exe"); service.Games.Add(game);
                    typeof(MainUI).GetMethod("RefreshGames",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                    var body=(FrameworkElement)ui.Window.Content; body.Measure(new Size(1060,790)); body.Arrange(new Rect(0,0,1060,790)); body.UpdateLayout();
                    var run=Visuals(ui.Window.Content as DependencyObject).OfType<Button>().First(x=>(string)x.Tag=="run");
                    run.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,run)); Yes(service.Running.Count==1 && game.Running);
                    ((Button)ui.Window.FindName("StopAll")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Yes(service.Running.Count==0 && !game.Running);
                    var check=(CheckBox)ui.Window.FindName("LightTheme"); check.IsChecked=true; check.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent)); Yes(((SolidColorBrush)ui.Window.Resources["Bg"]).Color==((Color)ColorConverter.ConvertFromString("#F6F5FA")));
                    check.IsChecked=false; check.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                    var profiles=new List<WireGuardProfile>{new WireGuardProfile{Id=Guid.NewGuid().ToString("N"),Name="PERFIL DE TESTE • 01",Configuration=Conf,Imported=DateTime.UtcNow},new WireGuardProfile{Id=Guid.NewGuid().ToString("N"),Name="PERFIL DE TESTE • 02",Configuration=Conf,Imported=DateTime.UtcNow}};
                    typeof(MainUI).GetField("profiles",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ui,profiles);
                    typeof(MainUI).GetMethod("LoadProfiles",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                    ui.Navigate("vpn");
                    var list=(ListBox)ui.Window.FindName("Profiles"); list.IsEnabled=false;
                    body.Measure(new Size(1060,790)); body.Arrange(new Rect(0,0,1060,790)); body.UpdateLayout();
                    Yes(!Visuals(list).OfType<Border>().Any(b=>b.Background is SolidColorBrush && ((SolidColorBrush)b.Background).Color==Colors.White));
                    list.IsEnabled=true;
                    ui.Window.ShowActivated=false; ui.Window.Show(); ui.Window.UpdateLayout(); Pump();
                    Yes(System.Windows.Shell.WindowChrome.GetWindowChrome(ui.Window)!=null);
                    NativeCapture(ui.Window,"native-window.png");
                    var notice=Updates.Parse(ReleaseJson(),"example/callrift",new Version(1,3,0),false).Update;
                    typeof(MainUI).GetMethod("PresentUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{notice,false});
                    Yes(((System.Windows.Controls.Border)ui.Window.FindName("UpdateBanner")).Visibility==Visibility.Visible);
                    Yes(((TextBlock)ui.Window.FindName("UpdateHeadline")).Text.Contains("9.8.7"));
                    ui.Window.Width=920; ui.Window.Height=700; ui.Window.UpdateLayout(); Pump();
                    var banner=(FrameworkElement)ui.Window.FindName("UpdateBanner"); Yes(banner.ActualWidth>500 && banner.ActualHeight>30 && banner.ActualHeight<150);
                    ui.Window.Width=1060; ui.Window.Height=790; ui.Window.UpdateLayout(); Pump();
                    var updateBody=(FrameworkElement)ui.Window.Content;
                    var updateBitmap=new RenderTargetBitmap(1060,790,96,96,PixelFormats.Pbgra32); updateBitmap.Render(updateBody);
                    var updateEncoder=new PngBitmapEncoder(); updateEncoder.Frames.Add(BitmapFrame.Create(updateBitmap)); using(var stream=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-update.png"))) updateEncoder.Save(stream);
                    Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,(Action)(()=>{
                        var dialog=Application.Current.Windows.Cast<Window>().First(w=>w!=ui.Window); var cancel=Visuals(dialog.Content as DependencyObject).OfType<Button>().First(b=>b.IsCancel); cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,cancel));
                    }));
                    typeof(MainUI).GetMethod("PromptUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                    ((System.Windows.Controls.Border)ui.Window.FindName("UpdateBanner")).Visibility=Visibility.Collapsed;

                    Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,(Action)(()=>{
                        var dialog=Application.Current.Windows.Cast<Window>().First(w=>w!=ui.Window); var cancel=Visuals(dialog.Content as DependencyObject).OfType<Button>().First(b=>b.IsCancel); cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,cancel));
                    }));
                    Yes(!AppDialog.Confirm(ui.Window,"Teste de reinício","Teste visual: nenhum Discord será encerrado.","Autorizar"));
                    ui.Navigate("activity"); service.Games.Add(Fake("5002","bin/long-name-validation-host.exe"));
                    typeof(MainUI).GetMethod("RefreshGames",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                    ui.Window.UpdateLayout(); Pump();
                    var combo=Visuals(ui.Window.Content as DependencyObject).OfType<ComboBox>().First(c=>c.Name=="ExeChoice");
                    Yes(((SolidColorBrush)combo.Background).Color!=Colors.White && ((SolidColorBrush)combo.Foreground).Color==((SolidColorBrush)ui.Window.Resources["Fg"]).Color);
                    combo.IsDropDownOpen=true; Pump();
                    var popup=(Popup)combo.Template.FindName("PART_Popup",combo); Yes(popup.IsOpen && popup.Child!=null);
                    var popupBitmap=new RenderTargetBitmap((int)Math.Ceiling(popup.Child.RenderSize.Width),(int)Math.Ceiling(popup.Child.RenderSize.Height),96,96,PixelFormats.Pbgra32); popupBitmap.Render(popup.Child);
                    var popupEncoder=new PngBitmapEncoder(); popupEncoder.Frames.Add(BitmapFrame.Create(popupBitmap)); using(var stream=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"dropdown.png"))) popupEncoder.Save(stream);
                    combo.IsDropDownOpen=false; ui.Window.Hide();
                    check.IsChecked=true; check.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                    Yes(((SolidColorBrush)combo.Foreground).Color==((SolidColorBrush)ui.Window.Resources["Fg"]).Color,"Combo foreground did not track light theme");
                    check.IsChecked=false; check.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                    ui.Window.Width=920; ui.Window.Height=700; ui.Window.UpdateLayout(); Pump();
                    foreach(string language in new[]{"en-US","es-ES","pt-BR"}) {
                        var lang=(ComboBox)ui.Window.FindName("Language"); lang.SelectedIndex=language=="en-US"?1:language=="es-ES"?2:0; Pump();
                        foreach(string screen in new[]{"vpn","activity","settings"}) { ui.Navigate(screen); body.Measure(new Size(920,700)); body.Arrange(new Rect(0,0,920,700)); body.UpdateLayout(); Yes(body.ActualWidth==920,"Minimum layout: "+language+"/"+screen+" = "+body.ActualWidth); }
                    }
                    ui.Window.Width=1060; ui.Window.Height=790; ui.Window.UpdateLayout(); Pump();
                    foreach(string screen in new []{"vpn","activity","logs","settings"}) {
                        ui.Navigate(screen); var content=(FrameworkElement)ui.Window.Content; content.Measure(new Size(1060,790)); content.Arrange(new Rect(0,0,1060,790)); content.UpdateLayout();
                        var bitmap=new RenderTargetBitmap(1060,790,96,96,PixelFormats.Pbgra32); bitmap.Render(content);
                        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using(var stream=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-"+screen+".png"))) encoder.Save(stream);
                        Yes(content.ActualWidth==1060);
                    }
                    ui.Window.Close();
                }
                app.Shutdown();
            });
            if(args.Contains("--network")) Test("Live Discord catalogue loads, caches, and works offline", () => {
                using(var service=new ActivityService()) { service.Load(true).GetAwaiter().GetResult(); Yes(service.Games.Count>1000 && !service.FromCache); Console.WriteLine("CATALOGUE "+service.Games.Count+" entries, "+service.Games.Count(g=>g.CanRun)+" Windows-compatible"); }
                using(var service=new ActivityService()) { service.Load(false).GetAwaiter().GetResult(); Yes(service.FromCache && service.Games.Count>1000); }
            });
        } finally {
            // This path is a unique test fixture under tests/output, never user data.
            if(root.StartsWith(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),StringComparison.OrdinalIgnoreCase)) try { Directory.Delete(root,true); } catch { }
        }
        Console.WriteLine("RESULT "+(count-failures)+"/"+count+" passed"); return failures==0 ? 0 : 1;
    }
}

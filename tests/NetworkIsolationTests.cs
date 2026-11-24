// Explicit, elevated integration test. It targets only SelectedProbe.exe in this test
// directory. It never redirects/restarts the user's Discord, OBS, or games.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Callrift;

static class NetworkIsolationTests {
    static string folder=AppDomain.CurrentDomain.BaseDirectory;
    static bool manualBind;
    static IPAddress targetAddress=IPAddress.Parse("1.1.1.1");
    static string tunnelAddress=AppNetworkIsolation.IPv4;
    static readonly byte[] query=Convert.FromBase64String("EjQBAAABAAAAAAAAB2V4YW1wbGUDY29tAAABAAE=");
    sealed class Sample { public int Tcp,Udp,Failures; public bool BoundToTunnel; public string EgressHash,Error; }
    sealed class TraceConnection : IDisposable {
        TcpClient client; SslStream stream;
        public bool Bound { get {
            // WFP source redirection is intentionally transparent to getsockname.
            // Inspect the actual TCP table using this socket's unique local port.
            var endpoint=(IPEndPoint)client.Client.LocalEndPoint;
            return endpoint!=null && System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections().Any(c=>c.LocalEndPoint.Port==endpoint.Port && c.LocalEndPoint.Address.ToString()==tunnelAddress && c.RemoteEndPoint.Address.Equals(targetAddress) && c.RemoteEndPoint.Port==443);
        } }
        public TraceConnection() {
            client=new TcpClient(targetAddress.AddressFamily); client.ReceiveTimeout=8000; client.SendTimeout=8000;
            if(manualBind) client.Client.Bind(new IPEndPoint(IPAddress.Parse(tunnelAddress),0));
            var connect=client.BeginConnect(targetAddress,443,null,null);
            using(connect.AsyncWaitHandle) { if(!connect.AsyncWaitHandle.WaitOne(8000)) { bool bound=client.Client.LocalEndPoint!=null && Bound; client.Close(); throw new TimeoutException("TCP connect; bound to private TUN: "+bound); } client.EndConnect(connect); }
            stream=new SslStream(client.GetStream(),false); stream.ReadTimeout=8000; stream.WriteTimeout=8000;
            stream.AuthenticateAsClient("one.one.one.one",null,System.Security.Authentication.SslProtocols.Tls12,false);
        }
        string Line() {
            var text=new StringBuilder(); int ch;
            while((ch=stream.ReadByte())!=-1) { if(ch==10) return text.ToString().TrimEnd('\r'); if(text.Length>16384) throw new IOException("HTTP header too large"); text.Append((char)ch); }
            throw new IOException("Persistent connection closed");
        }
        byte[] Read(int count) { if(count<0 || count>65536) throw new IOException("Unexpected response size"); var bytes=new byte[count]; int offset=0; while(offset<count) { int n=stream.Read(bytes,offset,count-offset); if(n==0) throw new IOException("HTTP EOF"); offset+=n; } return bytes; }
        public string Tick() {
            var request=Encoding.ASCII.GetBytes("GET /cdn-cgi/trace HTTP/1.1\r\nHost: one.one.one.one\r\nConnection: keep-alive\r\nAccept-Encoding: identity\r\nUser-Agent: Callrift-Isolation-Test\r\n\r\n");
            stream.Write(request,0,request.Length);
            string status=Line(); if(!status.Contains(" 200 ")) throw new IOException("Unexpected HTTPS status");
            int length=-1; bool chunked=false; string header;
            while((header=Line()).Length>0) { if(header.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)) length=Int32.Parse(header.Substring(15).Trim()); if(header.StartsWith("Transfer-Encoding:",StringComparison.OrdinalIgnoreCase) && header.ToLowerInvariant().Contains("chunked")) chunked=true; }
            string body;
            if(chunked) {
                var data=new MemoryStream();
                while(true) { int n=Convert.ToInt32(Line().Split(';')[0],16); if(n==0) { while(Line().Length>0) { } break; } var block=Read(n); data.Write(block,0,block.Length); if(data.Length>65536) throw new IOException("Response too large"); Line(); }
                body=Encoding.UTF8.GetString(data.ToArray());
            } else body=Encoding.UTF8.GetString(Read(length));
            string ip=body.Split('\n').FirstOrDefault(l=>l.StartsWith("ip="));
            if(ip==null) throw new IOException("Missing egress address");
            using(var sha=SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(ip.Trim())));
        }
        public void Dispose() { if(stream!=null) stream.Dispose(); if(client!=null) client.Close(); }
    }
    static void UdpTick(UdpClient udp) {
        udp.Send(query,query.Length,new IPEndPoint(targetAddress,53));
        IPEndPoint sender=null; var answer=udp.Receive(ref sender);
        if(answer.Length<12 || answer[0]!=query[0] || answer[1]!=query[1] || (answer[2]&128)==0) throw new IOException("Invalid UDP DNS response");
    }
    static int Probe(string file,bool continuous) {
        var result=new Sample();
        try {
            using(var tcp=new TraceConnection()) using(var udp=new UdpClient(targetAddress.AddressFamily)) {
                udp.Client.ReceiveTimeout=8000; udp.Client.SendTimeout=8000;
                do {
                    result.EgressHash=tcp.Tick(); result.Tcp++; result.BoundToTunnel=tcp.Bound;
                    UdpTick(udp); result.Udp++;
                    Files.AtomicText(file,Json.Write(result));
                    if(!continuous) break;
                    Thread.Sleep(1000);
                } while(!File.Exists(file+".stop"));
            }
        } catch(Exception e) { result.Failures++; result.Error=e.GetType().Name+": "+e.Message; }
        Files.AtomicText(file,Json.Write(result)); return result.Failures==0?0:1;
    }
    static Process Child(string name,string arguments) {
        return Process.Start(new ProcessStartInfo(Path.Combine(folder,name),arguments) { UseShellExecute=false,CreateNoWindow=true });
    }
    static Sample WaitSample(string path,int minimum,int seconds=30) {
        for(int i=0;i<seconds*10;i++) {
            try { var sample=Json.Read<Sample>(File.ReadAllText(path)); if(sample.Failures>0) throw new Exception("Probe failed: "+sample.Error); if(sample.Tcp>=minimum && sample.Udp>=minimum) return sample; }
            catch(IOException) { } Thread.Sleep(100);
        }
        throw new TimeoutException("Probe did not finish");
    }
    static void Check(bool condition,string description) { if(!condition) throw new Exception(description); Console.WriteLine("PASS "+description); }
    static int Controller(bool proton,int duration,bool testIPv6) {
        if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) throw new Exception("Run this integration test as administrator only after approval.");
        string directFile=Path.Combine(folder,"direct-sample.json"), selectedFile=Path.Combine(folder,"selected-sample.json"), freshFile=Path.Combine(folder,"fresh-sample.json"), configPath=Path.Combine(folder,"integration-tunnel.json");
        string direct6File=Path.Combine(folder,"direct6-sample.json"), selected6File=Path.Combine(folder,"selected6-sample.json"), guardFile=Path.Combine(folder,"guard-sample.json");
        var sampleFiles=new[]{directFile,selectedFile,freshFile,direct6File,selected6File,guardFile};
        foreach(string path in sampleFiles.SelectMany(p=>new[]{p,p+".stop"})) if(File.Exists(path)) File.Delete(path);
        var extraChildren=new List<Process>();
        Process direct=null, selected=null, fresh=null, engine=null; ChildJob job=null; IsolationDriver driver=null; AppNetworkIsolation isolation=null; SecondaryTunnelRoutes routes=null;
        var tunnelLog=new StringBuilder();
        var report=new Dictionary<string,object>();
        try {
            SecondaryTunnelRoutes.CheckAvailable();
            direct=Child("DirectProbe.exe","--continuity \""+directFile+"\"");
            var baseline=WaitSample(directFile,3);
            Check(!baseline.BoundToTunnel,"Baseline uses the native connection");
            Sample baseline6=null;
            if(testIPv6) {
                extraChildren.Add(Child("DirectProbe.exe","--continuity6 \""+direct6File+"\""));
                baseline6=WaitSample(direct6File,2);
                Check(!baseline6.BoundToTunnel,"IPv6 baseline uses the native connection");
            }
            int port; var socket=new TcpListener(IPAddress.Loopback,0); socket.Start(); port=((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
            string profile="[Interface]\nPrivateKey="+Convert.ToBase64String(Enumerable.Range(1,32).Select(i=>(byte)i).ToArray())+"\nAddress=10.2.0.2/32\n[Peer]\nPublicKey="+Convert.ToBase64String(Enumerable.Range(33,32).Select(i=>(byte)i).ToArray())+"\nEndpoint=192.0.2.1:51820\nAllowedIPs=0.0.0.0/0,::/0\n";
            if(proton) {
                var profiles=new ProfileStore().Load(null); var settings=Settings.Load();
                var chosen=profiles.FirstOrDefault(p=>p.Id==settings.SelectedProfile);
                if(chosen==null) throw new Exception("No selected Proton profile. Nothing will be imported or printed.");
                profile=chosen.Configuration;
            }
            var config=Json.Read<Dictionary<string,object>>(Json.Write(WireGuard.Parse(profile).Config(port,"integration-only")));
            config["log"]=new { level="debug",timestamp=false };
            if(!proton) config["endpoints"]=new object[0];
            // Test-only exact executable rule. Production worker never accepts this target.
            config["route"]=new { auto_detect_interface=true,default_domain_resolver="local-dns",rules=new object[] {
                new { inbound=new[]{"discord-tun","vpn-probe"},action="route",outbound=proton?"proton":"direct" },new { action="reject" }
            },final="direct" };
            Files.PrivateDirectory(folder);
            File.WriteAllText(configPath,Json.Write(config),new UTF8Encoding(false));
            job=new ChildJob();
            engine=Process.Start(new ProcessStartInfo(Path.Combine(folder,"engine","sing-box.exe"),"run --disable-color -c \""+configPath+"\"") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.Combine(folder,"engine") }); job.Add(engine);
            DataReceivedEventHandler collect=(s,e)=>{ if(e.Data!=null) lock(tunnelLog) { if(tunnelLog.Length<100000) tunnelLog.AppendLine(e.Data); } };
            engine.ErrorDataReceived+=collect; engine.OutputDataReceived+=collect; engine.BeginErrorReadLine(); engine.BeginOutputReadLine();
            bool listening=false;
            for(int i=0;i<100 && !engine.HasExited;i++) { try { using(var c=new TcpClient()) c.Connect(IPAddress.Loopback,port); listening=true; break; } catch(SocketException) { Thread.Sleep(100); } }
            if(!listening) throw new Exception("Integration engine did not start");
            File.Delete(configPath);
            if(proton) foreach(string hostname in new[]{"one.one.one.one","www.cloudflare.com"}) {
                try {
                    var request=(HttpWebRequest)WebRequest.Create("https://"+hostname+"/cdn-cgi/trace");
                    request.Proxy=new WebProxy("http://127.0.0.1:"+port) { Credentials=new NetworkCredential("probe","integration-only") };
                    request.Timeout=8000; request.ReadWriteTimeout=8000; request.KeepAlive=false;
                    using(var response=request.GetResponse()) using(var input=new StreamReader(response.GetResponseStream())) {
                        report["ProxyCheck_"+hostname]=input.ReadToEnd().Contains("ip=");
                    }
                } catch(Exception e) { report["ProxyCheck_"+hostname]=e.GetType().Name; }
            }
            driver=new IsolationDriver(Path.Combine(folder,"engine","ProtonVPN.CalloutDriver.sys"));
            isolation=new AppNetworkIsolation(new[]{Path.Combine(folder,"SelectedProbe.exe")});
            routes=new SecondaryTunnelRoutes();
            Check(isolation.RedirectFilterCount==4,"Exactly four app-specific TCP/UDP IPv4/IPv6 redirect filters");
            File.WriteAllText(Path.Combine(folder,"active.ready"),"");
            for(int i=0;!File.Exists(Path.Combine(folder,"active.ack"));i++) { if(i>=600) throw new TimeoutException("Network snapshot acknowledgement"); Thread.Sleep(100); }
            selected=Child("SelectedProbe.exe","--continuity \""+selectedFile+"\"");
            Thread.Sleep(2000);
            using(var diagnostic=Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+Path.Combine(folder,"Read-NetworkIsolationDiagnostics.ps1")+"\" -ProbeId "+selected.Id+" -OutputDirectory \""+folder.TrimEnd(Path.DirectorySeparatorChar)+"\"") { UseShellExecute=false,CreateNoWindow=true })) diagnostic.WaitForExit(5000);
            var routed=WaitSample(selectedFile,1);
            Check(routed.BoundToTunnel,"Selected executable is bound to the private TUN");
            Check(routed.Tcp>=1 && routed.Udp>=1 && routed.Failures==0,"Selected executable completed HTTPS and UDP");
            if(proton) Check(routed.EgressHash!=baseline.EgressHash,"Selected executable uses a different public egress through Proton");
            fresh=Child("DirectProbe.exe","--probe \""+freshFile+"\"");
            var newlyOpened=WaitSample(freshFile,1);
            Check(!newlyOpened.BoundToTunnel && newlyOpened.EgressHash==baseline.EgressHash && newlyOpened.Failures==0,"New non-selected TCP and UDP sockets also use the native connection");
            using(var guard=Child("DirectProbe.exe","--bound-probe \""+guardFile+"\"")) {
                if(!guard.WaitForExit(12000)) { guard.Kill(); throw new Exception("Guard probe timed out"); }
                var denied=Json.Read<Sample>(File.ReadAllText(guardFile));
                Check(denied.Failures==1 && denied.Tcp==0,"An unselected executable cannot explicitly bind to the private tunnel");
            }
            if(testIPv6) {
                extraChildren.Add(Child("SelectedProbe.exe","--continuity6 \""+selected6File+"\""));
                var routed6=WaitSample(selected6File,1);
                Check(routed6.BoundToTunnel && (!proton || routed6.EgressHash!=baseline6.EgressHash),"Selected IPv6 TCP and UDP use the isolated route");
            }
            var during=WaitSample(directFile,baseline.Tcp+duration,duration+30);
            var selectedDuring=WaitSample(selectedFile,Math.Max(2,duration/2),duration+30);
            report["SelectedTcpSamplesBeforeFailure"]=selectedDuring.Tcp; report["SelectedUdpSamplesBeforeFailure"]=selectedDuring.Udp;
            Check(selectedDuring.Failures==0 && selectedDuring.BoundToTunnel,"Selected TCP and UDP remain connected during the sustained test");
            Check(!during.BoundToTunnel && during.EgressHash==baseline.EgressHash && during.Failures==0,"Existing direct TCP and UDP sockets stay native during VPN use");
            if(testIPv6) {
                var direct6=WaitSample(direct6File,baseline6.Tcp+duration,duration+30);
                Check(!direct6.BoundToTunnel && direct6.EgressHash==baseline6.EgressHash,"Existing direct IPv6 TCP and UDP stay native");
            }
            string log; lock(tunnelLog) log=tunnelLog.ToString();
            Check(log.Contains("inbound/tun") && ((log.Contains("1.1.1.1:53") && log.Contains("1.1.1.1:443")) || (log.Contains("forward tcp connection from "+AppNetworkIsolation.IPv4) && log.Contains("forward udp connection from "+AppNetworkIsolation.IPv4))),"Both selected transports were observed at the TUN");
            Check(!log.Contains("DirectProbe.exe"),"Direct probe was never relayed by the engine");
            engine.Kill(); engine.WaitForExit(5000);
            var failedEngine=WaitSample(directFile,during.Tcp+4);
            Check(failedEngine.Failures==0 && failedEngine.EgressHash==baseline.EgressHash,"Direct sockets survive an abrupt engine failure");
            if(testIPv6) { var survived6=WaitSample(direct6File,baseline6.Tcp+duration+3); Check(survived6.Failures==0 && survived6.EgressHash==baseline6.EgressHash,"Direct IPv6 survives engine failure"); }
            routes.Dispose(); routes=null; isolation.Dispose(); isolation=null; driver.Dispose(); driver=null;
            var after=WaitSample(directFile,failedEngine.Tcp+3);
            Check(after.Failures==0 && after.EgressHash==baseline.EgressHash,"Direct sockets survive isolation removal");
            report["Passed"]=true; report["UsedProtonProfile"]=proton; report["TestedIPv6"]=testIPv6; report["DurationTargetSeconds"]=duration; report["DirectTcpSamples"]=after.Tcp; report["DirectUdpSamples"]=after.Udp; report["DirectReconnects"]=0;
            return 0;
        } catch(Exception e) {
            report["Passed"]=false; report["Error"]=e.GetType().Name+": "+e.Message; Console.WriteLine("FAIL "+e.Message);
            if(isolation!=null && routes!=null && !proton) {
                foreach(string kind in new[]{"bound","udp"}) {
                    string diagnosticFile=Path.Combine(folder,"diagnostic-"+kind+".json");
                    try {
                        using(var diagnostic=Child("SelectedProbe.exe","--"+kind+"-probe \""+diagnosticFile+"\"")) { if(!diagnostic.WaitForExit(25000)) diagnostic.Kill(); }
                        var sample=Json.Read<Sample>(File.ReadAllText(diagnosticFile));
                        report["Diagnostic"+kind]=new {sample.Tcp,sample.Udp,sample.Failures,sample.BoundToTunnel,sample.Error};
                    } catch(Exception diagnosticError) { report["Diagnostic"+kind]=diagnosticError.Message; }
                    finally { if(File.Exists(diagnosticFile)) File.Delete(diagnosticFile); }
                }
            }
            try { using(var diagnostic=Process.Start(new ProcessStartInfo("netsh.exe","wfp show netevents file=\""+Path.Combine(folder,"integration-wfp-events.xml")+"\" appid=\""+Path.Combine(folder,"SelectedProbe.exe")+"\" timewindow=120") { UseShellExecute=false,CreateNoWindow=true })) diagnostic.WaitForExit(5000); } catch { }
            return 1;
        }
        finally {
            foreach(string samplePath in sampleFiles) {
                try { var sample=Json.Read<Sample>(File.ReadAllText(samplePath)); report[Path.GetFileNameWithoutExtension(samplePath)]=new {sample.Tcp,sample.Udp,sample.Failures,sample.BoundToTunnel,sample.Error}; } catch { }
            }
            try { var nic=System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n=>n.Name==AppNetworkIsolation.AdapterName); if(nic!=null) { var stats=nic.GetIPv4Statistics(); report["TunnelBytesSent"]=stats.BytesSent; report["TunnelBytesReceived"]=stats.BytesReceived; } } catch { }
            if(routes!=null) routes.Dispose(); if(isolation!=null) isolation.Dispose();
            if(engine!=null) { try { if(!engine.HasExited) { engine.Kill(); engine.WaitForExit(5000); } } catch { } engine.Dispose(); }
            if(job!=null) job.Dispose(); if(driver!=null) driver.Dispose();
            foreach(string samplePath in sampleFiles) File.WriteAllText(samplePath+".stop","");
            foreach(var p in new[]{direct,selected,fresh}.Concat(extraChildren)) if(p!=null) { try { if(!p.WaitForExit(10000)) p.Kill(); } catch { } p.Dispose(); }
            if(File.Exists(configPath)) File.Delete(configPath);
            foreach(var path in sampleFiles.SelectMany(p=>new[]{p,p+".stop"})) if(File.Exists(path)) File.Delete(path);
            report["FinishedAt"]=DateTimeOffset.Now.ToString("o");
            if(!report.ContainsKey("Passed") || !(bool)report["Passed"]) { lock(tunnelLog) File.WriteAllText(Path.Combine(folder,"integration-engine.log"),tunnelLog.ToString()); }
            report["Scope"]="Synthetic HTTPS/UDP probes only; selected connections are intentionally interrupted by engine termination. No real Discord call, OBS live, or Valorant match was tested.";
            File.WriteAllText(Path.Combine(folder,"integration-result.json"),Json.Write(report));
        }
    }
    static int Main(string[] args) {
        try {
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            if(args.Length==2 && args[0].EndsWith("6")) { targetAddress=IPAddress.Parse("2606:4700:4700::1111"); tunnelAddress=AppNetworkIsolation.IPv6; args[0]=args[0].TrimEnd('6'); }
            if(args.Length==2 && args[0]=="--bound-probe") { manualBind=true; return Probe(args[1],false); }
            if(args.Length==2 && args[0]=="--udp-probe") {
                var sample=new Sample();
                try { using(var udp=new UdpClient(AddressFamily.InterNetwork)) { udp.Client.ReceiveTimeout=8000; UdpTick(udp); sample.Udp=1; } }
                catch(Exception e) { sample.Failures=1; sample.Error=e.GetType().Name+": "+e.Message; }
                File.WriteAllText(args[1],Json.Write(sample)); return sample.Failures==0?0:1;
            }
            if(args.Length==2 && (args[0]=="--probe" || args[0]=="--continuity")) return Probe(args[1],args[0]=="--continuity");
            int duration=8; string durationArg=args.FirstOrDefault(a=>a.StartsWith("--duration="));
            if(durationArg!=null) duration=Int32.Parse(durationArg.Substring(11));
            if(duration<8 || duration>600) throw new ArgumentException("Duration must be 8..600 seconds");
            return Controller(args.Contains("--proton"),duration,args.Contains("--ipv6"));
        }
        catch(Exception e) { Console.WriteLine("FAIL "+e.Message); return 1; }
    }
}

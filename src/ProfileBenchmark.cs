using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Callrift {
    public class ProfileMeasurement {
        public string ProfileId { get; set; }
        public string Batch { get; set; }
        public DateTime TestedAt { get; set; }
        public double? MedianMs { get; set; }
        public double? JitterMs { get; set; }
        public int Successes { get; set; }
        public string Error { get; set; }
        public string ExitIP { get; set; }
        public string Summary { get {
            return TestedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm") + " · " + Successes + "/3 HTTPS" +
                (MedianMs.HasValue ? " · mediana " + Math.Round(MedianMs.Value) + " ms · variação " + Math.Round(JitterMs ?? 0) + " ms" : "") +
                (String.IsNullOrEmpty(Error) ? "" : " · " + Error);
        } }
        public static ProfileMeasurement FromSamples(string id, string batch, IList<double> samples) {
            var ordered = samples.OrderBy(x=>x).ToArray();
            if (ordered.Any(x=>Double.IsNaN(x) || Double.IsInfinity(x) || x < 0) || ordered.Length > 3) throw new ArgumentException("Amostras inválidas.");
            return new ProfileMeasurement { ProfileId=id, Batch=batch, TestedAt=DateTime.UtcNow, Successes=ordered.Length,
                MedianMs=ordered.Length == 0 ? (double?)null : ordered.Length % 2 == 1 ? ordered[ordered.Length/2] : (ordered[ordered.Length/2-1]+ordered[ordered.Length/2])/2,
                JitterMs=ordered.Length == 0 ? (double?)null : ordered.Last()-ordered.First() };
        }
        public static ProfileMeasurement Best(IEnumerable<ProfileMeasurement> measurements, string batch) {
            return measurements.Where(x=>x.Batch == batch && x.MedianMs.HasValue && x.Successes > 0)
                .OrderByDescending(x=>x.Successes).ThenBy(x=>x.MedianMs).ThenBy(x=>x.JitterMs).FirstOrDefault();
        }
    }
    public sealed class ProfileBenchmark : IDisposable {
        ChildJob job;
        public static Dictionary<string,ProfileMeasurement> Load() {
            try { if (File.Exists(Files.At("profile-tests.json"))) return Json.Read<List<ProfileMeasurement>>(File.ReadAllText(Files.At("profile-tests.json"))).GroupBy(x=>x.ProfileId).ToDictionary(x=>x.Key,x=>x.Last()); } catch { }
            return new Dictionary<string,ProfileMeasurement>();
        }
        public static void Save(IEnumerable<WireGuardProfile> profiles) { Files.AtomicText(Files.At("profile-tests.json"),Json.Write(profiles.Where(x=>x.Measurement != null).Select(x=>x.Measurement).ToList())); }
        public async Task<ProfileMeasurement> Measure(WireGuardProfile profile, string batch, CancellationToken cancellation) {
            string directory = null; Process engine = null;
            var samples = new List<double>(); string ip = null, problem = null;
            try {
                cancellation.ThrowIfCancellationRequested();
                var wireguard = WireGuard.Parse(profile.Configuration);
                var listener = new TcpListener(IPAddress.Loopback,0); int port;
                try { listener.Start(); port=((IPEndPoint)listener.LocalEndpoint).Port; } finally { listener.Stop(); }
                string password=Guid.NewGuid().ToString("N");
                directory = Path.Combine(Files.At("runtime"), "test-" + Guid.NewGuid().ToString("N")); Files.PrivateDirectory(directory);
                string config = Path.Combine(directory,"probe.json");
                File.WriteAllText(config,Json.Write(wireguard.Config(port,password,true)),new System.Text.UTF8Encoding(false));
                if (job == null) job = new ChildJob();
                var start = new ProcessStartInfo(Engine.PathName,"run --disable-color -c \"" + config + "\"") {
                    UseShellExecute=false, CreateNoWindow=true, RedirectStandardError=true, RedirectStandardOutput=true, WorkingDirectory=Path.GetDirectoryName(Engine.PathName)
                };
                start.EnvironmentVariables["GOGC"]="50"; start.EnvironmentVariables["GOMEMLIMIT"]="64MiB";
                engine = Process.Start(start); job.Add(engine);
                // Drain both streams, with no unbounded string accumulation.
                engine.ErrorDataReceived += (s,e) => { }; engine.OutputDataReceived += (s,e) => { }; engine.BeginErrorReadLine(); engine.BeginOutputReadLine();
                bool ready=false;
                for (int i=0;i<65 && !engine.HasExited;i++) {
                    cancellation.ThrowIfCancellationRequested();
                    try { using(var client=new TcpClient()) { client.Connect(IPAddress.Loopback,port); ready=true; break; } } catch (SocketException) { }
                    await Task.Delay(80,cancellation);
                }
                if (!ready) throw new Exception("O motor de teste não abriu a conexão local.");
                File.Delete(config);
                // No TUN, default route, DNS change or Discord process rule exists in this engine.
                ip = await Request(port,password,"https://api.ipify.org",10000,true,cancellation);
                IPAddress address; if(!IPAddress.TryParse(ip,out address)) throw new Exception("A verificação do túnel retornou uma resposta inválida.");
                for (int i=0;i<3;i++) {
                    cancellation.ThrowIfCancellationRequested(); var watch=Stopwatch.StartNew();
                    try { await Request(port,password,"https://www.gstatic.com/generate_204",7000,false,cancellation); samples.Add(watch.Elapsed.TotalMilliseconds); }
                    catch(OperationCanceledException) { throw; }
                    catch(Exception e) { problem=e.Message; }
                    if (i<2) await Task.Delay(180,cancellation);
                }
            } catch(OperationCanceledException) { throw; }
            catch(Exception e) { cancellation.ThrowIfCancellationRequested(); problem=e.Message; }
            finally {
                if(engine != null) {
                    try { if(!engine.HasExited) { engine.Kill(); engine.WaitForExit(2000); } } catch { }
                    engine.Dispose();
                }
                // Only this unique, owned test-session directory is removed.
                if(directory != null) try { Directory.Delete(directory,true); } catch { }
            }
            var result=ProfileMeasurement.FromSamples(profile.Id,batch,samples); result.ExitIP=ip; result.Error=problem; return result;
        }
        static async Task<string> Request(int port,string password,string url,int timeout,bool body,CancellationToken cancellation) {
            var request=(HttpWebRequest)WebRequest.Create(url);
            request.Proxy=new WebProxy("http://127.0.0.1:" + port) { Credentials=new NetworkCredential("probe",password) };
            request.KeepAlive=false; request.AllowAutoRedirect=false; request.CachePolicy=new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation)) {
                deadline.CancelAfter(timeout);
                using(deadline.Token.Register(request.Abort)) {
                    try {
                        using(var response=(HttpWebResponse)await request.GetResponseAsync()) {
                            if(body) using(var stream=new StreamReader(response.GetResponseStream())) return (await stream.ReadToEndAsync()).Trim();
                            if(response.StatusCode != HttpStatusCode.NoContent) throw new Exception("A sonda HTTPS não retornou o resultado esperado.");
                            return "";
                        }
                    } catch(WebException) { cancellation.ThrowIfCancellationRequested(); if(deadline.IsCancellationRequested) throw new TimeoutException("Tempo limite da sonda HTTPS."); throw new Exception("A sonda HTTPS não conseguiu acessar a internet por este perfil."); }
                }
            }
        }
        public void Dispose() { if(job!=null) { job.Dispose(); job=null; } }
    }
}

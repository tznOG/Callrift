using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Callrift {
    public sealed class ReleaseConfiguration {
        public string Repository { get; set; }
        public static ReleaseConfiguration Load() {
            using (var stream = typeof(ReleaseConfiguration).Assembly.GetManifestResourceStream("ReleaseConfig.json"))
            using (var reader = new StreamReader(stream)) return Json.Read<ReleaseConfiguration>(reader.ReadToEnd());
        }
    }
    public sealed class ReleaseAsset {
        public string name { get; set; }
        public string browser_download_url { get; set; }
        public string state { get; set; }
        public long size { get; set; }
    }
    public sealed class GitHubRelease {
        public string tag_name { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public List<ReleaseAsset> assets { get; set; }
    }
    public sealed class AvailableUpdate {
        public string Version { get; set; }
        public Uri ReleasePage { get; set; }
        public Uri Download { get; set; }
    }
    public enum UpdateState { Available, Current, Unpublished, Unavailable, NotConfigured }
    public sealed class UpdateResult {
        public UpdateState State { get; set; }
        public AvailableUpdate Update { get; set; }
    }
    public static class Updates {
        public static Version Current { get { return typeof(Updates).Assembly.GetName().Version; } }
        public static bool Installed { get { return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"installed.marker")); } }
        public static bool ValidRepository(string repository) {
            return repository != null && Regex.IsMatch(repository,@"\A[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9_.-]{1,100}\z") && !repository.EndsWith("/.") && !repository.EndsWith("/..");
        }
        public static Version StableVersion(string tag) {
            if (tag == null || !Regex.IsMatch(tag,@"\A[vV]?[0-9]{1,8}\.[0-9]{1,8}\.[0-9]{1,8}\z")) return null;
            Version version;
            if (!Version.TryParse(tag.TrimStart('v','V'),out version)) return null;
            return new Version(version.Major,version.Minor,version.Build,0);
        }
        public static UpdateResult Parse(string json,string repository,Version current,bool installed) {
            if (!ValidRepository(repository)) throw new ArgumentException("Invalid release repository.");
            var release = Json.Read<GitHubRelease>(json);
            if (release == null || release.draft || release.prerelease) return new UpdateResult { State=UpdateState.Unpublished };
            var version = StableVersion(release.tag_name);
            if (version == null) return new UpdateResult { State=UpdateState.Unpublished };
            var normalized = new Version(current.Major,current.Minor,Math.Max(0,current.Build),Math.Max(0,current.Revision));
            if (version <= normalized) return new UpdateResult { State=UpdateState.Current };
            string number = version.ToString(3);
            string filename = installed ? "Callrift-Setup-" + number + ".exe" : "Callrift-" + number + "-Windows-x64.zip";
            // Derive all links from the configured repository; never open arbitrary URLs from JSON.
            var page = new Uri("https://github.com/" + repository + "/releases/tag/" + Uri.EscapeDataString(release.tag_name));
            var download = new Uri("https://github.com/" + repository + "/releases/download/" + Uri.EscapeDataString(release.tag_name) + "/" + filename);
            var asset = (release.assets ?? new List<ReleaseAsset>()).FirstOrDefault(x=>x != null && x.name == filename && x.state == "uploaded" && x.size > 0 && String.Equals(x.browser_download_url,download.AbsoluteUri,StringComparison.Ordinal));
            if (asset == null) return new UpdateResult { State=UpdateState.Unpublished };
            return new UpdateResult { State=UpdateState.Available, Update=new AvailableUpdate { Version=number, ReleasePage=page, Download=download } };
        }
        public static async Task<UpdateResult> Check(HttpClient client,string repository,Version current,bool installed,CancellationToken cancellation) {
            if (!ValidRepository(repository)) return new UpdateResult { State=UpdateState.NotConfigured };
            using (var request = new HttpRequestMessage(HttpMethod.Get,"https://api.github.com/repos/" + repository + "/releases/latest")) {
                request.Headers.UserAgent.ParseAdd("Callrift/" + current.ToString(3));
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using (var response = await client.SendAsync(request,cancellation).ConfigureAwait(false)) {
                    if (response.StatusCode == HttpStatusCode.NotFound) return new UpdateResult { State=UpdateState.Unpublished };
                    if (!response.IsSuccessStatusCode) return new UpdateResult { State=UpdateState.Unavailable };
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (json.Length > 1024*1024) throw new InvalidDataException("Release response too large.");
                    cancellation.ThrowIfCancellationRequested();
                    return Parse(json,repository,current,installed);
                }
            }
        }
        public static async Task<UpdateResult> Check(string repository,bool installed,CancellationToken cancellation) {
            try {
                using (var client = new HttpClient { Timeout=TimeSpan.FromSeconds(8), MaxResponseContentBufferSize=1024*1024 })
                    return await Check(client,repository,Current,installed,cancellation).ConfigureAwait(false);
            } catch (OperationCanceledException) { if (cancellation.IsCancellationRequested) throw; return new UpdateResult { State=UpdateState.Unavailable }; }
            catch { return new UpdateResult { State=UpdateState.Unavailable }; }
        }
    }
}

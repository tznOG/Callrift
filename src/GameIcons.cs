using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Callrift {
    public static class GameIcons {
        static Dictionary<string,ImageSource> images = new Dictionary<string,ImageSource>();
        static Queue<string> order = new Queue<string>();
        static HashSet<string> pending = new HashSet<string>(), failed = new HashSet<string>();
        static SemaphoreSlim limit = new SemaphoreSlim(4);
        static HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        public static void Clear() { images.Clear(); order.Clear(); }
        public static ImageSource Get(Game game) {
            if (Application.Current == null || !Regex.IsMatch(game.id ?? "", "^[0-9]{1,30}$") || !Regex.IsMatch(game.icon_hash ?? "", "^(a_)?[a-f0-9]{32}$")) return null;
            ImageSource source;
            if (images.TryGetValue(game.id,out source)) return source;
            if (pending.Contains(game.id) || failed.Contains(game.id)) return null;
            pending.Add(game.id); Load(game); return null;
        }
        static async void Load(Game game) {
            await limit.WaitAsync();
            try {
                string directory = Files.At("icons"); Directory.CreateDirectory(directory);
                string path = Path.Combine(directory,game.id + "-" + game.icon_hash + ".png");
                byte[] data;
                if (File.Exists(path)) data = await Task.Run(() => File.ReadAllBytes(path));
                else {
                    using (var response = await client.GetAsync("https://cdn.discordapp.com/app-icons/" + game.id + "/" + game.icon_hash + ".png?size=64", HttpCompletionOption.ResponseHeadersRead)) {
                        response.EnsureSuccessStatusCode();
                        if (response.Content.Headers.ContentLength > 131072) throw new Exception("Icon too large");
                        using (var input = await response.Content.ReadAsStreamAsync()) using (var output = new MemoryStream()) {
                            byte[] buffer = new byte[4096]; int read;
                            while ((read = await input.ReadAsync(buffer,0,buffer.Length)) > 0) { output.Write(buffer,0,read); if (output.Length > 131072) throw new Exception("Icon too large"); }
                            data = output.ToArray();
                        }
                    }
                    await Task.Run(() => File.WriteAllBytes(path,data));
                }
                var bitmap = new BitmapImage();
                using (var stream = new MemoryStream(data)) { bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 64; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); }
                if (images.Count >= 96) images.Remove(order.Dequeue());
                images[game.id] = bitmap; order.Enqueue(game.id); game.Notify();
            } catch { failed.Add(game.id); }
            finally { pending.Remove(game.id); limit.Release(); }
        }
    }
}

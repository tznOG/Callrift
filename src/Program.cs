using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Callrift {
    public static class Program {
        public static string Executable { get { return Assembly.GetExecutingAssembly().Location; } }
        [STAThread] public static int Main(string[] args) {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            if (args.Length == 2 && args[0] == "--vpn-worker") return VpnWorker.Run(args[1]);
            if (args.Length == 2 && args[0] == "--startup-worker" && (args[1] == "enable" || args[1] == "disable")) return Startup.Worker(args[1] == "enable");
            if (args.Length == 1 && args[0] == "--uninstall-cleanup") return Startup.CleanupOwned();
            try {
                if (args.Length >= 2 && args[0] == "--render") return Render(args);
                if (args.Length == 2 && args[0] == "--diagnostic-idle") return Diagnose(args[1]);
                bool created;
                using (var mutex = new Mutex(true, "Local\\DiscordVPN-" + System.Security.Principal.WindowsIdentity.GetCurrent().User.Value, out created)) {
                    if (!created) {
                        try { using (var signal = EventWaitHandle.OpenExisting("Local\\DiscordVPN-Show")) signal.Set(); } catch { }
                        return 0;
                    }
                    using (var installationMutex = new Mutex(false,"Local\\Callrift-Installer")) {
                    var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    var ui = new MainUI(args.Contains("--startup"));
                    app.DispatcherUnhandledException += (s,e) => { ui.Error(e.Exception); e.Handled = true; };
                    AppDomain.CurrentDomain.ProcessExit += (s,e) => ui.Dispose();
                    app.Run(ui.Window); ui.Dispose();
                    }
                }
                return 0;
            } catch (Exception e) {
                try { File.WriteAllText(Files.At("last-error.txt"), e.ToString()); } catch { }
                MessageBox.Show("Não foi possível iniciar o Callrift.\n" + e.Message, "Callrift", MessageBoxButton.OK, MessageBoxImage.Error); return 1;
            }
        }
        static int Render(string[] args) {
            var app = new Application();
            var ui = new MainUI(false, true);
            if (args.Length >= 3) ui.Navigate(args[2]);
            var content = (FrameworkElement)ui.Window.Content;
            content.Measure(new Size(1060,790)); content.Arrange(new Rect(0,0,1060,790)); content.UpdateLayout();
            var bitmap = new RenderTargetBitmap(1060,790,96,96,PixelFormats.Pbgra32); bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(args[1])) encoder.Save(stream);
            ui.Dispose(); app.Shutdown(); return 0;
        }
        static int Diagnose(string path) {
            Files.Root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), "diagnostic-state-" + Guid.NewGuid().ToString("N"));
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            using (var ui = new MainUI(true)) {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                TimeSpan baseline = TimeSpan.Zero; DateTime started = DateTime.UtcNow; bool primed = false;
                timer.Tick += (s,e) => {
                    using (var process = Process.GetCurrentProcess()) {
                        if (!primed) { primed = true; baseline = process.TotalProcessorTime; started = DateTime.UtcNow; timer.Interval = TimeSpan.FromSeconds(6); return; }
                        var data = new { WorkingSetMiB = Math.Round(process.WorkingSet64 / 1048576.0, 1), PrivateMiB = Math.Round(process.PrivateMemorySize64 / 1048576.0, 1), IdleCpuPercent = Math.Round((process.TotalProcessorTime - baseline).TotalSeconds / (DateTime.UtcNow - started).TotalSeconds / Environment.ProcessorCount * 100, 3), SampleSeconds = Math.Round((DateTime.UtcNow - started).TotalSeconds, 1), LogicalProcessors = Environment.ProcessorCount, VpnActive = false, CatalogueLoaded = false };
                        File.WriteAllText(path, Json.Write(data));
                    }
                    timer.Stop(); ui.Dispose(); app.Shutdown();
                };
                ui.Window.Loaded += (s,e) => timer.Start(); app.Run(ui.Window);
            }
            try { Directory.Delete(Files.Root,true); } catch { }
            return 0;
        }
    }

    public sealed class MainUI : IDisposable {
        public Window Window;
        Settings settings;
        ProfileStore profileStore = new ProfileStore();
        List<WireGuardProfile> profiles = new List<WireGuardProfile>();
        ActivityService activity = new ActivityService();
        VpnController vpn = new VpnController();
        Forms.NotifyIcon tray;
        DispatcherTimer statusTimer, gameTimer, searchTimer;
        EventWaitHandle showSignal;
        RegisteredWaitHandle showWait;
        string currentPage = "vpn", gameFilter = "all";
        int page = 1;
        bool initializing = true, catalogLoaded, loading, exit, disposed, startup, renderOnly, refreshingProfiles;
        bool testingProfiles, restartingDiscord, discordRestartRequired;
        CancellationTokenSource profileTestCancellation;
        Task profileTestTask;
        ProfileBenchmark benchmark;
        List<DiscordProcess> pendingDiscordRestart;
        readonly CancellationTokenSource updatesLifetime = new CancellationTokenSource();
        AvailableUpdate availableUpdate;
        bool checkingUpdates, pendingUpdatePrompt;
        public MainUI(bool startup, bool renderOnly = false) {
            this.startup = startup; this.renderOnly = renderOnly;
            settings = renderOnly ? new Settings() : Settings.Load(); I18n.Language = settings.Language;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml")) Window = (Window)XamlReader.Load(stream);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Controls.xaml")) {
                var controls=(ResourceDictionary)XamlReader.Load(stream);
                foreach(DictionaryEntry entry in controls) Window.Resources[entry.Key]=entry.Value;
            }
            WindowFrame.Attach(Window);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BrandMark.png")) {
                var mark = new BitmapImage(); mark.BeginInit(); mark.CacheOption = BitmapCacheOption.OnLoad;
                mark.DecodePixelWidth = 128; mark.StreamSource = stream; mark.EndInit(); mark.Freeze();
                C<Image>("BrandMark").Source = mark;
            }
            using(var icon=System.Drawing.Icon.ExtractAssociatedIcon(Program.Executable)) Window.Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            vpn.Log += Log; vpn.Lost += () => Window.Dispatcher.BeginInvoke((Action)UpdateConnection);
            BindEvents();
            if (!renderOnly) {
                profiles = profileStore.Load(Log); LoadProfiles();
                SetupTray();
                statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
                statusTimer.Tick += (s,e) => CheckDiscord();
                gameTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                gameTimer.Tick += (s,e) => { if (activity.Reap()) RefreshGames(); if (Window.IsVisible && currentPage == "activity") foreach (var g in activity.Running.Values) g.Game.Notify(); if (activity.Running.Count == 0) gameTimer.Stop(); };
                searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
                searchTimer.Tick += (s,e) => { searchTimer.Stop(); page = 1; RefreshGames(); };
                showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\DiscordVPN-Show");
                showWait = ThreadPool.RegisterWaitForSingleObject(showSignal, (s,t) => Window.Dispatcher.BeginInvoke((Action)Show), null, Timeout.Infinite, false);
                Window.Loaded += async (s,e) => {
                    CheckDiscord(); statusTimer.Start();
                    if (startup) Window.Hide();
                    Log("Aplicativo iniciado. Seus perfis são protegidos com a criptografia do Windows.");
                    if (settings.CheckUpdatesOnStart) { var ignored = CheckForUpdates(false,true); }
                    if (settings.AutoConnect && SelectedProfile != null) await ToggleVpn();
                };
            }
            C<CheckBox>("StartWindows").IsChecked = settings.StartWithWindows;
            C<CheckBox>("QuickStartup").IsChecked = settings.StartWithWindows;
            C<CheckBox>("AutoConnect").IsChecked = settings.AutoConnect;
            C<CheckBox>("AutoDiscord").IsChecked = settings.OpenDiscord;
            C<CheckBox>("TrayOption").IsChecked = settings.MinimizeToTray;
            C<CheckBox>("AutoUpdates").IsChecked = settings.CheckUpdatesOnStart;
            C<CheckBox>("LightTheme").IsChecked = settings.LightTheme;
            C<ComboBox>("Language").SelectedIndex = settings.Language == "en-US" ? 1 : settings.Language == "es-ES" ? 2 : 0;
            ApplyTheme(); Translate(Window); Navigate("vpn"); UpdateConnection();
            initializing = false;
        }
        T C<T>(string name) where T : FrameworkElement { return (T)Window.FindName(name); }
        void On(string name, Action action) { C<Button>(name).Click += (s,e) => { try { action(); } catch (Exception error) { Error(error); } }; }
        void OnAsync(string name, Func<Task> action) { C<Button>(name).Click += async (s,e) => { try { await action(); } catch (Exception error) { Error(error); } }; }
        void BindEvents() {
            C<Grid>("TitleBar").MouseLeftButtonDown += (s,e) => { if (e.ClickCount == 2) Maximize(); else if (e.ButtonState == MouseButtonState.Pressed) Window.DragMove(); };
            On("Minimize", () => Window.WindowState = WindowState.Minimized); On("Maximize", Maximize); On("Close", () => Window.Close());
            Window.Closing += (s,e) => {
                if (exit || renderOnly) return;
                e.Cancel = true;
                if (settings.MinimizeToTray) { Window.Hide(); if (tray != null) tray.ShowBalloonTip(1600, "Callrift", "O app continua na bandeja. Use Sair para encerrar a VPN e as atividades.", Forms.ToolTipIcon.Info); }
                else Exit();
            };
            Window.IsVisibleChanged += (s,e) => { if (statusTimer != null) { if (Window.IsVisible) { CheckDiscord(); statusTimer.Start(); } else statusTimer.Stop(); } if (gameTimer != null) gameTimer.Interval = TimeSpan.FromSeconds(Window.IsVisible ? 1 : 10); };
            On("NavVpn", () => Navigate("vpn")); On("NavActivity", () => Navigate("activity")); On("NavLogs", () => Navigate("logs")); On("NavSettings", () => Navigate("settings"));
            OnAsync("CheckUpdates", () => CheckForUpdates(true,false));
            On("DownloadUpdate", DownloadLatest);
            C<CheckBox>("AutoUpdates").Click += (s,e) => { settings.CheckUpdatesOnStart = C<CheckBox>("AutoUpdates").IsChecked == true; Save(); };
            OnAsync("Connect", ToggleVpn); OnAsync("OpenDiscord", HandleOpenDiscord); OnAsync("RestartDiscord", OfferRestartDiscord);
            OnAsync("TestProfiles", StartProfileTests); On("CancelTests", () => { if(profileTestCancellation!=null) profileTestCancellation.Cancel(); });
            On("GenerateProfile", () => OpenLink("https://account.protonvpn.com/downloads#wireguard-configuration"));
            On("ImportProfile", ImportProfile); On("RemoveProfile", RemoveProfile); On("FolderProfiles", () => Process.Start("explorer.exe", "\"" + profileStore.DirectoryPath + "\""));
            C<ListBox>("Profiles").SelectionChanged += (s,e) => { if (initializing || refreshingProfiles || vpn.IsActive || vpn.IsBusy) return; settings.SelectedProfile = SelectedProfile == null ? null : SelectedProfile.Id; Save(); UpdateConnection(); };
            C<ListBox>("Profiles").MouseDoubleClick += async (s,e) => { if (SelectedProfile != null && !vpn.IsBusy && !vpn.IsActive) await ToggleVpn(); };
            foreach (string name in new [] { "StartWindows", "QuickStartup" }) C<CheckBox>(name).Click += async (s,e) => await ChangeStartup(((CheckBox)s).IsChecked == true);
            C<CheckBox>("AutoConnect").Click += (s,e) => { settings.AutoConnect = C<CheckBox>("AutoConnect").IsChecked == true; Save(); };
            C<CheckBox>("AutoDiscord").Click += (s,e) => { settings.OpenDiscord = C<CheckBox>("AutoDiscord").IsChecked == true; Save(); };
            C<CheckBox>("TrayOption").Click += (s,e) => { settings.MinimizeToTray = C<CheckBox>("TrayOption").IsChecked == true; Save(); };
            C<CheckBox>("LightTheme").Click += (s,e) => { settings.LightTheme = C<CheckBox>("LightTheme").IsChecked == true; ApplyTheme(); Save(); };
            C<ComboBox>("Language").SelectionChanged += (s,e) => {
                if (initializing) return; var selected = (ComboBoxItem)C<ComboBox>("Language").SelectedItem;
                settings.Language = (string)selected.Tag; I18n.Language = settings.Language; Translate(Window); UpdateConnection(); RefreshGames(); Save();
                if (availableUpdate != null) PresentUpdate(availableUpdate,false);
            };
            On("ShowLicenses", () => Process.Start(new ProcessStartInfo("notepad.exe", "\"" + Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"licenses","THIRD-PARTY-NOTICES.md") + "\"") { UseShellExecute=false }));
            OnAsync("RefreshGames", () => LoadGames(true));
            On("FilterAll", () => { gameFilter = "all"; page = 1; RefreshGames(); }); On("FilterFavorites", () => { gameFilter = "favorites"; page = 1; RefreshGames(); }); On("FilterRunning", () => { gameFilter = "running"; page = 1; RefreshGames(); });
            On("StopAll", () => { activity.StopAll(); RefreshGames(); Log("Todas as atividades foram encerradas."); });
            On("PrevPage", () => { page--; RefreshGames(); }); On("NextPage", () => { page++; RefreshGames(); });
            C<TextBox>("Search").TextChanged += (s,e) => { if (!initializing && searchTimer != null) { searchTimer.Stop(); searchTimer.Start(); } };
            C<ListBox>("Games").AddHandler(Button.ClickEvent, new RoutedEventHandler(GameAction));
            On("CopyLogs", () => { Clipboard.SetText(C<TextBox>("Logs").Text); C<TextBlock>("FooterMessage").Text = "Registro copiado."; }); On("ClearLogs", () => C<TextBox>("Logs").Clear());
        }
        void Maximize() { Window.WindowState = Window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
        public void Navigate(string name) {
            bool leavingCatalogue = currentPage == "activity" && name != "activity";
            currentPage = name;
            foreach (var p in new [] { "Vpn", "Activity", "Logs", "Settings" }) {
                bool selected = p.Equals(name, StringComparison.OrdinalIgnoreCase);
                C<FrameworkElement>(p + "Page").Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
                C<Button>("Nav" + p).SetResourceReference(Control.BackgroundProperty, selected ? "AccentSoft" : "Sidebar");
                C<Button>("Nav" + p).SetResourceReference(Control.ForegroundProperty, selected ? "Accent" : "Muted");
            }
            if (leavingCatalogue && !loading) ReleaseCatalogue();
            if (name == "activity" && !catalogLoaded && !loading && !renderOnly) { var ignored = LoadGames(false); }
        }
        void ReleaseCatalogue() {
            C<ListBox>("Games").ItemsSource = null;
            activity.Games = new List<Game>(); catalogLoaded = false; GameIcons.Clear();
            // Only request collection while idle after releasing a large catalogue.
            Window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action)(() => GC.Collect(2, GCCollectionMode.Optimized, false)));
        }
        WireGuardProfile SelectedProfile { get { return C<ListBox>("Profiles").SelectedItem as WireGuardProfile; } }
        void LoadProfiles() {
            var measured=ProfileBenchmark.Load();
            foreach(var profile in profiles) { ProfileMeasurement result; if(profile.Measurement==null && measured.TryGetValue(profile.Id,out result)) profile.Measurement=result; }
            string batch=profiles.Where(x=>x.Measurement!=null).OrderByDescending(x=>x.Measurement.TestedAt).Select(x=>x.Measurement.Batch).FirstOrDefault();
            MarkBest(batch);
            refreshingProfiles = true;
            string selected = settings.SelectedProfile;
            C<ListBox>("Profiles").ItemsSource = profiles;
            C<ListBox>("Profiles").SelectedItem = profiles.FirstOrDefault(x => x.Id == selected) ?? profiles.FirstOrDefault();
            refreshingProfiles = false;
            C<Border>("EmptyProfiles").Visibility = profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateConnection();
        }
        void ImportProfile() {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "WireGuard (*.conf)|*.conf", Multiselect = true, Title = "Importar perfil WireGuard da Proton VPN" };
            if (dialog.ShowDialog(Window) != true) return;
            int count = 0;
            foreach (string path in dialog.FileNames) {
                try { var p = profileStore.Import(path); settings.SelectedProfile = p.Id; count++; } catch (Exception e) { Error(e); }
            }
            profiles = profileStore.Load(Log); LoadProfiles(); Save(); Log(count + " perfil(is) importado(s) e protegido(s) pelo Windows.");
        }
        void RemoveProfile() {
            var p = SelectedProfile; if (p == null) return;
            if (vpn.IsActive || vpn.IsBusy) throw new Exception("Desconecte a VPN antes de remover um perfil.");
            if (!AppDialog.Confirm(Window,"Callrift",I18n.T("RemoveConfirm"),I18n.T("Remove"))) return;
            profileStore.Remove(p); profiles = profileStore.Load(Log); LoadProfiles(); settings.SelectedProfile = SelectedProfile == null ? null : SelectedProfile.Id; Save(); Log("Perfil removido do aplicativo.");
        }
        async Task ToggleVpn() {
            if (vpn.IsBusy || restartingDiscord) return;
            if (testingProfiles) { Error(new Exception("Aguarde ou cancele o teste dos perfis antes de conectar a VPN.")); return; }
            if (!vpn.IsActive && SelectedProfile == null) { Error(new Exception(I18n.T("SelectProfile"))); return; }
            var profile = SelectedProfile;
            bool connecting = !vpn.IsActive;
            try {
                C<Button>("Connect").IsEnabled = false;
                if (connecting) {
                    C<TextBlock>("StatusTitle").Text = I18n.T("Connecting"); C<TextBlock>("StatusSubtitle").Text = I18n.T("Verifying");
                    C<TextBlock>("FooterStatus").Text = "●  " + I18n.T("Working");
                    LockProfiles(true); Log("Iniciando túnel para o perfil " + profile.Name + ".");
                    await vpn.Connect(profile);
                    settings.SelectedProfile = profile.Id; Save();
                    if (DiscordSession.Capture().Count > 0) await OfferRestartDiscord();
                    else if (settings.OpenDiscord) OpenDiscord();
                } else { await vpn.Disconnect(); discordRestartRequired=false; pendingDiscordRestart=null; }
            } catch (System.ComponentModel.Win32Exception e) { Error(new Exception(e.NativeErrorCode == 1223 ? "A permissão do Windows foi cancelada. A VPN não foi conectada." : e.Message)); }
            catch (Exception e) { Error(e); }
            finally { UpdateConnection(); }
        }
        void LockProfiles(bool locked) { C<ListBox>("Profiles").IsEnabled = !locked; C<Button>("ImportProfile").IsEnabled = !locked; C<Button>("RemoveProfile").IsEnabled = !locked && SelectedProfile!=null; }
        void UpdateConnection() {
            if (Window == null) return;
            bool active = vpn.IsActive;
            C<Button>("Connect").Content = I18n.T(active ? "Disconnect" : "Connect");
            C<Button>("Connect").IsEnabled = !vpn.IsBusy && !testingProfiles && !restartingDiscord && (active || SelectedProfile != null);
            C<TextBlock>("StatusTitle").Text = I18n.T(active ? "Connected" : "Ready");
            C<TextBlock>("StatusSubtitle").Text = active ? "IP: " + vpn.ExitIP + "  ·  " + (SelectedProfile == null ? "" : SelectedProfile.Name) : SelectedProfile == null ? I18n.T("SelectProfile") : SelectedProfile.Name + "  ·  " + SelectedProfile.Endpoint;
            C<TextBlock>("StatusIcon").SetResourceReference(TextBlock.ForegroundProperty, active ? "Green" : "Muted");
            C<TextBlock>("FooterStatus").Text = "●  " + I18n.T(active ? "Connected" : "Disconnected");
            C<TextBlock>("FooterStatus").SetResourceReference(TextBlock.ForegroundProperty, active ? "Green" : "Muted");
            LockProfiles(active || vpn.IsBusy || testingProfiles || restartingDiscord);
            C<Button>("TestProfiles").IsEnabled = !active && !vpn.IsBusy && !testingProfiles && !restartingDiscord && profiles.Count>0;
            C<Button>("TestProfiles").ToolTip = active ? "Desconecte a VPN antes de comparar os perfis, para preservar a sessão ativa." : I18n.T("TestHint");
            C<Button>("RestartDiscord").IsEnabled = active && !restartingDiscord;
            C<Button>("OpenDiscord").IsEnabled = !restartingDiscord;
            C<Border>("RestartBanner").Visibility = active && discordRestartRequired ? Visibility.Visible : Visibility.Collapsed;
            if (tray != null) tray.Text = "Callrift · " + I18n.T(active ? "Connected" : "Disconnected");
            C<Button>("GenerateProfile").Content = I18n.T("Generate");
        }
        async Task ChangeStartup(bool enabled) {
            if (initializing) return;
            C<CheckBox>("QuickStartup").IsEnabled = false; C<CheckBox>("StartWindows").IsEnabled = false;
            try { await Startup.Set(enabled); settings.StartWithWindows = enabled; Save(); Log(enabled ? "Inicialização do Windows ativada. Mantenha a pasta do app neste local." : "Inicialização do Windows desativada."); }
            catch (Exception e) { Error(e); }
            finally {
                C<CheckBox>("QuickStartup").IsChecked = settings.StartWithWindows; C<CheckBox>("StartWindows").IsChecked = settings.StartWithWindows;
                C<CheckBox>("QuickStartup").IsEnabled = true; C<CheckBox>("StartWindows").IsEnabled = true;
            }
        }
        public void OpenDiscord() {
            try {
                // Explorer brokers the protocol at normal user integrity, even after scheduled startup.
                Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "discord://-/") { UseShellExecute = false });
                Log("Solicitado ao Windows abrir o Discord.");
            } catch { throw new Exception("Não foi possível abrir o Discord. Instale a versão desktop e abra-a uma vez."); }
        }
        async Task HandleOpenDiscord() { if(vpn.IsActive && discordRestartRequired) await OfferRestartDiscord(); else OpenDiscord(); }
        async Task OfferRestartDiscord() {
            if(!vpn.IsActive || restartingDiscord) return;
            var approved=DiscordSession.Capture();
            if(approved.Count==0) { discordRestartRequired=false; pendingDiscordRestart=null; UpdateConnection(); OpenDiscord(); return; }
            discordRestartRequired=true; pendingDiscordRestart=approved; UpdateConnection();
            if(!Window.IsVisible) Show();
            if(!AppDialog.Confirm(Window,I18n.T("RestartTitle"),I18n.T("RestartMessage"),I18n.T("RestartDiscord"))) { Log("Reinício do Discord adiado. As conexões existentes podem precisar ser renovadas."); return; }
            restartingDiscord=true; UpdateConnection();
            try {
                await DiscordSession.Stop(approved);
                if(!vpn.IsActive) throw new Exception("A VPN caiu durante o reinício. O Discord não foi reaberto para evitar uma conexão direta.");
                foreach(string channel in approved.Select(x=>x.Channel).Distinct()) Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),DiscordSession.Protocol(channel)) { UseShellExecute=false });
                discordRestartRequired=false; pendingDiscordRestart=null; CheckDiscord(); Log("Discord reiniciado com sua autorização, depois da conexão VPN.");
            } finally { restartingDiscord=false; UpdateConnection(); }
        }
        void MarkBest(string batch) {
            var best=ProfileMeasurement.Best(profiles.Where(x=>x.Measurement!=null).Select(x=>x.Measurement),batch);
            foreach(var profile in profiles) { profile.Best=best!=null && profile.Id==best.ProfileId; profile.Notify(); }
        }
        async Task StartProfileTests() {
            if(testingProfiles || vpn.IsActive || vpn.IsBusy || restartingDiscord || profiles.Count==0) return;
            profileTestCancellation=new CancellationTokenSource();
            profileTestTask=RunProfileTests(profileTestCancellation.Token);
            try { await profileTestTask; } finally { profileTestCancellation.Dispose(); profileTestCancellation=null; profileTestTask=null; }
        }
        async Task RunProfileTests(CancellationToken cancellation) {
            testingProfiles=true; C<Button>("CancelTests").Visibility=Visibility.Visible;
            string batch=Guid.NewGuid().ToString("N");
            foreach(var profile in profiles) { profile.Best=false; profile.Notify(); }
            UpdateConnection(); benchmark=new ProfileBenchmark();
            try {
                await Task.Run((Action)Engine.Verify); cancellation.ThrowIfCancellationRequested();
                for(int i=0;i<profiles.Count;i++) {
                    cancellation.ThrowIfCancellationRequested(); var profile=profiles[i]; profile.Testing=true; profile.Notify();
                    C<TextBlock>("ProfileTestInfo").Text="Testando " + (i+1) + "/" + profiles.Count + ": " + profile.Name + " · três requisições HTTPS pelo túnel.";
                    profile.Measurement=await benchmark.Measure(profile,batch,cancellation); profile.Testing=false; profile.Notify();
                    ProfileBenchmark.Save(profiles); MarkBest(batch);
                    Log("Teste do perfil " + profile.Name + ": " + profile.Measurement.Summary);
                }
                var winner=ProfileMeasurement.Best(profiles.Where(x=>x.Measurement!=null).Select(x=>x.Measurement),batch);
                var best=winner==null ? null : profiles.FirstOrDefault(x=>x.Id==winner.ProfileId);
                C<TextBlock>("ProfileTestInfo").Text=best==null ? "Nenhum perfil respondeu ao teste HTTPS. Confira os arquivos e tente novamente." : "Melhor resposta: " + best.Name + " · " + Math.Round(winner.MedianMs.Value) + " ms · " + winner.Successes + "/3 respostas. Tempo HTTPS, não ping do jogo nem velocidade de download.";
            } catch(OperationCanceledException) { C<TextBlock>("ProfileTestInfo").Text="Teste cancelado. Resultados concluídos foram preservados."; Log("Teste de perfis cancelado."); }
            catch(Exception e) { Error(e); C<TextBlock>("ProfileTestInfo").Text=e.Message; }
            finally {
                foreach(var profile in profiles) { profile.Testing=false; profile.Notify(); }
                benchmark.Dispose(); benchmark=null; testingProfiles=false; C<Button>("CancelTests").Visibility=Visibility.Collapsed; UpdateConnection();
            }
        }
        void CheckDiscord() {
            var current=DiscordSession.Capture(); bool open=current.Count>0;
            if(discordRestartRequired && pendingDiscordRestart!=null && !current.Any(p=>pendingDiscordRestart.Any(old=>old.Id==p.Id && (old.Started==0 || old.Started==p.Started)))) {
                discordRestartRequired=false; pendingDiscordRestart=null; UpdateConnection();
            }
            C<TextBlock>("DiscordStatus").Text = I18n.T(open ? "DiscordOpen" : "DiscordClosed");
            C<TextBlock>("DiscordStatus").SetResourceReference(TextBlock.ForegroundProperty, open ? "Green" : "Muted");
        }
        static void OpenLink(string url) { Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "\"" + url + "\"") { UseShellExecute = false }); }
        async Task LoadGames(bool force) {
            if (loading) return; loading = true;
            C<Button>("RefreshGames").IsEnabled = false; C<TextBlock>("GameEmpty").Text = I18n.T("Loading");
            C<TextBlock>("GameEmpty").Visibility = activity.Games.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            try {
                await activity.Load(force); catalogLoaded = true; page = 1;
                C<TextBlock>("CacheInfo").Text = I18n.T(activity.Offline ? "Offline" : activity.FromCache ? "Cache" : "Live") + " · " + activity.CacheTime.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                Log("Catálogo Presence: " + activity.Games.Count + " aplicativos. " + (activity.FromCache ? "Cache local." : "Atualizado do Discord."));
                if (currentPage == "activity") RefreshGames(); else ReleaseCatalogue();
            } catch (Exception e) { Error(e); C<TextBlock>("GameEmpty").Text = e.Message; C<TextBlock>("GameEmpty").Visibility = Visibility.Visible; }
            finally { loading = false; C<Button>("RefreshGames").IsEnabled = true; }
        }
        void RefreshGames() {
            if (Window == null) return;
            var filtered = activity.Filter(C<TextBox>("Search").Text);
            if (gameFilter == "favorites") filtered = filtered.Where(g => g.Favorite).ToList();
            if (gameFilter == "running") filtered = filtered.Where(g => g.Running).ToList();
            var favorites = filtered.Where(g => g.Favorite).ToList();
            var others = filtered.Where(g => !g.Favorite).ToList();
            int pages = Math.Max(1, (int)Math.Ceiling(others.Count / 50.0)); page = Math.Max(1,Math.Min(pages,page));
            C<ListBox>("Games").ItemsSource = favorites.Concat(others.Skip((page-1)*50).Take(50)).ToList();
            C<TextBlock>("PageLabel").Text = page + " / " + pages; C<Button>("PrevPage").IsEnabled = page > 1; C<Button>("NextPage").IsEnabled = page < pages;
            C<TextBlock>("GameSummary").Text = filtered.Count + " " + I18n.T("Catalogue") + "  ·  " + activity.Running.Count + " " + I18n.T("Running").ToLowerInvariant();
            C<TextBlock>("GameEmpty").Text = I18n.T("NoResults"); C<TextBlock>("GameEmpty").Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            C<Button>("StopAll").IsEnabled = activity.Running.Count > 0;
            foreach (var g in filtered) g.Notify();
            foreach (string key in new [] { "All", "Favorites", "Running" }) C<Button>("Filter" + key).SetResourceReference(Control.BackgroundProperty, (key.ToLowerInvariant() == gameFilter) ? "AccentSoft" : "Card");
        }
        void GameAction(object sender, RoutedEventArgs e) {
            var button = e.OriginalSource as Button; if (button == null) return;
            var game = button.DataContext as Game; if (game == null) return;
            try {
                if ((string)button.Tag == "favorite") { activity.Favorite(game); RefreshGames(); }
                if ((string)button.Tag == "run") {
                    if (game.Running) { activity.Stop(game.id); Log("Atividade encerrada: " + game.name); }
                    else {
                        var grid = button.Parent as Grid; var selected = grid == null ? null : grid.Children.OfType<ComboBox>().FirstOrDefault();
                        var exe = selected == null ? null : selected.SelectedItem as GameExecutable;
                        activity.Start(game,exe == null ? null : exe.name); if (gameTimer != null) gameTimer.Start(); Log("Atividade iniciada: " + game.name);
                    }
                    RefreshGames();
                }
            } catch (Exception error) { Error(error); }
            e.Handled = true;
        }
        void ApplyTheme() {
            string[] keys = { "Bg", "Sidebar", "Card", "Input", "Fg", "Muted", "Line", "Accent", "AccentSoft", "Green", "Red" };
            string[] dark = { "#101116", "#14151C", "#191B24", "#12141C", "#F3F1FC", "#9496A9", "#2A2C3A", "#9566FF", "#29213F", "#64D8AD", "#FF8B9B" };
            string[] light = { "#F6F5FA", "#EEEAF5", "#FFFFFF", "#F8F7FB", "#201A30", "#6A637D", "#DBD5E6", "#7042C4", "#E9DFFB", "#197854", "#B62942" };
            var values = settings.LightTheme ? light : dark;
            for (int i=0;i<keys.Length;i++) Window.Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[i]));
            WindowFrame.Update(Window);
        }
        void Translate(DependencyObject element) {
            var control = element as FrameworkElement;
            if (control != null && control.Tag is string && ((string)control.Tag).StartsWith("t:")) {
                string text = I18n.T(((string)control.Tag).Substring(2));
                var block = control as TextBlock; if (block != null) block.Text = text;
                var content = control as ContentControl; if (content != null) content.Content = text;
            }
            foreach (var child in LogicalTreeHelper.GetChildren(element)) { var dep = child as DependencyObject; if (dep != null) Translate(dep); }
        }
        void Save() { if (!initializing && !renderOnly) { try { settings.Save(); } catch (Exception e) { Error(e); } } }
        public void Log(string message) {
            if (disposed) return;
            if (!Window.Dispatcher.CheckAccess()) { Window.Dispatcher.BeginInvoke((Action)(() => Log(message))); return; }
            var text = C<TextBox>("Logs");
            var lines = (text.Text + DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine).Split(new [] { Environment.NewLine },StringSplitOptions.None);
            text.Text = String.Join(Environment.NewLine,lines.Skip(Math.Max(0,lines.Length - 250))); text.ScrollToEnd();
            C<TextBlock>("FooterMessage").Text = message;
        }
        public void Error(Exception error) { Log("Erro: " + error.Message); if (!renderOnly && (!startup || Window.IsVisible)) AppDialog.Alert(Window,"Callrift",error.Message); }
        void SetupTray() {
            tray = new Forms.NotifyIcon { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Program.Executable), Text = "Callrift", Visible = true };
            tray.DoubleClick += (s,e) => Window.Dispatcher.BeginInvoke((Action)Show);
            tray.BalloonTipClicked += (s,e) => Window.Dispatcher.BeginInvoke((Action)Show);
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Abrir Callrift",null,(s,e) => Window.Dispatcher.BeginInvoke((Action)Show));
            menu.Items.Add("Conectar / Desconectar",null,(s,e) => Window.Dispatcher.BeginInvoke((Action)(async () => await ToggleVpn())));
            menu.Items.Add("Abrir Discord",null,(s,e) => Window.Dispatcher.BeginInvoke((Action)(async () => { try { await HandleOpenDiscord(); } catch(Exception error) { Error(error); } })));
            menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("Sair",null,(s,e) => Window.Dispatcher.BeginInvoke((Action)Exit)); tray.ContextMenuStrip = menu;
        }
        void Show() {
            Window.Show(); Window.WindowState = WindowState.Normal; Window.Activate();
            if (pendingUpdatePrompt) { pendingUpdatePrompt=false; Window.Dispatcher.BeginInvoke((Action)PromptUpdate); }
        }
        void DownloadLatest() { if (availableUpdate != null) OpenLink(availableUpdate.Download.AbsoluteUri); }
        void PresentUpdate(AvailableUpdate update,bool announce) {
            availableUpdate=update;
            C<Border>("UpdateBanner").Visibility=Visibility.Visible;
            C<TextBlock>("UpdateHeadline").Text=String.Format(I18n.T("UpdateVersion"),update.Version);
            C<TextBlock>("UpdateStatus").Tag=null; C<TextBlock>("UpdateStatus").Text=C<TextBlock>("UpdateHeadline").Text;
            if (!announce) return;
            if (Window.IsVisible && !vpn.IsBusy && !restartingDiscord && !Application.Current.Windows.Cast<Window>().Any(w=>w.Owner==Window)) PromptUpdate();
            else {
                pendingUpdatePrompt=true;
                if (tray!=null) tray.ShowBalloonTip(5000,"Callrift",C<TextBlock>("UpdateHeadline").Text,Forms.ToolTipIcon.Info);
            }
        }
        void PromptUpdate() {
            if (disposed || availableUpdate==null) return;
            if (AppDialog.Confirm(Window,I18n.T("UpdateTitle"),String.Format(I18n.T("UpdateMessage"),Updates.Current.ToString(3),availableUpdate.Version),I18n.T("GetUpdate"))) DownloadLatest();
        }
        async Task CheckForUpdates(bool manual,bool delayed) {
            if (checkingUpdates || disposed) return;
            checkingUpdates=true; C<Button>("CheckUpdates").IsEnabled=false;
            C<TextBlock>("UpdateStatus").Tag="t:UpdateChecking"; C<TextBlock>("UpdateStatus").Text=I18n.T("UpdateChecking");
            var cancellation=updatesLifetime.Token;
            try {
                if (delayed) await Task.Delay(3000,cancellation);
                var configuration=ReleaseConfiguration.Load();
                var result=await Updates.Check(configuration.Repository,Updates.Installed,cancellation);
                if (disposed || cancellation.IsCancellationRequested) return;
                if (result.State==UpdateState.Available) { PresentUpdate(result.Update,true); return; }
                string key=result.State==UpdateState.Current ? "UpdateCurrent" : result.State==UpdateState.Unpublished ? "UpdateUnpublished" : "UpdateUnavailable";
                C<TextBlock>("UpdateStatus").Tag="t:"+key; C<TextBlock>("UpdateStatus").Text=I18n.T(key);
                if (manual) Log(I18n.T(key));
            } catch (OperationCanceledException) { }
            catch { if(!disposed) { C<TextBlock>("UpdateStatus").Tag="t:UpdateUnavailable"; C<TextBlock>("UpdateStatus").Text=I18n.T("UpdateUnavailable"); } }
            finally { checkingUpdates=false; if(!disposed) C<Button>("CheckUpdates").IsEnabled=true; }
        }
        async void Exit() {
            if (vpn.IsBusy || restartingDiscord) { Log("Aguarde a operação terminar antes de sair."); return; }
            if(profileTestCancellation!=null) profileTestCancellation.Cancel();
            if(profileTestTask!=null) try { await profileTestTask; } catch { }
            exit = true; try { if (vpn.IsActive) await vpn.Disconnect(); } catch { }
            Dispose(); Window.Close(); Application.Current.Shutdown();
        }
        public void Dispose() {
            if (disposed) return; disposed = true;
            updatesLifetime.Cancel(); updatesLifetime.Dispose();
            if (statusTimer != null) statusTimer.Stop(); if (gameTimer != null) gameTimer.Stop(); if (searchTimer != null) searchTimer.Stop();
            if (showWait != null) showWait.Unregister(null); if (showSignal != null) showSignal.Dispose();
            if(profileTestCancellation!=null) profileTestCancellation.Cancel(); if(benchmark!=null) benchmark.Dispose();
            vpn.Dispose(); activity.Dispose();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
        }
    }
}

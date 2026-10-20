using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ClassroomGuard.Admin.Services;
using ClassroomGuard.Core.Models;
using ClassroomGuard.Core.Net;
using ClassroomGuard.Core.Security;
using Microsoft.Win32;

namespace ClassroomGuard.Admin;

public partial class MainWindow : Window
{
    private readonly AdminConfig _config = AdminConfig.Load();
    private Discovery? _discovery;
    private AdminClient? _client;
    private SyncEngine? _sync;
    private ClassroomKey? _key;
    private readonly TelegramClient _telegram = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly DispatcherTimer _displayTimer = new() { Interval=TimeSpan.FromSeconds(3) };
    private bool _polling;
    private bool _busy;
    private static readonly TimeSpan OfflineAfter = TimeSpan.FromSeconds(45);

    public MainWindow()
    {
        InitializeComponent();
        LoadSettingsIntoUi();
        InitKeyAndServices();

        _timer.Interval=TimeSpan.FromSeconds(15);
        _timer.Tick += async (_, _) => await PollAsync();
        Closed += (_, _) => { _timer.Stop(); _displayTimer.Stop(); _discovery?.Dispose(); };
        _timer.Start();
        _displayTimer.Tick += (_,_) => RefreshMachines();
        _displayTimer.Start();
        Loaded += async (_, _) => { RefreshMachines(); await PollAsync(); };
    }

    private void InitKeyAndServices()
    {
        _key=null;_client=null;_sync=null;
        if (!string.IsNullOrEmpty(_config.ClassroomKeyBase64))
        {
            try { _key = ClassroomKey.FromBase64(_config.ClassroomKeyBase64); } catch { _key = null; }
        }

        if (_key is not null)
        {
            KeyStatus.Text = "Kalit yuklangan";
            KeyStatus.Foreground = (Brush)FindResource("Success");
            _client = new AdminClient(_key);
            _sync = new SyncEngine(_client);
        }
        else
        {
            KeyStatus.Text = "Kalit yuklanmagan";
            KeyStatus.Foreground = (Brush)FindResource("Danger");
        }

        _discovery?.Dispose();
        _discovery = new Discovery(_config.AnnouncePort);
        try { _discovery.Start(); } catch(Exception ex) { SettingsStatus.Text=DashStatus.Text="Discovery unavailable: "+ex.Message; }
        if(_sync?.Intended is {} saved)
        {
            SitesBox.Text=string.Join("\n",saved.Sites);SiteOff.IsChecked=saved.SiteMode==SiteBlockingMode.Off;SiteBlack.IsChecked=saved.SiteMode==SiteBlockingMode.Blacklist;
            InstallChk.IsChecked=saved.InstallBlocking;DownloadChk.IsChecked=saved.DownloadBlocking;
            AppAllowChk.IsChecked=saved.AppAllowListEnabled;AppsBox.Text=string.Join("\n",saved.AllowedApps);
            LibraryBox.Text=string.Join("\n",saved.Library.Select(x=>$"{x.Kind}|{x.Title}|{x.PathOrUrl}|{x.Category}"));
            TestsJsonBox.Text=System.Text.Json.JsonSerializer.Serialize(saved.Tests,new System.Text.Json.JsonSerializerOptions{WriteIndented=true});
        }
        if(!string.IsNullOrEmpty(_sync?.LoadError))SettingsStatus.Text=_sync.LoadError;
    }

    // ---- navigation --------------------------------------------------------
    private void OnNav(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;

        PageDash.Visibility = Visibility.Collapsed;
        PagePush.Visibility = Visibility.Collapsed;
        PageSettings.Visibility = Visibility.Collapsed;

        NavDash.Style = (Style)FindResource("NavBtn");
        NavPush.Style = (Style)FindResource("NavBtn");
        NavSettings.Style = (Style)FindResource("NavBtn");

        btn.Style = (Style)FindResource("NavBtnActive");

        if (btn == NavDash) PageDash.Visibility = Visibility.Visible;
        else if (btn == NavPush) PagePush.Visibility = Visibility.Visible;
        else if (btn == NavSettings) PageSettings.Visibility = Visibility.Visible;
    }

    // ---- dashboard ---------------------------------------------------------
    private void OnAdminResize(object sender, SizeChangedEventArgs e) { if(IsLoaded)RefreshMachines(); }
    private void OnMachineSearch(object sender, TextChangedEventArgs e) { if(IsLoaded)RefreshMachines(); }
    private async void OnRefresh(object sender, RoutedEventArgs e) => await PollAsync();

    private void RefreshMachines()
    {
        if (_discovery is null) return;
        var all = _discovery.Machines.OrderBy(m => m.Name).ThenBy(m=>m.Ip).ToList();
        var query=MachineSearch?.Text?.Trim()??"";
        var machines=all.Where(m=>($"{m.Name} {m.Ip} {m.MachineId}").Contains(query,StringComparison.OrdinalIgnoreCase)).ToList();
        MachinePanel.Children.Clear();

        foreach (var m in machines)
            MachinePanel.Children.Add(BuildCard(m));

        MachineCount.Text = $"{all.Count} ta PC · {all.Count(m=>DateTimeOffset.UtcNow-m.LastSeen<OfflineAfter)} tarmoqda · {all.Count(m=>m.IdentityConflict)} ID ziddiyat";
        if(MachineEmpty is not null) MachineEmpty.Visibility=machines.Count==0?Visibility.Visible:Visibility.Collapsed;
        if(!string.IsNullOrEmpty(_discovery.LastError))DashStatus.Text="Discovery: "+_discovery.LastError;
    }

    private Border BuildCard(MachineInfo m)
    {
        var status = m.StatusText(OfflineAfter);
        var online = status.StartsWith("online", StringComparison.OrdinalIgnoreCase);
        var verified=!m.IdentityConflict && m.Verified && DateTimeOffset.UtcNow-m.LastVerified<TimeSpan.FromSeconds(45);
        var locked = m.ReportedState?.StudentModeActive ?? true;

        var statusBrush = online
            ? (locked ? (Brush)FindResource("Danger") : (Brush)FindResource("Success"))
            : (Brush)FindResource("Warning");
        var statusSoftBrush = online
            ? (locked ? (Brush)FindResource("DangerSoft") : (Brush)FindResource("SuccessSoft"))
            : (Brush)FindResource("WarningSoft");

        var statusLabel = m.IdentityConflict ? "ID ZIDDIYATI" : !online ? "OFFLINE" : !verified ? "UNVERIFIED" : m.Health?.Healthy!=true ? "PARTIAL FAILURE" : locked ? "STUDENT MODE" : "TEACHER · 30 MIN";

        var root = new Grid { Margin = new Thickness(0) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var left = new StackPanel { Orientation = Orientation.Vertical };

        var pillContent = new StackPanel { Orientation = Orientation.Horizontal };
        pillContent.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Style = (Style)FindResource("StatusDot"),
            Fill = statusBrush,
        });
        pillContent.Children.Add(new TextBlock
        {
            Text = statusLabel,
            Style = (Style)FindResource("StatusPillText"),
            Foreground = statusBrush,
        });
        left.Children.Add(new Border
        {
            Style = (Style)FindResource("StatusPill"),
            Background = statusSoftBrush,
            Child = pillContent,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0),
        });

        var info = new StackPanel();
        info.Children.Add(new TextBlock
        {
            Text = $"{m.Name}",
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
        });
        info.Children.Add(new TextBlock
        {
            Text = $"{m.Ip}:{m.Port}  ·  {m.MachineId}" + (m.IdentityConflict?"\nBir ID bir nechta manzilda. Qo‘llanmadagi tiklash bosqichlarini bajaring.":""),
            TextWrapping=TextWrapping.Wrap,
            MaxWidth=450,
            Foreground = (Brush)FindResource("TextMuted"),
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
        });
        left.Children.Add(info);
        Grid.SetColumn(left, 0);
        root.Children.Add(left);

        var actions = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var lockBtn = new Button
        {
            Content = "Cheklash",
            Style = (Style)FindResource("BtnPrimary"),
            Padding = new Thickness(14, 8, 14, 8),
            MinWidth = 80,
        };
        lockBtn.IsEnabled = online && verified && !_busy;
        lockBtn.Click += async (_, _) =>
            await SendOne(m, new NetRequest { Command = NetCommand.EnableStudent });

        var unlockBtn = new Button
        {
            Content = "30 daq. ruxsat",
            Style = (Style)FindResource("BtnSecondary"),
            Padding = new Thickness(14, 8, 14, 8),
            MinWidth = 80,
        };
        unlockBtn.IsEnabled = online && verified && !_busy;
        unlockBtn.Click += async (_, _) =>
            await SendOne(m, new NetRequest { Command = NetCommand.DisableStudent });

        var screenButton = new Button { Content = m.Health?.ScreenLocked==true ? "Ekranni ochish" : "Ekranni qulflash", Style=(Style)FindResource("BtnPrimary"), Padding=new Thickness(12,8,12,8), IsEnabled=online && verified && !_busy };
        screenButton.Click += async (_,_) => await SendOne(m,new NetRequest { Command=m.Health?.ScreenLocked==true?NetCommand.UnlockScreen:NetCommand.LockScreen });
        actions.Children.Add(screenButton);
        actions.Children.Add(lockBtn);
        actions.Children.Add(unlockBtn);
        Grid.SetColumn(actions, 0);
        Grid.SetRow(actions, 1);
        actions.Margin=new Thickness(0,12,0,0);
        root.Children.Add(actions);

        return new Border
        {
            Style = (Style)FindResource("MachineCard"),
            Width = Math.Max(280,(Math.Max(300,MachinePanel.ActualWidth)-24)/(MachinePanel.ActualWidth>=1000?3:MachinePanel.ActualWidth>=680?2:1)-12),
            Margin=new Thickness(0,0,12,12),
            Child = root,
        };
    }

    private async Task SendOne(MachineInfo m, NetRequest req)
    {
        if (_client is null)
        {
            MessageBox.Show("Avval Sozlamalardan sinf kalitini yuklang.", "ClassroomGuard");
            return;
        }
        if(_busy)return;
        _busy=true;RefreshMachines();
        NetResponse resp;
        try{resp=await _client.SendAsync(m,req);SyncEngine.Update(m,resp);}finally{_busy=false;}
        DashStatus.Text = $"{m.Name}: {resp.Message}";
        RefreshMachines();
    }

    private async Task PollAsync()
    {
        RefreshMachines();
        if(_polling || _busy || _sync is null || _discovery is null)return;
        _polling=true;
        try
        {
            await Task.WhenAll(_discovery.Machines.Where(m=>!m.IdentityConflict && DateTimeOffset.UtcNow-m.LastSeen<OfflineAfter).Select(m=>_sync.ReconcileAsync(m)));
            RefreshMachines();
        }
        catch(Exception ex){DashStatus.Text=ex.Message;}
        finally{_polling=false;RefreshMachines();}
    }
    private async void OnScreenLockAll(object sender,RoutedEventArgs e)=>await BulkAsync(NetCommand.LockScreen);
    private async void OnScreenUnlockAll(object sender,RoutedEventArgs e)=>await BulkAsync(NetCommand.UnlockScreen);
    private async void OnLockAll(object sender,RoutedEventArgs e)=>await BulkAsync(NetCommand.EnableStudent);
    private async void OnUnlockAll(object sender,RoutedEventArgs e)=>await BulkAsync(NetCommand.DisableStudent);
    private async Task BulkAsync(NetCommand command)
    {
        if(_busy || _polling || _client is null || _discovery is null)return;
        var machines=_discovery.Machines.Where(m=>!m.IdentityConflict && DateTimeOffset.UtcNow-m.LastSeen<OfflineAfter).ToList();
        if(MessageBox.Show($"Apply {command} to {machines.Count} online PCs? {(command is NetCommand.LockScreen or NetCommand.UnlockScreen ? "Screen lock persists until explicitly released. Offline PCs are not included." : "Teacher restriction unlock expires after 30 minutes or session end.")}","Confirm",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        _busy=true;RefreshMachines();
        try
        {
            using var slots=new SemaphoreSlim(6);
            var results=await Task.WhenAll(machines.Select(async m=>{await slots.WaitAsync();try{var r=await _client.SendAsync(m,new(){Command=command});SyncEngine.Update(m,r);return new SyncEngine.PushResult(m,r.Ok,r.Message);}finally{slots.Release();}}));
            ShowResults(command.ToString(),results);
        }
        finally{_busy=false;RefreshMachines();}
    }
    private async void OnReconcile(object sender,RoutedEventArgs e)=>await PollAsync();

    // ---- push (sites + policies together) ---------------------------------
    private async void OnPushAll(object sender, RoutedEventArgs e)
    {
        if (_sync is null || _discovery is null)
        {
            MessageBox.Show("Avval Sozlamalardan sinf kalitini yuklang.", "ClassroomGuard");
            return;
        }
        if(_busy || _polling)return;
        var state = CurrentIntent();
        state.SiteMode = SiteOff.IsChecked == true ? SiteBlockingMode.Off
                       : SiteWhite.IsChecked == true ? SiteBlockingMode.Whitelist
                       : SiteBlockingMode.Blacklist;
        state.Sites = SitesBox.Text.Split('\n').Select(s => s.Trim())
                        .Where(s => s.Length > 0).ToList();
        state.StudentModeActive = true;
        state.InstallBlocking = InstallChk.IsChecked == true;
        state.DownloadBlocking = DownloadChk.IsChecked == true;
        state.AppAllowListEnabled = AppAllowChk.IsChecked == true;
        state.AllowedApps = AppsBox.Text.Split('\n').Select(s => s.Trim())
                        .Where(s => s.Length > 0).ToList();
        try { state.Library = ParseLibrary(LibraryBox.Text); } catch(Exception ex) { MessageBox.Show(ex.Message); return; }
        state.Tests = ParseTestsJson(TestsJsonBox.Text, out var testsErr);
        if (testsErr is not null)
        {
            MessageBox.Show("Testlar JSON xato:\n" + testsErr, "ClassroomGuard",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            PolicyValidator.Validate(state);
            if(MessageBox.Show("Save this baseline and apply it to online classroom PCs? Offline PCs receive it when they return. This also activates student mode on updated PCs.","Apply policy",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            _sync.SetIntended(state);_busy=true;
            var results=await _sync.PushAllAsync(_discovery.Machines.Where(m=>!m.IdentityConflict && DateTimeOffset.UtcNow-m.LastSeen<OfflineAfter));
            PushStatus.Text=$"Saved. {results.Count(r=>r.Ok)}/{results.Count} online devices applied; offline devices pending.";
            ShowResults("Policy",results);
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"Policy rejected");}
        finally{_busy=false;RefreshMachines();}
    }

    /// <summary>
    /// Parse lines of the form: kind|title|pathOrUrl|category
    /// kind = offline | online. Extra fields ignored; malformed lines skipped.
    /// </summary>
    private static List<LibraryItem> ParseLibrary(string text)
    {
        var list = new List<LibraryItem>();
        foreach (var line in text.Split('\n'))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith('#')) continue;
            var parts = t.Split('|');
            if (parts.Length < 3) throw new ArgumentException("Malformed library row: " + t);
            var kind = parts[0].Trim().ToLowerInvariant();
            if (kind is not ("offline" or "online")) throw new ArgumentException("Invalid library kind: " + kind);
            list.Add(new LibraryItem
            {
                Kind = kind,
                Title = parts[1].Trim(),
                PathOrUrl = parts[2].Trim(),
                Category = parts.Length > 3 ? parts[3].Trim() : "",
            });
        }
        return list;
    }

    private static List<TestPackage> ParseTestsJson(string json, out string? error)
    {
        error = null;
        try
        {
            var text = (json ?? "").Trim();
            if (text.Length == 0) return new List<TestPackage>();
            var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var list = System.Text.Json.JsonSerializer.Deserialize<List<TestPackage>>(text, opts);
            if (list is null) { error = "Bo‘sh yoki noto‘g‘ri JSON."; return new(); }
            foreach (var t in list)
            {
                if (string.IsNullOrWhiteSpace(t.Id))
                    t.Id = Guid.NewGuid().ToString("N")[..8];
            }
            return list;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return new List<TestPackage>();
        }
    }

    private void OnLoadTestsJson(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
            Title = "Testlar JSON faylini tanlang",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            TestsJsonBox.Text = File.ReadAllText(dlg.FileName);
            var list = ParseTestsJson(TestsJsonBox.Text, out var err);
            TestsJsonStatus.Text = err is null
                ? $"{list.Count} ta test yuklandi"
                : "Xato: " + err;
        }
        catch (Exception ex)
        {
            TestsJsonStatus.Text = ex.Message;
        }
    }

    private EnforcementState CurrentIntent() => _sync?.Intended ?? EnforcementState.FailClosedDefault();

    private void ShowResults(string title, IReadOnlyList<SyncEngine.PushResult> results)
    {
        var failures = results.Where(r => !r.Ok).ToList();
        DashStatus.Text = $"{title}: {results.Count - failures.Count}/{results.Count} ok";
        if (failures.Count > 0)
        {
            var msg = string.Join("\n", failures.Select(f => $"{f.Machine.Name} ({f.Machine.Ip}): {f.Message}"));
            MessageBox.Show(msg, $"{title} — {failures.Count} failed");
        }
        RefreshMachines();
    }

    // ---- settings ----------------------------------------------------------
    private void LoadSettingsIntoUi()
    {
        TgToken.Text = _config.TelegramBotToken;
        TgChat.Text = _config.TelegramChatId;
        PortBox.Text = _config.AnnouncePort.ToString();
        if (!string.IsNullOrEmpty(_config.ClassroomKeyBase64) && _config.ClassroomKeyBase64.Length >= 8)
            KeyPreview.Text = "kalit: " + _config.ClassroomKeyBase64[..8] + "…";
    }

    private void OnLoadKey(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Agentdagi classroom.key faylini tanlang" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var b64 = File.ReadAllText(dlg.FileName).Trim();
            _key = ClassroomKey.FromBase64(b64);
            _config.ClassroomKeyBase64 = b64;
            _config.Save();
            InitKeyAndServices();
            KeyPreview.Text = "kalit: " + b64[..Math.Min(8, b64.Length)] + "…";
        }
        catch (Exception ex) { MessageBox.Show("Noto‘g‘ri kalit fayli: " + ex.Message); }
    }

    private void OnGenerateKey(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Yangi sinf kaliti yaratilsinmi?\n\nUni har bir agentga nusxalash kerak:\n%ProgramData%\\ClassroomGuard\\classroom.key",
                "Kalit yaratish", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var save=new SaveFileDialog{Filter="Classroom key|*.key",FileName="classroom.key"};
        if(save.ShowDialog()!=true)return;
        var k = ClassroomKey.Generate();
        File.WriteAllText(save.FileName,k.Base64);
        _config.ClassroomKeyBase64 = k.Base64;
        _config.Save();
        _key = k;
        InitKeyAndServices();
        KeyPreview.Text = "kalit: " + k.Base64[..8] + "…";
        MessageBox.Show("Yangi kalit saqlandi.\nHar bir agentning classroom.key fayliga nusxa ko‘chiring.",
            "Sinf kaliti");
    }

    private void OnShowCode(object sender, RoutedEventArgs e)
    {
        if (_key is null)
        {
            MessageBox.Show("Avval sinf kalitini yuklang.", "ClassroomGuard");
            return;
        }
        var id = UnlockMachineId.Text.Trim();
        if (id.Length == 0)
        {
            MessageBox.Show("Boshqaruv panelidan mashina ID kiriting.", "ClassroomGuard");
            return;
        }
        UnlockCode.Text = Totp.Now(_key.TotpSeed(id));
    }

    private async void OnSaveSettings(object sender, RoutedEventArgs e)
    {
        if(!int.TryParse(PortBox.Text.Trim(),out var validPort) || validPort is <1024 or >65535) { MessageBox.Show("Port must be 1024–65535.");return; }
        _config.TelegramBotToken = TgToken.Text.Trim();
        _config.TelegramChatId = TgChat.Text.Trim();
        if (int.TryParse(PortBox.Text.Trim(), out var p)) _config.AnnouncePort = p;
        _config.Save();
        SettingsStatus.Text = "Saqlandi.";
        InitKeyAndServices();

        if (!string.IsNullOrEmpty(_config.TelegramBotToken))
        {
            var (ok, m) = await _telegram.SendAsync(_config.TelegramBotToken, _config.TelegramChatId,
                "ClassroomGuard admin connected.");
            SettingsStatus.Text = ok ? "Saqlandi. Telegram test yuborildi." : "Saqlandi. Telegram: " + m;
        }
    }
}

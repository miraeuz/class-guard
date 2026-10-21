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
    private IAdminClient? _client;
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
        InitializeDashboard();
        LoadSettingsIntoUi();
        InitKeyAndServices();

        _timer.Interval=TimeSpan.FromSeconds(4);
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
            _client = new AuditedAdminClient(new AdminClient(_key),GetActivityJournal);
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
    private void OnAdminResize(object sender, SizeChangedEventArgs e) { if(IsLoaded){LayoutDashboard();RefreshMachines();} }
    private void OnMachineSearch(object sender, TextChangedEventArgs e) { if(IsLoaded)RefreshMachines(); }
    private async void OnRefresh(object sender, RoutedEventArgs e) => await PollAsync();

    private void RefreshMachines()
    {
        if (_discovery is null) return;
        var all = _discovery.Machines.Where(MatchesGroup).OrderBy(m => m.Name).ThenBy(m=>m.Ip).ToList();
        var query=MachineSearch?.Text?.Trim()??"";
        var machines=all.Where(m=>MatchesGroup(m) && ($"{StudentLabel(m)} {m.Ip} {m.MachineId}").Contains(query,StringComparison.OrdinalIgnoreCase)).ToList();
        RenderMachines(machines);
        MachineCount.Text=$"{all.Count(m=>m.Health?.HelpRequested==true&&IsOnline(m))} ta yordam so‘rovi · {all.Count} ta PC  ·  {all.Count(IsOnline)} tarmoqda  ·  {all.Count(m=>!IsOnline(m))} uzilgan";
        OverviewText.Text=$"{all.Count(IsCoverFresh)} ta ekran yopilganligi xabar qilingan · {all.Count(m=>m.Health?.ScreenLocked==true&&!IsCoverFresh(m))} ta qulf tasdig‘i kutilmoqda · {all.Count(m=>m.IdentityConflict || !string.IsNullOrEmpty(m.LastContactError))} ta muammo";
        MachineEmpty.Visibility=machines.Count==0?Visibility.Visible:Visibility.Collapsed;
        UpdateSelection();UpdateDetails();
        if(!string.IsNullOrEmpty(_discovery.LastError))DashStatus.Text="Discovery: "+_discovery.LastError;
    }

    private Border BuildCard(MachineInfo m)
    {
        var key=EndpointKey(m);
        var ready=CanTarget(m);
        var panel=new StackPanel();
        var title=new CheckBox { Content=StudentLabel(m),IsChecked=_selected.Contains(key),FontWeight=FontWeights.SemiBold,FontSize=16,Margin=new Thickness(0,0,0,12) };
        title.Checked+=(_,_)=>{_selected.Add(key);UpdateSelection();};
        title.Unchecked+=(_,_)=>{_selected.Remove(key);UpdateSelection();};
        panel.Children.Add(title);
        panel.Children.Add(new TextBlock { Text=DescribeStatus(m),TextWrapping=TextWrapping.Wrap,Foreground=(Brush)FindResource(m.IdentityConflict || !string.IsNullOrEmpty(m.LastContactError)?"Danger":m.Health?.ScreenCoverConfirmed==true?"Success":"TextSecondary"),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8) });
        panel.Children.Add(new TextBlock { Text=$"{m.Ip}:{m.Port}",Foreground=(Brush)FindResource("TextMuted"),FontSize=12,TextWrapping=TextWrapping.Wrap });
        if(m.Health?.HelpRequested==true)
        {
            var acknowledge=new Button { Content="✋ Yordamni qabul qilish",Style=(Style)FindResource("BtnSecondary"),IsEnabled=ready&&!_busy };
            acknowledge.Click+=async (_,_)=>await SendOne(m,new(){Command=NetCommand.AcknowledgeHelp});panel.Children.Add(acknowledge);
        }
        if(m.Health?.PausedTests>0)panel.Children.Add(new TextBlock { Text=$"⏸ {m.Health.PausedTests} test pauzada",Margin=new Thickness(0,6,0,0),Foreground=(Brush)FindResource("AccentText") });
        if(m.Health?.LessonId is not null)panel.Children.Add(new TextBlock { Text="Dars: "+m.Health.LessonTitle,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0),Foreground=(Brush)FindResource("TextMuted") });
        var actions=new WrapPanel { Margin=new Thickness(0,12,0,0) };
        var details=new Button { Content="Tafsilotlar",Style=(Style)FindResource("BtnSecondary"),Padding=new Thickness(10,7,10,7) };
        details.Click+=(_,_)=>SelectDetails(m);
        var locked=m.Health?.ScreenLocked==true;
        var screen=new Button { Content=locked?"Ekranni ochish":"Ekranni qulflash",Style=(Style)FindResource("BtnPrimary"),Padding=new Thickness(10,7,10,7),IsEnabled=ready&&!_busy };
        screen.Click+=async (_,_)=>await SendOne(m,new(){Command=locked?NetCommand.UnlockScreen:NetCommand.LockScreen});
        actions.Children.Add(details);actions.Children.Add(screen);panel.Children.Add(actions);
        var restrictions=new WrapPanel();
        foreach(var action in new[]{("Cheklash",NetCommand.EnableStudent),("30 daqiqa ruxsat",NetCommand.DisableStudent)})
        {
            var button=new Button { Content=action.Item1,Style=(Style)FindResource("BtnSecondary"),IsEnabled=ready&&!_busy,Padding=new Thickness(10,6,10,6) };
            button.Click+=async (_,_)=>await SendOne(m,new(){Command=action.Item2});restrictions.Children.Add(button);
        }
        panel.Children.Add(new Expander { Header="Ilova cheklovlari",Content=restrictions,Margin=new Thickness(0,8,0,0) });

        return new Border { Style=(Style)FindResource("MachineCard"),Width=CardWidth(),Margin=new Thickness(0,0,12,12),Child=panel,MinHeight=180 };
    }

    private async Task SendOne(MachineInfo m, NetRequest req)
    {
        if (_client is null)
        {
            MessageBox.Show("Avval Sozlamalardan sinf kalitini yuklang.", "ClassroomGuard");
            return;
        }
        if(_busy || _polling){DashStatus.Text="Joriy tekshiruv tugashini kuting.";return;}
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
    private async Task BulkAsync(NetCommand command,bool selectedOnly=false)
    {
        if(_busy || _polling){DashStatus.Text="Tekshiruv yoki buyruq davom etmoqda. Yakunlanishini kuting.";return;}
        if(_client is null || _discovery is null){DashStatus.Text="Avval Sozlamalarda sinf kalitini yuklang.";return;}
        var requested=_discovery.Machines.Where(m=>selectedOnly?_selected.Contains(EndpointKey(m)):MatchesGroup(m)).ToList();
        var machines=requested.Where(CanTarget).ToList();
        var skipped=requested.Count-machines.Count;
        if(machines.Count==0){DashStatus.Text="Yuborish mumkin bo‘lgan PC yo‘q. Tafsilotlardan ulanish va ro‘yxatdan o‘tishni tekshiring.";return;}
        if(MessageBox.Show($"{(selectedOnly?"Tanlangan":"Barcha")} PC: {machines.Count} ta qabul qiluvchi, {skipped} ta o‘tkazib yuboriladi. Buyruq: {command}. {(command is NetCommand.LockScreen or NetCommand.UnlockScreen ? "Screen lock persists until explicitly released. Offline PCs are not included." : "Only the explicit 30-minute permission command expires automatically. Exam pause persists until resumed.")}","Confirm",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        _busy=true;RefreshMachines();
        try
        {
            using var slots=new SemaphoreSlim(6);
            var results=await Task.WhenAll(machines.Select(async m=>{await slots.WaitAsync();try{var r=await _client.SendAsync(m,new(){Command=command});SyncEngine.Update(m,r);return new SyncEngine.PushResult(m,r.Ok,r.Message);}finally{slots.Release();}}));
            ShowResults(command.ToString(),results);
            DashStatus.Text+=$" · {skipped} ta yuborilmadi";
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

        try
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
        catch(Exception ex){MessageBox.Show("Sozlamalar saqlanmadi: "+ex.Message);}

    }
}

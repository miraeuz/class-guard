using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ClassroomGuard.Core;
using ClassroomGuard.Core.Ipc;

namespace ClassroomGuard.StudentShell;

public partial class MainWindow : Window
{
    private DispatcherTimer? _autoCloseTimer;
    private DispatcherTimer? _connectionTimer;
    private TrayIcon? _tray;
    private bool _reallyExiting;
    private bool _checking;
    private bool _actionBusy;

    public MainWindow()
    {
        UiStrings.Apply(ClassroomGuard.Core.I18n.Loc.Current);
        InitializeComponent();
        ThemeManager.Load();ThemeButton.Content=ThemeManager.Dark?"☀":"☾";
        MachineNameText.Text = $"· {MachineIdentity.Name}";
        ShowPage(PageStarter, animate: false);

        // Auto-start (Task Scheduler, AtLogOn — see scripts/install.ps1) means this
        // window pops up unattended every login. Keep a tray icon alive so
        // "O'qituvchi kirishi" stays reachable after the page auto-closes, instead
        // of the app disappearing until the next reboot.
        Loaded += (_, _) =>
        {
            _tray = new TrayIcon(this);
            StartScreenLock();
            StartConnectionCheck();
            ApplyLanguage(ClassroomGuard.Core.I18n.Loc.Current);
        };
        Closing += OnWindowClosing;
        Application.Current.Exit += (_, _) => { _connectionTimer?.Stop(); _testTimer.Stop(); _tray?.Dispose(); };
    }

    /// <summary>Pings the Agent's local pipe every few seconds and reflects the result in
    /// the header pill, so "why isn't anything working" has an answer on screen instead
    /// of only surfacing as an error after the person tries to do something.</summary>
    private void StartConnectionCheck()
    {
        _ = CheckConnectionOnceAsync();
        _connectionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _connectionTimer.Tick += async (_, _) => await CheckConnectionOnceAsync();
        _connectionTimer.Start();
    }

    private async Task CheckConnectionOnceAsync()
    {
        if(_checking)return;
        _checking=true;
        try
        {
            var resp=await AgentPipeClient.SendAsync(new ControlRequest{Action=ControlAction.GetStatus},timeoutMs:3000);
            if(resp.Ok && resp.Health is not null)
            {
                ApplyScreenLock(resp.Health.ScreenLocked);
                if(resp.Health.ScreenLocked && !string.IsNullOrEmpty(resp.Health.ScreenLockToken))
                    await AgentPipeClient.SendAsync(new ControlRequest { Action=ControlAction.ReportScreenCover,ScreenLockToken=resp.Health.ScreenLockToken,ScreenCovered=_lockWindows.Count>0 && _lockWindows.All(w=>w.CoverageReady),MonitorCount=_lockWindows.Count },timeoutMs:1500);
            }
            ConnectionDot.Fill=(System.Windows.Media.Brush)FindResource(resp.Ok?"Success":"Danger");
            ConnectionText.Text=resp.Ok?"Agent bilan aloqa bor":"Agent topilmadi";
            StudentStatus.Text=resp.Ok?(resp.Health?.Summary??"Holat tekshirilmoqda") : "Aloqa yo‘q — holat noma’lum";
            ModeText.Text=resp.Ok?(resp.State?.StudentModeActive==true?"O‘quv rejimi":"O‘qituvchi rejimi"):"Noma’lum";
            if(resp.Ok && resp.Health is not null){ReflectClassroom(resp.Health);await RefreshAttemptStateAsync();}
            if(resp.Health?.UnlockUntil is {} until)ModeText.Text+=$" · {until.ToLocalTime():HH:mm} gacha";
        }
        finally{_checking=false;}
    }

    /// <summary>Only a genuine Exit (tray menu) or app shutdown actually closes the window; anything
    /// else — auto-close, "Yopish" after unlock, Alt+F4 — hides to the tray instead. Restrictions
    /// themselves live in the Agent service regardless of whether this UI is visible or hidden.</summary>
    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_screenLocked) { e.Cancel=true; return; }
        if (_reallyExiting) return;
        e.Cancel = true;
        Hide();
    }

    /// <summary>Called by the tray icon's "Chiqish" (Exit) item and app shutdown.</summary>
    public void AllowRealClose() => _reallyExiting = true;

    /// <summary>Called by the tray icon's "O'qituvchi kirishi" item / double-click.</summary>
    public void GoToTeacherLogin()
    {
        StopAutoClose();
        ShowTeacherLogin();
    }

    private void ShowPage(UIElement page, bool animate = true)
    {
        PageStarter.Visibility = Visibility.Collapsed;
        PageStudent.Visibility = Visibility.Collapsed;
        PageTeacher.Visibility = Visibility.Collapsed;
        PageUnlocked.Visibility = Visibility.Collapsed;
        PageInstallCode.Visibility = Visibility.Collapsed;
        page.Visibility = Visibility.Visible;

        if (!animate || _reduceMotion)
        {
            page.BeginAnimation(OpacityProperty, null);
            page.Opacity = 1;
            return;
        }
        page.Opacity = 0;
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
        fadeIn.Completed += (_, _) =>
        {
            page.BeginAnimation(OpacityProperty, null);
            page.Opacity = 1;
        };
        page.BeginAnimation(OpacityProperty, fadeIn);
    }

    private void OnToggleTheme(object sender,RoutedEventArgs e)
    {
        try { ThemeManager.Toggle();ThemeButton.Content=ThemeManager.Dark?"☀":"☾";SendRichQuestion();ApplyLearningTheme(); }
        catch(Exception ex) { MessageBox.Show("Ko‘rinish saqlanmadi: "+ex.Message); }
    }

    private bool _reduceMotion;
    private void OnGoToPc(object sender, RoutedEventArgs e)
    {
        StopAutoClose();
        if (_screenLocked) return;
        // UI-only: never send Unlock or change service policy. Timer remains active.
        WindowState = WindowState.Minimized;
    }
    private void OnLayoutSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if(MachineNameText is not null)MachineNameText.Visibility=ActualWidth<1050?Visibility.Collapsed:Visibility.Visible;
        if (SidebarColumn is null || QuickCards is null || RoleCards is null) return;
        SidebarColumn.Width = new GridLength(_sidebarCollapsed ? 76 : (ActualWidth < 1000 ? 190 : 230));
        QuickCards.Columns = ActualWidth < 1120 ? 2 : 4;
        RoleCards.Columns = ActualWidth < 760 ? 1 : 2;
        HomeArtwork.Visibility = ActualWidth < 900 ? Visibility.Collapsed : Visibility.Visible;

        DashboardScroll.Padding = new Thickness(ActualWidth < 900 ? 16 : 28);
    }
    private void OnPasswordKeyState(object sender, RoutedEventArgs e) =>
        CapsLockHint.Visibility = System.Windows.Input.Keyboard.IsKeyToggled(System.Windows.Input.Key.CapsLock) ? Visibility.Visible : Visibility.Collapsed;
    private void OnReduceMotion(object sender, RoutedEventArgs e) =>
        _reduceMotion = (sender as System.Windows.Controls.CheckBox)?.IsChecked == true;
    private async void OnRefreshConnection(object sender, RoutedEventArgs e)
    {
        await CheckConnectionOnceAsync();
        UtilityStatus.Text = ConnectionText.Text;
    }
    private void OnCopyPc(object sender, RoutedEventArgs e)
    {
        try { System.Windows.Clipboard.SetText(MachineIdentity.Name); UtilityStatus.Text = "PC nomi nusxalandi."; }
        catch { UtilityStatus.Text = "Nusxalab bo‘lmadi. Qayta urinib ko‘ring."; }
    }

    private void OnTeacherLoginClick(object sender, RoutedEventArgs e) => ShowTeacherLogin();

    private void ShowTeacherLogin()
    {
        StopAutoClose();
        LoginError.Text = "";
        UserBox.Text = "teacher";
        PassBox.Password = "";
        ShowPage(PageTeacher);
        PassBox.Focus();
    }

    private void OnShowTeacherLogin(object sender, RoutedEventArgs e)
    {
        // User explicitly wants teacher UI — cancel auto-close
        StopAutoClose();
        ShowTeacherLogin();
    }

    private void OnBackToStarter(object sender, RoutedEventArgs e)
    {
        StopAutoClose();
        ShowPage(PageStarter);
    }

    private async void OnContinue(object sender, RoutedEventArgs e)
    {
        if(_actionBusy)return;_actionBusy=true;
        StarterStatus.Text = "Student Mode yoqilmoqda…";
        var resp = await AgentPipeClient.SendAsync(new ControlRequest
        {
            Action = ControlAction.EnableStudentMode,
        });

        _actionBusy=false;
        if (resp.Ok)
        {
            // Cheklovlar AGENT da qoladi. Dashboard yashirilganda ham Agent cheklovlari saqlanadi.
            StudentStatus.Text = "Himoya faol";
            var pc = MachineIdentity.Name;
            if (string.IsNullOrWhiteSpace(pc)) pc = Environment.MachineName;
            PcNumberWatermark.Text = pc;
            PcNumberPill.Text = pc;
            SettingsPcNumber.Text = pc;
            GreetingText.Text = GetGreeting();
            ShowDashboardSection("Home");
            ShowPage(PageStudent);
            await ResumeAttemptAsync();
            await CheckConnectionOnceAsync();
            // Dashboard can be minimized independently of service enforcement.
        }
        else
        {
            StarterStatus.Text = resp.Message;
            MessageBox.Show(resp.Message, "ClassroomGuard", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string GetGreeting()
    {
        var h = DateTime.Now.Hour;
        if (h < 12) return ClassroomGuard.Core.I18n.Loc.T("home.greeting.morning");
        if (h < 18) return ClassroomGuard.Core.I18n.Loc.T("home.greeting.day");
        return ClassroomGuard.Core.I18n.Loc.T("home.greeting.evening");
    }

    private void ShowDashboardSection(string section)
    {
        DashboardScroll.Visibility = section == "Togarak" ? Visibility.Collapsed : Visibility.Visible;
        ContentTogarak.Visibility = section == "Togarak" ? Visibility.Visible : Visibility.Collapsed;
        if(section == "Togarak") _ = EnsureLearningAsync();
        ContentHome.Visibility = section == "Home" ? Visibility.Visible : Visibility.Collapsed;
        ContentLibrary.Visibility = section == "Library" ? Visibility.Visible : Visibility.Collapsed;
        ContentSites.Visibility = section == "Sites" ? Visibility.Visible : Visibility.Collapsed;
        ContentApps.Visibility = section == "Apps" ? Visibility.Visible : Visibility.Collapsed;
        ContentTests.Visibility = section == "Tests" ? Visibility.Visible : Visibility.Collapsed;
        ContentSettings.Visibility = section == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        foreach(var button in new[]{NavHome,NavLibrary,NavSites,NavApps,NavTests,NavTogarak,NavSettings})
            button.Background=(System.Windows.Media.Brush)FindResource((string)button.Tag==section?"AccentSoft":"BgCard");
    }

    private void OnNavClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string tag)
        {
            ShowDashboardSection(tag);
            if (tag == "Library") _ = RefreshLibraryAsync();
            if (tag == "Tests" && _attempt is null) _ = RefreshTestsAsync();
            if (tag == "Sites") _ = RefreshSitesAsync();
            if (tag == "Apps") _ = RefreshAppsAsync();
        }
    }

    private void OnQuickLibrary(object sender, RoutedEventArgs e)
    {
        ShowDashboardSection("Library");
        _ = RefreshLibraryAsync();
    }
    private void OnQuickSites(object sender, RoutedEventArgs e)
    {
        ShowDashboardSection("Sites");
        _ = RefreshSitesAsync();
    }
    private void OnQuickApps(object sender, RoutedEventArgs e)
    {
        ShowDashboardSection("Apps");
        _ = RefreshAppsAsync();
    }
    private void OnQuickTests(object sender, RoutedEventArgs e)
    {
        ShowDashboardSection("Tests");
        if(_attempt is null)_ = RefreshTestsAsync();
    }

    private async void OnRefreshLibrary(object sender, RoutedEventArgs e) => await RefreshLibraryAsync();

    private async Task RefreshLibraryAsync()
    {
        var resp = await AgentPipeClient.SendAsync(new ControlRequest { Action = ControlAction.GetStatus }, timeoutMs: 3000);
        var items = resp.State?.Library ?? new List<ClassroomGuard.Core.Models.LibraryItem>();
        var offline = items.Where(x => string.Equals(x.Kind, "offline", StringComparison.OrdinalIgnoreCase)).ToList();
        var online = items.Where(x => string.Equals(x.Kind, "online", StringComparison.OrdinalIgnoreCase)).ToList();
        LibraryOfflineList.ItemsSource = offline;
        LibraryOnlineList.ItemsSource = online;
        LibraryOfflineEmpty.Visibility = offline.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        LibraryOnlineEmpty.Visibility = online.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOpenLibraryItem(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not ClassroomGuard.Core.Models.LibraryItem item)
            return;
        if (!ClassroomGuard.Core.Models.PolicyValidator.LibraryTargetAllowed(item.Kind,item.PathOrUrl)) { MessageBox.Show("Faqat HTTPS havola yoki PDF fayl ochiladi."); return; }
        if(item.Kind=="offline" && !System.IO.File.Exists(item.PathOrUrl)){MessageBox.Show("PDF bu kompyuterga hali o‘rnatilmagan. O‘qituvchiga ayting.");return;}

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = item.PathOrUrl,
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ochib bo‘lmadi:\n{item.PathOrUrl}\n\n{ex.Message}", "Kutubxona",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnRefreshSites(object sender, RoutedEventArgs e) => await RefreshSitesAsync();
    private async void OnRefreshApps(object sender, RoutedEventArgs e) => await RefreshAppsAsync();

    private async Task RefreshSitesAsync()
    {
        var resp = await AgentPipeClient.SendAsync(new ControlRequest { Action = ControlAction.GetStatus }, timeoutMs: 3000);
        var state = resp.State;
        var sites = state?.Sites ?? new List<string>();
        var mode = state?.SiteMode.ToString() ?? "Off";
        SitesModeText.Text = mode switch
        {
            "Blacklist" => "Rejim: Qora ro‘yxat (bloklangan domenlar)",
            "Whitelist" => "Rejim: Oq ro‘yxat (hosts orqali to‘liq emas)",
            _ => "Rejim: O‘chiq",
        };
        SitesListBox.ItemsSource = sites;
        SitesEmptyHint.Visibility = sites.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task RefreshAppsAsync()
    {
        var resp = await AgentPipeClient.SendAsync(new ControlRequest { Action = ControlAction.GetStatus }, timeoutMs: 3000);
        var state = resp.State;
        var apps = state?.AllowedApps ?? new List<string>();
        var on = state?.StudentModeActive==true && state.AppAllowListEnabled && apps.Count>0 && !state.Overrides.GetValueOrDefault("apps");
        AppsListStatus.Text = on
            ? $"Allow-list YOQILGAN — {apps.Count} ta dastur"
            : "Allow-list O‘CHIRILGAN — barcha dasturlar (install/download cheklovi alohida)";
        AppsListBox.ItemsSource = apps;
    }

    // ---- Service-owned assessments ------------------------------------------
    private List<ClassroomGuard.Core.Models.TestPackage> _allTests=new();
    private ClassroomGuard.Core.Models.TestPackage? _currentTest;
    private ClassroomGuard.Core.Models.AttemptView? _attempt;
    private int _qIndex;
    private int[] _answers=Array.Empty<int>();
    private readonly HashSet<int> _flagged=new();
    private bool _rendering;
    private bool _finishing;
    private readonly SemaphoreSlim _attemptGate=new(1,1);
    private readonly DispatcherTimer _testTimer=new(){Interval=TimeSpan.FromSeconds(1)};
    private bool _testTimerStarted;
    private async void OnRefreshTests(object sender,RoutedEventArgs e)
    {
        if(_attempt is not null){TestSaveStatus.Text="Test davom etmoqda; yangilash bekor qilindi.";return;}
        await RefreshTestsAsync();
    }
    private async Task RefreshTestsAsync()
    {
        var r=await AgentPipeClient.SendAsync(new(){Action=ControlAction.GetStatus});
        if(!r.Ok){TestsEmptyHint.Text=r.Message;TestsEmptyHint.Visibility=Visibility.Visible;return;}
        _allTests=r.State?.Tests??new();
        TestClassBox.ItemsSource=new[]{"—"}.Concat(_allTests.Select(t=>t.ClassName).Distinct().OrderBy(x=>x)).ToList();
        TestSubjectBox.ItemsSource=new[]{"—"}.Concat(_allTests.Select(t=>t.Subject).Distinct().OrderBy(x=>x)).ToList();
        TestClassBox.SelectedIndex=0;TestSubjectBox.SelectedIndex=0;ApplyTestFilter();ShowTestPanel("picker");
    }
    private void OnTestFilterChanged(object sender,System.Windows.Controls.SelectionChangedEventArgs e)=>ApplyTestFilter();
    private void ApplyTestFilter()
    {
        var cls=TestClassBox.SelectedItem as string;var sub=TestSubjectBox.SelectedItem as string;
        var items=_allTests.Where(t=>(string.IsNullOrEmpty(cls)||cls=="—"||t.ClassName==cls)&&(string.IsNullOrEmpty(sub)||sub=="—"||t.Subject==sub)).ToList();
        TestListBox.ItemsSource=items.Select(t=>$"{t.Title}  ·  {t.Questions.Count} savol  ·  {(t.TimeLimitMinutes>0?t.TimeLimitMinutes+" daqiqa":"vaqt cheklanmagan")}").ToList();TestListBox.Tag=items;
        TestsEmptyHint.Visibility=items.Count==0?Visibility.Visible:Visibility.Collapsed;
    }
    private async void OnStartTest(object sender,RoutedEventArgs e)
    {
        if(_actionBusy)return;
        if(TestListBox.Tag is not List<ClassroomGuard.Core.Models.TestPackage> items || TestListBox.SelectedIndex<0)return;
        _actionBusy=true;
        var response=await AgentPipeClient.SendAsync(new(){Action=ControlAction.StartTest,TestId=items[TestListBox.SelectedIndex].Id});
        _actionBusy=false;
        if(!response.Ok || response.Attempt is null){MessageBox.Show(response.Message);return;}
        LoadAttempt(response.Attempt);
    }
    private async Task ResumeAttemptAsync()
    {
        var response=await AgentPipeClient.SendAsync(new(){Action=ControlAction.ResumeTest});
        if(response.Ok && response.Attempt is {Finished:false} attempt)LoadAttempt(attempt);
    }
    private void LoadAttempt(ClassroomGuard.Core.Models.AttemptView view)
    {
        if(_attempt?.Id!=view.Id)_flagged.Clear();
        _attempt=view;_currentTest=view.Test;_answers=(int[])view.Answers.Clone();_qIndex=0;
        ShowDashboardSection("Tests");TestTakingTitle.Text=view.Test.Title;TestTakingMeta.Text=$"{view.Test.ClassName} · {view.Test.Subject}";
        if(view.Finished){ShowResult(view);return;}
        ShowTestPanel("taking");RenderQuestion();ApplyPauseUi();
        if(!_testTimerStarted){_testTimer.Tick+=async (_,_)=>await UpdateDeadlineAsync();_testTimerStarted=true;}
        _testTimer.Start();
    }
    private async Task UpdateDeadlineAsync()
    {
        if(_attempt is null || _attempt.Finished)return;
        if(_attempt.Paused){ApplyPauseUi();return;}
        if(_attempt.Deadline is {} end)
        {
            var left=end-DateTimeOffset.UtcNow;
            var seconds=Math.Max(0,(int)Math.Ceiling(left.TotalSeconds));
            TestTimerText.Text=seconds>0?$"Qolgan vaqt: {seconds/60:00}:{seconds%60:00}":"Vaqt tugadi";
            TestTimerText.Foreground=(System.Windows.Media.Brush)FindResource(seconds<=60?"Danger":"Text");
            if(left<=TimeSpan.Zero && !_finishing)await FinishAsync(false);
        }
        else TestTimerText.Text="Vaqt chegarasi yo‘q";
    }
    private void RenderQuestion()
    {
        if(_currentTest is null || _attempt?.Finished==true)return;
        _rendering=true;
        try
        {
            var q=_currentTest.Questions[_qIndex];TestQuestionText.Text=$"{_qIndex+1}. {q.Text}";TestOptionsPanel.Children.Clear();
            for(var i=0;i<q.Options.Count;i++)
            {
                var option=i;var question=_qIndex;
                var rb=new System.Windows.Controls.RadioButton{Content=new System.Windows.Controls.TextBlock{Text=$"{(char)(65+i)}.  {q.Options[i]}",TextWrapping=TextWrapping.Wrap,MaxWidth=650},GroupName="Answer",Margin=new Thickness(0,0,0,12),FontSize=15,IsChecked=_answers[question]==option};
                rb.Checked+=async(_,_)=>{if(_rendering || _attempt?.Paused==true)return;_answers[question]=option;UpdateQuestionPalette();await SaveAnswersAsync();};
                TestOptionsPanel.Children.Add(rb);
            }
            _ = ShowRichQuestionAsync();
            UpdateQuestionPalette();
            TestPreviousButton.IsEnabled=_qIndex>0;TestNextButton.IsEnabled=_qIndex<_currentTest.Questions.Count-1;
            TestProgressText.Text=$"Savol {_qIndex+1}/{_currentTest.Questions.Count} · Javob berilgan: {_answers.Count(x=>x>=0)}";
        }
        finally{_rendering=false;}
    }
    private async Task SaveAnswersAsync()
    {
        if(_attempt is null || _attempt.Paused)return;var id=_attempt.Id;var answers=(int[])_answers.Clone();
        await _attemptGate.WaitAsync();
        try
        {
            var r=await AgentPipeClient.SendAsync(new(){Action=ControlAction.SaveAnswers,AttemptId=id,Answers=answers});
            if(_attempt?.Id!=id)return;
            TestSaveStatus.Text=r.Ok?"Javoblar saqlandi":"Saqlanmadi: "+r.Message;
            if(r.Ok && r.Attempt is {Finished:true} finished)ShowResult(finished);
        }
        finally{_attemptGate.Release();}
    }
    private void OnTestPrev(object sender,RoutedEventArgs e){if(_qIndex>0){_qIndex--;RenderQuestion();}}
    private void OnTestNext(object sender,RoutedEventArgs e){if(_currentTest is not null && _qIndex<_currentTest.Questions.Count-1){_qIndex++;RenderQuestion();}}
    private async void OnTestFinish(object sender,RoutedEventArgs e)=>await FinishAsync(true);
    private async Task FinishAsync(bool confirm)
    {
        if(_attempt is null || _finishing || _attempt.Paused)return;
        if(confirm && MessageBox.Show($"Testni yakunlaysizmi? Javobsiz: {_answers.Count(x=>x<0)}","Tasdiqlash",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        _finishing=true;await _attemptGate.WaitAsync();
        try
        {
            var r=await AgentPipeClient.SendAsync(new(){Action=ControlAction.FinishTest,AttemptId=_attempt.Id,Answers=(int[])_answers.Clone()});
            if(r.Ok && r.Attempt is not null)ShowResult(r.Attempt);else TestSaveStatus.Text="Natija saqlanmadi: "+r.Message;
        }
        finally{_attemptGate.Release();_finishing=false;}
    }
    private void ShowResult(ClassroomGuard.Core.Models.AttemptView view)
    {
        _attempt=view;_testTimer.Stop();
        var total=view.Test.Questions.Count;TestResultTitle.Text=view.Test.Title;TestResultScore.Text=$"{100.0*view.Correct/Math.Max(1,total):0.0}%";
        TestResultDetail.Text=$"To‘g‘ri: {view.Correct}/{total}\nAgentda saqlandi. Telegram: {view.Delivery}\nNatija ID: {view.Id}";
        BuildTestReview(view);
        ShowTestPanel("result");
    }
    private async void OnTestBackToList(object sender,RoutedEventArgs e){_attempt=null;_currentTest=null;await RefreshTestsAsync();}
    private void ShowTestPanel(string which)
    {
        TestPickerPanel.Visibility=which=="picker"?Visibility.Visible:Visibility.Collapsed;
        TestTakingPanel.Visibility=which=="taking"?Visibility.Visible:Visibility.Collapsed;
        TestResultPanel.Visibility=which=="result"?Visibility.Visible:Visibility.Collapsed;
    }

    public async void GoToDashboard()
    {
        ShowPage(PageStudent);ShowDashboardSection("Home");
        PcNumberWatermark.Text=PcNumberPill.Text=SettingsPcNumber.Text=MachineIdentity.Name;
        GreetingText.Text=GetGreeting();await CheckConnectionOnceAsync();await ResumeAttemptAsync();
    }
    private async void OnChangePassword(object sender,RoutedEventArgs e)
    {
        if(_actionBusy)return;
        var next=Prompt.Ask(this,"Parolni o‘zgartirish","Yangi parol (kamida 12 belgi):",true);
        if(string.IsNullOrEmpty(next))return;
        var confirm=Prompt.Ask(this,"Tasdiqlash","Yangi parolni qayta kiriting:",true);
        if(next!=confirm){LoginError.Text="Parollar bir xil emas.";return;}
        _actionBusy=true;
        var r=await AgentPipeClient.SendAsync(new(){Action=ControlAction.ChangePassword,Username=UserBox.Text.Trim(),Password=PassBox.Password,NewPassword=next});
        _actionBusy=false;PassBox.Password="";LoginError.Text=r.Message;
    }

    private void OnLangUz(object sender, RoutedEventArgs e) => ApplyLanguage("uz");
    private void OnLangRu(object sender, RoutedEventArgs e) => ApplyLanguage("ru");
    private void OnLangEn(object sender, RoutedEventArgs e) => ApplyLanguage("en");

    private void ApplyLanguage(string lang)
    {
        ClassroomGuard.Core.I18n.Loc.SetLanguage(lang);
        UiStrings.Apply(lang);
        var L = ClassroomGuard.Core.I18n.Loc.T;

        if (GreetingText is not null) GreetingText.Text = GetGreeting();
        _ = CheckConnectionOnceAsync();
        if (LibraryOfflineEmpty is not null) LibraryOfflineEmpty.Text = L("library.empty.offline");
        if (LibraryOnlineEmpty is not null) LibraryOnlineEmpty.Text = L("library.empty.online");
        if (TestsEmptyHint is not null) TestsEmptyHint.Text = L("tests.empty");

    }

    private void StartAutoClose(TimeSpan delay)
    {
        StopAutoClose();
        _autoCloseTimer = new DispatcherTimer { Interval = delay };
        _autoCloseTimer.Tick += (_, _) =>
        {
            StopAutoClose();
            // Oyna yashiriladi (tray'da qoladi) — desktop ochiq; agent cheklovlarni ushlab turadi.
            Hide();
        };
        _autoCloseTimer.Start();
    }

    private void StopAutoClose()
    {
        if (_autoCloseTimer is null) return;
        _autoCloseTimer.Stop();
        _autoCloseTimer = null;
    }

    private async void OnTeacherUnlock(object sender, RoutedEventArgs e)
    {
        if(_actionBusy)return;
        LoginError.Text = "";
        var user = string.IsNullOrWhiteSpace(UserBox.Text) ? "teacher" : UserBox.Text.Trim();
        var secret = PassBox.Password;

        if (string.IsNullOrEmpty(secret))
        {
            LoginError.Text = "Parol yoki 6 xonali kod kiriting.";
            return;
        }

        _actionBusy=true;
        var resp = await AgentPipeClient.SendAsync(new ControlRequest
        {
            Action = ControlAction.DisableStudentMode,
            Username = user,
            Password = secret,
        });

        _actionBusy=false;
        if (resp.Ok)
        {
            UnlockedStatus.Text = resp.Message;
            PassBox.Password = "";
            ShowPage(PageUnlocked);
        }
        else if (resp.MustChangePassword)
        {
            LoginError.Text = resp.Message + " Avval standart parolni o‘zgartiring (cgctl passwd).";
        }
        else
        {
            LoginError.Text = resp.Message;
        }
    }

    private async void OnRemoveApp(object sender, RoutedEventArgs e)
    {
        var secret = PassBox.Password;
        if (string.IsNullOrWhiteSpace(secret))
        {
            LoginError.Text = "Parol yoki kodni kiriting, keyin Xavfsiz o‘chirishni bosing.";
            return;
        }

        var confirm = MessageBox.Show(
            "Barcha himoyalar bekor qilinadi va dastur olib tashlashga tayyorlanadi.\n\nDavom etasizmi?",
            "Xavfsiz o‘chirish",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        var user = string.IsNullOrWhiteSpace(UserBox.Text) ? "teacher" : UserBox.Text.Trim();
        var resp = await AgentPipeClient.SendAsync(new ControlRequest
        {
            Action = ControlAction.Uninstall,
            Username = user,
            Password = secret,
            NewPassword = secret,
        });

        MessageBox.Show(resp.Message + (resp.Ok
                ? "\n\nXizmatni to‘liq olib tashlash uchun scripts\\uninstall.ps1 ni (Administrator) ishga tushiring."
                : ""),
            "Dasturni olib tashlash");

        if (resp.Ok)
        {
            UnlockedStatus.Text = "Cheklovlar olib tashlandi. uninstall.ps1 bilan yakunlang.";
            ShowPage(PageUnlocked);
        }
        else
        {
            LoginError.Text = resp.Message;
        }
    }

    private void OnCloseShell(object sender, RoutedEventArgs e)
    {
        StopAutoClose();
        // Cheklovlar o‘chirilgan holda yashiriladi (tray orqali qayta ochish mumkin).
        Hide();
    }

    private async void OnApproveInstall(object sender, RoutedEventArgs e)
    {
        InstallCodeError.Text = "";
        var code = InstallCodeBox.Text.Trim();
        if (code.Length == 0)
        {
            InstallCodeError.Text = "Ustozdan olgan kodni kiriting.";
            return;
        }

        var resp = await AgentPipeClient.SendAsync(new ControlRequest
        {
            Action = ControlAction.ApproveInstall,
            Code = code,
        });

        if (resp.Ok)
        {
            InstallCodeBox.Text = "";
            MessageBox.Show(resp.Message, "O'rnatishga ruxsat berildi",
                MessageBoxButton.OK, MessageBoxImage.Information);
            ShowPage(PageStarter);
        }
        else
        {
            InstallCodeError.Text = resp.Message;
        }
    }

    private void OnShowInstallCode(object sender, RoutedEventArgs e)
    {
        StopAutoClose();
        InstallCodeError.Text = "";
        InstallCodeBox.Text = "";
        ShowPage(PageInstallCode);
    }
}

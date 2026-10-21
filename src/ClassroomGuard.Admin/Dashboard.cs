using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClassroomGuard.Admin.Services;
using ClassroomGuard.Core.Net;
namespace ClassroomGuard.Admin;
public partial class MainWindow
{
    private DashboardPreferences _layout=new();
    private readonly HashSet<string> _selected=new();
    private string? _detailKey;
    private string _renderKey="";
    private bool _dashboardReady;
    private static string EndpointKey(MachineInfo m)=>DashboardTargeting.EndpointKey(m);
    private static bool IsOnline(MachineInfo m)=>DateTimeOffset.UtcNow-m.LastSeen<OfflineAfter;
    private bool CanTarget(MachineInfo m)=>DashboardTargeting.CanTarget(m,DateTimeOffset.UtcNow);
    private static bool IsCoverFresh(MachineInfo m)=>m.Verified && DateTimeOffset.UtcNow>=m.LastVerified && DateTimeOffset.UtcNow-m.LastVerified<TimeSpan.FromSeconds(8) && m.Health?.ScreenLocked==true && m.Health.ScreenCoverConfirmed && m.Health.ScreenCoverAgeSeconds+(DateTimeOffset.UtcNow-m.LastVerified).TotalSeconds<=8;
    private string DescribeStatus(MachineInfo m)
    {
        if(m.IdentityConflict)return "ID ziddiyati · buyruqlar bloklangan";
        if(!IsOnline(m))return "Aloqa uzilgan · oxirgi holat";
        if(!string.IsNullOrEmpty(m.LastContactError))return "Ulanish / buyruq xatosi";
        if(!CanTarget(m))return "Tekshirish kutilmoqda";
        if(m.Health?.ScreenLocked==true)return IsCoverFresh(m)?$"Ekran yopilgan · {m.Health.ScreenMonitorCount} monitor":"Qulf saqlangan · ekran tasdig‘i yo‘q";
        return m.ReportedState?.StudentModeActive==true?"Ekran ochiq · ilova cheklovlari faol":"Ekran ochiq · vaqtinchalik ruxsat";
    }
    private void InitializeDashboard()
    {
        _layout=DashboardPreferences.Load();SeatingToggle.IsChecked=_layout.Seating;SeatColumns.SelectedIndex=_layout.Columns-2;
        ThemeManager.Load();ThemeButton.Content=ThemeManager.Dark?"☀ Kun":"☾ Tun";
        PopulateGroups();_dashboardReady=true;
        Loaded+=(_,_)=>LayoutDashboard();
    }
    private void LayoutDashboard()
    {
        var wide=ActualWidth>=1280;
        DetailsColumn.Width=new GridLength(wide?280:0);
        Grid.SetColumn(DetailsPanel,wide?1:0);Grid.SetRow(DetailsPanel,wide?0:1);
        MachineHost.Margin=new Thickness(0,0,wide?16:0,wide?0:12);
    }
    private void OnToggleTheme(object sender,RoutedEventArgs e)
    {
        try { ThemeManager.Toggle();ThemeButton.Content=ThemeManager.Dark?"☀ Kun":"☾ Tun";_renderKey="";RefreshMachines(); }
        catch(Exception ex){DashStatus.Text="Ko‘rinish saqlanmadi: "+ex.Message;}
    }
    private void OnMachineAreaResize(object sender,SizeChangedEventArgs e){if(IsLoaded && Math.Abs(e.NewSize.Width-e.PreviousSize.Width)>1)RefreshMachines();}
    private double CardWidth()
    {
        var width=Math.Max(240,MachineHost.ActualWidth);
        if(_layout.Seating)return Math.Max(220,Math.Floor(width/_layout.Columns)-12);
        return Math.Max(228,Math.Floor(width/Math.Max(1,(int)(width/260)))-12);
    }
    private void RenderMachines(List<MachineInfo> machines)
    {
        var signature=$"{Math.Round(CardWidth())}|{_busy}|{_layout.Seating}|{_layout.Columns}|"+string.Join(";",machines.Select(m=>$"{EndpointKey(m)}:{StudentLabel(m)}:{DescribeStatus(m)}:{m.Health?.HelpRequested}:{m.Health?.PausedTests}:{m.Health?.LessonId}"))+string.Join(";",_layout.Seats.OrderBy(x=>x.Key));
        if(signature==_renderKey)return;
        _renderKey=signature;MachinePanel.Children.Clear();
        TeacherDesk.Visibility=_layout.Seating?Visibility.Visible:Visibility.Collapsed;
        MachinePanel.Width=_layout.Seating?(CardWidth()+12)*_layout.Columns:Math.Max(240,MachineHost.ActualWidth);
        if(_layout.Seating)
        {
            var assigned=machines.Where(m=>!m.IdentityConflict && _layout.Seats.ContainsKey(m.MachineId)).GroupBy(m=>_layout.Seats[m.MachineId]).ToDictionary(g=>g.Key,g=>g.OrderByDescending(m=>m.LastSeen).First());
            var last=Math.Max(_layout.Columns,_layout.Seats.Values.DefaultIfEmpty(0).Max());
            last=(int)Math.Ceiling((double)last/_layout.Columns)*_layout.Columns;
            for(var seat=1;seat<=last;seat++)
            {
                Border card;
                if(assigned.TryGetValue(seat,out var machine))card=BuildCard(machine);
                else card=new Border { Width=CardWidth(),MinHeight=180,Margin=new Thickness(0,0,12,12),Background=(Brush)FindResource("BgInput"),CornerRadius=new CornerRadius(16),Child=new TextBlock { Text="Bo‘sh / ko‘rsatilmagan",HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Foreground=(Brush)FindResource("TextMuted") } };
                if(!assigned.ContainsKey(seat))
                {
                    var reserved=_layout.Seats.FirstOrDefault(x=>x.Value==seat).Key;
                    if(reserved is not null)
                    {
                        var placeholder=new StackPanel { Margin=new Thickness(12),VerticalAlignment=VerticalAlignment.Center };
                        placeholder.Children.Add(new TextBlock { Text="Ajratilgan joy · PC ko‘rinmayapti",TextWrapping=TextWrapping.Wrap,Foreground=(Brush)FindResource("TextMuted") });
                        var clear=new Button { Content="Joyni bo‘shatish",Style=(Style)FindResource("BtnSecondary"),Margin=new Thickness(0,12,0,0) };
                        clear.Click+=(_,_)=> { if(MessageBox.Show("Faqat o‘rindiq biriktirilishi o‘chirilsinmi? PC cheklovlari o‘zgarmaydi.","Joyni bo‘shatish",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;_layout.Seats.Remove(reserved);SaveLayout();RefreshMachines(); };
                        placeholder.Children.Add(clear);card.Child=placeholder;
                    }
                }
                var cell=new StackPanel();cell.Children.Add(new TextBlock { Text=$"JOY {seat:00}",FontSize=11,FontWeight=FontWeights.SemiBold,Margin=new Thickness(4,0,0,6),Foreground=(Brush)FindResource("TextMuted") });cell.Children.Add(card);MachinePanel.Children.Add(cell);
            }
            foreach(var m in machines.Where(m=>m.IdentityConflict || !_layout.Seats.ContainsKey(m.MachineId)))MachinePanel.Children.Add(BuildCard(m));
        }
        else foreach(var m in machines)MachinePanel.Children.Add(BuildCard(m));
    }
    private void OnViewChanged(object sender,RoutedEventArgs e)
    {
        if(!_dashboardReady)return;_layout.Seating=SeatingToggle.IsChecked==true;SaveLayout();RefreshMachines();
    }
    private void OnColumnsChanged(object sender,SelectionChangedEventArgs e)
    {
        if(!_dashboardReady)return;_layout.Columns=SeatColumns.SelectedIndex+2;SaveLayout();RefreshMachines();
    }
    private void SaveLayout(){try{_layout.Save();SeatStatus.Text="Joylashuv saqlandi.";}catch(Exception ex){SeatStatus.Text="Saqlash xatosi: "+ex.Message;}}
    private void OnSelectVisible(object sender,RoutedEventArgs e)
    {
        var query=MachineSearch.Text.Trim();
        foreach(var m in _discovery?.Machines??Array.Empty<MachineInfo>())if(MatchesGroup(m) && ($"{StudentLabel(m)} {m.Ip} {m.MachineId}").Contains(query,StringComparison.OrdinalIgnoreCase))_selected.Add(EndpointKey(m));
        _renderKey="";RefreshMachines();
    }
    private void OnClearSelection(object sender,RoutedEventArgs e){_selected.Clear();_renderKey="";RefreshMachines();}
    private void UpdateSelection()
    {
        var all=_discovery?.Machines.ToList()??new();
        _selected.RemoveWhere(key=>!all.Any(m=>EndpointKey(m)==key));
        var ready=all.Count(m=>_selected.Contains(EndpointKey(m))&&CanTarget(m));
        SelectionText.Text=$"{_selected.Count} ta tanlangan · {ready} ta tayyor";
        SelectedLockButton.IsEnabled=SelectedUnlockButton.IsEnabled=ready>0&&!_busy&&!_polling;
        SelectedLockButton.Content=$"{ready} ta ekranni qulflash";SelectedUnlockButton.Content=$"{ready} ta ekranni ochish";
    }
    private async void OnLockSelected(object sender,RoutedEventArgs e)=>await BulkAsync(NetCommand.LockScreen,true);
    private async void OnRestrictSelected(object sender,RoutedEventArgs e)=>await BulkAsync(NetCommand.EnableStudent,true);
    private async void OnPermitSelected(object sender,RoutedEventArgs e)=>await BulkAsync(NetCommand.DisableStudent,true);
    private async void OnUnlockSelected(object sender,RoutedEventArgs e)=>await BulkAsync(NetCommand.UnlockScreen,true);
    private void SelectDetails(MachineInfo m){_detailKey=EndpointKey(m);SeatNumberBox.Text=_layout.Seats.TryGetValue(m.MachineId,out var seat)?seat.ToString():"";SeatStatus.Text="";StudentNameBox.Text=_layout.Names.GetValueOrDefault(m.MachineId)??"";StudentGroupBox.Text=_layout.Groups.GetValueOrDefault(m.MachineId)??"";UpdateDetails();}
    private MachineInfo? DetailMachine()=>_discovery?.Machines.FirstOrDefault(m=>EndpointKey(m)==_detailKey);
    private void UpdateDetails()
    {
        var m=DetailMachine();if(m is null)return;
        DetailTitle.Text=StudentLabel(m);
        var advice=m.IdentityConflict?"Bir xil ID topildi. UPDATE-NETWORK-TESTS.md dagi ID tiklash yo‘lidan foydalaning.":!IsOnline(m)?"PC yoqilganini, Agent xizmatini va bir xil mahalliy tarmoqni tekshiring.":!_config.DevicePins.ContainsKey(m.MachineId)?"Sozlamalarda ushbu PC ning device-enrollment.json faylini kiriting.":!m.Verified?"Sinf kaliti, sertifikat va TCP portini tekshiring. Yangilashni bosing.":m.Health?.ScreenLocked==true&&!IsCoverFresh(m)?"StudentShell ishlayotganini va Windows konsolida o‘quvchi kirganini tekshiring. Bu holat ekranning yopilganini tasdiqlamaydi.":"Ulanish tekshirildi. Qulf tasdig‘i StudentShell xabari; Windows xavfsizlik kafolati emas.";
        DetailText.Text=$"{DescribeStatus(m)}\n\nIP: {m.Ip}:{m.Port}\nID: {m.MachineId}\nOxirgi aloqa: {m.LastSeen.ToLocalTime():HH:mm:ss}\n\n{advice}";
        if(m.Health?.LessonId is not null)DetailText.Text+=$"\n\nDars: {m.Health.LessonTitle} · {m.Health.LessonGroup}";
        if(m.Health?.PausedTests>0)DetailText.Text+=$"\nPauzadagi testlar: {m.Health.PausedTests}";
        if(m.Health?.HelpRequested==true)DetailText.Text+=$"\nYordam so‘raldi: {m.Health.HelpRequestedAt?.ToLocalTime():HH:mm:ss}";
        if(!string.IsNullOrEmpty(m.LastContactError))DetailText.Text+="\n\nXato: "+m.LastContactError;
        if(m.Health?.Healthy==false)DetailText.Text+="\n\n"+string.Join("\n",m.Health.Controls.Where(c=>!c.Ok).Select(c=>$"{c.Name}: {c.Detail}"));
    }
    private void OnSaveSeat(object sender,RoutedEventArgs e)
    {
        var m=DetailMachine();if(m is null || m.IdentityConflict){SeatStatus.Text="Avval yagona ID li PC ni tanlang.";return;}
        if(!int.TryParse(SeatNumberBox.Text,out var n)||n<1||n>120){SeatStatus.Text="1–120 oralig‘ida raqam kiriting.";return;}
        if(_layout.Seats.Any(x=>x.Key!=m.MachineId&&x.Value==n)){SeatStatus.Text="Bu joy band. Avval uning joylashuvini olib tashlang.";return;}
        _layout.Seats[m.MachineId]=n;SaveLayout();RefreshMachines();
    }
    private void OnRemoveSeat(object sender,RoutedEventArgs e){var m=DetailMachine();if(m is null)return;_layout.Seats.Remove(m.MachineId);SeatNumberBox.Clear();SaveLayout();RefreshMachines();}
}

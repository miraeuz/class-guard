using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ClassroomGuard.Admin.Services;
using ClassroomGuard.Core.Models;
using ClassroomGuard.Core.Net;
using ClassroomGuard.Core.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
namespace ClassroomGuard.Admin;
public partial class MainWindow
{
    private ActivityJournal? _activity;
    private string _activeGroup="";
    private ActivityJournal GetActivityJournal()=>_activity??=new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ClassroomGuard","activity.json"),NullLogger.Instance);
    private bool MatchesGroup(MachineInfo m)=>_activeGroup.Length==0 || (_layout.Groups.GetValueOrDefault(m.MachineId)??"").Equals(_activeGroup,StringComparison.OrdinalIgnoreCase);
    private string StudentLabel(MachineInfo m)=>_layout.Names.TryGetValue(m.MachineId,out var name)&&!string.IsNullOrWhiteSpace(name)?$"{name} · {m.Name}":m.Name;
    private void PopulateGroups()
    {
        var groups=new[]{"Barcha guruhlar"}.Concat(_layout.Groups.Values.Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s=>s)).ToList();
        GroupSelector.ItemsSource=groups;GroupSelector.SelectedItem=_activeGroup.Length==0?groups[0]:groups.FirstOrDefault(x=>x.Equals(_activeGroup,StringComparison.OrdinalIgnoreCase))??groups[0];
    }
    private void OnGroupChanged(object sender,SelectionChangedEventArgs e)
    {
        if(!_dashboardReady)return;
        _activeGroup=GroupSelector.SelectedIndex<=0?"":GroupSelector.SelectedItem?.ToString()??"";
        _selected.Clear();_renderKey="";RefreshMachines();
    }
    private void OnSaveProfile(object sender,RoutedEventArgs e)
    {
        var m=DetailMachine();if(m is null||m.IdentityConflict){SeatStatus.Text="Avval yagona ID li PC ni tanlang.";return;}
        _layout.Names[m.MachineId]=StudentNameBox.Text.Trim();_layout.Groups[m.MachineId]=StudentGroupBox.Text.Trim();SaveLayout();PopulateGroups();_renderKey="";RefreshMachines();
    }
    private EnforcementState ReadPolicyForm()
    {
        var s=CurrentIntent();s.StudentModeActive=true;
        s.SiteMode=SiteOff.IsChecked==true?SiteBlockingMode.Off:SiteWhite.IsChecked==true?SiteBlockingMode.Whitelist:SiteBlockingMode.Blacklist;
        s.Sites=SitesBox.Text.Split('\n').Select(x=>x.Trim()).Where(x=>x.Length>0).ToList();
        s.AllowedApps=AppsBox.Text.Split('\n').Select(x=>x.Trim()).Where(x=>x.Length>0).ToList();
        s.InstallBlocking=InstallChk.IsChecked==true;s.DownloadBlocking=DownloadChk.IsChecked==true;s.AppAllowListEnabled=AppAllowChk.IsChecked==true;
        s.Library=ParseLibrary(LibraryBox.Text);s.Tests=ParseTestsJson(TestsJsonBox.Text,out var error);if(error is not null)throw new ArgumentException(error);
        PolicyValidator.Validate(s);return s;
    }
    private List<MachineInfo> ClassroomTargets()=>_discovery?.Machines.Where(m=>_selected.Count>0?_selected.Contains(EndpointKey(m)):MatchesGroup(m)).ToList()??new();
    private async void OnBeginLesson(object sender,RoutedEventArgs e)=>await LessonAction(true);
    private async void OnEndLesson(object sender,RoutedEventArgs e)=>await LessonAction(false);
    private async Task LessonAction(bool begin)
    {
        if(_busy||_polling){DashStatus.Text="Joriy tekshiruv tugashini kuting.";return;}
        if(_client is null){DashStatus.Text="Avval sinf kalitini yuklang.";return;}
        var requested=ClassroomTargets();var targets=requested.Where(CanTarget).ToList();
        if(targets.Count==0){DashStatus.Text="Tayyor PC yo‘q.";return;}
        LessonSession? lesson=null;
        try
        {
            if(begin){lesson=new(){Id=Guid.NewGuid().ToString("N"),Title=LessonTitleBox.Text.Trim(),Group=LessonGroupBox.Text.Trim(),Policy=ReadPolicyForm()};if(lesson.Title.Length==0)throw new ArgumentException("Dars nomini kiriting.");}
            if(MessageBox.Show($"{(begin?"Darsni boshlash":"Darsni yakunlash")}: {targets.Count} PC. {requested.Count-targets.Count} PC tayyor emas.\n"+string.Join(", ",targets.Select(m=>m.Name))+"\n\nBoshlash: Push sahifasidagi ilovalar, saytlar, kutubxona va testlar vaqtincha qo‘llanadi. Yakunlash: oldingi asosiy siyosat tiklanadi. Ekran qulfi va test pauzasi alohida boshqariladi.","Dars sessiyasi",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            _busy=true;RefreshMachines();
            var results=new List<SyncEngine.PushResult>();var summaries=new List<object>();
            foreach(var m in targets)
            {
                var request=new NetRequest { Command=begin?NetCommand.BeginLesson:NetCommand.EndLesson,Lesson=lesson,LessonId=m.Health?.LessonId };
                if(!begin&&request.LessonId is null){results.Add(new(m,false,"Faol dars yo‘q."));continue;}
                var response=await _client.SendAsync(m,request);SyncEngine.Update(m,response);results.Add(new(m,response.Ok,response.Message));
                summaries.Add(new { m.Name,m.MachineId,response.Ok,response.Message,response.LessonSummary });
            }
            ShowResults(begin?"Dars boshlandi":"Dars yakunlandi",results);
            if(!begin)
            {
                var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ClassroomGuard","LessonSummaries");Directory.CreateDirectory(dir);
                var file=Path.Combine(dir,$"lesson-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");File.WriteAllText(file,JsonSerializer.Serialize(summaries,new JsonSerializerOptions{WriteIndented=true}));
                DashStatus.Text+=" · Hisobot saqlandi: "+file;
            }
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"Dars sessiyasi");}
        finally{_busy=false;RefreshMachines();}
    }
    private async void OnPauseSelected(object sender,RoutedEventArgs e)=>await BulkAsync(NetCommand.PauseTests,true);
    private async void OnResumeSelected(object sender,RoutedEventArgs e)=>await BulkAsync(NetCommand.ResumeTests,true);
    private async void OnPushSelected(object sender,RoutedEventArgs e)
    {
        if(_busy||_polling||_sync is null||_discovery is null)return;
        var selected=_discovery.Machines.Where(m=>_selected.Contains(EndpointKey(m))&&!m.IdentityConflict).ToList();
        if(selected.Count==0){MessageBox.Show("Avval boshqaruv sahifasida PC larni tanlang.");return;}
        try
        {
            var state=ReadPolicyForm();
            if(MessageBox.Show($"Push sahifasidagi to‘liq siyosat va {state.Tests.Count} test {selected.Count} ta tanlangan PC ga biriktirilsinmi? Offline PC qaytganda oladi. Faol dars tugaguncha yangilash kutiladi.","Tanlangan PC siyosati",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            _busy=true;_sync.SetDeviceIntended(selected.Select(m=>m.MachineId),state);
            var results=await _sync.PushAllAsync(selected.Where(m=>CanTarget(m)&&m.Health?.LessonId is null));ShowResults("Tanlangan siyosat",results);
            PushStatus.Text=$"{selected.Count} ta PC uchun saqlandi. Offline yoki faol darsdagi PC uchun keyin qo‘llanadi.";
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"Biriktirish xatosi");}
        finally{_busy=false;RefreshMachines();}
    }
    private void OnActivityHistory(object sender,RoutedEventArgs e)
    {
        try
        {
            var rows=GetActivityJournal().Read().Reverse().ToList();
            var panel=new DockPanel();var export=new Button { Content="JSON eksport",Margin=new Thickness(12),Padding=new Thickness(16,8,16,8) };DockPanel.SetDock(export,Dock.Top);panel.Children.Add(export);
            var grid=new DataGrid { IsReadOnly=true,AutoGenerateColumns=true,ItemsSource=rows,Margin=new Thickness(12),CanUserAddRows=false };
            grid.SetResourceReference(Control.BackgroundProperty,"BgCard");grid.SetResourceReference(Control.ForegroundProperty,"Text");panel.Children.Add(grid);
            export.Click+=(_,_)=>{var dialog=new SaveFileDialog{Filter="JSON|*.json",FileName="teacher-activity.json"};if(dialog.ShowDialog()==true){try{File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));}catch(Exception ex){MessageBox.Show(ex.Message);}}};
            new Window { Owner=this,Title="O‘qituvchi faoliyati · oxirgi 2000 yozuv",Content=panel,Width=950,Height=600,WindowStartupLocation=WindowStartupLocation.CenterOwner }.ShowDialog();
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"Tarix ochilmadi");}
    }
}

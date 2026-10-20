using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClassroomGuard.Core.Ipc;

namespace ClassroomGuard.StudentShell;

public partial class MainWindow
{
    private bool _screenLockConfirmed;
    private bool _screenLocked = true; // startup gate until authenticated local service answers
    private readonly List<LessonLockWindow> _lockWindows = new();
    private DispatcherTimer? _lockWatch;
    private string _displayLayout = "";
    private void StartScreenLock()
    {
        EnsureLockWindows();
        _lockWatch = new DispatcherTimer { Interval=TimeSpan.FromSeconds(1) };
        _lockWatch.Tick += (_,_) => { if(_screenLocked) EnsureLockWindows(); };
        _lockWatch.Start();
        Application.Current.Exit += (_,_) => _lockWatch.Stop();
    }
    private void ApplyScreenLock(bool locked)
    {
        _screenLockConfirmed=true;
        _screenLocked=locked;
        if(locked) { StopAutoClose(); EnsureLockWindows(); }
        else
        {
            foreach(var window in _lockWindows) window.Release();
            _lockWindows.Clear();_displayLayout="";
        }
    }
    private void EnsureLockWindows()
    {
        var screens=System.Windows.Forms.Screen.AllScreens;
        var layout=string.Join(";",screens.Select(s=>s.Bounds.ToString()));
        if(layout!=_displayLayout || _lockWindows.Count==0)
        {
            var old=_lockWindows.ToArray();_lockWindows.Clear();
            foreach(var screen in screens)
            {
                var window=new LessonLockWindow(screen.Bounds, async () => await CheckConnectionOnceAsync());
                _lockWindows.Add(window);window.Show();
            }
            foreach(var window in old)window.Release();
            _displayLayout=layout;
        }
        foreach(var window in _lockWindows) { window.SetConfirmed(_screenLockConfirmed); window.KeepVisible(); }
        if(!_lockWindows.Any(w=>w.IsActive)) _lockWindows.FirstOrDefault()?.Activate();
    }
}

/// <summary>Visible classroom attention screen, not a Windows secure desktop or kiosk boundary.</summary>
internal sealed class LessonLockWindow : Window
{
    private bool _released;
    private readonly TextBlock _message;
    private readonly System.Drawing.Rectangle _bounds;
    [DllImport("user32.dll",SetLastError=true)] private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int cx,int cy,uint flags);
    public LessonLockWindow(System.Drawing.Rectangle bounds,Func<Task> refresh)
    {
        _bounds=bounds;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        ShowInTaskbar=false;Topmost=true;Title="Mirae — Darsga e’tibor";
        Background=new LinearGradientBrush(Color.FromRgb(26,20,55),Color.FromRgb(81,45,145),45);
        Foreground=Brushes.White;
        var panel=new StackPanel { Width=600,Margin=new Thickness(40) };
        panel.Children.Add(Text("M I R A E   A C A D E M Y",18,FontWeights.SemiBold));
        panel.Children.Add(Text("\uE72E",90,FontWeights.Normal,"Segoe MDL2 Assets"));
        panel.Children.Add(Text("Darsga e’tibor bering",40,FontWeights.Bold));
        _message=Text("Agent holati tekshirilmoqda…",21,FontWeights.Normal);
        panel.Children.Add(_message);
        panel.Children.Add(Text("O‘qituvchi ekranni ochguncha kuting.\nQayta ishga tushirish qulfni bekor qilmaydi.",17,FontWeights.Normal));
        panel.Children.Add(Text(Environment.MachineName+"  •  O‘quv mashg‘uloti",14,FontWeights.Normal));
        var expander=new Expander { Header="O‘qituvchi uchun mahalliy ochish",Foreground=Brushes.White,Margin=new Thickness(0,24,0,0) };
        var form=new StackPanel();
        form.Children.Add(Text("Tarmoq ishlamasa, o‘qituvchi hisobi yoki bir martalik koddan foydalaning.",14,FontWeights.Normal));
        var username=new TextBox { Text="teacher",FontSize=16,Margin=new Thickness(0,6,0,6),Padding=new Thickness(10),Foreground=Brushes.Black,MaxLength=128 };
        var password=new PasswordBox { FontSize=16,Margin=new Thickness(0,6,0,6),Padding=new Thickness(10),Foreground=Brushes.Black,MaxLength=256 };
        var status=Text("Agent holati har 2 soniyada tekshiriladi. Aloqa uzilsa, qulf saqlanadi.",13,FontWeights.Normal);
        var button=new Button { Content="Ekranni ochish",Padding=new Thickness(18,10,18,10),Margin=new Thickness(0,8,0,8) };
        button.Click+=async (_,_) =>
        {
            button.IsEnabled=false;
            try
            {
                var secret=password.Password;password.Clear();
                var response=await AgentPipeClient.SendAsync(new ControlRequest { Action=ControlAction.UnlockScreen,Username=username.Text.Trim(),Password=secret,Code=secret.Length==6?secret:null },timeoutMs:5000);
                status.Text=response.Ok?"Qulf ochildi.":response.Message;
                await refresh();
            }
            catch { status.Text="Agent bilan aloqa yo‘q. Administrator tiklash vositasidan foydalaning."; }
            finally { button.IsEnabled=true; }
        };
        form.Children.Add(username);form.Children.Add(password);form.Children.Add(button);form.Children.Add(status);expander.Content=form;panel.Children.Add(expander);
        Content=new Viewbox { Stretch=Stretch.Uniform,Child=panel,Margin=new Thickness(32) };
        SourceInitialized+=(_,_)=>Position();
        Closing+=(_,e)=>{if(!_released)e.Cancel=true;};
        StateChanged+=(_,_)=>{if(!_released && WindowState==WindowState.Minimized)WindowState=WindowState.Normal;};
    }
    private static TextBlock Text(string value,double size,FontWeight weight,string font="Segoe UI") => new()
    { Text=value,FontSize=size,FontWeight=weight,FontFamily=new FontFamily(font),TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,10,0,10) };
    private void Position()=>SetWindowPos(new WindowInteropHelper(this).Handle,new IntPtr(-1),_bounds.X,_bounds.Y,_bounds.Width,_bounds.Height,0x0010|0x0040);
    public void SetConfirmed(bool confirmed) => _message.Text=confirmed?"Kompyuter o‘qituvchi tomonidan qulflangan.":"Agent javobi kutilmoqda. Aloqa tiklanmasa, o‘qituvchiga murojaat qiling.";
    public void KeepVisible() { if(!IsVisible)Show();if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;Position(); }
    public void Release() { _released=true;Close(); }
}

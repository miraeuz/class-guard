using System.Windows;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace ClassroomGuard.StudentShell;

/// <summary>
/// Keeps ClassroomGuard reachable from the notification area.
/// P0: no unauthenticated "Exit" — students must not dismiss the shell via tray.
/// Teacher login and show-dashboard are the only actions; full removal requires
/// teacher credentials + uninstall.ps1.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly MainWindow _window;

    public TrayIcon(MainWindow window)
    {
        _window = window;

        var menu = new Forms.ContextMenuStrip();
        var unlockItem = new Forms.ToolStripMenuItem("O'qituvchi kirishi…");
        unlockItem.Click += (_, _) => ShowTeacherLogin();
        menu.Items.Add(unlockItem);

        var showItem = new Forms.ToolStripMenuItem("Dashboardni ko‘rsatish");
        showItem.Click += (_, _) => ShowDashboard();
        menu.Items.Add(showItem);

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "ClassroomGuard — dashboardni ochish",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => ShowDashboard();
    }

    private void ShowTeacherLogin()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _window.Show();
            _window.WindowState = WindowState.Maximized;
            _window.Activate();
            _window.GoToTeacherLogin();
        });
    }

    private void ShowDashboard()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _window.Show();
            _window.WindowState = WindowState.Maximized;
            _window.Activate();
            _window.GoToDashboard();
        });
    }

    private static Drawing.Icon LoadIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/app.ico");
            var streamInfo = Application.GetResourceStream(uri);
            if (streamInfo is not null) return new Drawing.Icon(streamInfo.Stream);
        }
        catch { /* fall back rather than crash */ }
        return Drawing.SystemIcons.Shield;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}

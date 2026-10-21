using System.IO;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace ClassroomGuard.StudentShell;

public partial class MainWindow
{
    private bool _learningReady, _learningBusy, _recoveryBusy;
    private async Task EnsureLearningAsync()
    {
        if (_learningReady || _learningBusy) return;
        _learningBusy = true;
        LearningErrorPanel.Visibility = Visibility.Collapsed;
        LearningView.Visibility = Visibility.Visible;
        try
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "Learning");
            if (!File.Exists(Path.Combine(folder, "index.html"))) throw new FileNotFoundException("Learning/index.html paketda topilmadi. To‘liq StudentShell papkasini o‘rnating.");
            var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClassroomGuard", "LearningWebView");
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
            await LearningView.EnsureCoreWebView2Async(env);
            var web = LearningView.CoreWebView2;
            web.SetVirtualHostNameToFolderMapping("learning.mirae.local", folder, CoreWebView2HostResourceAccessKind.DenyCors);
            // Local learning has no privileged native bridge and cannot launch commands.
            web.Settings.AreHostObjectsAllowed = false;
            web.Settings.IsWebMessageEnabled = false;
            web.Settings.IsStatusBarEnabled = false;
            web.Settings.AreDevToolsEnabled = false;
            web.Settings.AreDefaultContextMenusEnabled = false;
            web.NavigationStarting += (_, e) =>
            {
                if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "learning.mirae.local") e.Cancel = true;
            };
            web.NewWindowRequested += (_, e) => e.Handled = true;
            web.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            web.NavigationCompleted += (_, e) =>
            {
                if(e.IsSuccess)ApplyLearningTheme();
                if (!e.IsSuccess)
                {
                    _learningReady = false;
                    LearningError.Text = "Dars fayli yuklanmadi: " + e.WebErrorStatus + ". O‘rnatilgan Learning papkasini tekshiring.";
                    LearningView.Visibility = Visibility.Collapsed;
                    LearningErrorPanel.Visibility = Visibility.Visible;
                }
            };
            web.Navigate("https://learning.mirae.local/index.html");
            _learningReady = true;
        }
        catch (Exception ex)
        {
            LearningError.Text = "Microsoft Edge WebView2 Runtime va to‘liq Learning papkasi kerak.\n\n" + ex.Message;
            LearningView.Visibility = Visibility.Collapsed;
                    LearningErrorPanel.Visibility = Visibility.Visible;
        }
        finally { _learningBusy = false; }
    }
    private void ApplyLearningTheme()
    {
        if(Uri.TryCreate(LearningView.CoreWebView2?.Source,UriKind.Absolute,out var page) && page.Scheme=="https" && page.Host=="learning.mirae.local" && page.AbsolutePath=="/index.html")
            LearningView.CoreWebView2.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(new { kind="theme",dark=ThemeManager.Dark }));
    }
    private void OnRetryLearning(object sender, RoutedEventArgs e) => _ = EnsureLearningAsync();
    private void OnOpenLearning(object sender, RoutedEventArgs e)
    {
        // Read-only learning remains reachable when the Agent is unavailable.
        // This does not change any enforcement state or unlock the machine.
        StopAutoClose();
        ShowPage(PageStudent);
        ShowDashboardSection("Togarak");
    }
    private void OnEmergencyUninstall(object sender, RoutedEventArgs e) => LaunchRecovery(false);
    private void OnChangeEmergencyPassword(object sender, RoutedEventArgs e) => LaunchRecovery(true);
    private async void LaunchRecovery(bool changePassword)
    {
        if (_recoveryBusy) return;
        var script = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ClassroomGuard", "Recovery", "emergency-uninstall.ps1");
        if (!File.Exists(script))
        {
            MessageBox.Show("Tiklash skripti o‘rnatilmagan. Yangilangan scripts/install.ps1 bilan paketni o‘rnating. Manba paketda scripts/emergency-uninstall.ps1 mavjud.", "ClassroomGuard");
            return;
        }
        _recoveryBusy = true;
        try
        {
            // Fixed protected path; never accepts student-supplied code or a password in arguments.
            var info = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
                Arguments = "-NoProfile -STA -File \"" + script + "\"" + (changePassword ? " -ChangePassword" : ""),
                UseShellExecute = true, Verb = "runas", WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System)
            };
            using var process = Process.Start(info);
            if (process is not null) await process.WaitForExitAsync();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { MessageBox.Show("Windows administrator tasdig‘i bekor qilindi. Hech narsa olib tashlanmadi.", "ClassroomGuard"); }
        catch (Exception ex) { MessageBox.Show("Tiklash oynasi ochilmadi: " + ex.Message, "ClassroomGuard"); }
        finally { _recoveryBusy = false; }
    }
}

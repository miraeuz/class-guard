using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
namespace ClassroomGuard.StudentShell;
public partial class MainWindow
{
    private Task? _richInitialization;
    private bool _richReady;
    private const string RichOrigin="https://tests.mirae.local/index.html";
    private async Task ShowRichQuestionAsync()
    {
        if(_currentTest is null)return;
        var q=_currentTest.Questions[_qIndex];
        // Keep a complete plain fallback if WebView2 is unavailable.
        TestQuestionText.Text=$"{_qIndex+1}. {q.Text}"+(string.IsNullOrEmpty(q.Code)?"":"\n\n"+q.Code)+(string.IsNullOrEmpty(q.Latex)?"":"\n\n"+q.Latex);
        try { await (_richInitialization??=InitializeRichQuestionAsync()); SendRichQuestion(); }
        catch { QuestionRichView.Visibility=Visibility.Collapsed;TestQuestionText.Visibility=Visibility.Visible; }
    }
    private async Task InitializeRichQuestionAsync()
    {
        var folder=Path.Combine(AppContext.BaseDirectory,"TestAssets");
        if(!File.Exists(Path.Combine(folder,"index.html")))throw new FileNotFoundException("TestAssets missing");
        var env=await CoreWebView2Environment.CreateAsync(userDataFolder:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ClassroomGuard","TestsWebView"));
        await QuestionRichView.EnsureCoreWebView2Async(env);
        var web=QuestionRichView.CoreWebView2;
        web.SetVirtualHostNameToFolderMapping("tests.mirae.local",folder,CoreWebView2HostResourceAccessKind.DenyCors);
        web.Settings.AreHostObjectsAllowed=false;web.Settings.AreDevToolsEnabled=false;web.Settings.AreDefaultContextMenusEnabled=false;web.Settings.IsStatusBarEnabled=false;
        web.NavigationStarting+=(_,e)=>{if(e.Uri!=RichOrigin)e.Cancel=true;};
        web.NewWindowRequested+=(_,e)=>e.Handled=true;
        web.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
        web.DownloadStarting+=(_,e)=>e.Cancel=true;
        web.WebMessageReceived+=(_,e)=>
        {
            if(e.Source!=RichOrigin || e.WebMessageAsJson.Length>100)return;
            try {using var doc=JsonDocument.Parse(e.WebMessageAsJson);if(doc.RootElement.TryGetProperty("height",out var h) && h.TryGetDouble(out var height) && double.IsFinite(height))QuestionRichView.Height=Math.Clamp(height,100,520);}catch { }
        };
        web.NavigationCompleted+=(_,e)=>
        {
            _richReady=e.IsSuccess && web.Source==RichOrigin;
            if(_richReady)SendRichQuestion();else {QuestionRichView.Visibility=Visibility.Collapsed;TestQuestionText.Visibility=Visibility.Visible;}
        };
        web.Navigate(RichOrigin);
    }
    private void SendRichQuestion()
    {
        if(!_richReady || _currentTest is null || QuestionRichView.CoreWebView2.Source!=RichOrigin)return;
        var q=_currentTest.Questions[_qIndex];
        QuestionRichView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new {Dark=ThemeManager.Dark,q.Text,q.TextHtml,q.Code,q.Language,q.Latex,q.Topic,q.Difficulty}));
        QuestionRichView.Visibility=Visibility.Visible;TestQuestionText.Visibility=Visibility.Collapsed;
    }
}

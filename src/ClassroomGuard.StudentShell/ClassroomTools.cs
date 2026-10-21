using System.Windows;
using ClassroomGuard.Core.Ipc;
using ClassroomGuard.Core.Models;
namespace ClassroomGuard.StudentShell;
public partial class MainWindow
{
    private bool _handRaised;
    private bool _helpBusy;
    private async void OnRaiseHand(object sender,RoutedEventArgs e)
    {
        if(_helpBusy)return;_helpBusy=true;
        try
        {
            var r=await AgentPipeClient.SendAsync(new(){Action=_handRaised?ControlAction.LowerHand:ControlAction.RaiseHand});
            if(!r.Ok)MessageBox.Show(r.Message);await CheckConnectionOnceAsync();
        }
        finally{_helpBusy=false;}
    }
    private void ReflectClassroom(AgentHealth health)
    {
        _handRaised=health.HelpRequested;RaiseHandButton.Content=_handRaised?"✋ ✓":"✋";
        RaiseHandButton.ToolTip=_handRaised?"Yordam so‘ralgan — bekor qilish":"O‘qituvchidan yordam so‘rash";
        if(health.LessonId is not null)StudentStatus.Text+=$" · Dars: {health.LessonTitle}";
    }
    private async Task RefreshAttemptStateAsync()
    {
        if(_attempt is null || _attempt.Finished || !await _attemptGate.WaitAsync(0))return;
        try
        {
            var response=await AgentPipeClient.SendAsync(new(){Action=ControlAction.ResumeTest});
            if(!response.Ok || response.Attempt is not {} state || state.Id!=_attempt.Id)return;
            if(state.Finished){ShowResult(state);return;}
            var changed=state.Paused!=_attempt.Paused;
            _attempt=state;
            if(changed){_answers=(int[])state.Answers.Clone();RenderQuestion();if(!state.Paused)TestSaveStatus.Text="Test davom ettirildi.";}
            ApplyPauseUi();
        }
        finally{_attemptGate.Release();}
    }
    private void ApplyPauseUi()
    {
        var paused=_attempt?.Paused==true;
        TestOptionsPanel.IsEnabled=!paused;TestFinishButton.IsEnabled=!paused;TestClearButton.IsEnabled=!paused;
        if(paused){TestTimerText.Text="⏸ O‘qituvchi testni pauza qildi";TestSaveStatus.Text="Vaqt saqlangan. Javob berish va topshirish davom ettirilguncha yopiq.";}
    }
}

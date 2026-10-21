using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClassroomGuard.Core.Models;
namespace ClassroomGuard.StudentShell;
public partial class MainWindow
{
    private void UpdateQuestionPalette()
    {
        if(_currentTest is null)return;
        TestPalette.Children.Clear();
        for(int i=0;i<_answers.Length;i++)
        {
            var index=i;
            var button=new Button {Content=$"{i+1}"+(_flagged.Contains(i)?"★":""),MinWidth=42,Height=38,Margin=new Thickness(3),Padding=new Thickness(4),Style=(Style)FindResource("BtnGhost"),Background=(Brush)FindResource(_answers[i]>=0?"AccentSoft":"BgCard"),Foreground=(Brush)FindResource("Text"),FontWeight=i==_qIndex?FontWeights.Bold:FontWeights.Normal,ToolTip=$"Savol {i+1}: "+(_answers[i]>=0?"javob berilgan":"javobsiz")};
            if(i==_qIndex){button.Style=(Style)FindResource("BtnPrimary");button.Background=(Brush)FindResource("Accent");button.Foreground=Brushes.White;}
            button.Click+=(_,_)=>{_qIndex=index;RenderQuestion();DashboardScroll.ScrollToTop();};TestPalette.Children.Add(button);
        }
        TestCompletion.Maximum=_answers.Length;TestCompletion.Value=_answers.Count(x=>x>=0);
        TestFlagButton.Content=_flagged.Contains(_qIndex)?"★ Belgilangan":"☆ Belgilash";
        TestProgressText.Text=$"Savol {_qIndex+1}/{_answers.Length} · Javob berilgan: {_answers.Count(x=>x>=0)} · Belgilangan: {_flagged.Count}";
    }
    private void OnFlagQuestion(object sender,RoutedEventArgs e){if(!_flagged.Add(_qIndex))_flagged.Remove(_qIndex);UpdateQuestionPalette();}
    private async void OnClearAnswer(object sender,RoutedEventArgs e)
    {
        if(_attempt is null || _attempt.Finished || _attempt.Paused || _finishing)return;
        _answers[_qIndex]=-1;RenderQuestion();await SaveAnswersAsync();
    }
    private void BuildTestReview(AttemptView view)
    {
        TestReviewPanel.Children.Clear();
        if(!view.Test.ShowReviewAfterFinish)return;
        TestReviewPanel.Children.Add(new TextBlock{Text="Javoblar tahlili",FontSize=20,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,12)});
        for(int i=0;i<view.Test.Questions.Count;i++)
        {
            var q=view.Test.Questions[i];if(q.CorrectIndex<0 || q.CorrectIndex>=q.Options.Count)continue;
            var answer=view.Answers[i];var selected=answer>=0 && answer<q.Options.Count?q.Options[answer]:"Javob berilmagan";
            TestReviewPanel.Children.Add(new TextBlock{Text=$"{i+1}. {q.Text}\nSizning javobingiz: {selected}\nTo‘g‘ri javob: {q.Options[q.CorrectIndex]}\n{q.Explanation}",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,18),Foreground=(Brush)FindResource(answer==q.CorrectIndex?"Success":"Text")});
        }
    }
}

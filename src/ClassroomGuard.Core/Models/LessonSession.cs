namespace ClassroomGuard.Core.Models;
public sealed class LessonSession
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Group { get; set; } = "";
    public DateTimeOffset Started { get; set; }
    public EnforcementState Policy { get; set; } = new();
}

public sealed record LessonSummary(string Id,string Title,string Group,DateTimeOffset Started,DateTimeOffset Ended,int CompletedTests,int ActiveTests,int CorrectAnswers,int TotalQuestions);

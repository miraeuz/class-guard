namespace ClassroomGuard.Core.Models;

/// <summary>
/// One test package pushed from Admin (JSON). Student picks class + subject,
/// answers questions, sees a local result page.
/// </summary>
public sealed class TestPackage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = "";
    public string ClassName { get; set; } = "";   // e.g. "7-A"
    public string Subject { get; set; } = "";      // e.g. "Matematika"
    public int TimeLimitMinutes { get; set; }      // 0 = no limit
    public List<TestQuestion> Questions { get; set; } = new();
}

public sealed class TestQuestion
{
    public string Text { get; set; } = "";
    /// <summary>Answer choices (A, B, C, …).</summary>
    public List<string> Options { get; set; } = new();
    /// <summary>0-based index of the correct option.</summary>
    public int CorrectIndex { get; set; }
}

/// <summary>
/// Local result after a student finishes a test (not sent to server yet).
/// </summary>
public sealed class TestResult
{
    public string TestId { get; set; } = "";
    public string Title { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string Subject { get; set; } = "";
    public int Total { get; set; }
    public int Correct { get; set; }
    public double Percent => Total == 0 ? 0 : Math.Round(100.0 * Correct / Total, 1);
    public DateTime FinishedAt { get; set; } = DateTime.Now;
}

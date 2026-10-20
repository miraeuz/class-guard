using ClassroomGuard.Core.Models;

namespace ClassroomGuard.Core.Ipc;

/// <summary>
/// Newline-delimited JSON messages exchanged over the local named pipe between
/// the Agent service and a local privileged client (the Phase-3 Student/Admin
/// shell; for now the ClassroomGuard.Ctl test tool).
///
/// Phase 2 will add the network TLS command channel for admin↔agent traffic;
/// this local pipe stays as the on-box control path.
/// </summary>
public enum ControlAction
{
    GetStatus,
    EnableStudentMode,   // no auth required (restricting is always allowed)
    DisableStudentMode,  // requires teacher credential
    SetState,            // replace intended state (Phase-2 admin will use the TLS path)
    Uninstall,           // requires teacher credential; triggers full revert
    ChangePassword,
    ApproveInstall,      // redeem a Telegram-delivered one-time install code (no teacher auth needed locally — the code IS the auth)
    StartTest, SaveAnswers, FinishTest, ResumeTest,
    LockScreen = 12, UnlockScreen = 13,
    ReportTestResult = 11,    // student finished a test — Agent forwards summary to teacher Telegram
}

public sealed class ControlRequest
{
    public ControlAction Action { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? NewPassword { get; set; }
    public string? Code { get; set; }
    public EnforcementState? State { get; set; }
    /// <summary>Free-form payload (e.g. test result summary for ReportTestResult).</summary>
    public string? Message { get; set; }
    public string? TestId { get; set; }
    public string? AttemptId { get; set; }
    public int[]? Answers { get; set; }
}

public sealed class ControlResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
    public bool MustChangePassword { get; set; }
    public AgentHealth? Health { get; set; }
    public AttemptView? Attempt { get; set; }
    public EnforcementState? State { get; set; }

    public static ControlResponse Fail(string msg) => new() { Ok = false, Message = msg };
    public static ControlResponse Success(string msg = "OK") => new() { Ok = true, Message = msg };
}

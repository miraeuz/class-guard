namespace ClassroomGuard.Core.Models;

/// <summary>
/// The full intended configuration for one machine. This object is what the
/// Admin app (Phase 2+) pushes and what each Agent persists locally and enforces
/// entirely on its own — with or without a network connection.
///
/// FAIL-CLOSED DEFAULT: when local state is missing or corrupt, the Agent loads
/// <see cref="FailClosedDefault"/> instead, so a crash never hands a student open
/// internet. It never affects the OS itself — only the app's own reversible rules.
/// </summary>
public sealed class EnforcementState
{
    /// <summary>Bumped whenever intent changes; used by the Phase-4 sync engine.</summary>
    public long Version { get; set; } = 1;
    public string PolicyId { get; set; } = Guid.NewGuid().ToString("N");

    public SiteBlockingMode SiteMode { get; set; } = SiteBlockingMode.Off;

    /// <summary>Domains blocked in Blacklist mode (or allowed in Whitelist mode).</summary>
    public List<string> Sites { get; set; } = new();

    public bool InstallBlocking { get; set; }
    public bool DownloadBlocking { get; set; }

    /// <summary>
    /// When true and StudentMode is active, only processes whose executable path
    /// matches an entry in <see cref="AllowedApps"/> (or a built-in system safe
    /// list) are allowed to start. Everything else is killed.
    /// Empty AllowedApps = no extra app restriction (only install/download/sites).
    /// </summary>
    public bool AppAllowListEnabled { get; set; }

    /// <summary>
    /// Full or partial executable paths / names the student may run
    /// (e.g. "WINWORD.EXE", "C:\\Program Files\\Microsoft Office\\root\\Office16\\EXCEL.EXE",
    /// "chrome.exe"). Comparison is case-insensitive; if the entry has no path
    /// separator it matches by file name only.
    /// </summary>
    public List<string> AllowedApps { get; set; } = new();

    /// <summary>
    /// Library entries (offline PDFs + online links) pushed from Admin.
    /// StudentShell shows them under Kutubxona.
    /// </summary>
    public List<LibraryItem> Library { get; set; } = new();

    /// <summary>
    /// Test packages pushed from Admin (JSON). StudentShell lists by class/subject.
    /// </summary>
    public List<TestPackage> Tests { get; set; } = new();

    /// <summary>
    /// Student Mode is the restricted experience. Turning it ON needs no auth;
    /// turning it OFF requires the teacher credential (see spec §4.1).
    /// </summary>
    public bool StudentModeActive { get; set; }

    /// <summary>
    /// Per-machine exceptions the teacher set deliberately (e.g. "installs allowed
    /// on this one PC"). The Phase-4 sync engine treats these as explicit
    /// exceptions, not drift to be corrected.
    /// </summary>
    public Dictionary<string, bool> Overrides { get; set; } = new();

    public EnforcementState Clone() => new()
    {
        Version = Version,
        PolicyId = PolicyId,
        SiteMode = SiteMode,
        Sites = new List<string>(Sites),
        InstallBlocking = InstallBlocking,
        DownloadBlocking = DownloadBlocking,
        AppAllowListEnabled = AppAllowListEnabled,
        AllowedApps = new List<string>(AllowedApps),
        Library = Library.Select(x => new LibraryItem
        {
            Title = x.Title,
            PathOrUrl = x.PathOrUrl,
            Kind = x.Kind,
            Category = x.Category,
        }).ToList(),
        Tests = Tests.Select(t => new TestPackage
        {
            Id = t.Id,
            Title = t.Title,
            ClassName = t.ClassName,
            Subject = t.Subject,
            TimeLimitMinutes = t.TimeLimitMinutes,
            Questions = t.Questions.Select(q => new TestQuestion
            {
                Text = q.Text,
                Options = new List<string>(q.Options),
                CorrectIndex = q.CorrectIndex,
            }).ToList(),
        }).ToList(),
        StudentModeActive = StudentModeActive,
        Overrides = new Dictionary<string, bool>(Overrides),
    };

    public EnforcementState StudentView()
    {
        var copy = Clone();
        foreach (var test in copy.Tests)
            foreach (var q in test.Questions) q.CorrectIndex = -1;
        return copy;
    }

    /// <summary>
    /// The safe default applied when persisted state is missing/corrupt:
    /// restrictions ON. Site blocking is left Off here only because a Blacklist
    /// with no domains is a no-op anyway; install/download blocking default ON.
    /// App allow-list starts empty (disabled) so a school is not locked out of
    /// Word/Excel until the teacher pushes a list.
    /// </summary>
    public static EnforcementState FailClosedDefault() => new()
    {
        Version = 0,
        PolicyId = "baseline",
        SiteMode = SiteBlockingMode.Off,
        Sites = new(),
        InstallBlocking = true,
        DownloadBlocking = true,
        AppAllowListEnabled = false,
        AllowedApps = new(),
        Library = new(),
        Tests = new(),
        StudentModeActive = true,
        Overrides = new(),
    };
}

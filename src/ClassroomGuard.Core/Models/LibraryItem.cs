namespace ClassroomGuard.Core.Models;

/// <summary>
/// One entry in the student Library (offline PDF or online link).
/// Pushed from Admin; StudentShell displays and opens them.
/// </summary>
public sealed class LibraryItem
{
    /// <summary>Display title shown to the student.</summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Offline: full path to a local/share PDF (e.g. \\SERVER\Books\math.pdf
    /// or C:\ClassroomGuard\Library\math.pdf).
    /// Online: https://… URL.
    /// </summary>
    public string PathOrUrl { get; set; } = "";

    /// <summary>"offline" or "online".</summary>
    public string Kind { get; set; } = "offline";

    /// <summary>Optional subject / category label.</summary>
    public string Category { get; set; } = "";
}

using System.Text.Json;
using System.Text.RegularExpressions;
namespace ClassroomGuard.Core.Models;

public static class PolicyValidator
{
    public static void Validate(EnforcementState s)
    {
        if (string.IsNullOrWhiteSpace(s.PolicyId) || s.PolicyId.Length > 80) throw new ArgumentException("Policy ID required.");
        if (!s.StudentModeActive) throw new ArgumentException("Baseline must be restricted. Use temporary teacher unlock instead.");
        if (s.SiteMode is not (SiteBlockingMode.Off or SiteBlockingMode.Blacklist)) throw new ArgumentException("Only Off / Blacklist supported.");
        if (s.Sites is null || s.AllowedApps is null || s.Library is null || s.Tests is null || s.Overrides is null) throw new ArgumentException("Null policy collections.");
        if (s.Sites.Count > 500 || s.AllowedApps.Count > 100 || s.Library.Count > 300 || s.Tests.Count > 100) throw new ArgumentException("Policy is too large.");
        if (s.Overrides.Keys.Any(k => k is not ("install" or "download" or "apps"))) throw new ArgumentException("Unknown override.");
        foreach (var d in s.Sites)
            if (string.IsNullOrWhiteSpace(d) || d.Length > 253 || !Regex.IsMatch(d, @"^(?=.{1,253}$)([a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,63}$"))
                throw new ArgumentException("Use exact hostnames only (no URL, wildcard or whitespace): " + d);
        if (s.AppAllowListEnabled && s.AllowedApps.Count == 0) throw new ArgumentException("An enabled application list cannot be empty.");
        foreach (var a in s.AllowedApps)
            if (string.IsNullOrWhiteSpace(a) || a.Length > 400 || a.Contains('\n') || a.Contains('\r')) throw new ArgumentException("Invalid application rule.");
        foreach (var item in s.Library)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 200 || !LibraryTargetAllowed(item.Kind,item.PathOrUrl))
                throw new ArgumentException("Library item must be an HTTPS link or an absolute PDF path.");
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int count = 0;
        foreach (var t in s.Tests)
        {
            if (t is null || string.IsNullOrWhiteSpace(t.Id) || !ids.Add(t.Id) || string.IsNullOrWhiteSpace(t.Title) || t.Title.Length > 200 || t.TimeLimitMinutes < 0 || t.TimeLimitMinutes > 180 || t.Questions is null || t.Questions.Count == 0 || t.Questions.Count > 200)
                throw new ArgumentException("Invalid test: unique ID, title, 1–200 questions and 0–180 minutes required.");
            count += t.Questions.Count;
            foreach (var q in t.Questions)
                if (q is null || string.IsNullOrWhiteSpace(q.Text) || q.Text.Length > 4000 || q.Options is null || q.Options.Count < 2 || q.Options.Count > 8 || q.Options.Any(x=>string.IsNullOrWhiteSpace(x) || x.Length>2000) || q.Options.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=q.Options.Count || q.CorrectIndex<0 || q.CorrectIndex>=q.Options.Count)
                    throw new ArgumentException("Invalid question/options/answer index.");
        }
        if (count>1000 || JsonSerializer.SerializeToUtf8Bytes(s).Length>Wire.MaxChars/2) throw new ArgumentException("Policy exceeds v2 capacity (1000 questions / 512 KiB).");
    }
    public static bool LibraryTargetAllowed(string kind, string target)
    {
        if (string.IsNullOrWhiteSpace(target) || target.Length > 1000) return false;
        if (kind == "online") return Uri.TryCreate(target,UriKind.Absolute,out var uri) && uri.Scheme=="https" && string.IsNullOrEmpty(uri.UserInfo);
        return kind == "offline" && Path.IsPathFullyQualified(target) && Path.GetExtension(target).Equals(".pdf",StringComparison.OrdinalIgnoreCase) && !target.Contains('\n') && !target.Contains('\r');
    }
}

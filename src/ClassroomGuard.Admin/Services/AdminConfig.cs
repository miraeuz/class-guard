using System.IO;
using System.Text.Json;
using ClassroomGuard.Core;

namespace ClassroomGuard.Admin.Services;

/// <summary>Admin-side config, stored in %AppData%\ClassroomGuard on the teacher PC.</summary>
public sealed class AdminConfig
{
    public Dictionary<string,string> DevicePins { get; set; } = new();
    public string ClassroomKeyBase64 { get; set; } = "";
    public int AnnouncePort { get; set; } = AppPaths.DefaultAnnouncePort;
    public string TelegramBotToken { get; set; } = "";
    public string TelegramChatId { get; set; } = "";

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClassroomGuard");
    private static string File_ => Path.Combine(Dir, "admin.json");

    /// <summary>Written by the teacher Setup (derived from the classroom password).</summary>
    public static string MachineKeyFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ClassroomGuardTeacher", "classroom.key");

    public static AdminConfig Load()
    {
        var config = new AdminConfig();
        try
        {
            if (File.Exists(File_))
                config = JsonSerializer.Deserialize<AdminConfig>(File.ReadAllText(File_)) ?? new AdminConfig();
        }
        catch { /* ignore */ }
        // The key installed by Setup wins, so re-running Setup with a new classroom password
        // takes effect for every teacher account on this PC.
        try
        {
            if (File.Exists(MachineKeyFile))
            {
                var machineKey = File.ReadAllText(MachineKeyFile).Trim();
                if (Convert.FromBase64String(machineKey).Length == 32) config.ClassroomKeyBase64 = machineKey;
            }
        }
        catch { /* fall back to the per-user key */ }
        return config;
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(File_, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

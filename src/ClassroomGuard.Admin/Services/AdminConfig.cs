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

    public static AdminConfig Load()
    {
        try
        {
            if (File.Exists(File_))
                return JsonSerializer.Deserialize<AdminConfig>(File.ReadAllText(File_)) ?? new AdminConfig();
        }
        catch { /* ignore */ }
        return new AdminConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(File_, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

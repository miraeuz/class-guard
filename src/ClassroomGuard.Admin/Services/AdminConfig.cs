using System.IO;
using System.Text.Json;
using ClassroomGuard.Core;
using ClassroomGuard.Core.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

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
                {
                    var c=JsonSerializer.Deserialize<AdminConfig>(File.ReadAllText(File_))??new();
                    c.DevicePins??=new();c.ClassroomKeyBase64??="";c.TelegramBotToken??="";c.TelegramChatId??="";
                    c.DevicePins=c.DevicePins.Where(x=>!string.IsNullOrWhiteSpace(x.Key)&&x.Value is not null&&x.Value.Length==64&&x.Value.All(Uri.IsHexDigit)).ToDictionary(x=>x.Key,x=>x.Value);
                    if(c.AnnouncePort is <1024 or >65535)c.AnnouncePort=AppPaths.DefaultAnnouncePort;
                    return c;
                }
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
        new AtomicJsonStore<AdminConfig>(File_,()=>new(),NullLogger.Instance).Save(this);
    }
}

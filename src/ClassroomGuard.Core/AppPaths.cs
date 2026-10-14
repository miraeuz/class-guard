namespace ClassroomGuard.Core;

/// <summary>Protected machine-wide paths. Production state is always under ProgramData; no environment-variable override.</summary>
public static class AppPaths
{
    public const string PipeName = "ClassroomGuardAgent";

    public static string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ClassroomGuard");

    public static string StateFile => Path.Combine(DataDir, "state.json");
    public static string UsersFile => Path.Combine(DataDir, "users.json");
    public static string BootstrapFile => Path.Combine(DataDir, "bootstrap.txt");
    public static string LogFile => Path.Combine(DataDir, "agent.log");
    public static string ConfigFile => Path.Combine(DataDir, "agent.json");
    public static string ClassroomKeyFile => Path.Combine(DataDir, "classroom.key");

    public const int DefaultCommandPort = 8787;   // TLS admin<->agent channel
    public const int DefaultAnnouncePort = 8788;   // UDP discovery broadcast
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
namespace ClassroomGuard.Core.Enforcement;
public static class StudentProcess
{
    [DllImport("advapi32.dll",SetLastError=true)]
    private static extern bool OpenProcessToken(IntPtr process,uint access,out SafeAccessTokenHandle token);
    public static bool IsStudent(Process p,IReadOnlyCollection<string> sids)
    {
        if(p.SessionId==0 || sids.Count==0) return false;
        if(!OpenProcessToken(p.Handle,8,out var token))return false;
        using(token)using(var id=new WindowsIdentity(token.DangerousGetHandle()))
            return id.User is not null && sids.Contains(id.User.Value,StringComparer.OrdinalIgnoreCase);
    }
    public static bool ProtectedPath(string path)
    {
        var full=Path.GetFullPath(path);
        return new[]{Environment.GetFolderPath(Environment.SpecialFolder.Windows),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}
            .Where(x=>!string.IsNullOrWhiteSpace(x)).Any(root=>full.StartsWith(Path.TrimEndingDirectorySeparator(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase));
    }
}

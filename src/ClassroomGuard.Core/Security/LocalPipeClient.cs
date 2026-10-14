using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
namespace ClassroomGuard.Core.Security;
public static class LocalPipeClient
{
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern SafePipeHandle CreateFile(string name,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    [DllImport("advapi32.dll",SetLastError=true)]
    private static extern uint GetSecurityInfo(SafePipeHandle handle,int type,uint info,out IntPtr owner,out IntPtr group,out IntPtr dacl,out IntPtr sacl,out IntPtr descriptor);
    [DllImport("kernel32.dll")]private static extern IntPtr LocalFree(IntPtr ptr);
    public static async Task<NamedPipeClientStream> ConnectAsync(CancellationToken ct)
    {
        while(true)
        {
            ct.ThrowIfCancellationRequested();
            // Individual rights intentionally exclude FILE_CREATE_PIPE_INSTANCE.
            var handle=CreateFile(@"\\.\pipe\"+AppPaths.PipeName,0x00120083,0,IntPtr.Zero,3,0x40120000,IntPtr.Zero);
            if(!handle.IsInvalid)
            {
                try
                {
                    var error=GetSecurityInfo(handle,6,1,out var owner,out _,out _,out _,out var descriptor);
                    try
                    {
                        if(error!=0)throw new Win32Exception((int)error);
                        if(!new SecurityIdentifier(owner).IsWellKnown(WellKnownSidType.LocalSystemSid))throw new UnauthorizedAccessException("Pipe server is not owned by LocalSystem.");
                    }
                    finally{if(descriptor!=IntPtr.Zero)LocalFree(descriptor);}
                    return new NamedPipeClientStream(PipeDirection.InOut,true,true,handle);
                }
                catch{handle.Dispose();throw;}
            }
            var code=Marshal.GetLastWin32Error();handle.Dispose();
            if(code is not (2 or 231))throw new Win32Exception(code);
            await Task.Delay(50,ct);
        }
    }
}

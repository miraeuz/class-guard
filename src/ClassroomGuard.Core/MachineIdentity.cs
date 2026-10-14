using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;

namespace ClassroomGuard.Core;

/// <summary>Stable identity + primary LAN address for a machine.</summary>
public static class MachineIdentity
{
    public static string MachineId { get; } = ResolveId();
    public static string Name { get; } = Environment.MachineName;
    public static string Version { get; } = "1.0.0";

    private static string ResolveId()
    {
        // MachineGuid is stable across reboots and unique per install.
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Cryptography");
            var guid = key?.GetValue("MachineGuid") as string;
            if (!string.IsNullOrWhiteSpace(guid)) return guid;
        }
        catch { /* fall through */ }
        return Environment.MachineName;
    }

    /// <summary>First non-loopback IPv4 address on an up interface.</summary>
    public static string LocalIPv4()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        return ip.Address.ToString();
                }
            }
        }
        catch { /* ignore */ }
        return IPAddress.Loopback.ToString();
    }
}

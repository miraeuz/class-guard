using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using ClassroomGuard.Core.Models;
using Microsoft.Extensions.Logging;
namespace ClassroomGuard.Core.Enforcement;
public sealed class HostsEnforcer : IEnforcer
{
    public string Name=>"Exact-host blacklist";
    private const string Start="# === ClassroomGuard START (auto-managed, do not edit) ===";
    private const string End="# === ClassroomGuard END ===";
    private static readonly string Hosts=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"drivers","etc","hosts");
    public HostsEnforcer(ILogger log){}
    public void Apply(EnforcementState s)
    {
        if(s.SiteMode==SiteBlockingMode.Off){Revert();return;}
        if(s.SiteMode!=SiteBlockingMode.Blacklist)throw new InvalidOperationException("Whitelist is unsupported.");
        var body=Read();var clean=Strip(body);
        var block=Start+"\r\n"+string.Join("\r\n",s.Sites.Distinct(StringComparer.OrdinalIgnoreCase).SelectMany(d=>new[]{"0.0.0.0 "+d,":: "+d}))+"\r\n"+End;
        var next=clean+(clean.Length>0 && !clean.EndsWith("\n")?"\r\n":"")+block; // Strip removes only the fenced text; outside bytes/text retained.
        if(next!=body)Write(next);
    }
    public void Revert(){var body=Read();if(body.Contains(Start))Write(Strip(body));}
    private static string Read()=>File.Exists(Hosts)?File.ReadAllText(Hosts):"";
    private static string Strip(string body)
    {
        if(body.Contains(Start)!=body.Contains(End))throw new IOException("Incomplete ClassroomGuard hosts fence; manual review required.");
        return Regex.Replace(body,Regex.Escape(Start)+".*?"+Regex.Escape(End),"",RegexOptions.Singleline);
    }
    private static void Write(string content)
    {
        var tmp=Hosts+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(tmp,content,new UTF8Encoding(false));
            using(var fs=new FileStream(tmp,FileMode.Open,FileAccess.Write))fs.Flush(true);
            if(File.Exists(Hosts))File.Replace(tmp,Hosts,null);else File.Move(tmp,Hosts);
            using var p=Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"ipconfig.exe"),"/flushdns"){CreateNoWindow=true,UseShellExecute=false});
            p?.WaitForExit(3000);
        }
        finally{if(File.Exists(tmp))File.Delete(tmp);}
    }
}

using ClassroomGuard.Core.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
namespace ClassroomGuard.Admin.Services;
public sealed class DashboardPreferences
{
    public Dictionary<string,string> Names { get; set; } = new();
    public Dictionary<string,string> Groups { get; set; } = new();
    public int Columns { get; set; } = 4;
    public bool Seating { get; set; }
    public Dictionary<string,int> Seats { get; set; } = new();
    private static AtomicJsonStore<DashboardPreferences> Store()=>new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ClassroomGuard","dashboard-layout.json"),()=>new(),NullLogger.Instance);
    public static DashboardPreferences Load()
    {
        var result=Store().Load();result.Columns=Math.Clamp(result.Columns,2,6);result.Seats??=new();result.Names??=new();result.Groups??=new();
        result.Seats=result.Seats.Where(x=>x.Value is >=1 and <=120).GroupBy(x=>x.Value).Select(g=>g.First()).ToDictionary(x=>x.Key,x=>x.Value);
        return result;
    }
    public void Save()=>Store().Save(this);
}

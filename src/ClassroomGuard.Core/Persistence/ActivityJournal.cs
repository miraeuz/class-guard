using Microsoft.Extensions.Logging;
namespace ClassroomGuard.Core.Persistence;
public sealed record ActivityEntry(DateTimeOffset Time,string OperationId,string Actor,string Device,string Action,string Result,string Detail);
public sealed class ActivityJournal
{
    private readonly AtomicJsonStore<List<ActivityEntry>> _store;
    private List<ActivityEntry> _entries;
    private readonly object _gate=new();
    public ActivityJournal(string path,ILogger log){_store=new(path,()=>throw new InvalidDataException("Activity journal corrupt; preserve it before recovery."),log);_entries=File.Exists(path)?_store.Load():new();}
    public void Append(ActivityEntry entry){lock(_gate){var next=_entries.TakeLast(1999).Append(entry with { Detail=entry.Detail[..Math.Min(512,entry.Detail.Length)] }).ToList();_store.Save(next);_entries=next;}}
    public IReadOnlyList<ActivityEntry> Read(){lock(_gate)return _entries.ToArray();}
}

using System.IO;
using ClassroomGuard.Core.Models;
using ClassroomGuard.Core.Net;
using ClassroomGuard.Core.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
namespace ClassroomGuard.Admin.Services;
public sealed class SyncEngine
{
    private readonly IAdminClient _client;
    private readonly object _gate=new();
    private readonly SemaphoreSlim _slots=new(6,6);
    private EnforcementState? _intended;
    private readonly AtomicJsonStore<EnforcementState> _store;
    public bool HasIntent {get{lock(_gate)return _intended is not null;}}
    public EnforcementState? Intended {get{lock(_gate)return _intended?.Clone();}}
    public string LoadError {get;private set;}="";
    public SyncEngine(IAdminClient client,string? storagePath=null)
    {
        _client=client;
        var path=storagePath??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"ClassroomGuard","intended-policy.json");
        _store=new(path,()=>throw new InvalidDataException("Saved intent corrupt. Import a valid policy before synchronizing."),NullLogger.Instance);
        try{if(File.Exists(path)){var s=_store.Load();PolicyValidator.Validate(s);_intended=s;}}catch(Exception ex){LoadError=ex.Message;}
    }
    public void SetIntended(EnforcementState state)
    {
        lock(_gate)
        {
            var next=state.Clone();next.StudentModeActive=true;next.Version=(_intended?.Version??0)+1;next.PolicyId=Guid.NewGuid().ToString("N");
            PolicyValidator.Validate(next);_store.Save(next);_intended=next;
        }
    }
    public sealed record PushResult(MachineInfo Machine,bool Ok,string Message);
    public async Task<IReadOnlyList<PushResult>> PushAllAsync(IEnumerable<MachineInfo> machines)
    {
        var state=Intended;if(state is null)return machines.Select(m=>new PushResult(m,false,"No baseline policy configured.")).ToList();
        return await Task.WhenAll(machines.Select(async m=>
        {
            await _slots.WaitAsync();try
            {
                var response=await _client.SendAsync(m,new(){Command=NetCommand.SetState,State=state.Clone()});Update(m,response);
                return new PushResult(m,response.Ok,response.Message);
            }finally{_slots.Release();}
        }));
    }
    public async Task<PushResult> ReconcileAsync(MachineInfo m)
    {
        await _slots.WaitAsync();try
        {
            var status=await _client.SendAsync(m,new(){Command=NetCommand.GetStatus});Update(m,status);
            if(!status.Ok)return new(m,false,status.Message);
            var desired=Intended;
            if(desired is null)return new(m,true,"Connected; no baseline configured yet.");
            if(status.State?.PolicyId==desired.PolicyId)return new(m,status.Health?.Healthy==true,status.Health?.Summary??"Verified");
            var push=await _client.SendAsync(m,new(){Command=NetCommand.SetState,State=desired});Update(m,push);
            return new(m,push.Ok,push.Message);
        }finally{_slots.Release();}
    }
    public static void Update(MachineInfo m,NetResponse r)
    {
        m.Verified=r.State is not null && r.Machine?.MachineId==m.MachineId;
        if(m.Verified){m.ReportedState=r.State;m.Health=r.Health;m.LastVerified=DateTimeOffset.UtcNow;m.LastSeen=m.LastVerified;}
    }
}

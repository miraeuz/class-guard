using ClassroomGuard.Core.Net;
using ClassroomGuard.Core.Persistence;
namespace ClassroomGuard.Admin.Services;
public sealed class AuditedAdminClient : IAdminClient
{
    private readonly IAdminClient _inner;
    private readonly Func<ActivityJournal> _journal;
    public AuditedAdminClient(IAdminClient inner,Func<ActivityJournal> journal){_inner=inner;_journal=journal;}
    public async Task<NetResponse> SendAsync(MachineInfo machine,NetRequest request,int timeoutMs=10000)
    {
        if(request.Command is NetCommand.GetStatus or NetCommand.Ping)return await _inner.SendAsync(machine,request,timeoutMs);
        var id=Guid.NewGuid().ToString("N");
        var entry=new ActivityEntry(DateTimeOffset.UtcNow,id,Environment.UserDomainName+"\\"+Environment.UserName,$"{machine.Name} ({machine.Ip}:{machine.Port})",request.Command.ToString(),"Requested","");
        ActivityJournal journal;
        try{journal=_journal();journal.Append(entry);}catch(Exception ex){return NetResponse.Fail("Command not sent: activity history unavailable. "+ex.Message);}
        NetResponse response;
        try{response=await _inner.SendAsync(machine,request,timeoutMs);}catch(Exception ex){response=NetResponse.Fail(ex.Message);}
        try{journal.Append(entry with {Time=DateTimeOffset.UtcNow,Result=response.Ok?"Succeeded":"Failed",Detail=response.Message});}
        catch(Exception ex){response.Message+=" | Outcome history save failed: "+ex.Message;}
        return response;
    }
}

using ClassroomGuard.Core;
using ClassroomGuard.Core.Models;
using ClassroomGuard.Core.Persistence;
using ClassroomGuard.Core.Security;
using Microsoft.Extensions.Logging;
namespace ClassroomGuard.Agent;
// Protected service-owned storage. Notification is secondary to durable results.
public sealed class AttemptStore
{
    private readonly AtomicJsonStore<List<StoredAttempt>> _store;
    private List<StoredAttempt> _items;
    private readonly object _gate=new();
    private readonly SemaphoreSlim _deliveryGate=new(1,1);
    public AttemptStore(ILogger log, string? storagePath=null)
    {
        var path=storagePath??Path.Combine(AppPaths.DataDir,"attempts.json");
        _store=new(path,()=>throw new InvalidDataException("Attempt storage corrupt; preserve it for recovery."),log);
        _items=File.Exists(path)?_store.Load():new();
    }
    private void Save()=>_store.Save(_items);
    public AttemptView Start(string owner,TestPackage test)
    {
        lock(_gate)
        {
            var existing=_items.LastOrDefault(x=>x.OwnerSid==owner && !x.Finished);
            if(existing is not null){Expire(existing);Save();return existing.View();}
            if(_items.Count>=5000)throw new IOException("Result archive full; administrator must export/archive results.");
            var a=new StoredAttempt{OwnerSid=owner,Test=test,Answers=Enumerable.Repeat(-1,test.Questions.Count).ToArray(),Deadline=test.TimeLimitMinutes>0?DateTimeOffset.UtcNow.AddMinutes(test.TimeLimitMinutes):null};
            _items.Add(a);try{Save();}catch{_items.Remove(a);throw;}return a.View();
        }
    }
    public AttemptView? Resume(string owner)
    {
        lock(_gate){var a=_items.LastOrDefault(x=>x.OwnerSid==owner);if(a is null)return null;Expire(a);Save();return a.View();}
    }
    public AttemptView Update(string owner,string id,int[]? answers,bool finish)
    {
        lock(_gate)
        {
            var a=_items.FirstOrDefault(x=>x.Id==id && x.OwnerSid==owner)??throw new UnauthorizedAccessException("Attempt not found for this Windows account.");
            Expire(a);
            if(!a.Finished)
            {
                if(answers is not null)
                {
                    if(answers.Length!=a.Answers.Length || answers.Where((n,i)=>n < -1 || n>=a.Test.Questions[i].Options.Count).Any())throw new ArgumentException("Invalid answers.");
                    a.Answers=(int[])answers.Clone();
                }
                if(finish)Finish(a);
            }
            Save();return a.View();
        }
    }
    private static void Expire(StoredAttempt a){if(!a.Finished && a.Deadline is {} d && DateTimeOffset.UtcNow>=d)Finish(a);}
    private static void Finish(StoredAttempt a)
    {
        a.Correct=a.Answers.Where((n,i)=>n>=0 && n==a.Test.Questions[i].CorrectIndex).Count();
        a.Finished=true;a.Delivery="Queued";
    }
    public async Task DeliverAsync(TelegramNotifier telegram,CancellationToken ct)
    {
        if(!await _deliveryGate.WaitAsync(0,ct))return;
        try
        {
            List<StoredAttempt> pending;
            lock(_gate){foreach(var a in _items)Expire(a);Save();pending=_items.Where(x=>x.Finished && x.Delivery!="Delivered").Take(10).ToList();}
            foreach(var a in pending)
            {
                ct.ThrowIfCancellationRequested();
                var sent=await telegram.SendAsync($"Test result #{a.Id}\nPC: {MachineIdentity.Name}\nWindows SID: {a.OwnerSid}\nTest: {a.Test.Title}\nScore: {a.Correct}/{a.Answers.Length}\nStarted UTC: {a.Started:O}",ct);
                lock(_gate){a.Delivery=sent?"Delivered":"Queued — retry pending";Save();}
            }
        }
        finally{_deliveryGate.Release();}
    }
}

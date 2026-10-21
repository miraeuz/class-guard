using System.Text.Json;
using ClassroomGuard.Core;
using ClassroomGuard.Core.Models;
using ClassroomGuard.Core.Persistence;
using ClassroomGuard.Core.Security;
using Microsoft.Extensions.Logging;
namespace ClassroomGuard.Agent;
public sealed class AttemptStore
{
    private readonly AtomicJsonStore<List<StoredAttempt>> _store;
    private List<StoredAttempt> _items;
    private readonly object _gate=new();
    private readonly SemaphoreSlim _deliveryGate=new(1,1);
    private readonly Func<DateTimeOffset> _now;
    public AttemptStore(ILogger log,string? storagePath=null,Func<DateTimeOffset>? now=null)
    {
        _now=now??(()=>DateTimeOffset.UtcNow);
        var path=storagePath??Path.Combine(AppPaths.DataDir,"attempts.json");
        _store=new(path,()=>throw new InvalidDataException("Attempt storage corrupt; preserve it for recovery."),log);
        _items=File.Exists(path)?_store.Load():new();
    }
    private static StoredAttempt Copy(StoredAttempt a)=>new(){Id=a.Id,OwnerSid=a.OwnerSid,Test=a.Test,Answers=(int[])a.Answers.Clone(),Started=a.Started,Deadline=a.Deadline,Paused=a.Paused,RemainingSeconds=a.RemainingSeconds,Finished=a.Finished,Correct=a.Correct,Delivery=a.Delivery,TimingHistory=new(a.TimingHistory??new())};
    private List<StoredAttempt> Draft()=>_items.Select(Copy).ToList();
    private void Commit(List<StoredAttempt> next){_store.Save(next);_items=next;}
    public LessonSummary Summarize(LessonSession lesson)
    {
        lock(_gate){var attempts=_items.Where(a=>a.Started>=lesson.Started).ToList();var done=attempts.Where(a=>a.Finished).ToList();return new(lesson.Id,lesson.Title,lesson.Group,lesson.Started,_now(),done.Count,attempts.Count(a=>!a.Finished),done.Sum(a=>a.Correct),done.Sum(a=>a.Answers.Length));}
    }
    public int PausedCount {get{lock(_gate)return _items.Count(a=>a.Paused&&!a.Finished);}}
    public AttemptView Start(string owner,TestPackage test)
    {
        lock(_gate)
        {
            var next=Draft();var existing=next.LastOrDefault(x=>x.OwnerSid==owner&&!x.Finished);
            if(existing is not null){if(Expire(existing))Commit(next);return existing.View();}
            if(next.Count>=5000)throw new IOException("Result archive full; administrator must export/archive results.");
            var snapshot=JsonSerializer.Deserialize<TestPackage>(JsonSerializer.Serialize(test))!;
            var a=new StoredAttempt{OwnerSid=owner,Test=snapshot,Started=_now(),Answers=Enumerable.Repeat(-1,test.Questions.Count).ToArray(),Deadline=test.TimeLimitMinutes>0?_now().AddMinutes(test.TimeLimitMinutes):null};
            next.Add(a);Commit(next);return a.View();
        }
    }
    public AttemptView? Resume(string owner)
    {
        lock(_gate){var next=Draft();var a=next.LastOrDefault(x=>x.OwnerSid==owner);if(a is null)return null;if(Expire(a))Commit(next);return a.View();}
    }
    public AttemptView Update(string owner,string id,int[]? answers,bool finish)
    {
        lock(_gate)
        {
            var next=Draft();var a=next.FirstOrDefault(x=>x.Id==id&&x.OwnerSid==owner)??throw new UnauthorizedAccessException("Attempt not found for this Windows account.");
            if(a.Paused&&!a.Finished)throw new InvalidOperationException("Test paused by teacher. Answers and submission are disabled until resumed.");
            var changed=Expire(a);
            if(!a.Finished)
            {
                if(answers is not null)
                {
                    if(answers.Length!=a.Answers.Length||answers.Where((n,i)=>n < -1||n>=a.Test.Questions[i].Options.Count).Any())throw new ArgumentException("Invalid answers.");
                    a.Answers=(int[])answers.Clone();changed=true;
                }
                if(finish){Finish(a);changed=true;}
            }
            if(changed)Commit(next);return a.View();
        }
    }
    public int SetPaused(bool paused,string actor)
    {
        lock(_gate)
        {
            var next=Draft();var changed=false;var count=0;
            foreach(var a in next.Where(a=>!a.Finished))
            {
                if(Expire(a)){changed=true;continue;}
                if(a.Paused==paused)continue;
                if(paused){a.RemainingSeconds=a.Deadline is {} d?Math.Max(0,(d-_now()).TotalSeconds):null;a.Deadline=null;}
                else {a.Deadline=a.RemainingSeconds is {} seconds?_now().AddSeconds(seconds):null;a.RemainingSeconds=null;}
                a.Paused=paused;
                a.TimingHistory.Add($"{_now():O} | {(paused?"pause":"resume")} | {actor[..Math.Min(actor.Length,128)]}");
                if(a.TimingHistory.Count>200)a.TimingHistory.RemoveAt(0);
                count++;changed=true;
            }
            if(changed)Commit(next);return count;
        }
    }
    private bool Expire(StoredAttempt a){if(!a.Finished&&!a.Paused&&a.Deadline is {} d&&_now()>=d){Finish(a);return true;}return false;}
    private static void Finish(StoredAttempt a){a.Correct=a.Answers.Where((n,i)=>n>=0&&n==a.Test.Questions[i].CorrectIndex).Count();a.Finished=true;a.Delivery="Queued";}
    public async Task DeliverAsync(TelegramNotifier telegram,CancellationToken ct)
    {
        if(!await _deliveryGate.WaitAsync(0,ct))return;
        try
        {
            List<StoredAttempt> pending;
            lock(_gate){var next=Draft();var changed=false;foreach(var a in next)changed|=Expire(a);if(changed)Commit(next);pending=_items.Where(x=>x.Finished&&x.Delivery!="Delivered").Take(10).Select(Copy).ToList();}
            foreach(var a in pending)
            {
                ct.ThrowIfCancellationRequested();
                var sent=await telegram.SendAsync($"Test result #{a.Id}\nPC: {MachineIdentity.Name}\nWindows SID: {a.OwnerSid}\nTest: {a.Test.Title}\nScore: {a.Correct}/{a.Answers.Length}\nStarted UTC: {a.Started:O}",ct);
                lock(_gate){var next=Draft();var current=next.First(x=>x.Id==a.Id);var delivery=sent?"Delivered":"Queued — retry pending";if(current.Delivery!=delivery){current.Delivery=delivery;Commit(next);}}
            }
        }
        finally{_deliveryGate.Release();}
    }
}

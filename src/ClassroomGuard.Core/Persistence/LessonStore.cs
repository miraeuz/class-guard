using ClassroomGuard.Core.Models;
using Microsoft.Extensions.Logging;
namespace ClassroomGuard.Core.Persistence;
public sealed class LessonStore
{
    public sealed class Document { public LessonSession? Active { get; set; } }
    private readonly AtomicJsonStore<Document> _store;
    public LessonSession? Active { get; private set; }
    public LessonStore(string path,ILogger log)
    {
        _store=new(path,()=>throw new InvalidDataException("Lesson state corrupt; preserve file and use administrator recovery."),log);
        if(File.Exists(path)){Active=_store.Load().Active;if(Active is not null)Validate(Active);}
    }
    private static void Validate(LessonSession s)
    {
        if(!Guid.TryParse(s.Id,out _) || string.IsNullOrWhiteSpace(s.Title) || s.Title.Length>120 || s.Group is null || s.Group.Length>80 || s.Policy is null)throw new ArgumentException("Invalid lesson ID, title or group.");
        PolicyValidator.Validate(s.Policy);
    }
    public void Begin(LessonSession session)
    {
        Validate(session);
        if(Active is not null){if(Active.Id==session.Id)return;throw new InvalidOperationException("End the existing lesson first.");}
        var next=new LessonSession { Id=session.Id,Title=session.Title.Trim(),Group=session.Group.Trim(),Started=DateTimeOffset.UtcNow,Policy=session.Policy.Clone() };
        next.Policy.PolicyId="lesson-"+session.Id;
        _store.Save(new(){Active=next});Active=next;
    }
    public void End(string id)
    {
        if(Active is null)return;
        if(Active.Id!=id)throw new InvalidOperationException("Lesson changed; refresh before ending it.");
        _store.Save(new());Active=null;
    }
}

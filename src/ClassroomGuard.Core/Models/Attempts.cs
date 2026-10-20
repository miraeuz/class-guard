namespace ClassroomGuard.Core.Models;
public sealed class AttemptView
{
    public string Id {get;set;}="";
    public TestPackage Test {get;set;}=new();
    public int[] Answers {get;set;}=Array.Empty<int>();
    public DateTimeOffset? Deadline {get;set;}
    public bool Finished {get;set;}
    public int Correct {get;set;}
    public string Delivery {get;set;}="Saved";
}
public sealed class StoredAttempt
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string OwnerSid {get;set;}="";
    public TestPackage Test {get;set;}=new();
    public int[] Answers {get;set;}=Array.Empty<int>();
    public DateTimeOffset Started {get;set;}=DateTimeOffset.UtcNow;
    public DateTimeOffset? Deadline {get;set;}
    public bool Finished {get;set;}
    public int Correct {get;set;}
    public string Delivery {get;set;}="Saved";
    public AttemptView View()
    {
        var test=new TestPackage{Id=Test.Id,Title=Test.Title,ClassName=Test.ClassName,Subject=Test.Subject,TimeLimitMinutes=Test.TimeLimitMinutes,
            ShowReviewAfterFinish=Test.ShowReviewAfterFinish,
            Questions=Test.Questions.Select(q=>q.Copy(Finished && Test.ShowReviewAfterFinish)).ToList()};
        return new(){Id=Id,Test=test,Answers=(int[])Answers.Clone(),Deadline=Deadline,Finished=Finished,Correct=Correct,Delivery=Delivery};
    }
}

using ClassroomGuard.Core.Persistence;
using Microsoft.Extensions.Logging;
namespace ClassroomGuard.Core;
public sealed class AgentConfig
{
    public bool EnableSrpEnforcement {get;set;}
    public List<string> StudentUserSids {get;set;}=new();
    public int CommandPort {get;set;}=AppPaths.DefaultCommandPort;
    public int AnnouncePort {get;set;}=AppPaths.DefaultAnnouncePort;
    public string TelegramBotToken {get;set;}="";
    public string TelegramChatId {get;set;}="";
    public static AgentConfig Load(ILogger log)
    {
        var store=new AtomicJsonStore<AgentConfig>(AppPaths.ConfigFile,()=>throw new InvalidDataException("Invalid agent.json"),log);
        if(!File.Exists(AppPaths.ConfigFile)){var c=new AgentConfig();c.Save(log);return c;}
        var config=store.Load();if(config.StudentUserSids is null)throw new InvalidDataException("StudentUserSids required.");return config;
    }
    public void Save(ILogger log)=>new AtomicJsonStore<AgentConfig>(AppPaths.ConfigFile,()=>new(),log).Save(this);
}

using System.Text.Json;
using ClassroomGuard.Core.Models;
using Microsoft.Extensions.Logging;
namespace ClassroomGuard.Core.Persistence;
public sealed class PolicyStore
{
    private readonly ILogger _log;
    private readonly string _path;
    public PolicyStore(string path,ILogger log){_path=path;_log=log;}
    public EnforcementState Load()
    {
        foreach(var file in new[]{_path,_path+".lastgood"})
        {
            try
            {
                if(!File.Exists(file)) continue;
                var text=File.ReadAllText(file);
                using var doc=JsonDocument.Parse(text);
                if(!doc.RootElement.TryGetProperty("StudentModeActive",out _)) throw new InvalidDataException("Incomplete state.");
                var s=JsonSerializer.Deserialize<EnforcementState>(text) ?? throw new InvalidDataException();
                s.StudentModeActive=true; // Never restore a historical teacher unlock.
                PolicyValidator.Validate(s); return s;
            }
            catch(Exception ex){_log.LogError(ex,"Invalid policy {File}; trying recovery",file);}
        }
        return EnforcementState.FailClosedDefault();
    }
    public void Save(EnforcementState s)
    {
        PolicyValidator.Validate(s);
        new AtomicJsonStore<EnforcementState>(_path,EnforcementState.FailClosedDefault,_log).Save(s);
        new AtomicJsonStore<EnforcementState>(_path+".lastgood",EnforcementState.FailClosedDefault,_log).Save(s);
    }
}

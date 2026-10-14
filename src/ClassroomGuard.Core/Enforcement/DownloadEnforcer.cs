using System.Text.Json;
using ClassroomGuard.Core.Models;
using ClassroomGuard.Core.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
namespace ClassroomGuard.Core.Enforcement;
public sealed class DownloadEnforcer : IEnforcer
{
    public string Name=>"Chrome / Edge download policy";
    private const string ValueName="DownloadRestrictions";
    private readonly AtomicJsonStore<List<Backup>> _store;
    public sealed class Backup
    {
        public RegistryView View {get;set;}
        public string Path {get;set;}="";
        public bool Present {get;set;}
        public RegistryValueKind Kind {get;set;}
        public string Json {get;set;}="null";
    }
    private readonly string _file=Path.Combine(AppPaths.DataDir,"browser-policy-backup.json");
    public DownloadEnforcer(ILogger log)=>_store=new(_file,()=>throw new InvalidDataException("Browser backup missing/corrupt; refusing destructive recovery."),log);
    private List<Backup> Load()=>File.Exists(_file)?_store.Load():new();
    public void Apply(EnforcementState state)
    {
        if(!state.DownloadBlocking || state.Overrides.GetValueOrDefault("download")){Revert();return;}
        var list=Load();
        foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})
        foreach(var path in new[]{@"SOFTWARE\Policies\Google\Chrome",@"SOFTWARE\Policies\Microsoft\Edge"})
        {
            using var hive=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view);
            using var key=hive.CreateSubKey(path,true);
            if(!list.Any(b=>b.View==view && b.Path==path))
            {
                var value=key.GetValue(ValueName,null,RegistryValueOptions.DoNotExpandEnvironmentNames);
                list.Add(new Backup{View=view,Path=path,Present=value is not null,Kind=value is null?RegistryValueKind.None:key.GetValueKind(ValueName),Json=JsonSerializer.Serialize(value)});
                _store.Save(list); // durable prior value before first mutation
            }
        }
        // Capture every registry view before any write: Policies may be shared
        // between 32/64-bit views, so a first write must not become a "prior" value.
        foreach(var b in list)
        {
            using var hive=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,b.View);
            using var key=hive.CreateSubKey(b.Path,true);
            var current=key.GetValue(ValueName,null,RegistryValueOptions.DoNotExpandEnvironmentNames);
            if(current is not int managed || managed!=3)
                if(!EqualsJson(current,Decode(b)))throw new IOException("Browser policy changed externally; refusing to overwrite it.");
            key.SetValue(ValueName,3,RegistryValueKind.DWord);
            if(key.GetValue(ValueName) is not int n || n!=3)throw new IOException("Download policy verification failed.");
        }
    }

    public void Revert()
    {
        var list=Load();
        foreach(var b in list.ToList())
        {
            using var hive=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,b.View);
            using var key=hive.CreateSubKey(b.Path,true);
            var current=key.GetValue(ValueName,null,RegistryValueOptions.DoNotExpandEnvironmentNames);
            var old=Decode(b);
            if(!EqualsJson(current,old))
            {
                if(current is not int n || n!=3) throw new IOException("Browser policy changed externally; backup retained for administrator review.");
                if(b.Present)key.SetValue(ValueName,old!,b.Kind);else key.DeleteValue(ValueName,false);
            }
            list.Remove(b);_store.Save(list);
        }
        if(list.Count==0 && File.Exists(_file))File.Delete(_file);
    }
    private static bool EqualsJson(object? a,object? b)=>JsonSerializer.Serialize(a)==JsonSerializer.Serialize(b);
    private static object? Decode(Backup b)=>!b.Present?null:b.Kind switch
    {
        RegistryValueKind.DWord=>JsonSerializer.Deserialize<int>(b.Json),
        RegistryValueKind.QWord=>JsonSerializer.Deserialize<long>(b.Json),
        RegistryValueKind.MultiString=>JsonSerializer.Deserialize<string[]>(b.Json),
        RegistryValueKind.Binary or RegistryValueKind.None=>JsonSerializer.Deserialize<byte[]>(b.Json),
        _=>JsonSerializer.Deserialize<string>(b.Json)
    };
}

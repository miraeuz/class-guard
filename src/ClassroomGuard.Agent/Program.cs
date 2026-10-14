using ClassroomGuard.Agent;
using ClassroomGuard.Core;
using Microsoft.Extensions.Logging;

// The agent runs as a Windows Service under LocalSystem so it:
//   * starts at boot before any user logs in,
//   * cannot be closed via Task Manager / Alt+F4 / sign-out,
//   * has the rights to edit the hosts file and (later) apply policy.
// (spec §3 — "Why a Windows Service, not a normal app")

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ClassroomGuardAgent";
});

// Simple file + event log. (A rolling file sink via Serilog is a fine upgrade.)
builder.Logging.AddSimpleConsole(o => o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ");
Directory.CreateDirectory(AppPaths.DataDir);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

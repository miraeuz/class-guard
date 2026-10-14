using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ClassroomGuard.Core;
using ClassroomGuard.Core.Ipc;

namespace ClassroomGuard.StudentShell;

/// <summary>Talks to the local agent's control pipe.</summary>
public static class AgentPipeClient
{
    public static async Task<ControlResponse> SendAsync(ControlRequest req, int timeoutMs = 4000)
    {
        try
        {
            using var deadline = new CancellationTokenSource(timeoutMs);
            using var client = await ClassroomGuard.Core.Security.LocalPipeClient.ConnectAsync(deadline.Token);

            using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, leaveOpen: true);

            await Wire.WriteAsync(writer, JsonSerializer.Serialize(req), deadline.Token);
            var line = await Wire.ReadAsync(reader, deadline.Token);
            return line is null ? ControlResponse.Fail("No response from agent.")
                                : JsonSerializer.Deserialize<ControlResponse>(line)
                                  ?? ControlResponse.Fail("Bad response.");
        }
        catch (Exception ex)
        {
            return ControlResponse.Fail("Agent not reachable: " + ex.Message);
        }
    }
}

/// <summary>Minimal modal text prompt (no built-in WPF InputBox).</summary>
public static class Prompt
{
    public static string? Ask(Window owner, string title, string message, bool password = false)
    {
        var win = new Window
        {
            Title = title, Width = 360, Height = 190, Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.ToolWindow,
        };
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap,
                                           Margin = new Thickness(0, 0, 0, 8) });

        Control input = password ? new PasswordBox() : new TextBox();
        input.MinWidth = 300;
        panel.Children.Add(input);

        string? result = null;
        var ok = new Button { Content = "OK", Width = 90, Margin = new Thickness(0, 12, 0, 0),
                              HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true };
        ok.Click += (_, _) =>
        {
            result = password ? ((PasswordBox)input).Password : ((TextBox)input).Text;
            win.DialogResult = true;
        };
        panel.Children.Add(ok);
        win.Content = panel;
        return win.ShowDialog() == true ? result : null;
    }
}

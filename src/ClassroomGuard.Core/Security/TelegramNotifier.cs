using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ClassroomGuard.Core.Security;

/// <summary>Agent-side notification sender. The token is stored in the administrator-only Agent configuration. Results are persisted and retried by AttemptStore.</summary>
public sealed class TelegramNotifier
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly ILogger _log;
    private readonly Func<string> _token;
    private readonly Func<string> _chatId;

    public TelegramNotifier(ILogger log, Func<string> token, Func<string> chatId)
    {
        _log = log;
        _token = token;
        _chatId = chatId;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_token()) && !string.IsNullOrWhiteSpace(_chatId());

    public async Task<bool> SendAsync(string text, CancellationToken ct = default)
    {
        var token = _token();
        var chatId = _chatId();
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        {
            _log.LogWarning("[Telegram] not configured (TelegramBotToken/TelegramChatId empty) — message not sent.");
            return false;
        }

        try
        {
            var url = $"https://api.telegram.org/bot{token}/sendMessage";
            var payload = JsonSerializer.Serialize(new { chat_id = chatId, text });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var resp = await Http.PostAsync(url, content, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _log.LogError("[Telegram] send failed: {Status} {Body}", resp.StatusCode, body);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[Telegram] send threw.");
            return false;
        }
    }
}

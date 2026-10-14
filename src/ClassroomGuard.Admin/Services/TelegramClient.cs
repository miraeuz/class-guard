using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ClassroomGuard.Admin.Services;

/// <summary>
/// Optional Telegram path (spec §4.2 primary unlock delivery). Only the teacher's
/// Admin PC needs outbound HTTPS; student PCs never touch the internet for this.
/// The offline TOTP path (Totp/ClassroomKey) is the fallback when Telegram or the
/// internet is unavailable.
/// </summary>
public sealed class TelegramClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<(bool ok, string message)> SendAsync(string token, string chatId, string text)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
            return (false, "Telegram not configured.");
        try
        {
            var url = $"https://api.telegram.org/bot{token}/sendMessage";
            var payload = JsonSerializer.Serialize(new { chat_id = chatId, text });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var resp = await Http.PostAsync(url, content);
            var body = await resp.Content.ReadAsStringAsync();
            return resp.IsSuccessStatusCode ? (true, "sent") : (false, body);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}

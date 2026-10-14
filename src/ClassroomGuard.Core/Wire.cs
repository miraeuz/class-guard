using System.Text;
namespace ClassroomGuard.Core;
public static class Wire
{
    public const int MaxChars = 1024 * 1024;
    // One reader per session. Character buffering belongs to StreamReader, which
    // preserves subsequent frames; the explicit length cap bounds line allocation.
    public static async Task<string?> ReadAsync(StreamReader reader, CancellationToken ct)
    {
        var text = new StringBuilder();
        var one = new char[1];
        while (true)
        {
            var n = await reader.ReadAsync(one.AsMemory(), ct);
            if (n==0) return text.Length==0 ? null : throw new IOException("Incomplete frame.");
            if (one[0]=='\n') return text.ToString().TrimEnd('\r');
            if (text.Length>=MaxChars) throw new IOException("Frame too large.");
            text.Append(one[0]);
        }
    }
    public static async Task WriteAsync(StreamWriter writer,string json,CancellationToken ct)
    {
        if (json.Length>MaxChars) throw new IOException("Frame too large.");
        await writer.WriteLineAsync(json.AsMemory(),ct);
        await writer.FlushAsync(ct);
    }
}

using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;
using Application.DTOs;
using Domain.Entities.QuestionBank;

namespace Application.Common;

// Versioned, namespaced payloads in the existing TEXT column. Plain task notes
// (including MATRIX rejection comments) are never interpreted or rewritten.
public static class TaskJournalCodec
{
    public const string Prefix = "{\"format\":\"phucvm.task-journal\"";
    private const int ChunkBytes = 16384;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public sealed record Lookup(ulong EntityId, string Purpose, ulong? AcademicYearId);
    public sealed record Part(string Format, int Schema, Guid EventId, string Stream, int Revision,
        string Action, int Index, int Count, string Base64, Lookup? Lookup = null, string Encoding = "json", string? Hash = null);
    public sealed record Entry(ulong Id, string Stream, int Revision, string Action, ulong ActorId, DateTime At, string Data)
    {
        public T Read<T>()
        {
            try
            {
                return JsonSerializer.Deserialize<T>(Data, Json)
                    ?? throw new QuestionException("JOURNAL_INVALID", "Dữ liệu lịch sử không hợp lệ.", 409);
            }
            catch (JsonException)
            {
                throw new QuestionException("JOURNAL_INVALID", "Dữ liệu lịch sử không hợp lệ.", 409);
            }
        }
    }

    public static IReadOnlyList<string> Encode<T>(string stream, int revision, string action, T data, Lookup? lookup = null)
    {
        if (string.IsNullOrWhiteSpace(stream) || revision < 1 || string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Invalid task journal envelope.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, Json);
        if (bytes.Length > 128 * 1024 * 1024) throw new QuestionException("SNAPSHOT_TOO_LARGE", "Dữ liệu bản chụp vượt giới hạn 128 MB.");
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        using (var output = new MemoryStream())
        {
            using (var compressor = new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true)) compressor.Write(bytes);
            bytes = output.ToArray();
        }
        if (bytes.Length > 48 * 1024 * 1024) throw new QuestionException("SNAPSHOT_TOO_LARGE", "Bản chụp sau khi nén vượt giới hạn an toàn của giao dịch.");
        var count = Math.Max(1, (bytes.Length + ChunkBytes - 1) / ChunkBytes);
        var id = Guid.NewGuid();
        return Enumerable.Range(0, count).Select(index => JsonSerializer.Serialize(new Part(
            "phucvm.task-journal", 1, id, stream, revision, action, index, count,
            Convert.ToBase64String(bytes.AsSpan(index * ChunkBytes, Math.Min(ChunkBytes, bytes.Length - index * ChunkBytes))), lookup, "br", hash), Json)).ToArray();
    }

    public static IReadOnlyList<Entry> Decode(IEnumerable<TaskHistory> rows, string stream, bool latestOnly = false)
    {
        try
        {
            var parts = rows.Where(row => row.Content.StartsWith(Prefix, StringComparison.Ordinal))
                .Select(row => (Row: row, Part: JsonSerializer.Deserialize<Part>(row.Content, Json)!))
                .Where(pair => pair.Part.Stream == stream).ToArray();
            var entries = new List<Entry>();
            var latestRevision = parts.Select(pair => pair.Part.Revision).DefaultIfEmpty(0).Max();
            foreach (var group in parts.GroupBy(pair => pair.Part.EventId))
            {
                var ordered = group.OrderBy(pair => pair.Part.Index).ToArray();
                var first = ordered[0]; var header = first.Part;
                if (header.Schema != 1 || header.Revision < 1 || header.Count < 1 || header.Count > 8192 ||
                    ordered.Length != header.Count || ordered.Where((pair, index) => pair.Part.Index != index ||
                        pair.Part.Schema != 1 || pair.Part.Count != header.Count || pair.Part.Action != header.Action ||
                        pair.Part.Format != "phucvm.task-journal" || pair.Part.Lookup != header.Lookup ||
                        pair.Part.Revision != header.Revision || pair.Part.Encoding != header.Encoding || pair.Part.Hash != header.Hash ||
                        pair.Row.CreateBy != first.Row.CreateBy).Any())
                    throw new InvalidOperationException("Incomplete task journal event.");
                if (latestOnly && header.Revision != latestRevision)
                {
                    entries.Add(new(first.Row.Id, stream, header.Revision, header.Action, first.Row.CreateBy,
                        DateTime.SpecifyKind(first.Row.CreateAt, DateTimeKind.Utc), ""));
                    continue;
                }
                var bytes = ordered.SelectMany(pair => Convert.FromBase64String(pair.Part.Base64)).ToArray();
                if (header.Encoding == "br")
                {
                    using var input = new MemoryStream(bytes);
                    using var decompressor = new BrotliStream(input, CompressionMode.Decompress);
                    using var output = new MemoryStream();
                    var buffer = new byte[16384]; int read;
                    while ((read = decompressor.Read(buffer)) > 0)
                    {
                        if (output.Length + read > 128 * 1024 * 1024) throw new InvalidDataException("Snapshot too large.");
                        output.Write(buffer, 0, read);
                    }
                    bytes = output.ToArray();
                }
                else if (header.Encoding != "json") throw new InvalidDataException("Unknown snapshot encoding.");
                if (header.Hash is not null && header.Hash != Convert.ToHexString(SHA256.HashData(bytes)))
                    throw new InvalidDataException("Snapshot checksum mismatch.");
                entries.Add(new(first.Row.Id, stream, header.Revision, header.Action, first.Row.CreateBy,
                    DateTime.SpecifyKind(first.Row.CreateAt, DateTimeKind.Utc), Encoding.UTF8.GetString(bytes)));
            }
            var result = entries.OrderBy(entry => entry.Revision).ToArray();
            if (result.Where((entry, index) => entry.Revision != index + 1).Any())
                throw new InvalidOperationException("Non-contiguous task journal revision.");
            return result;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException or NullReferenceException or InvalidDataException or IOException)
        {
            throw new QuestionException("JOURNAL_INVALID", "Lịch sử không đầy đủ hoặc sai định dạng. Vui lòng liên hệ quản trị viên.", 409);
        }
    }
}

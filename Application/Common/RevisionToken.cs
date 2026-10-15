using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Application.Common;

// A deterministic response token for tables whose schema has no version column.
// The mutation compares it after locking the current native row.
public static class RevisionToken
{
    public static uint For(params object?[] values)
    {
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(values));
        return BinaryPrimitives.ReadUInt32BigEndian(hash) | 1u;
    }
}

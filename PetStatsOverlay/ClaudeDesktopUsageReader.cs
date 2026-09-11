using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PetStatsOverlay;

/// <summary>
/// Read-only reader for Claude Desktop's Chromium conversation cache. No database
/// connection, app injection, credentials, or prompt text is written to disk.
/// LevelDB and V8 formats: https://github.com/google/leveldb/tree/main/doc and
/// https://github.com/v8/v8/blob/main/src/objects/value-serializer.cc.
/// Unsupported/corrupt records are skipped rather than guessed.
/// </summary>
public static class ClaudeDesktopUsageReader
{
    private const int MaxDecodedBytes = 64 * 1024 * 1024;
    private sealed record Entry(byte[] Key, byte[] Value, ulong Sequence, bool Deleted);

    public static IEnumerable<(string Source, JsonElement Payload)> Read(string rootPath, int maxFileMb = 128)
    {
        var root = AiLogRootDiscovery.ExpandPath(rootPath);
        var directory = Path.Combine(root, "IndexedDB", "https_claude.ai_0.indexeddb.leveldb");
        if (root.EndsWith("https_claude.ai_0.indexeddb.leveldb", StringComparison.OrdinalIgnoreCase)) directory = root;
        if (!Directory.Exists(directory)) yield break;
        var entries = ReadEntries(directory, Math.Clamp(maxFileMb, 1, 512) * 1024L * 1024L);
        // Only the named conversation store is relevant; never deserialize auth/key stores.
        var databaseIds = FindConversationDatabases(entries.Values);
        foreach (var entry in entries.Values.Where(item => !item.Deleted))
        {
            Dictionary<string, object?>? conversation = null;
            try
            {
                var prefix = ReadPrefix(entry.Key);
                if (prefix.Index != 1 || !databaseIds.Contains(prefix.Database)) continue;
                var cursor = new Cursor(entry.Value);
                cursor.Varint(); // IndexedDB record version, not the V8 version.
                var bytes = Unwrap(cursor.Remaining(), entry, entries, directory, 0);
                if (bytes is null) continue;
                conversation = new V8Reader(bytes).Read() as Dictionary<string, object?>;
            }
            catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or OverflowException or ArgumentException or IOException or UnauthorizedAccessException)
            {
                // App writes and database compaction may leave a partial read. Retry next refresh.
            }
            if (conversation is null) continue;
            foreach (var payload in ExtractUsage(conversation)) yield return (directory, payload);
        }
    }

    private static HashSet<ulong> FindConversationDatabases(IEnumerable<Entry> entries)
    {
        var ids = new HashSet<ulong>();
        foreach (var entry in entries.Where(item => !item.Deleted && item.Key.Length > 5))
        {
            if (!entry.Key.AsSpan(0, 5).SequenceEqual(new byte[] { 0, 0, 0, 0, 201 })) continue;
            try
            {
                var c = new Cursor(entry.Key, 5);
                var origin = c.String(Encoding.BigEndianUnicode, checked(c.Length() * 2));
                var name = c.String(Encoding.BigEndianUnicode, checked(c.Length() * 2));
                if (name == "claude-conversation-store" && origin.Contains("claude.ai", StringComparison.OrdinalIgnoreCase))
                    ids.Add(LittleInteger(entry.Value));
            }
            catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or OverflowException) { }
        }
        return ids;
    }

    private static IEnumerable<JsonElement> ExtractUsage(Dictionary<string, object?> conversation)
    {
        if (GetObject(conversation, "tree") is not { } tree || GetArray(tree, "events") is not { } events) yield break;
        // Result events contain session aggregates, overlapping assistant messages.
        // Prefer per-message usage; retain result/modelUsage when a cache only has summaries.
        var messages = events.OfType<Dictionary<string, object?>>()
            .Select(item => (Event: item, Payload: GetObject(item, "payload")))
            .Where(item => item.Payload is not null).ToList();
        var sessionsWithMessages = messages.Where(item => GetObject(GetObject(item.Payload!, "message"), "usage") is not null)
            .Select(item => GetString(item.Payload!, "session_id")).ToHashSet(StringComparer.Ordinal);
        foreach (var item in messages)
        {
            var payload = item.Payload!;
            var message = GetObject(payload, "message");
            var result = GetString(payload, "type") == "result";
            // Per-model summaries may include a second model whose assistant
            // messages were not retained. The aggregator removes covered models
            // by session/model, so preserve modelUsage rather than dropping the
            // whole result whenever any message exists in the session.
            if (GetObject(message, "usage") is null && (!result
                || (sessionsWithMessages.Contains(GetString(payload, "session_id")) && GetObject(payload, "modelUsage") is null))) continue;
            var clean = CopyFields(payload, "type", "uuid", "request_id", "session_id", "timestamp", "created_at");
            if (!clean.ContainsKey("timestamp") && !clean.ContainsKey("created_at") && item.Event.TryGetValue("serverCreatedAt", out var created))
                clean["timestamp"] = created;
            // Explicitly whitelist metadata; never pass message content, results, or tool input on.
            if (message is not null) clean["message"] = CopyFields(message, "id", "model", "role", "usage", "stop_reason");
            if (result)
            {
                foreach (var pair in CopyFields(payload, "usage", "modelUsage", "total_cost_usd", "num_turns")) clean[pair.Key] = pair.Value;
            }
            yield return JsonSerializer.SerializeToElement(clean);
        }
    }

    private static Dictionary<string, object?> CopyFields(Dictionary<string, object?> source, params string[] names) =>
        names.Where(source.ContainsKey).ToDictionary(name => name, name => source[name]);
    private static Dictionary<string, object?>? GetObject(Dictionary<string, object?>? obj, string key) => obj?.GetValueOrDefault(key) as Dictionary<string, object?>;
    private static List<object?>? GetArray(Dictionary<string, object?> obj, string key) => obj.GetValueOrDefault(key) as List<object?>;
    private static string GetString(Dictionary<string, object?> obj, string key) => obj.GetValueOrDefault(key) as string ?? "";

    private static byte[]? Unwrap(byte[] value, Entry entry, Dictionary<string, Entry> entries, string directory, int depth)
    {
        if (depth > 3) throw new InvalidDataException("Too many cache wrappers.");
        var c = new Cursor(value);
        if (c.Byte() != 255) return null;
        var version = c.Varint();
        if (version == 17)
        {
            var command = c.Byte();
            if (command == 2) return Unwrap(Snappy(c.Remaining()), entry, entries, directory, depth + 1);
            if (command != 1) return null;
            var expectedLength = c.Length();
            var blobIndex = c.Length();
            if (expectedLength > MaxDecodedBytes) return null;
            var prefix = ReadPrefix(entry.Key);
            var blobKey = entry.Key.ToArray();
            // Index 1 and 3 use the same encoded length; change only the index bytes.
            var indexOffset = prefix.Length - prefix.IndexBytes;
            Array.Clear(blobKey, indexOffset, prefix.IndexBytes);
            blobKey[indexOffset] = 3;
            if (!entries.TryGetValue(Convert.ToHexString(blobKey), out var blobs) || blobs.Deleted) return null;
            var info = new Cursor(blobs.Value);
            for (var i = 0; i <= blobIndex; i++)
            {
                var kind = info.Byte();
                if (kind > 1) return null;
                var number = info.Varint();
                info.Take(checked(info.Length() * 2)); // MIME type
                info.Varint(); // size
                if (kind == 1) { info.Take(checked(info.Length() * 2)); info.Varint(); }
                if (i != blobIndex) continue;
                var blobRoot = directory[..^".leveldb".Length] + ".blob";
                var path = Path.Combine(blobRoot, prefix.Database.ToString("x"), (number >> 8).ToString("x2"), number.ToString("x"));
                var content = ReadShared(path, MaxDecodedBytes);
                return content is null || content.Length != expectedLength ? null : Unwrap(content, entry, entries, directory, depth + 1);
            }
            return null;
        }
        if (version >= 21)
        {
            if (c.Byte() != 254) return null;
            c.Take(12); // Blink trailer offset and length.
        }
        return c.Remaining();
    }

    private static Dictionary<string, Entry> ReadEntries(string directory, long limit)
    {
        var latest = new Dictionary<string, Entry>(StringComparer.Ordinal);
        // Read tables before WAL files. Sequence numbers, including tombstones, decide winners.
        IEnumerable<string> files;
        try { files = Directory.GetFiles(directory).Where(path => Path.GetExtension(path) is ".ldb" or ".sst" or ".log").OrderBy(path => path).ToArray(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return latest; }
        foreach (var file in files)
        {
            try
            {
                var bytes = ReadShared(file, limit);
                if (bytes is null) continue;
                var records = file.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ? ReadLog(bytes) : ReadTable(bytes);
                foreach (var record in records)
                {
                    var key = Convert.ToHexString(record.Key);
                    if (!latest.TryGetValue(key, out var old) || record.Sequence > old.Sequence) latest[key] = record;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or OverflowException or ArgumentException) { }
        }
        return latest;
    }

    private static byte[]? ReadShared(string path, long limit)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > limit || stream.Length > int.MaxValue) return null;
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static IEnumerable<Entry> ReadLog(byte[] bytes)
    {
        using var fragments = new MemoryStream();
        var assembling = false;
        for (var p = 0; p + 7 <= bytes.Length;)
        {
            var remaining = 32768 - p % 32768;
            if (remaining < 7) { p += remaining; continue; }
            var length = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(p + 4, 2));
            var type = bytes[p + 6];
            if (length + 7 > remaining || p + length + 7 > bytes.Length) { p += remaining; assembling = false; continue; }
            if (type == 0) { p += remaining; assembling = false; continue; }
            var crc = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(p, 4));
            if (MaskedCrc(bytes.AsSpan(p + 6, length + 1)) != crc) { p += length + 7; assembling = false; continue; }
            if (type is 1 or 2) { fragments.SetLength(0); assembling = true; }
            if (assembling) fragments.Write(bytes, p + 7, length);
            p += length + 7;
            if (fragments.Length > MaxDecodedBytes) { assembling = false; fragments.SetLength(0); }
            if (!assembling || type is not (1 or 4)) continue;
            assembling = false;
            foreach (var entry in ReadBatch(fragments.ToArray())) yield return entry;
        }
    }

    private static List<Entry> ReadBatch(byte[] bytes)
    {
        var records = new List<Entry>();
        if (bytes.Length < 12) return records;
        var seq = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        var count = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8));
        if (count > 1_000_000) return records;
        var c = new Cursor(bytes, 12);
        try
        {
            for (uint i = 0; i < count; i++)
            {
                var type = c.Byte();
                if (type > 1) return [];
                var key = c.Take(c.Length());
                var value = type == 1 ? c.Take(c.Length()) : [];
                records.Add(new Entry(key, value, seq + i, type == 0));
            }
            return c.Position == bytes.Length ? records : [];
        }
        catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException or OverflowException) { return []; }
    }

    private static IEnumerable<Entry> ReadTable(byte[] bytes)
    {
        if (bytes.Length < 48 || BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(bytes.Length - 8)) != 0xdb4775248b80fb57UL) yield break;
        var footer = new Cursor(bytes, bytes.Length - 48);
        footer.Varint(); footer.Varint(); // metaindex
        var index = ReadBlock(bytes, footer.Length(), footer.Length());
        foreach (var pair in BlockEntries(index))
        {
            var handle = new Cursor(pair.Value);
            var block = ReadBlock(bytes, handle.Length(), handle.Length());
            foreach (var item in BlockEntries(block))
            {
                if (item.Key.Length < 8) continue;
                var trailer = BinaryPrimitives.ReadUInt64LittleEndian(item.Key.AsSpan(item.Key.Length - 8));
                if ((trailer & 255) > 1) continue;
                yield return new Entry(item.Key[..^8], item.Value, trailer >> 8, (trailer & 255) == 0);
            }
        }
    }

    private static byte[] ReadBlock(byte[] bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || (long)offset + length + 5 > bytes.Length) throw new InvalidDataException("Invalid table block.");
        var crc = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + length + 1));
        if (MaskedCrc(bytes.AsSpan(offset, length + 1)) != crc) throw new InvalidDataException("Table checksum mismatch.");
        return bytes[offset + length] switch
        {
            0 => bytes.AsSpan(offset, length).ToArray(),
            1 => Snappy(bytes.AsSpan(offset, length).ToArray()),
            _ => throw new InvalidDataException("Unsupported table compression.")
        };
    }

    private static IEnumerable<(byte[] Key, byte[] Value)> BlockEntries(byte[] bytes)
    {
        if (bytes.Length < 4) yield break;
        var restarts = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(bytes.Length - 4));
        var end = bytes.Length - 4L - restarts * 4L;
        if (end < 0) yield break;
        var c = new Cursor(bytes);
        byte[] previous = [];
        while (c.Position < end)
        {
            var shared = c.Length(); var added = c.Length(); var valueLength = c.Length();
            if (shared > previous.Length || (long)c.Position + added + valueLength > end) throw new InvalidDataException("Invalid table entry.");
            var key = new byte[checked(shared + added)];
            previous.AsSpan(0, shared).CopyTo(key);
            c.Take(added).CopyTo(key, shared);
            previous = key;
            yield return (key, c.Take(valueLength));
        }
    }

    private static byte[] Snappy(byte[] bytes)
    {
        var c = new Cursor(bytes);
        var length = c.Length();
        if (length > MaxDecodedBytes) throw new InvalidDataException("Cache record too large.");
        var output = new byte[length];
        var p = 0;
        while (p < length)
        {
            var tag = c.Byte();
            var kind = tag & 3;
            int count;
            if (kind == 0)
            {
                count = (tag >> 2) + 1;
                if (count > 60) count = checked((int)LittleInteger(c.Take(count - 60)) + 1);
                if ((long)p + count > length) throw new InvalidDataException("Invalid Snappy literal.");
                c.Take(count).CopyTo(output, p); p += count;
            }
            else
            {
                count = kind == 1 ? 4 + ((tag >> 2) & 7) : 1 + (tag >> 2);
                var offset = kind == 1 ? ((tag & 224) << 3) | c.Byte() : checked((int)LittleInteger(c.Take(kind == 2 ? 2 : 4)));
                if (offset <= 0 || offset > p || (long)p + count > length) throw new InvalidDataException("Invalid Snappy copy.");
                for (var i = 0; i < count; i++) { output[p] = output[p - offset]; p++; }
            }
        }
        if (c.Position != bytes.Length) throw new InvalidDataException("Trailing Snappy data.");
        return output;
    }

    private static uint MaskedCrc(ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        foreach (var b in data)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0x82f63b78U : 0);
        }
        crc = ~crc;
        return unchecked(((crc >> 15) | (crc << 17)) + 0xa282ead8U);
    }

    private static (ulong Database, ulong Store, ulong Index, int Length, int IndexBytes) ReadPrefix(byte[] bytes)
    {
        var c = new Cursor(bytes);
        var sizes = c.Byte();
        var dbBytes = (sizes >> 5) + 1; var storeBytes = ((sizes >> 2) & 7) + 1; var indexBytes = (sizes & 3) + 1;
        return (LittleInteger(c.Take(dbBytes)), LittleInteger(c.Take(storeBytes)), LittleInteger(c.Take(indexBytes)), c.Position, indexBytes);
    }

    private static ulong LittleInteger(byte[] bytes)
    {
        if (bytes.Length > 8) throw new InvalidDataException("Integer too long.");
        ulong value = 0;
        for (var i = 0; i < bytes.Length; i++) value |= (ulong)bytes[i] << (i * 8);
        return value;
    }

    private sealed class Cursor(byte[] bytes, int position = 0)
    {
        public int Position { get; private set; } = position;
        public byte Peek() => Position < bytes.Length ? bytes[Position] : throw new EndOfStreamException();
        public byte Byte() { var value = Peek(); Position++; return value; }
        public ulong Varint()
        {
            ulong value = 0;
            for (var shift = 0; shift < 64; shift += 7)
            {
                var b = Byte();
                if (shift == 63 && b > 1) throw new InvalidDataException("Invalid varint.");
                value |= (ulong)(b & 127) << shift;
                if ((b & 128) == 0) return value;
            }
            throw new InvalidDataException("Invalid varint.");
        }
        public int Length() => checked((int)Varint());
        public byte[] Take(int count)
        {
            if (count < 0 || (long)Position + count > bytes.Length) throw new EndOfStreamException();
            var value = bytes.AsSpan(Position, count).ToArray(); Position += count; return value;
        }
        public byte[] Remaining() => Take(bytes.Length - Position);
        public string String(Encoding encoding, int count) => encoding.GetString(Take(count));
        public double Double() => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(Take(8)));
    }

    private sealed class V8Reader(byte[] bytes)
    {
        private readonly Cursor c = new(bytes);
        private readonly List<object?> objects = [];
        private int valuesRead;
        public object? Read()
        {
            if (c.Byte() != 255) throw new InvalidDataException("Missing V8 header.");
            var version = c.Varint();
            if (version is < 10 or > 16) throw new InvalidDataException("Unsupported V8 version.");
            return Value(0);
        }
        private object? Value(int depth)
        {
            if (depth > 100 || ++valuesRead > 2_000_000) throw new InvalidDataException("Cache object too complex.");
            byte tag;
            do { tag = c.Byte(); } while (tag == 0);
            switch ((char)tag)
            {
                case '_': case '-': case '0': return null;
                case 'T': return true;
                case 'F': return false;
                case 'I': var zigzag = c.Varint(); return (long)(zigzag >> 1) ^ -((long)zigzag & 1);
                case 'U': return c.Varint();
                case 'N': var number = c.Double(); return double.IsFinite(number) ? number : null;
                case '"': return c.String(Encoding.Latin1, c.Length());
                case 'S': return c.String(Encoding.UTF8, c.Length());
                case 'c': return c.String(Encoding.Unicode, c.Length());
                case '^': var id = c.Length(); return id < objects.Count ? objects[id] : throw new InvalidDataException("Invalid object reference.");
                case '?': c.Varint(); return Value(depth + 1);
                case 'o':
                    var obj = new Dictionary<string, object?>(); objects.Add(obj);
                    while (c.Peek() != (byte)'{')
                    {
                        var key = Convert.ToString(Value(depth + 1), CultureInfo.InvariantCulture) ?? "";
                        obj[key] = Value(depth + 1);
                    }
                    c.Byte(); c.Varint(); return obj;
                case 'A':
                    var count = c.Length();
                    if (count > 1_000_000) throw new InvalidDataException("Array too large.");
                    var array = new List<object?>(count); objects.Add(array);
                    for (var i = 0; i < count; i++) array.Add(Value(depth + 1));
                    while (c.Peek() != (byte)'$') { Value(depth + 1); Value(depth + 1); }
                    c.Byte(); c.Varint(); if (c.Length() != count) throw new InvalidDataException("Invalid array length."); return array;
                case 'a':
                    var sparseLength = c.Length();
                    if (sparseLength > 1_000_000) throw new InvalidDataException("Array too large.");
                    var sparse = Enumerable.Repeat<object?>(null, sparseLength).ToList(); objects.Add(sparse);
                    while (c.Peek() != (byte)'@')
                    {
                        var key = Convert.ToString(Value(depth + 1), CultureInfo.InvariantCulture);
                        var value = Value(depth + 1);
                        if (int.TryParse(key, out var index) && index >= 0 && index < sparseLength) sparse[index] = value;
                    }
                    c.Byte(); c.Varint(); if (c.Length() != sparseLength) throw new InvalidDataException("Invalid sparse array."); return sparse;
                case 'D': var date = c.Double(); objects.Add(date); return date;
                default: throw new InvalidDataException($"Unsupported V8 tag {tag}.");
            }
        }
    }
}

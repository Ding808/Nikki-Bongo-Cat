using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using PetStatsOverlay;

if (args.Length == 2 && args[0] == "--inspect-local")
{
    // Only aggregate usage metadata leaves the reader. No identifiers or message text.
    var usage = ClaudeDesktopUsageReader.Read(args[1]).ToList();
    var models = usage.Where(r => r.Payload.TryGetProperty("message", out _))
        .GroupBy(r => r.Payload.GetProperty("message").GetProperty("model").GetString())
        .Select(g => new { Model = g.Key, Records = g.Count(), Tokens = g.Sum(r =>
            r.Payload.GetProperty("message").GetProperty("usage").EnumerateObject()
                .Where(p => p.Name.EndsWith("tokens") && p.Value.ValueKind == JsonValueKind.Number).Sum(p => p.Value.GetInt64())) });
    var unique = usage.Where(r => r.Payload.TryGetProperty("message", out _))
        .Select(r => r.Payload.GetProperty("message").GetProperty("id").GetString()).Distinct().Count();
    var dates = usage.Select(r => r.Payload.TryGetProperty("timestamp", out var timestamp) ? timestamp.ToString() : "")
        .Where(value => value.Length >= 10).Select(value => value[..10]).GroupBy(value => value).Select(group => new { Date = group.Key, Records = group.Count() });
    Console.WriteLine(JsonSerializer.Serialize(new { Records = usage.Count, UniqueMessages = unique, Models = models, Dates = dates }));
    return;
}

var root = Path.Combine(Path.GetTempPath(), "nikki-desktop-test-" + Guid.NewGuid().ToString("N"));
var db = Path.Combine(root, "IndexedDB", "https_claude.ai_0.indexeddb.leveldb");
Directory.CreateDirectory(db);
var passed = 0;
try
{
    var metadataKey = Concat([0, 0, 0, 0, 201], IdbString("https_claude.ai"), IdbString("claude-conversation-store"));
    byte[] key = [0, 3, 1, 1, 1, 2, 0, 65];
    var payload = Conversation("m-one", 100, 20);
    var value = Concat(Varint(1), Blink(payload));
    File.WriteAllBytes(Path.Combine(db, "000001.log"), Wal(Batch(1, (metadataKey, new byte[] { 3 }), (key, value))));
    Check("Claude desktop V8 metadata and token payload", rows => rows.Count == 1 && rows[0].Payload.GetProperty("message").GetProperty("usage").GetProperty("input_tokens").GetInt32() == 100);
    Check("never returns prompt or result content", rows => !rows[0].Payload.GetRawText().Contains("PRIVATE-FIXTURE-TEXT"));

    File.WriteAllBytes(Path.Combine(db, "000002.log"), Wal(Batch(10, (key, Concat(Varint(2), Blink(Conversation("m-one", 120, 50)))))));
    Check("latest snapshot replaces previous revision", rows => rows.Count == 1 && rows[0].Payload.GetProperty("message").GetProperty("usage").GetProperty("output_tokens").GetInt32() == 50);

    File.WriteAllBytes(Path.Combine(db, "000003.log"), Wal(Batch(20, (key, null))));
    Check("tombstone removes obsolete cache snapshot", rows => rows.Count == 0);

    File.WriteAllBytes(Path.Combine(db, "000004.log"), Wal(Batch(30, (key, Concat(Varint(3), new byte[] {255,17,2}, SnappyLiteral(Blink(payload)))))));
    Check("Snappy wrapped V8 payload", rows => rows.Count == 1);

    var large = Conversation("m-large", 400, 90, new string('x', 90_000));
    File.WriteAllBytes(Path.Combine(db, "000005.log"), Wal(Batch(40, (key, Concat(Varint(4), Blink(large))))));
    Check("WAL record spans multiple 32KB blocks", rows => rows.Count == 1 && rows[0].Payload.GetProperty("message").GetProperty("id").GetString() == "m-large");

    var corrupted = Wal(Batch(50, (key, value))); corrupted[0] ^= 1;
    File.WriteAllBytes(Path.Combine(db, "000006.log"), corrupted);
    Check("invalid checksum ignored without replacing valid usage", rows => rows.Count == 1 && rows[0].Payload.GetProperty("message").GetProperty("id").GetString() == "m-large");

    var truncated = Wal(Batch(60, (key, value)))[..^12];
    File.WriteAllBytes(Path.Combine(db, "000007.log"), truncated);
    Check("partial app write ignored", rows => rows.Count == 1);

    var blobPayload = Blink(Conversation("m-blob", 500, 200));
    byte[] blobKey = [0, 3, 1, 3, 1, 2, 0, 65];
    var blobDir = Path.Combine(root, "IndexedDB", "https_claude.ai_0.indexeddb.blob", "3", "01");
    Directory.CreateDirectory(blobDir);
    File.WriteAllBytes(Path.Combine(blobDir, "123"), blobPayload);
    var blobInfo = Concat(new byte[] { 0 }, Varint(0x123), Varint(0), Varint((ulong)blobPayload.Length));
    var blobReference = Concat(Varint(5), new byte[] {255,17,1}, Varint((ulong)blobPayload.Length), Varint(0));
    File.WriteAllBytes(Path.Combine(db, "000008.log"), Wal(Batch(70, (blobKey, blobInfo), (key, blobReference))));
    Check("external IndexedDB blob", rows => rows.Count == 1 && rows[0].Payload.GetProperty("message").GetProperty("id").GetString() == "m-blob");

    // Compacted table has a newer sequence than every WAL. The index block is uncompressed;
    // its data block uses the actual Snappy literal wire format.
    File.WriteAllBytes(Path.Combine(db, "000009.ldb"), Table(key, Concat(Varint(6), Blink(Conversation("m-table", 600, 300))), 80));
    Check("SSTable with Snappy data block", rows => rows.Count == 1 && rows[0].Payload.GetProperty("message").GetProperty("id").GetString() == "m-table");
    File.WriteAllBytes(Path.Combine(db, "000010.log"), Wal(Batch(90, (key, null))));
    Check("new WAL deletion supersedes compacted table", rows => rows.Count == 0);
    var mixed = Conversation("m-mixed", 600, 300);
    var mixedEvents = (object?[])((Dictionary<string, object?>)mixed["tree"]!)["events"]!;
    var mixedResult = (Dictionary<string, object?>)((Dictionary<string, object?>)mixedEvents[1]!)["payload"]!;
    mixedResult["modelUsage"] = new Dictionary<string, object?>
    {
        ["claude-opus-5"] = new Dictionary<string, object?> { ["inputTokens"] = 600, ["outputTokens"] = 300 },
        ["claude-haiku-4-5"] = new Dictionary<string, object?> { ["inputTokens"] = 200, ["outputTokens"] = 40 }
    };
    File.WriteAllBytes(Path.Combine(db, "000011.log"), Wal(Batch(100, (key, Concat(Varint(7), Blink(mixed))))));
    Check("per-model summaries retain models missing from assistant messages", rows => rows.Count == 2
        && rows.Any(row => row.Payload.TryGetProperty("modelUsage", out var models) && models.TryGetProperty("claude-haiku-4-5", out _)));
    Console.WriteLine($"PASS: {passed} desktop cache regression checks.");
}
finally { Directory.Delete(root, true); }

void Check(string name, Func<List<(string Source, JsonElement Payload)>, bool> predicate)
{
    if (!predicate(ClaudeDesktopUsageReader.Read(root).ToList())) throw new Exception("FAIL: " + name);
    passed++; Console.WriteLine("PASS: " + name);
}

static Dictionary<string, object?> Conversation(string id, int input, int output, string text = "PRIVATE-FIXTURE-TEXT") => new()
{
    ["conversationUuid"] = "fixture-conversation", ["tree"] = new Dictionary<string, object?>
    {
        ["events"] = new object?[]
        {
            new Dictionary<string, object?> { ["payload"] = new Dictionary<string, object?>
            {
                ["type"] = "assistant", ["timestamp"] = "2026-09-11T12:00:00Z", ["session_id"] = "fixture-session",
                ["message"] = new Dictionary<string, object?>
                {
                    ["id"] = id, ["model"] = "claude-opus-5", ["role"] = "assistant", ["content"] = text,
                    ["usage"] = new Dictionary<string, object?> { ["input_tokens"] = input, ["output_tokens"] = output, ["cache_read_input_tokens"] = 10 }
                }
            }},
            new Dictionary<string, object?> { ["payload"] = new Dictionary<string, object?>
            {
                ["type"] = "result", ["session_id"] = "fixture-session", ["result"] = text,
                ["usage"] = new Dictionary<string, object?> { ["input_tokens"] = input, ["output_tokens"] = output }, ["total_cost_usd"] = 5
            }}
        }
    }
};

static byte[] Blink(object? obj) => Concat(new byte[] {255,21,254}, new byte[12], new byte[] {255,16}, V8(obj));
static byte[] V8(object? obj)
{
    if (obj is null) return [(byte)'0'];
    if (obj is string s) { var bytes = Encoding.UTF8.GetBytes(s); return Concat([(byte)'S'], Varint((ulong)bytes.Length), bytes); }
    if (obj is int n) return Concat([(byte)'I'], Varint((ulong)((n << 1) ^ (n >> 31))));
    if (obj is Dictionary<string, object?> d) return Concat([(byte)'o'], Concat(d.SelectMany(kv => new[] {V8(kv.Key),V8(kv.Value)}).ToArray()), [(byte)'{'], Varint((ulong)d.Count));
    if (obj is object?[] a) return Concat([(byte)'A'], Varint((ulong)a.Length), Concat(a.Select(V8).ToArray()), [(byte)'$'], Varint(0), Varint((ulong)a.Length));
    throw new Exception("unsupported fixture value");
}
static byte[] IdbString(string s) => Concat(Varint((ulong)s.Length), Encoding.BigEndianUnicode.GetBytes(s));
static byte[] Batch(ulong sequence, params (byte[] Key, byte[]? Value)[] rows)
{
    var header = new byte[12]; BinaryPrimitives.WriteUInt64LittleEndian(header, sequence); BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), (uint)rows.Length);
    return Concat(header, Concat(rows.Select(row => row.Value is null ? Concat([0], Varint((ulong)row.Key.Length), row.Key) : Concat([1], Varint((ulong)row.Key.Length), row.Key, Varint((ulong)row.Value.Length), row.Value)).ToArray()));
}
static byte[] Wal(byte[] batch)
{
    using var stream = new MemoryStream(); var offset = 0;
    while (offset < batch.Length)
    {
        var space = 32768 - (int)(stream.Length % 32768);
        if (space < 7) { stream.Write(new byte[space]); continue; }
        var length = Math.Min(space - 7, batch.Length - offset);
        var type = (byte)(offset == 0 ? (length == batch.Length ? 1 : 2) : (offset + length == batch.Length ? 4 : 3));
        var header = new byte[7]; BinaryPrimitives.WriteUInt32LittleEndian(header, Crc(Concat([type],batch.AsSpan(offset,length).ToArray()))); BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4),(ushort)length);header[6]=type;
        stream.Write(header);stream.Write(batch,offset,length);offset+=length;
    }
    return stream.ToArray();
}
static byte[] Table(byte[] key, byte[] value, ulong sequence)
{
    var trailer = new byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(trailer, (sequence << 8) | 1);
    var data = Block(Concat(key, trailer),value);
    var compressed = SnappyLiteral(data); var dataWire = BlockWire(compressed,1);
    var indexWire = BlockWire(Block(Concat(key,trailer),Concat(Varint(0),Varint((ulong)compressed.Length))),0);
    var footer = new byte[48]; var handles = Concat(Varint(0),Varint(0),Varint((ulong)dataWire.Length),Varint((ulong)indexWire.Length-5));handles.CopyTo(footer,0);
    BinaryPrimitives.WriteUInt64LittleEndian(footer.AsSpan(40),0xdb4775248b80fb57UL);
    return Concat(dataWire,indexWire,footer);
}
static byte[] Block(byte[] key,byte[] value) => Concat(Varint(0),Varint((ulong)key.Length),Varint((ulong)value.Length),key,value,new byte[]{0,0,0,0,1,0,0,0});
static byte[] BlockWire(byte[] data,byte type) { var crc = new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(crc,Crc(Concat(data,[type])));return Concat(data,[type],crc); }
static byte[] SnappyLiteral(byte[] data)
{
    var n=data.Length-1; var lengthBytes=n<=255?1:n<=65535?2:3; var size=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(size,n);
    return Concat(Varint((ulong)data.Length),new byte[]{(byte)((59+lengthBytes)<<2)},size[..lengthBytes],data);
}
static uint Crc(byte[] data)
{
    uint crc=uint.MaxValue;foreach(var b in data){crc^=b;for(var i=0;i<8;i++)crc=(crc>>1)^((crc&1)!=0?0x82f63b78U:0);}crc=~crc;return unchecked(((crc>>15)|(crc<<17))+0xa282ead8U);
}
static byte[] Varint(ulong n) { var bytes=new List<byte>(); do{var b=(byte)(n&127);n>>=7;bytes.Add((byte)(b|(n==0?0:128)));}while(n!=0);return bytes.ToArray(); }
static byte[] Concat(params byte[][] arrays) => arrays.SelectMany(x=>x).ToArray();

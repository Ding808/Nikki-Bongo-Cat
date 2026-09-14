namespace PetStatsOverlay;

// Limits a live file read to its opening length, even when another process is
// appending faster than we can parse. The caller owns the underlying stream.
internal sealed class UsageSnapshotStream(Stream source, CancellationToken cancellationToken) : Stream
{
    private long remaining = source.Length - source.Position;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override int Read(Span<byte> buffer)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var read = source.Read(buffer[..(int)Math.Min(buffer.Length, remaining)]);
        remaining -= read;
        return read;
    }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

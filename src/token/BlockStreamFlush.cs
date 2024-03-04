using System.IO;

// StreamWriter.Flush flushes both the encoder and the underlying stream. So
// wrapping the underlying stream with this class allows one to flush
// StreamWriter's encoder without flushing to the file.
class BlockStreamFlush : Stream {
  public Stream Underlying { get; }
  public BlockStreamFlush(Stream underlying) { Underlying = underlying; }

  public override bool CanRead => Underlying.CanRead;

  public override bool CanSeek => Underlying.CanSeek;

  public override bool CanWrite => Underlying.CanWrite;

  public override long Length => Underlying.Length;

  public override long Position {
    get => Underlying.Position;
    set => Underlying.Position = value;
  }

  public override void Flush() {}

  public override int Read(byte[] buffer, int offset, int count) {
    return Underlying.Read(buffer, offset, count);
  }

  public override long Seek(long offset, SeekOrigin origin) {
    return Underlying.Seek(offset, origin);
  }

  public override void SetLength(long value) { Underlying.SetLength(value); }

  public override void Write(byte[] buffer, int offset, int count) {
    Underlying.Write(buffer, offset, count);
  }
}

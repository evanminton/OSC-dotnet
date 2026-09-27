using System.Buffers.Binary;
using System.Text;

namespace Osc;

/// <summary>Big-endian, 4-byte aligned writer for OSC data.</summary>
internal struct OscWriter
{
    private byte[] _buffer;
    private int _position;

    public OscWriter()
    {
        _buffer = new byte[64];
        _position = 0;
    }

    public readonly int Written => _position;

    public readonly ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _position);

    public readonly byte[] ToArray() => WrittenSpan.ToArray();

    private Span<byte> Take(int count)
    {
        if (_position + count > _buffer.Length)
            Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _position + count));
        var span = _buffer.AsSpan(_position, count);
        _position += count;
        return span;
    }

    public void WriteInt32(int value) => BinaryPrimitives.WriteInt32BigEndian(Take(4), value);

    public void WriteInt64(long value) => BinaryPrimitives.WriteInt64BigEndian(Take(8), value);

    public void WriteUInt64(ulong value) => BinaryPrimitives.WriteUInt64BigEndian(Take(8), value);

    public void WriteFloat32(float value) => BinaryPrimitives.WriteSingleBigEndian(Take(4), value);

    public void WriteFloat64(double value) => BinaryPrimitives.WriteDoubleBigEndian(Take(8), value);

    public void WriteBytes(byte a, byte b, byte c, byte d)
    {
        var span = Take(4);
        span[0] = a;
        span[1] = b;
        span[2] = c;
        span[3] = d;
    }

    public void WriteRaw(ReadOnlySpan<byte> bytes) => bytes.CopyTo(Take(bytes.Length));

    /// <summary>Writes an OSC-string: the bytes, a null terminator, then null padding to a multiple of 4.</summary>
    public void WriteString(string value)
    {
        var length = Encoding.UTF8.GetByteCount(value);
        var span = Take(Pad(length + 1));
        span.Clear();
        Encoding.UTF8.GetBytes(value, span);
    }

    /// <summary>Writes an OSC-blob: an int32 size, the bytes, then null padding to a multiple of 4.</summary>
    public void WriteBlob(ReadOnlySpan<byte> value)
    {
        WriteInt32(value.Length);
        var span = Take(Pad(value.Length));
        span.Clear();
        value.CopyTo(span);
    }

    public int ReserveInt32()
    {
        var slot = _position;
        Take(4);
        return slot;
    }

    public readonly void PatchInt32(int slot, int value) =>
        BinaryPrimitives.WriteInt32BigEndian(_buffer.AsSpan(slot, 4), value);

    public static int Pad(int length) => (length + 3) & ~3;
}

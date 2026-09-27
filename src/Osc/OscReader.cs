using System.Buffers.Binary;
using System.Text;

namespace Osc;

/// <summary>Big-endian, 4-byte aligned reader for OSC data. Throws <see cref="OscException"/> on truncation.</summary>
internal ref struct OscReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> _data = data;
    private int _position;

    public readonly bool IsAtEnd => _position >= _data.Length;

    public readonly int Remaining => _data.Length - _position;

    public readonly byte Peek() => _data[_position];

    public ReadOnlySpan<byte> ReadRaw(int count)
    {
        if (count < 0 || count > Remaining)
            throw new OscException($"Unexpected end of OSC data: needed {count} bytes but only {Remaining} remain.");
        var span = _data.Slice(_position, count);
        _position += count;
        return span;
    }

    public int ReadInt32() => BinaryPrimitives.ReadInt32BigEndian(ReadRaw(4));

    public long ReadInt64() => BinaryPrimitives.ReadInt64BigEndian(ReadRaw(8));

    public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64BigEndian(ReadRaw(8));

    public float ReadFloat32() => BinaryPrimitives.ReadSingleBigEndian(ReadRaw(4));

    public double ReadFloat64() => BinaryPrimitives.ReadDoubleBigEndian(ReadRaw(8));

    public ReadOnlySpan<byte> ReadFour() => ReadRaw(4);

    public string ReadString()
    {
        var rest = _data[_position..];
        var terminator = rest.IndexOf((byte)0);
        if (terminator < 0)
            throw new OscException("OSC-string is missing its null terminator.");
        var padded = OscWriter.Pad(terminator + 1);
        if (padded > rest.Length)
            throw new OscException("OSC-string padding runs past the end of the data.");
        for (var i = terminator + 1; i < padded; i++)
        {
            if (rest[i] != 0)
                throw new OscException("OSC-string padding must be null bytes.");
        }
        _position += padded;
        return Encoding.UTF8.GetString(rest[..terminator]);
    }

    public byte[] ReadBlob()
    {
        var size = ReadInt32();
        if (size < 0)
            throw new OscException($"OSC-blob size cannot be negative ({size}).");
        if (size > Remaining)
            throw new OscException($"OSC-blob size {size} exceeds the {Remaining} bytes remaining.");
        var bytes = ReadRaw(size).ToArray();
        ReadRaw(OscWriter.Pad(size) - size);
        return bytes;
    }
}

using System.Buffers;

namespace Osc;

/// <summary>
/// The unit of transmission in OSC: either an <see cref="OscMessage"/> or an <see cref="OscBundle"/>.
/// </summary>
/// <remarks>
/// Two packets are equal when their binary encodings are identical, so equality is structural
/// (blob contents are compared byte-by-byte) and <c>int 1</c> never equals <c>float 1.0</c>.
/// </remarks>
public abstract class OscPacket : IEquatable<OscPacket>
{
    private protected OscPacket() { }

    /// <summary>Encodes the packet into a new byte array. The length is always a multiple of 4.</summary>
    public byte[] ToBytes()
    {
        var writer = new OscWriter();
        Write(ref writer);
        return writer.ToArray();
    }

    /// <summary>Encodes the packet into <paramref name="output"/>.</summary>
    public void WriteTo(IBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var writer = new OscWriter();
        Write(ref writer);
        output.Write(writer.WrittenSpan);
    }

    internal abstract void Write(ref OscWriter writer);

    /// <summary>
    /// Decodes an OSC packet. The first byte decides the kind: <c>#</c> starts a bundle
    /// (<c>#bundle</c>) and <c>/</c> starts a message.
    /// </summary>
    /// <exception cref="OscException">The bytes are not a well-formed OSC 1.0 packet.</exception>
    public static OscPacket Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
            throw new OscException("An OSC packet cannot be empty.");
        if (data.Length % 4 != 0)
            throw new OscException($"An OSC packet's size must be a multiple of 4 bytes, but was {data.Length}.");

        return data[0] switch
        {
            (byte)'#' => OscBundle.ParseBody(data),
            (byte)'/' => OscMessage.ParseBody(data),
            _ => throw new OscException($"An OSC packet must start with '/' or '#', not 0x{data[0]:X2}."),
        };
    }

    /// <summary>Tries to decode an OSC packet, returning false instead of throwing on malformed input.</summary>
    public static bool TryParse(ReadOnlySpan<byte> data, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out OscPacket? packet)
    {
        try
        {
            packet = Parse(data);
            return true;
        }
        catch (OscException)
        {
            packet = null;
            return false;
        }
    }

    /// <inheritdoc />
    public bool Equals(OscPacket? other) =>
        other is not null && (ReferenceEquals(this, other) || ToBytes().AsSpan().SequenceEqual(other.ToBytes()));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OscPacket other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(ToBytes());
        return hash.ToHashCode();
    }
}

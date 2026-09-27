using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Osc;

/// <summary>How OSC packets are delimited on a stream transport (TCP, serial, pipes).</summary>
public enum OscFraming
{
    /// <summary>OSC 1.1: SLIP (RFC 1055) with the double END encoding.</summary>
    Slip,

    /// <summary>OSC 1.0: each packet is preceded by its size as a big-endian int32.</summary>
    LengthPrefixed,
}

/// <summary>Reads and writes OSC packets on a <see cref="Stream"/>.</summary>
public static class OscStreamExtensions
{
    /// <summary>Writes one packet using <paramref name="framing"/> (SLIP by default, per OSC 1.1).</summary>
    public static async ValueTask WriteOscPacketAsync(this Stream stream, OscPacket packet,
        OscFraming framing = OscFraming.Slip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(packet);
        var bytes = packet.ToBytes();
        if (framing == OscFraming.Slip)
        {
            await stream.WriteAsync(OscSlip.Encode(bytes), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var frame = new byte[bytes.Length + 4];
            BinaryPrimitives.WriteInt32BigEndian(frame, bytes.Length);
            bytes.CopyTo(frame, 4);
            await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads packets until the stream ends, using <paramref name="framing"/> (SLIP by default, per OSC 1.1).
    /// </summary>
    /// <exception cref="OscException">A frame is not a well-formed OSC packet, or a length-prefixed frame is truncated or oversized.</exception>
    public static async IAsyncEnumerable<OscPacket> ReadOscPacketsAsync(this Stream stream,
        OscFraming framing = OscFraming.Slip, int maxPacketSize = 1 << 20,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPacketSize);

        if (framing == OscFraming.Slip)
        {
            var decoder = new OscSlipDecoder(maxPacketSize);
            var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                foreach (var frame in decoder.Feed(buffer.AsSpan(0, read)))
                    yield return OscPacket.Parse(frame);
            }
        }
        else
        {
            var header = new byte[4];
            while (await ReadExactlyOrEndAsync(stream, header, cancellationToken).ConfigureAwait(false))
            {
                var size = BinaryPrimitives.ReadInt32BigEndian(header);
                if (size <= 0 || size > maxPacketSize)
                    throw new OscException($"Length-prefixed OSC packet size {size} is out of range.");
                var body = new byte[size];
                if (!await ReadExactlyOrEndAsync(stream, body, cancellationToken).ConfigureAwait(false))
                    throw new OscException("Stream ended in the middle of a length-prefixed OSC packet.");
                yield return OscPacket.Parse(body);
            }
        }
    }

    /// <summary>Fills <paramref name="buffer"/>; returns false if the stream ended before any byte was read.</summary>
    private static async ValueTask<bool> ReadExactlyOrEndAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                if (total == 0)
                    return false;
                throw new OscException("Stream ended in the middle of a length-prefixed OSC packet.");
            }
            total += read;
        }
        return true;
    }
}

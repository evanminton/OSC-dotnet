namespace Osc;

/// <summary>
/// SLIP (RFC 1055) framing for OSC over stream transports such as TCP and serial, as required by
/// OSC 1.1. Each packet is sent with the "double END" encoding: an END byte, the escaped packet, and
/// another END byte.
/// </summary>
public static class OscSlip
{
    /// <summary>Frame delimiter.</summary>
    public const byte End = 0xC0;
    /// <summary>Escape byte.</summary>
    public const byte Esc = 0xDB;
    /// <summary>Follows <see cref="Esc"/> to stand for a literal <see cref="End"/>.</summary>
    public const byte EscEnd = 0xDC;
    /// <summary>Follows <see cref="Esc"/> to stand for a literal <see cref="Esc"/>.</summary>
    public const byte EscEsc = 0xDD;

    /// <summary>SLIP-encodes one packet's bytes with the double END encoding.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> packet)
    {
        var extra = 0;
        foreach (var b in packet)
        {
            if (b is End or Esc)
                extra++;
        }

        var frame = new byte[packet.Length + extra + 2];
        var i = 0;
        frame[i++] = End;
        foreach (var b in packet)
        {
            switch (b)
            {
                case End:
                    frame[i++] = Esc;
                    frame[i++] = EscEnd;
                    break;
                case Esc:
                    frame[i++] = Esc;
                    frame[i++] = EscEsc;
                    break;
                default:
                    frame[i++] = b;
                    break;
            }
        }
        frame[i] = End;
        return frame;
    }

    /// <summary>Encodes <paramref name="packet"/> and SLIP-frames it.</summary>
    public static byte[] Encode(OscPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        return Encode(packet.ToBytes());
    }
}

/// <summary>
/// Incremental SLIP decoder: feed it bytes as they arrive from a stream and it returns each complete
/// frame. Empty frames (such as the back-to-back END bytes of the double END encoding) are skipped.
/// </summary>
public sealed class OscSlipDecoder
{
    private readonly int _maxFrameSize;
    private byte[] _buffer = new byte[256];
    private int _length;
    private bool _escaped;
    private bool _discarding;

    /// <summary>Creates a decoder.</summary>
    /// <param name="maxFrameSize">Frames longer than this are dropped (and counted in <see cref="DroppedFrames"/>) to bound memory use.</param>
    public OscSlipDecoder(int maxFrameSize = 1 << 20)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFrameSize);
        _maxFrameSize = maxFrameSize;
    }

    /// <summary>Number of frames dropped for exceeding the maximum frame size.</summary>
    public int DroppedFrames { get; private set; }

    /// <summary>Consumes <paramref name="data"/> and returns the frames it completed, in order.</summary>
    public IReadOnlyList<byte[]> Feed(ReadOnlySpan<byte> data)
    {
        List<byte[]>? frames = null;
        foreach (var b in data)
        {
            if (b == OscSlip.End)
            {
                if (_length > 0 && !_discarding)
                    (frames ??= []).Add(_buffer.AsSpan(0, _length).ToArray());
                _length = 0;
                _escaped = false;
                _discarding = false;
                continue;
            }

            if (_escaped)
            {
                _escaped = false;
                // RFC 1055: an ESC followed by anything else is a protocol violation; keep the byte as is.
                Append(b switch { OscSlip.EscEnd => OscSlip.End, OscSlip.EscEsc => OscSlip.Esc, _ => b });
            }
            else if (b == OscSlip.Esc)
            {
                _escaped = true;
            }
            else
            {
                Append(b);
            }
        }
        return frames ?? (IReadOnlyList<byte[]>)[];
    }

    /// <summary>Discards any partially received frame.</summary>
    public void Reset()
    {
        _length = 0;
        _escaped = false;
        _discarding = false;
    }

    private void Append(byte b)
    {
        if (_discarding)
            return;
        if (_length == _maxFrameSize)
        {
            _discarding = true;
            _length = 0;
            DroppedFrames++;
            return;
        }
        if (_length == _buffer.Length)
            Array.Resize(ref _buffer, Math.Min(_buffer.Length * 2, _maxFrameSize));
        _buffer[_length++] = b;
    }
}

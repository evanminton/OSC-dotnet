namespace Osc;

/// <summary>An OSC-string sent with the alternate type tag <c>S</c> (symbol).</summary>
public readonly record struct OscSymbol(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>A 32-bit RGBA color (type tag <c>r</c>).</summary>
public readonly record struct OscColor(byte R, byte G, byte B, byte A);

/// <summary>A 4-byte MIDI message (type tag <c>m</c>): port id, status byte, data1, data2.</summary>
public readonly record struct OscMidi(byte Port, byte Status, byte Data1, byte Data2);

/// <summary>The "Infinitum" / impulse value (type tag <c>I</c>). Carries no data.</summary>
public readonly record struct OscImpulse
{
    /// <summary>The single impulse value.</summary>
    public static readonly OscImpulse Value = default;

    /// <inheritdoc />
    public override string ToString() => "Impulse";
}

/// <summary>Thrown when bytes cannot be decoded as an OSC packet or a value cannot be encoded.</summary>
public class OscException : Exception
{
    /// <summary>Creates an exception with a message.</summary>
    public OscException(string message) : base(message) { }

    /// <summary>Creates an exception with a message and an inner exception.</summary>
    public OscException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Type tag sets defined by the OSC specifications.</summary>
public static class OscTypeTags
{
    /// <summary>Types every OSC 1.0 implementation must support: int32, float32, string, blob.</summary>
    public const string Osc10Required = "ifsb";

    /// <summary>Types every OSC 1.1 implementation must support: the 1.0 set plus True, False, Nil, Impulse and time tag.</summary>
    public const string Osc11Required = "ifsbTFNIt";

    /// <summary>
    /// True if every argument in <paramref name="typeTags"/> (with or without the leading comma) is in
    /// <paramref name="supported"/>, so that any conforming receiver of that version can decode it.
    /// </summary>
    public static bool UsesOnly(string typeTags, string supported)
    {
        ArgumentNullException.ThrowIfNull(typeTags);
        ArgumentNullException.ThrowIfNull(supported);
        var tags = typeTags.StartsWith(',') ? typeTags.AsSpan(1) : typeTags.AsSpan();
        foreach (var tag in tags)
        {
            if (!supported.Contains(tag))
                return false;
        }
        return true;
    }
}

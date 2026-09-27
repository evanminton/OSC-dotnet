namespace Osc;

/// <summary>
/// A 64-bit OSC time tag in NTP format: the upper 32 bits are seconds since midnight on
/// January 1, 1900 (UTC) and the lower 32 bits are fractional parts of a second.
/// The raw value 1 is the special "immediately" tag.
/// </summary>
public readonly record struct OscTimeTag(ulong Value) : IComparable<OscTimeTag>
{
    private static readonly DateTime Epoch = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const double FractionScale = 4294967296.0; // 2^32

    /// <summary>The special time tag meaning "execute immediately" (63 zero bits followed by a one).</summary>
    public static readonly OscTimeTag Immediate = new(1);

    /// <summary>Creates a time tag from its seconds and fraction parts.</summary>
    public OscTimeTag(uint seconds, uint fraction) : this(((ulong)seconds << 32) | fraction) { }

    /// <summary>Whole seconds since 1900-01-01T00:00:00Z.</summary>
    public uint Seconds => (uint)(Value >> 32);

    /// <summary>Fractional part of a second, in units of 1/2^32 seconds.</summary>
    public uint Fraction => (uint)Value;

    /// <summary>True when this is the special "immediately" tag.</summary>
    public bool IsImmediate => Value == 1;

    /// <summary>Converts a <see cref="DateTime"/> to a time tag. Local and unspecified times are treated as local and converted to UTC.</summary>
    public static OscTimeTag FromDateTime(DateTime time)
    {
        var utc = time.Kind == DateTimeKind.Utc ? time : time.ToUniversalTime();
        var ticks = (utc - Epoch).Ticks;
        if (ticks < 0)
            throw new ArgumentOutOfRangeException(nameof(time), "OSC time tags cannot represent times before 1900.");
        var seconds = (ulong)(ticks / TimeSpan.TicksPerSecond);
        if (seconds > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(time), "Time is past the end of the NTP era 0 range (2036-02-07).");
        var remainder = ticks % TimeSpan.TicksPerSecond;
        var fraction = (uint)Math.Min(uint.MaxValue, Math.Round(remainder * FractionScale / TimeSpan.TicksPerSecond));
        return new OscTimeTag((uint)seconds, fraction);
    }

    /// <summary>Converts a <see cref="DateTimeOffset"/> to a time tag.</summary>
    public static OscTimeTag FromDateTimeOffset(DateTimeOffset time) => FromDateTime(time.UtcDateTime);

    /// <summary>A time tag for the current instant.</summary>
    public static OscTimeTag Now => FromDateTime(DateTime.UtcNow);

    /// <summary>Converts this time tag to a UTC <see cref="DateTime"/> (precision is limited to 100ns ticks).</summary>
    public DateTime ToDateTime()
    {
        var fractionTicks = (long)Math.Round(Fraction * (double)TimeSpan.TicksPerSecond / FractionScale);
        return Epoch.AddTicks(Seconds * TimeSpan.TicksPerSecond + fractionTicks);
    }

    /// <inheritdoc />
    public int CompareTo(OscTimeTag other) => Value.CompareTo(other.Value);

    /// <summary>Compares two time tags.</summary>
    public static bool operator <(OscTimeTag a, OscTimeTag b) => a.Value < b.Value;
    /// <summary>Compares two time tags.</summary>
    public static bool operator >(OscTimeTag a, OscTimeTag b) => a.Value > b.Value;
    /// <summary>Compares two time tags.</summary>
    public static bool operator <=(OscTimeTag a, OscTimeTag b) => a.Value <= b.Value;
    /// <summary>Compares two time tags.</summary>
    public static bool operator >=(OscTimeTag a, OscTimeTag b) => a.Value >= b.Value;

    /// <inheritdoc />
    public override string ToString() => IsImmediate ? "Immediate" : ToDateTime().ToString("O");
}

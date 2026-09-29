namespace Osc;

/// <summary>
/// A 64-bit OSC time tag in NTP format: the upper 32 bits are seconds and the lower 32 bits are
/// fractional parts of a second. The raw value 1 is the special "immediately" tag.
/// </summary>
/// <remarks>
/// The 32-bit seconds wrap on 2036-02-07T06:28:16Z. As in RFC 4330, seconds with the top bit set count
/// from 1900-01-01 (NTP era 0) and seconds with it clear count from 2036-02-07 (era 1), so a time tag
/// covers 1968-01-20T03:14:08Z to 2104-02-26T09:42:24Z, and comparisons stay chronological across the wrap.
/// </remarks>
public readonly record struct OscTimeTag(ulong Value) : IComparable<OscTimeTag>
{
    private static readonly DateTime Epoch = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const double FractionScale = 4294967296.0; // 2^32
    private const long EraSeconds = 1L << 32;
    private const uint EraZeroStart = 1u << 31; // seconds below this belong to era 1

    /// <summary>The earliest time a time tag can represent (1968-01-20T03:14:08Z).</summary>
    public static readonly DateTime MinDateTime = Epoch.AddSeconds(EraZeroStart);

    /// <summary>The first instant a time tag cannot represent (2104-02-26T09:42:24Z).</summary>
    public static readonly DateTime MaxDateTimeExclusive = Epoch.AddSeconds(EraSeconds + EraZeroStart);

    /// <summary>The special time tag meaning "execute immediately" (63 zero bits followed by a one).</summary>
    public static readonly OscTimeTag Immediate = new(1);

    /// <summary>Creates a time tag from its seconds and fraction parts.</summary>
    public OscTimeTag(uint seconds, uint fraction) : this(((ulong)seconds << 32) | fraction) { }

    /// <summary>Whole seconds since the start of the NTP era (1900-01-01T00:00:00Z, or 2036-02-07T06:28:16Z when the top bit is clear).</summary>
    public uint Seconds => (uint)(Value >> 32);

    /// <summary>Fractional part of a second, in units of 1/2^32 seconds.</summary>
    public uint Fraction => (uint)Value;

    /// <summary>True when this is the special "immediately" tag.</summary>
    public bool IsImmediate => Value == 1;

    /// <summary>Converts a <see cref="DateTime"/> to a time tag. Local and unspecified times are treated as local and converted to UTC.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The time is before <see cref="MinDateTime"/> or not before <see cref="MaxDateTimeExclusive"/>.</exception>
    public static OscTimeTag FromDateTime(DateTime time)
    {
        var utc = time.Kind == DateTimeKind.Utc ? time : time.ToUniversalTime();
        if (utc < MinDateTime || utc >= MaxDateTimeExclusive)
            throw new ArgumentOutOfRangeException(nameof(time), $"OSC time tags can only represent times from {MinDateTime:O} up to {MaxDateTimeExclusive:O}.");
        var ticks = (utc - Epoch).Ticks;
        var seconds = ticks / TimeSpan.TicksPerSecond;
        var remainder = ticks % TimeSpan.TicksPerSecond;
        var fraction = (uint)Math.Min(uint.MaxValue, Math.Round(remainder * FractionScale / TimeSpan.TicksPerSecond));
        return new OscTimeTag((uint)(seconds % EraSeconds), fraction);
    }

    /// <summary>Converts a <see cref="DateTimeOffset"/> to a time tag.</summary>
    public static OscTimeTag FromDateTimeOffset(DateTimeOffset time) => FromDateTime(time.UtcDateTime);

    /// <summary>A time tag for the current instant.</summary>
    public static OscTimeTag Now => FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Converts this time tag to a UTC <see cref="DateTime"/> (precision is limited to 100ns ticks).
    /// <see cref="Immediate"/> has no fixed time; it converts like any other value, to just after 2036-02-07T06:28:16Z.
    /// </summary>
    public DateTime ToDateTime()
    {
        var seconds = Seconds < EraZeroStart ? Seconds + EraSeconds : Seconds;
        var fractionTicks = (long)Math.Round(Fraction * (double)TimeSpan.TicksPerSecond / FractionScale);
        return Epoch.AddTicks(seconds * TimeSpan.TicksPerSecond + fractionTicks);
    }

    /// <summary>
    /// A key that orders time tags chronologically across the era wrap, with <see cref="Immediate"/> first.
    /// Flipping the top bit puts era 0 (top bit set) before era 1 (top bit clear) and keeps order within each.
    /// </summary>
    private (bool, ulong) SortKey => (!IsImmediate, Value ^ (1UL << 63));

    /// <summary>Compares time tags chronologically. <see cref="Immediate"/> comes before every other time tag.</summary>
    public int CompareTo(OscTimeTag other) => SortKey.CompareTo(other.SortKey);

    /// <summary>Compares two time tags.</summary>
    public static bool operator <(OscTimeTag a, OscTimeTag b) => a.CompareTo(b) < 0;
    /// <summary>Compares two time tags.</summary>
    public static bool operator >(OscTimeTag a, OscTimeTag b) => a.CompareTo(b) > 0;
    /// <summary>Compares two time tags.</summary>
    public static bool operator <=(OscTimeTag a, OscTimeTag b) => a.CompareTo(b) <= 0;
    /// <summary>Compares two time tags.</summary>
    public static bool operator >=(OscTimeTag a, OscTimeTag b) => a.CompareTo(b) >= 0;

    /// <inheritdoc />
    public override string ToString() => IsImmediate ? "Immediate" : ToDateTime().ToString("O");
}

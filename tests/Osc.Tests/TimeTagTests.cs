namespace Osc.Tests;

public class TimeTagTests
{
    [Fact]
    public void Immediate_is_raw_value_one()
    {
        Assert.Equal(1UL, OscTimeTag.Immediate.Value);
        Assert.True(OscTimeTag.Immediate.IsImmediate);
        Assert.Equal(0u, OscTimeTag.Immediate.Seconds);
        Assert.Equal(1u, OscTimeTag.Immediate.Fraction);
    }

    [Fact]
    public void Seconds_and_fraction_split_the_raw_value()
    {
        var tag = new OscTimeTag(0x01020304, 0x05060708);
        Assert.Equal(0x0102030405060708UL, tag.Value);
        Assert.Equal(0x01020304u, tag.Seconds);
        Assert.Equal(0x05060708u, tag.Fraction);
    }

    [Fact]
    public void Unix_epoch_maps_to_ntp_seconds()
    {
        var tag = OscTimeTag.FromDateTime(DateTime.UnixEpoch);
        Assert.Equal(2208988800u, tag.Seconds);
        Assert.Equal(0u, tag.Fraction);
        Assert.Equal(DateTime.UnixEpoch, tag.ToDateTime());
    }

    [Fact]
    public void Half_second_is_half_the_fraction_range()
    {
        var tag = OscTimeTag.FromDateTime(DateTime.UnixEpoch.AddMilliseconds(500));
        Assert.Equal(0x80000000u, tag.Fraction);
    }

    [Fact]
    public void DateTime_round_trips_to_tick_precision()
    {
        var time = new DateTime(2026, 9, 27, 22, 25, 16, DateTimeKind.Utc).AddTicks(1234567);
        Assert.Equal(time, OscTimeTag.FromDateTime(time).ToDateTime());
        Assert.Equal(time, OscTimeTag.FromDateTimeOffset(new DateTimeOffset(time)).ToDateTime());
    }

    [Fact]
    public void Times_after_2036_wrap_into_era_one()
    {
        var wrap = new DateTime(2036, 2, 7, 6, 28, 16, DateTimeKind.Utc);
        Assert.Equal(new OscTimeTag(0, 0), OscTimeTag.FromDateTime(wrap));
        Assert.Equal(new OscTimeTag(uint.MaxValue, 0), OscTimeTag.FromDateTime(wrap.AddSeconds(-1)));

        var time = new DateTime(2040, 6, 1, 12, 0, 0, DateTimeKind.Utc).AddTicks(1234567);
        var tag = OscTimeTag.FromDateTime(time);
        Assert.Equal((uint)(time - wrap).TotalSeconds, tag.Seconds);
        Assert.Equal(time, tag.ToDateTime());
    }

    [Fact]
    public void Range_runs_from_1968_to_2104()
    {
        Assert.Equal(new DateTime(1968, 1, 20, 3, 14, 8, DateTimeKind.Utc), OscTimeTag.MinDateTime);
        Assert.Equal(new DateTime(2104, 2, 26, 9, 42, 24, DateTimeKind.Utc), OscTimeTag.MaxDateTimeExclusive);

        Assert.Equal(OscTimeTag.MinDateTime, OscTimeTag.FromDateTime(OscTimeTag.MinDateTime).ToDateTime());
        Assert.Equal(new OscTimeTag(0x80000000, 0), OscTimeTag.FromDateTime(OscTimeTag.MinDateTime));
        var last = OscTimeTag.MaxDateTimeExclusive.AddSeconds(-1);
        Assert.Equal(last, OscTimeTag.FromDateTime(last).ToDateTime());
        Assert.Equal(new OscTimeTag(0x7FFFFFFF, 0), OscTimeTag.FromDateTime(last));

        Assert.Throws<ArgumentOutOfRangeException>(() => OscTimeTag.FromDateTime(OscTimeTag.MinDateTime.AddTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => OscTimeTag.FromDateTime(OscTimeTag.MaxDateTimeExclusive));
        Assert.Throws<ArgumentOutOfRangeException>(() => OscTimeTag.FromDateTime(new DateTime(1899, 12, 31, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Comparison_stays_chronological_across_the_wrap()
    {
        var before = OscTimeTag.FromDateTime(new DateTime(2036, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var after = OscTimeTag.FromDateTime(new DateTime(2037, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.True(before.Value > after.Value);
        Assert.True(before < after);
        Assert.True(after >= before);
        Assert.Equal(-1, before.CompareTo(after));
        Assert.Equal(0, after.CompareTo(after));
    }

    [Fact]
    public void Immediate_sorts_before_every_other_time_tag()
    {
        Assert.True(OscTimeTag.Immediate < new OscTimeTag(0));
        Assert.True(OscTimeTag.Immediate < new OscTimeTag(0x80000000, 0));
        Assert.True(OscTimeTag.Immediate < OscTimeTag.Now);
        Assert.Equal("Immediate", OscTimeTag.Immediate.ToString());
    }

    [Fact]
    public void Time_tags_compare_chronologically()
    {
        var earlier = new OscTimeTag(10, 0xFFFFFFFF);
        var later = new OscTimeTag(11, 0);
        Assert.True(earlier < later);
        Assert.True(later >= earlier);
        Assert.Equal(-1, earlier.CompareTo(later));
    }
}

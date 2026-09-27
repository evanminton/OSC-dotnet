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
    public void Times_outside_era_zero_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OscTimeTag.FromDateTime(new DateTime(1899, 12, 31, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Throws<ArgumentOutOfRangeException>(() => OscTimeTag.FromDateTime(new DateTime(2037, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
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

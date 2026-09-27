using static Osc.Tests.SpecExampleTests;

namespace Osc.Tests;

public class BundleTests
{
    [Fact]
    public void Bundle_encodes_header_time_tag_and_sized_elements()
    {
        var bundle = new OscBundle(OscTimeTag.Immediate, new OscMessage("/a", 1), new OscMessage("/b"));
        var expected = Hex(
            "2362756e 646c6500" +          // "#bundle\0"
            "00000000 00000001" +          // immediately
            "0000000c 2f610000 2c690000 00000001" +
            "00000008 2f620000 2c000000");
        Assert.Equal(expected, bundle.ToBytes());
    }

    [Fact]
    public void Empty_bundle_round_trips()
    {
        var bundle = new OscBundle(new OscTimeTag(5, 6));
        Assert.Equal(16, bundle.ToBytes().Length);
        var decoded = Assert.IsType<OscBundle>(OscPacket.Parse(bundle.ToBytes()));
        Assert.Equal(new OscTimeTag(5, 6), decoded.TimeTag);
        Assert.Empty(decoded.Elements);
    }

    [Fact]
    public void Nested_bundles_round_trip()
    {
        var inner = new OscBundle(new OscTimeTag(200, 0), new OscMessage("/inner", "x"));
        var outer = new OscBundle(new OscTimeTag(100, 0), new OscMessage("/first", 1), inner, new OscMessage("/last", 2f));

        var decoded = Assert.IsType<OscBundle>(OscPacket.Parse(outer.ToBytes()));
        Assert.Equal(outer, decoded);
        Assert.Equal(3, decoded.Elements.Count);
        Assert.Equal("/first", Assert.IsType<OscMessage>(decoded.Elements[0]).Address);
        var decodedInner = Assert.IsType<OscBundle>(decoded.Elements[1]);
        Assert.Equal(new OscTimeTag(200, 0), decodedInner.TimeTag);
        Assert.Equal("x", Assert.IsType<OscMessage>(Assert.Single(decodedInner.Elements)).Get<string>(0));
    }

    [Fact]
    public void Flatten_yields_messages_in_order_with_innermost_time_tag()
    {
        var outer = new OscBundle(new OscTimeTag(100, 0),
            new OscMessage("/a"),
            new OscBundle(new OscTimeTag(200, 0), new OscMessage("/b"), new OscBundle(new OscTimeTag(300, 0), new OscMessage("/c"))),
            new OscMessage("/d"));

        var flat = outer.Flatten().Select(x => (x.Message.Address, x.TimeTag.Seconds)).ToArray();
        Assert.Equal([("/a", 100u), ("/b", 200u), ("/c", 300u), ("/d", 100u)], flat);
    }

    [Fact]
    public void Null_elements_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new OscBundle(OscTimeTag.Immediate, new OscMessage("/a"), null!));
    }
}

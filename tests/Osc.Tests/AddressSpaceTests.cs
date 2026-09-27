namespace Osc.Tests;

public class AddressSpaceTests
{
    [Fact]
    public void Exact_address_invokes_only_that_method()
    {
        var space = new OscAddressSpace();
        var calls = new List<string>();
        space.Register("/a/b", m => calls.Add("ab"));
        space.Register("/a/c", m => calls.Add("ac"));

        Assert.Equal(1, space.Dispatch(new OscMessage("/a/b")));
        Assert.Equal(["ab"], calls);
    }

    [Fact]
    public void Pattern_invokes_every_matching_method()
    {
        var space = new OscAddressSpace();
        var calls = new List<string>();
        foreach (var address in new[] { "/synth/1/freq", "/synth/2/freq", "/synth/3/gain", "/fx/1/freq" })
            space.Register(address, m => calls.Add(address));

        Assert.Equal(2, space.Dispatch(new OscMessage("/synth/*/freq", 440f)));
        Assert.Equal(["/synth/1/freq", "/synth/2/freq"], calls);
        Assert.Equal(["/fx/1/freq", "/synth/1/freq", "/synth/2/freq"], space.Match("/*/[12]/freq"));
    }

    [Fact]
    public void Pattern_does_not_match_containers()
    {
        var space = new OscAddressSpace();
        space.Register("/a/b/c", _ => { });
        Assert.Equal(0, space.Dispatch(new OscMessage("/a/*")));
        Assert.Equal(0, space.Dispatch(new OscMessage("/a/b")));
    }

    [Fact]
    public void Bundle_messages_dispatch_in_order_with_bundle_time_tag()
    {
        var space = new OscAddressSpace();
        var calls = new List<(string, OscTimeTag)>();
        space.Register("/x", (m, t) => calls.Add(("x", t)));
        space.Register("/y", (m, t) => calls.Add(("y", t)));

        var outerTime = new OscTimeTag(100, 0);
        var innerTime = new OscTimeTag(200, 0);
        var bundle = new OscBundle(outerTime, new OscMessage("/y"), new OscBundle(innerTime, new OscMessage("/x")), new OscMessage("/x"));

        Assert.Equal(3, space.Dispatch(bundle));
        Assert.Equal([("y", outerTime), ("x", innerTime), ("x", outerTime)], calls);
    }

    [Fact]
    public void Bare_message_dispatches_with_immediate_time_tag()
    {
        var space = new OscAddressSpace();
        OscTimeTag? seen = null;
        space.Register("/x", (m, t) => seen = t);
        space.Dispatch(new OscMessage("/x"));
        Assert.Equal(OscTimeTag.Immediate, seen);
    }

    [Fact]
    public void Disposing_registration_removes_handler()
    {
        var space = new OscAddressSpace();
        var count = 0;
        var registration = space.Register("/x", _ => count++);
        var other = space.Register("/x", _ => count += 10);

        registration.Dispose();
        registration.Dispose();
        space.Dispatch(new OscMessage("/x"));
        Assert.Equal(10, count);

        other.Dispose();
        Assert.Empty(space.Addresses);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("noslash")]
    [InlineData("/a//b")]
    [InlineData("/a/")]
    [InlineData("/has space")]
    [InlineData("/has#hash")]
    [InlineData("/has*star")]
    [InlineData("/has?q")]
    [InlineData("/has,comma")]
    [InlineData("/has[bracket]")]
    [InlineData("/has{brace}")]
    [InlineData("/café")]
    public void Invalid_method_addresses_are_rejected(string address)
    {
        Assert.Throws<ArgumentException>(() => new OscAddressSpace().Register(address, _ => { }));
    }

    [Fact]
    public void Round_trip_from_bytes_to_handler()
    {
        var space = new OscAddressSpace();
        float? frequency = null;
        space.Register("/oscillator/4/frequency", m => frequency = m.Get<float>(0));

        var bytes = new OscBundle(OscTimeTag.Immediate, new OscMessage("/oscillator/[0-9]/frequency", 440f)).ToBytes();
        space.Dispatch(OscPacket.Parse(bytes));
        Assert.Equal(440f, frequency);
    }
}

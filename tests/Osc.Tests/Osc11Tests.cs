namespace Osc.Tests;

/// <summary>Features added by OSC 1.1 (Freed and Schmeder, NIME 2009).</summary>
public class Osc11Tests
{
    [Theory]
    // The paper's example: "//spherical" matches at any depth.
    [InlineData("//spherical", "/position/spherical", true)]
    [InlineData("//spherical", "/spherical", true)]
    [InlineData("//spherical", "/a/b/c/spherical", true)]
    [InlineData("//spherical", "/position/spherical/x", false)]
    [InlineData("//spherical", "/position/notspherical", false)]
    // "//" in the middle of a pattern.
    [InlineData("/synth//freq", "/synth/freq", true)]
    [InlineData("/synth//freq", "/synth/1/osc/2/freq", true)]
    [InlineData("/synth//freq", "/fx/1/freq", false)]
    // Combined with 1.0 wildcards.
    [InlineData("//osc[0-9]/*", "/synth/1/osc3/freq", true)]
    [InlineData("//{x,y}", "/pos/y", true)]
    [InlineData("/a//*/c", "/a/b/c", true)]
    [InlineData("/a//*/c", "/a/c", false)]
    [InlineData("///x", "/a/x", true)]
    public void Path_traversing_wildcard_matches_any_depth(string pattern, string address, bool expected)
    {
        Assert.Equal(expected, OscAddressPattern.IsMatch(pattern, address));
    }

    [Fact]
    public void Double_slash_counts_as_a_wildcard()
    {
        Assert.True(new OscAddressPattern("//x").HasWildcards);
        Assert.True(OscAddressPattern.ContainsWildcards("/a//b"));
        Assert.False(OscAddressPattern.ContainsWildcards("/a/b"));
    }

    [Fact]
    public void Address_space_dispatches_double_slash_patterns()
    {
        var space = new OscAddressSpace();
        var hits = new List<string>();
        foreach (var address in new[] { "/position/spherical", "/object/2/position/spherical", "/position/cartesian" })
            space.Register(address, _ => hits.Add(address));

        Assert.Equal(2, space.Dispatch(new OscMessage("//spherical")));
        Assert.Equal(["/object/2/position/spherical", "/position/spherical"], hits);
    }

    [Fact]
    public void Slip_uses_double_end_encoding_and_escapes()
    {
        var frame = OscSlip.Encode(new byte[] { 1, 0xC0, 2, 0xDB, 3 });
        Assert.Equal(new byte[] { 0xC0, 1, 0xDB, 0xDC, 2, 0xDB, 0xDD, 3, 0xC0 }, frame);
    }

    [Fact]
    public void Slip_decoder_reassembles_frames_split_across_reads()
    {
        var a = new OscMessage("/a", 1).ToBytes();
        var b = new OscMessage("/blob", new byte[] { 0xC0, 0xDB, 0xC0 }).ToBytes();
        var stream = OscSlip.Encode(a).Concat(OscSlip.Encode(b)).ToArray();

        var decoder = new OscSlipDecoder();
        var frames = new List<byte[]>();
        foreach (var chunk in stream.Chunk(3))
            frames.AddRange(decoder.Feed(chunk));

        Assert.Equal([a, b], frames);
    }

    [Fact]
    public void Slip_decoder_handles_single_end_framing_and_unknown_escapes()
    {
        var decoder = new OscSlipDecoder();
        var frames = decoder.Feed(new byte[] { 1, 2, 0xC0, 0xDB, 0x05, 0xC0, 0xC0, 0xC0 });
        Assert.Equal([new byte[] { 1, 2 }, new byte[] { 5 }], frames);
    }

    [Fact]
    public void Slip_decoder_drops_oversized_frames()
    {
        var decoder = new OscSlipDecoder(maxFrameSize: 4);
        var frames = decoder.Feed(new byte[] { 0xC0, 1, 2, 3, 4, 5, 6, 0xC0, 7, 8, 0xC0 });
        Assert.Equal([new byte[] { 7, 8 }], frames);
        Assert.Equal(1, decoder.DroppedFrames);
    }

    [Theory]
    [InlineData(OscFraming.Slip)]
    [InlineData(OscFraming.LengthPrefixed)]
    public async Task Stream_round_trips_packets(OscFraming framing)
    {
        var packets = new OscPacket[]
        {
            new OscMessage("/a", 1, "x"),
            new OscBundle(OscTimeTag.Immediate, new OscMessage("/b", new byte[] { 0xC0, 0xDB })),
            new OscMessage("/c", true, false, null, OscImpulse.Value, new OscTimeTag(7, 8)),
        };

        using var stream = new MemoryStream();
        foreach (var packet in packets)
            await stream.WriteOscPacketAsync(packet, framing);
        stream.Position = 0;

        var read = new List<OscPacket>();
        await foreach (var packet in stream.ReadOscPacketsAsync(framing))
            read.Add(packet);
        Assert.Equal(packets, read);
    }

    [Fact]
    public void Length_prefixed_framing_writes_big_endian_size()
    {
        using var stream = new MemoryStream();
        stream.WriteOscPacketAsync(new OscMessage("/a"), OscFraming.LengthPrefixed).AsTask().Wait();
        Assert.Equal(new byte[] { 0, 0, 0, 8 }, stream.ToArray()[..4]);
    }

    [Fact]
    public async Task Truncated_length_prefixed_stream_throws()
    {
        using var stream = new MemoryStream(new byte[] { 0, 0, 0, 8, 0x2f, 0x61 });
        await Assert.ThrowsAsync<OscException>(async () =>
        {
            await foreach (var _ in stream.ReadOscPacketsAsync(OscFraming.LengthPrefixed)) { }
        });
    }

    [Theory]
    [InlineData(",ifsbTFNIt", true)]
    [InlineData("ifsb", true)]
    [InlineData(",", true)]
    [InlineData(",ih", false)]
    [InlineData(",d", false)]
    [InlineData(",[i]", false)]
    public void Osc11_required_type_set(string tags, bool expected)
    {
        Assert.Equal(expected, OscTypeTags.UsesOnly(tags, OscTypeTags.Osc11Required));
    }

    [Fact]
    public void Osc10_required_set_excludes_osc11_additions()
    {
        Assert.False(OscTypeTags.UsesOnly(",T", OscTypeTags.Osc10Required));
        Assert.True(OscTypeTags.UsesOnly(",ifsb", OscTypeTags.Osc10Required));
    }
}

using static Osc.Tests.SpecExampleTests;

namespace Osc.Tests;

public class DecodingErrorTests
{
    [Theory]
    [InlineData("")]                                   // empty
    [InlineData("2f6100")]                             // not a multiple of 4
    [InlineData("41424300")]                           // doesn't start with '/' or '#'
    [InlineData("2f616263")]                           // address has no terminator
    [InlineData("2f610000 2c690000")]                  // int argument missing
    [InlineData("2f610000 2c730000 61626364")]         // string argument unterminated
    [InlineData("2f610000 2c620000 00000008 01020304")] // blob longer than data
    [InlineData("2f610000 2c620000 ffffffff")]         // negative blob size
    [InlineData("2f610000 2c780000")]                  // unknown type tag 'x'
    [InlineData("2f610000 2c5b0000")]                  // unterminated array
    [InlineData("2f610000 2c5d0000")]                  // unmatched ']'
    [InlineData("2f610000 2c000000 00000001")]         // trailing bytes after arguments
    [InlineData("2f610000 00000001")]                  // data but no type tag string
    [InlineData("2f610001")]                           // non-null string padding
    public void Malformed_messages_throw(string hex)
    {
        var bytes = Hex(hex);
        Assert.Throws<OscException>(() => OscPacket.Parse(bytes));
        Assert.False(OscPacket.TryParse(bytes, out var packet));
        Assert.Null(packet);
    }

    [Theory]
    [InlineData("2362756e 646c6500")]                                    // missing time tag
    [InlineData("2362756e 78787800 00000000 00000001")]                  // wrong "#bundle" string
    [InlineData("2362756e 646c6500 00000000 00000001 00000008 2f610000")] // element size exceeds data
    [InlineData("2362756e 646c6500 00000000 00000001 00000006 2f610000 2c000000")] // size not multiple of 4
    [InlineData("2362756e 646c6500 00000000 00000001 00000000")]          // zero-size element
    [InlineData("2362756e 646c6500 00000000 00000001 00000004 41424300")] // element isn't a packet
    public void Malformed_bundles_throw(string hex)
    {
        Assert.Throws<OscException>(() => OscPacket.Parse(Hex(hex)));
    }

    [Fact]
    public void Message_without_type_tag_string_decodes_with_no_arguments()
    {
        var message = Assert.IsType<OscMessage>(OscPacket.Parse(Hex("2f666f6f 00000000")));
        Assert.Equal("/foo", message.Address);
        Assert.Empty(message.Arguments);
    }

    [Fact]
    public void TryParse_succeeds_on_valid_input()
    {
        Assert.True(OscPacket.TryParse(new OscMessage("/ok", 1).ToBytes(), out var packet));
        Assert.Equal("/ok", Assert.IsType<OscMessage>(packet).Address);
    }
}

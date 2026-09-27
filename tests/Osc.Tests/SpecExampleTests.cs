namespace Osc.Tests;

/// <summary>Byte-exact examples taken from the OSC 1.0 specification.</summary>
public class SpecExampleTests
{
    internal static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", ""));

    [Fact]
    public void Oscillator_frequency_example_encodes_exactly()
    {
        var message = new OscMessage("/oscillator/4/frequency", 440.0f);
        var expected = Hex("2f6f7363 696c6c61 746f722f 342f6672 65717565 6e637900 2c660000 43dc0000");
        Assert.Equal(expected, message.ToBytes());
    }

    [Fact]
    public void Foo_example_encodes_exactly()
    {
        var message = new OscMessage("/foo", 1000, -1, "hello", 1.234f, 5.678f);
        var expected = Hex("2f666f6f 00000000 2c696973 66660000 000003e8 ffffffff 68656c6c 6f000000 3f9df3b6 40b5b22d");
        Assert.Equal(expected, message.ToBytes());
        Assert.Equal(",iisff", message.TypeTags);
    }

    [Fact]
    public void Foo_example_decodes()
    {
        var bytes = Hex("2f666f6f 00000000 2c696973 66660000 000003e8 ffffffff 68656c6c 6f000000 3f9df3b6 40b5b22d");
        var message = Assert.IsType<OscMessage>(OscPacket.Parse(bytes));
        Assert.Equal("/foo", message.Address);
        Assert.Equal(",iisff", message.TypeTags);
        Assert.Equal([1000, -1, "hello", 1.234f, 5.678f], message.Arguments);
    }

    [Theory]
    [InlineData("OSC", "4f534300")]
    [InlineData("data", "64617461 00000000")]
    [InlineData("", "00000000")]
    [InlineData("abcdefg", "61626364 65666700")]
    public void Strings_are_null_terminated_and_padded_to_four_bytes(string value, string expectedHex)
    {
        var bytes = new OscMessage("/s", value).ToBytes();
        // Skip "/s\0\0" and ",s\0\0".
        Assert.Equal(Hex(expectedHex), bytes[8..]);
    }
}

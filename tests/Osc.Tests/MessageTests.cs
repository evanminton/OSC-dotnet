using static Osc.Tests.SpecExampleTests;

namespace Osc.Tests;

public class MessageTests
{
    private static OscMessage RoundTrip(OscMessage message)
    {
        var bytes = message.ToBytes();
        Assert.Equal(0, bytes.Length % 4);
        return Assert.IsType<OscMessage>(OscPacket.Parse(bytes));
    }

    [Fact]
    public void Message_without_arguments_has_empty_type_tag_string()
    {
        var message = new OscMessage("/ping");
        Assert.Equal(",", message.TypeTags);
        Assert.Equal(Hex("2f70696e 67000000 2c000000"), message.ToBytes());
        Assert.Empty(RoundTrip(message).Arguments);
    }

    [Fact]
    public void All_types_round_trip()
    {
        var blob = new byte[] { 1, 2, 3, 4, 5 };
        var time = new OscTimeTag(0x83AA7E80, 0x80000000);
        var message = new OscMessage("/all",
            42, 3.5f, "text", blob, 1L << 40, time, Math.PI, new OscSymbol("sym"), 'x',
            new OscColor(255, 128, 0, 64), new OscMidi(0, 0x90, 60, 100),
            true, false, null, OscImpulse.Value);

        Assert.Equal(",ifsbhtdScrmTFNI", message.TypeTags);
        var decoded = RoundTrip(message);
        Assert.Equal(message.TypeTags, decoded.TypeTags);
        Assert.Equal(42, decoded.Get<int>(0));
        Assert.Equal(3.5f, decoded.Get<float>(1));
        Assert.Equal("text", decoded.Get<string>(2));
        Assert.Equal(blob, decoded.Get<byte[]>(3));
        Assert.Equal(1L << 40, decoded.Get<long>(4));
        Assert.Equal(time, decoded.Get<OscTimeTag>(5));
        Assert.Equal(Math.PI, decoded.Get<double>(6));
        Assert.Equal(new OscSymbol("sym"), decoded.Get<OscSymbol>(7));
        Assert.Equal('x', decoded.Get<char>(8));
        Assert.Equal(new OscColor(255, 128, 0, 64), decoded.Get<OscColor>(9));
        Assert.Equal(new OscMidi(0, 0x90, 60, 100), decoded.Get<OscMidi>(10));
        Assert.True(decoded.Get<bool>(11));
        Assert.False(decoded.Get<bool>(12));
        Assert.Null(decoded.Arguments[13]);
        Assert.Equal(OscImpulse.Value, decoded.Get<OscImpulse>(14));
        Assert.Equal(message, decoded);
    }

    [Theory]
    [InlineData(0, "00000000")]
    [InlineData(1, "00000001 01000000")]
    [InlineData(3, "00000003 01010100")]
    [InlineData(4, "00000004 01010101")]
    [InlineData(5, "00000005 01010101 01000000")]
    public void Blobs_are_size_prefixed_and_padded(int length, string expectedHex)
    {
        var blob = Enumerable.Repeat((byte)1, length).ToArray();
        var bytes = new OscMessage("/b", blob).ToBytes();
        Assert.Equal(Hex(expectedHex), bytes[8..]);
        Assert.Equal(blob, RoundTrip(new OscMessage("/b", blob)).Get<byte[]>(0));
    }

    [Fact]
    public void ReadOnlyMemory_is_encoded_as_blob()
    {
        var message = new OscMessage("/b", new ReadOnlyMemory<byte>([9, 8, 7]));
        Assert.Equal(",b", message.TypeTags);
        Assert.Equal(new byte[] { 9, 8, 7 }, RoundTrip(message).Get<byte[]>(0));
    }

    [Fact]
    public void Tags_without_data_encode_no_argument_bytes()
    {
        var message = new OscMessage("/x", true, false, null, OscImpulse.Value);
        Assert.Equal(Hex("2f780000 2c54464e 49000000"), message.ToBytes());
    }

    [Fact]
    public void Arrays_round_trip_including_nesting()
    {
        var message = new OscMessage("/arr", 1, new object?[] { 2, "two", new object?[] { 3f, true } }, 4);
        Assert.Equal(",i[is[fT]]i", message.TypeTags);
        var decoded = RoundTrip(message);
        var array = decoded.Get<object?[]>(1);
        Assert.Equal(2, array[0]);
        Assert.Equal("two", array[1]);
        Assert.Equal(new object?[] { 3f, true }, Assert.IsAssignableFrom<IReadOnlyList<object?>>(array[2]));
        Assert.Equal(4, decoded.Get<int>(2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Arrays_cannot_be_changed_through_the_message(bool decode)
    {
        var message = new OscMessage("/arr", (object)new object?[] { 1, new object?[] { 2 } });
        if (decode)
            message = RoundTrip(message);
        var bytes = message.ToBytes();

        var array = Assert.IsAssignableFrom<IReadOnlyList<object?>>(message.Arguments[0]);
        Assert.IsNotType<object?[]>(array);
        Assert.IsNotType<object?[]>(message.Arguments);
        Assert.Throws<NotSupportedException>(() => ((IList<object?>)array)[0] = "changed");

        var copy = message.Get<object?[]>(0);
        copy[0] = "changed";
        Assert.NotSame(copy, message.Get<object?[]>(0));
        Assert.Equal(1, message.Get<IReadOnlyList<object?>>(0)[0]);
        Assert.Equal(bytes, message.ToBytes());
    }

    [Fact]
    public void Arguments_of_one_message_can_build_another()
    {
        var message = RoundTrip(new OscMessage("/arr", 1, (object)new object?[] { 2, new object?[] { 3f } }));
        var copy = new OscMessage("/copy", message.Arguments.ToArray());
        Assert.Equal(",i[i[f]]", copy.TypeTags);
        Assert.Equal("/copy ,i[i[f]] 1 [2 [3]]", copy.ToString());
    }

    [Fact]
    public void Lists_are_encoded_as_arrays()
    {
        var message = new OscMessage("/arr", new List<int> { 1, 2, 3 }, "after");
        Assert.Equal(",[iii]s", message.TypeTags);
        Assert.Equal(new object?[] { 1, 2, 3 }, RoundTrip(message).Get<object?[]>(0));
    }

    [Fact]
    public void Non_ascii_strings_round_trip_as_utf8()
    {
        Assert.Equal("héllo ✓", RoundTrip(new OscMessage("/s", "héllo ✓")).Get<string>(0));
    }

    [Fact]
    public void Special_float_values_round_trip()
    {
        var decoded = RoundTrip(new OscMessage("/f", float.NaN, float.PositiveInfinity, -0.0f, double.NegativeInfinity));
        Assert.True(float.IsNaN(decoded.Get<float>(0)));
        Assert.Equal(float.PositiveInfinity, decoded.Get<float>(1));
        Assert.True(float.IsNegative(decoded.Get<float>(2)));
        Assert.Equal(double.NegativeInfinity, decoded.Get<double>(3));
    }

    [Fact]
    public void Get_throws_for_wrong_type()
    {
        var message = new OscMessage("/x", 1);
        Assert.Throws<InvalidCastException>(() => message.Get<float>(0));
    }

    [Fact]
    public void Int_and_float_with_same_value_are_not_equal()
    {
        Assert.NotEqual<OscPacket>(new OscMessage("/x", 1), new OscMessage("/x", 1f));
        Assert.Equal<OscPacket>(new OscMessage("/x", new byte[] { 1 }), new OscMessage("/x", new byte[] { 1 }));
        Assert.Equal(new OscMessage("/x", 1).GetHashCode(), new OscMessage("/x", 1).GetHashCode());
    }

    [Fact]
    public void ToString_is_readable()
    {
        Assert.Equal("/foo ,ifsN 1 2.5 \"a\" nil", new OscMessage("/foo", 1, 2.5f, "a", null).ToString());
        Assert.Equal("/bar", new OscMessage("/bar").ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("foo")]
    [InlineData("#bundle")]
    [InlineData("/a\0b")]
    public void Invalid_addresses_are_rejected(string address)
    {
        Assert.Throws<ArgumentException>(() => new OscMessage(address));
    }

    [Fact]
    public void Unsupported_argument_types_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new OscMessage("/x", 1m));
        Assert.Throws<ArgumentException>(() => new OscMessage("/x", (short)1));
        Assert.Throws<ArgumentException>(() => new OscMessage("/x", "a\0b"));
    }

    [Fact]
    public void WriteTo_matches_ToBytes()
    {
        var message = new OscMessage("/foo", 1, "two");
        var writer = new System.Buffers.ArrayBufferWriter<byte>();
        message.WriteTo(writer);
        Assert.Equal(message.ToBytes(), writer.WrittenSpan.ToArray());
    }

    [Theory]
    [InlineData("/a[")]
    [InlineData("/a{b")]
    [InlineData("/{a/b}")]
    public void Malformed_address_patterns_are_rejected(string address)
    {
        Assert.Throws<ArgumentException>(() => new OscMessage(address));
    }

    [Fact]
    public void Single_reference_type_array_is_the_argument_list_unless_cast_to_object()
    {
        var names = new[] { "x", "y" };
        Assert.Equal(",ss", new OscMessage("/a", names).TypeTags);
        Assert.Equal(",[ss]", new OscMessage("/a", (object)names).TypeTags);
    }

    [Fact]
    public void Blobs_are_copied_so_later_changes_do_not_affect_the_message()
    {
        var blob = new byte[] { 1, 2, 3 };
        var memory = new byte[] { 4, 5, 6 };
        var message = new OscMessage("/b", blob, new ReadOnlyMemory<byte>(memory));
        var bytes = message.ToBytes();
        var hash = message.GetHashCode();

        blob[0] = 9;
        memory[0] = 9;

        Assert.Equal(bytes, message.ToBytes());
        Assert.Equal(hash, message.GetHashCode());
        Assert.Equal(new byte[] { 1, 2, 3 }, message.Get<byte[]>(0));
        Assert.Equal(new byte[] { 4, 5, 6 }, message.Get<byte[]>(1));
    }

    [Fact]
    public void Blobs_cannot_be_changed_through_the_message()
    {
        var message = RoundTrip(new OscMessage("/b", new byte[] { 1, 2, 3 }));
        var bytes = message.ToBytes();

        Assert.IsType<ReadOnlyMemory<byte>>(message.Arguments[0]);
        Assert.Equal(new byte[] { 1, 2, 3 }, message.Get<ReadOnlyMemory<byte>>(0).ToArray());
        message.Get<byte[]>(0)[0] = 9;

        Assert.NotSame(message.Get<byte[]>(0), message.Get<byte[]>(0));
        Assert.Equal(bytes, message.ToBytes());
    }

    [Fact]
    public void Largest_char_round_trips()
    {
        Assert.Equal(char.MaxValue, RoundTrip(new OscMessage("/c", char.MaxValue)).Get<char>(0));
    }
}

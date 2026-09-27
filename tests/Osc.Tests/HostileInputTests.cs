using System.Text;
using static Osc.Tests.SpecExampleTests;

namespace Osc.Tests;

/// <summary>Input crafted to exhaust the stack or desynchronise encoding must fail cleanly.</summary>
public class HostileInputTests
{
    /// <summary>Encodes a message "/a" whose type tag string is <paramref name="tags"/>, with no argument data.</summary>
    private static byte[] MessageWithTypeTags(string tags)
    {
        var padded = (tags.Length / 4 + 1) * 4;
        var bytes = new byte[4 + padded];
        "/a"u8.CopyTo(bytes);
        Encoding.ASCII.GetBytes(tags).CopyTo(bytes, 4);
        return bytes;
    }

    /// <summary>Encodes <paramref name="depth"/> bundles each containing only the next, the innermost empty.</summary>
    private static byte[] NestedBundles(int depth)
    {
        var bytes = new byte[20 * (depth - 1) + 16];
        for (var level = 0; level < depth; level++)
        {
            var offset = 20 * level;
            "#bundle\0"u8.CopyTo(bytes.AsSpan(offset));
            bytes[offset + 15] = 1; // time tag: immediately
            if (level < depth - 1)
                System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset + 16), bytes.Length - offset - 20);
        }
        return bytes;
    }

    [Fact]
    public void Arrays_nested_up_to_the_limit_decode()
    {
        var depth = OscPacket.MaxNestingDepth;
        var message = Assert.IsType<OscMessage>(OscPacket.Parse(MessageWithTypeTags("," + new string('[', depth) + new string(']', depth))));
        Assert.Single(message.Arguments);
    }

    [Theory]
    [InlineData(OscPacket.MaxNestingDepth + 1, true)]
    [InlineData(60_000, true)]
    [InlineData(60_000, false)] // unterminated
    public void Arrays_nested_too_deeply_are_rejected(int depth, bool closed)
    {
        var bytes = MessageWithTypeTags("," + new string('[', depth) + (closed ? new string(']', depth) : ""));
        Assert.Throws<OscException>(() => OscPacket.Parse(bytes));
        Assert.False(OscPacket.TryParse(bytes, out _));
    }

    [Fact]
    public void Bundles_nested_up_to_the_limit_decode()
    {
        var bundle = Assert.IsType<OscBundle>(OscPacket.Parse(NestedBundles(OscPacket.MaxNestingDepth)));
        Assert.Empty(bundle.Flatten());
    }

    [Theory]
    [InlineData(OscPacket.MaxNestingDepth + 1)]
    [InlineData(50_000)]
    public void Bundles_nested_too_deeply_are_rejected(int depth)
    {
        var bytes = NestedBundles(depth);
        Assert.Throws<OscException>(() => OscPacket.Parse(bytes));
        Assert.False(OscPacket.TryParse(bytes, out _));
    }

    [Fact]
    public void Constructing_bundles_nested_too_deeply_throws()
    {
        var bundle = new OscBundle(OscTimeTag.Immediate);
        for (var i = 1; i < OscPacket.MaxNestingDepth; i++)
            bundle = new OscBundle(OscTimeTag.Immediate, bundle);
        Assert.Throws<ArgumentException>(() => new OscBundle(OscTimeTag.Immediate, bundle));
    }

    [Fact]
    public void Constructing_arrays_nested_too_deeply_throws()
    {
        object? value = 1;
        for (var i = 0; i < OscPacket.MaxNestingDepth; i++)
            value = new object?[] { value };
        var depth = OscPacket.MaxNestingDepth;
        Assert.Equal("," + new string('[', depth) + "i" + new string(']', depth), new OscMessage("/a", value).TypeTags);
        Assert.Throws<ArgumentException>(() => new OscMessage("/a", (object?)new object?[] { value }));
    }

    [Fact]
    public void Self_referencing_list_throws_instead_of_overflowing()
    {
        var list = new List<object?>();
        list.Add(list);
        Assert.Throws<ArgumentException>(() => new OscMessage("/a", (object?)list));
    }

    [Fact]
    public void Changing_a_nested_list_after_construction_does_not_change_the_message()
    {
        var inner = new List<object?> { 1 };
        var message = new OscMessage("/a", (object?)new List<object?> { inner });
        var bytes = message.ToBytes();
        var hash = message.GetHashCode();

        inner.Add("added later");

        Assert.Equal(",[[i]]", message.TypeTags);
        Assert.Equal(bytes, message.ToBytes());
        Assert.Equal(hash, message.GetHashCode());
        Assert.Equal(message, OscPacket.Parse(message.ToBytes()));
    }

    [Theory]
    [InlineData("/a", true)]
    [InlineData("/b", false)]
    public void Many_empty_alternatives_match_without_overflowing(string address, bool expected)
    {
        var pattern = "/" + string.Concat(Enumerable.Repeat("{}", 30_000)) + "a";
        Assert.Equal(expected, OscAddressPattern.IsMatch(pattern, address));
    }

    [Fact]
    public void Hostile_pattern_is_dispatched_without_overflowing()
    {
        var space = new OscAddressSpace();
        var calls = 0;
        space.Register("/synth/gain", _ => calls++);
        var pattern = "/" + string.Concat(Enumerable.Repeat("*{,x}", 20_000)) + "/gain";
        Assert.Equal(1, space.Dispatch(new OscMessage(pattern)));
        Assert.Equal(1, calls);
    }
}

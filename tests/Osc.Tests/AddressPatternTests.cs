namespace Osc.Tests;

public class AddressPatternTests
{
    [Theory]
    // Literals
    [InlineData("/a/b", "/a/b", true)]
    [InlineData("/a/b", "/a/bc", false)]
    [InlineData("/a/b", "/a", false)]
    // '?' matches any single character except '/'
    [InlineData("/a?c", "/abc", true)]
    [InlineData("/a?c", "/ac", false)]
    [InlineData("/a?c", "/a/c", false)]
    // '*' matches zero or more characters within one part
    [InlineData("/osc*", "/osc", true)]
    [InlineData("/osc*", "/oscillator", true)]
    [InlineData("/*", "/anything", true)]
    [InlineData("/*", "/two/parts", false)]
    [InlineData("/*/frequency", "/oscillator/frequency", true)]
    [InlineData("/*/*", "/a/b", true)]
    [InlineData("/*/*", "/a/b/c", false)]
    [InlineData("/a*b*c", "/aXXbYYc", true)]
    [InlineData("/a*b*c", "/aXXbYY", false)]
    [InlineData("/a**b", "/aZb", true)]
    [InlineData("/*x", "/x", true)]
    // Character classes
    [InlineData("/[abc]", "/b", true)]
    [InlineData("/[abc]", "/d", false)]
    [InlineData("/ch[0-9]", "/ch7", true)]
    [InlineData("/ch[0-9]", "/chA", false)]
    [InlineData("/[a-cx-z]", "/y", true)]
    [InlineData("/[!0-9]", "/a", true)]
    [InlineData("/[!0-9]", "/5", false)]
    [InlineData("/[-a]", "/-", true)]
    [InlineData("/[a-]", "/-", true)]
    [InlineData("/[!]", "/x", true)]
    [InlineData("/[]", "/x", false)]
    [InlineData("/[!a]", "/", false)]
    [InlineData("/a[!b]c", "/a/c", false)]
    // Alternatives
    [InlineData("/{foo,bar}", "/foo", true)]
    [InlineData("/{foo,bar}", "/bar", true)]
    [InlineData("/{foo,bar}", "/baz", false)]
    [InlineData("/{foo,bar}", "/foobar", false)]
    [InlineData("/x{,y}", "/x", true)]
    [InlineData("/x{,y}", "/xy", true)]
    [InlineData("/{a,ab}c", "/abc", true)]
    [InlineData("/{abcdef}", "/abc", false)]
    // Combinations
    [InlineData("/mixer/{in,out}/ch[1-4]/*", "/mixer/in/ch3/gain", true)]
    [InlineData("/mixer/{in,out}/ch[1-4]/*", "/mixer/out/ch5/gain", false)]
    [InlineData("/*/[0-9]/fr?q*", "/oscillator/4/frequency", true)]
    public void Matches(string pattern, string address, bool expected)
    {
        Assert.Equal(expected, OscAddressPattern.IsMatch(pattern, address));
        Assert.Equal(expected, new OscAddressPattern(pattern).IsMatch(address));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("/[abc")]
    [InlineData("/{a,b")]
    [InlineData("/{a/b,c}")]
    public void Malformed_patterns_throw(string pattern)
    {
        Assert.Throws<ArgumentException>(() => new OscAddressPattern(pattern));
    }

    [Fact]
    public void HasWildcards_detects_pattern_characters()
    {
        Assert.False(new OscAddressPattern("/plain/address").HasWildcards);
        Assert.True(new OscAddressPattern("/a/*").HasWildcards);
        Assert.True(new OscAddressPattern("/a/{b,c}").HasWildcards);
    }

    [Fact]
    public void Many_stars_do_not_backtrack_exponentially()
    {
        var address = "/" + new string('a', 200);
        var pattern = new OscAddressPattern("/" + string.Concat(Enumerable.Repeat("*a", 50)) + "b");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.False(pattern.IsMatch(address));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2));
    }
}

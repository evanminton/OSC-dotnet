namespace Osc;

/// <summary>
/// A compiled OSC address pattern. Supports the OSC 1.0 wildcards <c>?</c> (any single character),
/// <c>*</c> (any run of zero or more characters), <c>[abc]</c>, <c>[a-z]</c> and <c>[!a-z]</c> character
/// classes, and <c>{foo,bar}</c> alternatives, none of which ever match '/'. Also supports the OSC 1.1
/// path-traversing wildcard <c>//</c> (from XPath), which matches zero or more whole address parts, so
/// <c>//spherical</c> matches <c>/spherical</c> and <c>/position/spherical</c>.
/// </summary>
public sealed class OscAddressPattern
{
    /// <summary>Characters with special meaning in address patterns (plus ',' inside braces).</summary>
    public static ReadOnlySpan<char> SpecialCharacters => "?*[]{}";

    private readonly Token[] _tokens;

    /// <summary>Compiles <paramref name="pattern"/>.</summary>
    /// <exception cref="ArgumentException">The pattern is malformed (does not start with '/', or has an unclosed '[' or '{').</exception>
    public OscAddressPattern(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Length == 0 || pattern[0] != '/')
            throw new ArgumentException("An OSC address pattern must start with '/'.", nameof(pattern));
        Pattern = pattern;
        _tokens = Compile(pattern, out var error) ?? throw new ArgumentException(error, nameof(pattern));
    }

    /// <summary>Returns why <paramref name="pattern"/> (which starts with '/') cannot be compiled, or null if it can.</summary>
    internal static string? GetError(string pattern) => Compile(pattern, out var error) is null ? error : null;

    /// <summary>The source pattern.</summary>
    public string Pattern { get; }

    /// <summary>True if the pattern contains any wildcard characters.</summary>
    public bool HasWildcards => _tokens.Any(t => t.Kind != TokenKind.Literal);

    /// <summary>True if <paramref name="addressPattern"/> contains any OSC 1.0 wildcard or the OSC 1.1 <c>//</c> wildcard.</summary>
    public static bool ContainsWildcards(string addressPattern)
    {
        ArgumentNullException.ThrowIfNull(addressPattern);
        return addressPattern.AsSpan().IndexOfAny(SpecialCharacters) >= 0 || addressPattern.Contains("//", StringComparison.Ordinal);
    }

    /// <summary>Tests whether this pattern matches the OSC address <paramref name="address"/>.</summary>
    public bool IsMatch(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        // Dynamic programming over tokens from last to first, without recursion, so that a hostile
        // pattern (e.g. thousands of "{}") cannot exhaust the stack. After processing token t,
        // cur[p] says whether tokens t.. match address[p..]; next holds the same for tokens t+1...
        var length = address.Length;
        var next = new bool[length + 1];
        var cur = new bool[length + 1];
        next[length] = true;
        for (var t = _tokens.Length - 1; t >= 0; t--)
        {
            var token = _tokens[t];
            switch (token.Kind)
            {
                case TokenKind.Literal:
                    for (var p = 0; p <= length; p++)
                        cur[p] = p < length && address[p] == token.Char && next[p + 1];
                    break;
                case TokenKind.AnyChar:
                    for (var p = 0; p <= length; p++)
                        cur[p] = p < length && address[p] != '/' && next[p + 1];
                    break;
                case TokenKind.CharClass:
                    for (var p = 0; p <= length; p++)
                        cur[p] = p < length && address[p] != '/' && token.MatchesClass(address[p]) && next[p + 1];
                    break;
                case TokenKind.Star:
                    // Either match nothing here, or consume one non-'/' character and stay on the star.
                    cur[length] = next[length];
                    for (var p = length - 1; p >= 0; p--)
                        cur[p] = next[p] || (address[p] != '/' && cur[p + 1]);
                    break;
                case TokenKind.PathTraversal:
                {
                    // Consumes a '/' plus any number of whole parts, ending at a '/' boundary:
                    // matches at p if address[p] is '/' and some '/' at q >= p has next[q + 1].
                    cur[length] = false;
                    var anyBoundary = false;
                    for (var p = length - 1; p >= 0; p--)
                    {
                        if (address[p] == '/')
                        {
                            anyBoundary |= next[p + 1];
                            cur[p] = anyBoundary;
                        }
                        else
                        {
                            cur[p] = false;
                        }
                    }
                    break;
                }
                case TokenKind.Alternatives:
                    for (var p = 0; p <= length; p++)
                    {
                        cur[p] = false;
                        foreach (var alt in token.Alternatives!)
                        {
                            if (p + alt.Length <= length
                                && string.CompareOrdinal(address, p, alt, 0, alt.Length) == 0
                                && next[p + alt.Length])
                            {
                                cur[p] = true;
                                break;
                            }
                        }
                    }
                    break;
                default:
                    throw new InvalidOperationException();
            }
            (cur, next) = (next, cur);
        }
        return next[0];
    }

    /// <summary>Tests whether <paramref name="pattern"/> matches <paramref name="address"/>.</summary>
    public static bool IsMatch(string pattern, string address) => new OscAddressPattern(pattern).IsMatch(address);

    /// <inheritdoc />
    public override string ToString() => Pattern;

    private static Token[]? Compile(string pattern, out string? error)
    {
        error = null;
        var tokens = new List<Token>();
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            switch (c)
            {
                case '/' when i + 1 < pattern.Length && pattern[i + 1] == '/':
                    tokens.Add(new Token(TokenKind.PathTraversal));
                    i++;
                    // Extra slashes after "//" add nothing: "///a" behaves like "//a".
                    while (i + 1 < pattern.Length && pattern[i + 1] == '/')
                        i++;
                    break;
                case '?':
                    tokens.Add(new Token(TokenKind.AnyChar));
                    break;
                case '*':
                    // Consecutive stars are equivalent to one.
                    if (tokens.Count == 0 || tokens[^1].Kind != TokenKind.Star)
                        tokens.Add(new Token(TokenKind.Star));
                    break;
                case '[':
                {
                    var close = pattern.IndexOf(']', i + 1);
                    if (close < 0)
                    {
                        error = $"Unclosed '[' at position {i} in address pattern \"{pattern}\".";
                        return null;
                    }
                    tokens.Add(ParseClass(pattern.AsSpan(i + 1, close - i - 1)));
                    i = close;
                    break;
                }
                case '{':
                {
                    var close = pattern.IndexOf('}', i + 1);
                    if (close < 0)
                    {
                        error = $"Unclosed '{{' at position {i} in address pattern \"{pattern}\".";
                        return null;
                    }
                    var alternatives = pattern.Substring(i + 1, close - i - 1).Split(',');
                    if (alternatives.Any(a => a.Contains('/')))
                    {
                        error = $"Alternatives in {{...}} cannot contain '/' in address pattern \"{pattern}\".";
                        return null;
                    }
                    tokens.Add(new Token(TokenKind.Alternatives) { Alternatives = alternatives });
                    i = close;
                    break;
                }
                default:
                    tokens.Add(new Token(TokenKind.Literal) { Char = c });
                    break;
            }
        }
        return [.. tokens];
    }

    private static Token ParseClass(ReadOnlySpan<char> body)
    {
        var negated = body.Length > 0 && body[0] == '!';
        if (negated)
            body = body[1..];

        var ranges = new List<(char From, char To)>();
        for (var i = 0; i < body.Length; i++)
        {
            // A '-' between two characters is a range; a '-' at the start or end is literal.
            if (i + 2 < body.Length && body[i + 1] == '-')
            {
                var (a, b) = (body[i], body[i + 2]);
                ranges.Add(a <= b ? (a, b) : (b, a));
                i += 2;
            }
            else
            {
                ranges.Add((body[i], body[i]));
            }
        }
        return new Token(TokenKind.CharClass) { Ranges = [.. ranges], Negated = negated };
    }

    private enum TokenKind { Literal, AnyChar, Star, CharClass, Alternatives, PathTraversal }

    private sealed class Token(TokenKind kind)
    {
        public TokenKind Kind { get; } = kind;
        public char Char { get; init; }
        public (char From, char To)[]? Ranges { get; init; }
        public bool Negated { get; init; }
        public string[]? Alternatives { get; init; }

        public bool MatchesClass(char c)
        {
            var inClass = false;
            foreach (var (from, to) in Ranges!)
            {
                if (c >= from && c <= to) { inClass = true; break; }
            }
            return inClass != Negated;
        }
    }
}

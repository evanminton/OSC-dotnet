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
        _tokens = Compile(pattern);
    }

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
        // memo[t, p]: 0 = unknown, 1 = matches, 2 = does not match.
        var memo = new byte[_tokens.Length + 1, address.Length + 1];
        return Match(0, 0, address, memo);
    }

    /// <summary>Tests whether <paramref name="pattern"/> matches <paramref name="address"/>.</summary>
    public static bool IsMatch(string pattern, string address) => new OscAddressPattern(pattern).IsMatch(address);

    /// <inheritdoc />
    public override string ToString() => Pattern;

    private bool Match(int t, int p, string address, byte[,] memo)
    {
        if (memo[t, p] != 0)
            return memo[t, p] == 1;

        bool result;
        if (t == _tokens.Length)
        {
            result = p == address.Length;
        }
        else
        {
            var token = _tokens[t];
            switch (token.Kind)
            {
                case TokenKind.Literal:
                    result = p < address.Length && address[p] == token.Char && Match(t + 1, p + 1, address, memo);
                    break;
                case TokenKind.AnyChar:
                    result = p < address.Length && address[p] != '/' && Match(t + 1, p + 1, address, memo);
                    break;
                case TokenKind.Star:
                    result = false;
                    for (var end = p; ; end++)
                    {
                        if (Match(t + 1, end, address, memo)) { result = true; break; }
                        if (end == address.Length || address[end] == '/') break;
                    }
                    break;
                case TokenKind.CharClass:
                    result = p < address.Length && address[p] != '/' && token.MatchesClass(address[p]) && Match(t + 1, p + 1, address, memo);
                    break;
                case TokenKind.PathTraversal:
                    // Consumes a '/' plus any number of whole parts, ending at a '/' boundary.
                    result = false;
                    if (p < address.Length && address[p] == '/')
                    {
                        for (var q = p; q < address.Length; q++)
                        {
                            if (address[q] == '/' && Match(t + 1, q + 1, address, memo)) { result = true; break; }
                        }
                    }
                    break;
                case TokenKind.Alternatives:
                    result = false;
                    foreach (var alt in token.Alternatives!)
                    {
                        if (p + alt.Length <= address.Length
                            && string.CompareOrdinal(address, p, alt, 0, alt.Length) == 0
                            && Match(t + 1, p + alt.Length, address, memo))
                        {
                            result = true;
                            break;
                        }
                    }
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        memo[t, p] = result ? (byte)1 : (byte)2;
        return result;
    }

    private static Token[] Compile(string pattern)
    {
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
                        throw new ArgumentException($"Unclosed '[' at position {i} in address pattern \"{pattern}\".", nameof(pattern));
                    tokens.Add(ParseClass(pattern.AsSpan(i + 1, close - i - 1)));
                    i = close;
                    break;
                }
                case '{':
                {
                    var close = pattern.IndexOf('}', i + 1);
                    if (close < 0)
                        throw new ArgumentException($"Unclosed '{{' at position {i} in address pattern \"{pattern}\".", nameof(pattern));
                    var alternatives = pattern.Substring(i + 1, close - i - 1).Split(',');
                    if (alternatives.Any(a => a.Contains('/')))
                        throw new ArgumentException($"Alternatives in {{...}} cannot contain '/' in address pattern \"{pattern}\".", nameof(pattern));
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

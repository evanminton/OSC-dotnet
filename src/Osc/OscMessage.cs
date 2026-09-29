using System.Collections;
using System.Text;

namespace Osc;

/// <summary>
/// An OSC message: an address pattern, a type tag string, and zero or more arguments.
/// </summary>
/// <remarks>
/// Arguments are plain .NET values; the type tag of each is inferred from its type:
/// <list type="table">
///   <listheader><term>.NET type</term><description>Type tag</description></listheader>
///   <item><term><see cref="int"/></term><description><c>i</c> int32</description></item>
///   <item><term><see cref="float"/></term><description><c>f</c> float32</description></item>
///   <item><term><see cref="string"/></term><description><c>s</c> OSC-string</description></item>
///   <item><term><see cref="byte"/>[] / <see cref="ReadOnlyMemory{T}"/></term><description><c>b</c> OSC-blob (decoded as byte[])</description></item>
///   <item><term><see cref="long"/></term><description><c>h</c> int64</description></item>
///   <item><term><see cref="OscTimeTag"/></term><description><c>t</c> time tag</description></item>
///   <item><term><see cref="double"/></term><description><c>d</c> float64</description></item>
///   <item><term><see cref="OscSymbol"/></term><description><c>S</c> symbol</description></item>
///   <item><term><see cref="char"/></term><description><c>c</c> ASCII character</description></item>
///   <item><term><see cref="OscColor"/></term><description><c>r</c> RGBA color</description></item>
///   <item><term><see cref="OscMidi"/></term><description><c>m</c> MIDI message</description></item>
///   <item><term><see cref="bool"/></term><description><c>T</c> / <c>F</c></description></item>
///   <item><term><see langword="null"/></term><description><c>N</c> nil</description></item>
///   <item><term><see cref="OscImpulse"/></term><description><c>I</c> infinitum</description></item>
///   <item><term><see cref="object"/>[] (any non-byte <see cref="IList"/>)</term><description><c>[</c> … <c>]</c> array (decoded as object?[])</description></item>
/// </list>
/// </remarks>
public sealed class OscMessage : OscPacket
{
    /// <summary>Creates a message.</summary>
    /// <param name="address">The OSC address pattern; must start with '/'.</param>
    /// <param name="arguments">
    /// The arguments, in order. Passing a single <c>object?[]</c>, <c>string[]</c> or other
    /// <see cref="IEnumerable{T}"/> of reference types supplies the whole argument list; to send it as one
    /// array argument instead, cast it to <see cref="object"/>: <c>new OscMessage("/a", (object)names)</c>.
    /// </param>
    /// <remarks>
    /// Nested lists (as <c>object?[]</c>) and blobs (as <c>byte[]</c>) are copied, so changing them afterwards
    /// does not affect the message.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The address is invalid or a malformed pattern (e.g. an unclosed '[' or '{'), an argument has an
    /// unsupported type, or arrays nest deeper than <see cref="OscPacket.MaxNestingDepth"/>.
    /// </exception>
    public OscMessage(string address, params IEnumerable<object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(arguments);
        if (address.Length == 0 || address[0] != '/')
            throw new ArgumentException("An OSC address pattern must start with '/'.", nameof(address));
        if (address.Contains('\0'))
            throw new ArgumentException("An OSC address pattern cannot contain a null character.", nameof(address));
        if (PatternError(address) is { } error)
            throw new ArgumentException(error, nameof(address));

        Address = address;
        var args = arguments.ToArray();
        var tags = new StringBuilder(",", args.Length + 1);
        for (var i = 0; i < args.Length; i++)
            args[i] = AppendTypeTag(tags, args[i], depth: 0);
        Arguments = args;
        TypeTags = tags.ToString();
    }

    private OscMessage(string address, object?[] arguments, string typeTags)
    {
        Address = address;
        Arguments = arguments;
        TypeTags = typeTags;
    }

    /// <summary>The OSC address pattern, e.g. <c>/mixer/channel/1/gain</c>. It may contain pattern characters.</summary>
    public string Address { get; }

    /// <summary>The arguments, in order.</summary>
    public IReadOnlyList<object?> Arguments { get; }

    /// <summary>The OSC type tag string, including the leading comma, e.g. <c>,ifs</c>.</summary>
    public string TypeTags { get; }

    /// <summary>Gets the argument at <paramref name="index"/> as <typeparamref name="T"/>.</summary>
    /// <exception cref="InvalidCastException">The argument is not a <typeparamref name="T"/>.</exception>
    public T Get<T>(int index) => Arguments[index] is T value
        ? value
        : throw new InvalidCastException($"Argument {index} of {Address} is {Arguments[index]?.GetType().Name ?? "null"}, not {typeof(T).Name}.");

    /// <inheritdoc />
    public override string ToString() =>
        Arguments.Count == 0 ? Address : $"{Address} {TypeTags} {string.Join(" ", Arguments.Select(Format))}";

    private static string Format(object? value) => value switch
    {
        null => "nil",
        string s => $"\"{s}\"",
        byte[] b => $"blob[{b.Length}]",
        object?[] a => $"[{string.Join(" ", a.Select(Format))}]",
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    /// <summary>Appends the type tag for <paramref name="arg"/> and returns the value to store: the argument itself, or a copy of a list.</summary>
    private static object? AppendTypeTag(StringBuilder tags, object? arg, int depth)
    {
        switch (arg)
        {
            case null: tags.Append('N'); break;
            case int: tags.Append('i'); break;
            case float: tags.Append('f'); break;
            case string s:
                if (s.Contains('\0'))
                    throw new ArgumentException("OSC-string arguments cannot contain a null character.");
                tags.Append('s');
                break;
            case byte[] blob:
                tags.Append('b');
                return blob.Clone();
            case ReadOnlyMemory<byte> blob:
                tags.Append('b');
                return blob.ToArray();
            case long: tags.Append('h'); break;
            case OscTimeTag: tags.Append('t'); break;
            case double: tags.Append('d'); break;
            case OscSymbol sym:
                if (sym.Value is null || sym.Value.Contains('\0'))
                    throw new ArgumentException("OSC symbol arguments must be non-null and cannot contain a null character.");
                tags.Append('S');
                break;
            case char: tags.Append('c'); break;
            case OscColor: tags.Append('r'); break;
            case OscMidi: tags.Append('m'); break;
            case bool b: tags.Append(b ? 'T' : 'F'); break;
            case OscImpulse: tags.Append('I'); break;
            case IList list:
                if (depth >= MaxNestingDepth)
                    throw new ArgumentException($"Array arguments cannot nest more than {MaxNestingDepth} deep.");
                tags.Append('[');
                var copy = new object?[list.Count];
                for (var i = 0; i < copy.Length; i++)
                    copy[i] = AppendTypeTag(tags, list[i], depth + 1);
                tags.Append(']');
                return copy;
            default:
                throw new ArgumentException($"Arguments of type {arg.GetType().FullName} cannot be encoded as OSC.");
        }
        return arg;
    }

    internal override void Write(ref OscWriter writer)
    {
        writer.WriteString(Address);
        writer.WriteString(TypeTags);
        foreach (var arg in Arguments)
            WriteArgument(ref writer, arg);
    }

    private static void WriteArgument(ref OscWriter writer, object? arg)
    {
        switch (arg)
        {
            case int i: writer.WriteInt32(i); break;
            case float f: writer.WriteFloat32(f); break;
            case string s: writer.WriteString(s); break;
            case byte[] b: writer.WriteBlob(b); break;
            case long h: writer.WriteInt64(h); break;
            case OscTimeTag t: writer.WriteUInt64(t.Value); break;
            case double d: writer.WriteFloat64(d); break;
            case OscSymbol sym: writer.WriteString(sym.Value); break;
            case char c: writer.WriteInt32(c); break;
            case OscColor r: writer.WriteBytes(r.R, r.G, r.B, r.A); break;
            case OscMidi m: writer.WriteBytes(m.Port, m.Status, m.Data1, m.Data2); break;
            case IList list:
                foreach (var item in list)
                    WriteArgument(ref writer, item);
                break;
            // null (N), bool (T/F) and OscImpulse (I) have no argument data.
        }
    }

    internal static OscMessage ParseBody(ReadOnlySpan<byte> data)
    {
        var reader = new OscReader(data);
        var address = reader.ReadString();
        if (address.Length == 0 || address[0] != '/')
            throw new OscException("An OSC address pattern must start with '/'.");
        if (PatternError(address) is { } error)
            throw new OscException(error);

        // Older implementations may omit the type tag string; with no data after the address,
        // that is simply a message without arguments.
        if (reader.IsAtEnd)
            return new OscMessage(address, [], ",");
        if (reader.Peek() != (byte)',')
            throw new OscException($"Message {address} has argument data but no type tag string.");

        var typeTags = reader.ReadString();
        var index = 1;
        var args = ReadArguments(ref reader, typeTags, ref index, depth: 0);
        if (!reader.IsAtEnd)
            throw new OscException($"Message {address} has {reader.Remaining} bytes left over after its arguments.");
        return new OscMessage(address, args, typeTags);
    }

    /// <summary>Returns why <paramref name="address"/> is not a valid address pattern, or null if it is.</summary>
    private static string? PatternError(string address) =>
        OscAddressPattern.ContainsWildcards(address) ? OscAddressPattern.GetError(address) : null;

    private static object?[] ReadArguments(ref OscReader reader, string tags, ref int index, int depth)
    {
        var nested = depth > 0;
        if (depth > MaxNestingDepth)
            throw new OscException($"Arrays nest more than {MaxNestingDepth} deep in type tag string.");
        var args = new List<object?>();
        while (index < tags.Length)
        {
            var tag = tags[index++];
            switch (tag)
            {
                case 'i': args.Add(reader.ReadInt32()); break;
                case 'f': args.Add(reader.ReadFloat32()); break;
                case 's': args.Add(reader.ReadString()); break;
                case 'b': args.Add(reader.ReadBlob()); break;
                case 'h': args.Add(reader.ReadInt64()); break;
                case 't': args.Add(new OscTimeTag(reader.ReadUInt64())); break;
                case 'd': args.Add(reader.ReadFloat64()); break;
                case 'S': args.Add(new OscSymbol(reader.ReadString())); break;
                case 'c':
                {
                    var c = reader.ReadInt32();
                    if (c is < char.MinValue or > char.MaxValue)
                        throw new OscException($"Character argument 0x{c:X} is outside the range of a .NET char.");
                    args.Add((char)c);
                    break;
                }
                case 'r':
                {
                    var b = reader.ReadFour();
                    args.Add(new OscColor(b[0], b[1], b[2], b[3]));
                    break;
                }
                case 'm':
                {
                    var b = reader.ReadFour();
                    args.Add(new OscMidi(b[0], b[1], b[2], b[3]));
                    break;
                }
                case 'T': args.Add(true); break;
                case 'F': args.Add(false); break;
                case 'N': args.Add(null); break;
                case 'I': args.Add(OscImpulse.Value); break;
                case '[': args.Add(ReadArguments(ref reader, tags, ref index, depth + 1)); break;
                case ']':
                    if (!nested)
                        throw new OscException($"Unmatched ']' in type tag string \"{tags}\".");
                    return [.. args];
                default:
                    throw new OscException($"Unknown OSC type tag '{tag}' in \"{tags}\".");
            }
        }
        if (nested)
            throw new OscException($"Unterminated '[' in type tag string \"{tags}\".");
        return [.. args];
    }
}

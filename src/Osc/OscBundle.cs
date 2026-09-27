namespace Osc;

/// <summary>
/// An OSC bundle: the string <c>#bundle</c>, a time tag, and zero or more size-prefixed elements,
/// each of which is a message or another bundle.
/// </summary>
public sealed class OscBundle : OscPacket
{
    private static ReadOnlySpan<byte> BundleTag => "#bundle\0"u8;

    /// <summary>Creates a bundle.</summary>
    public OscBundle(OscTimeTag timeTag, params IEnumerable<OscPacket> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        var list = elements.ToArray();
        if (Array.IndexOf(list, null) >= 0)
            throw new ArgumentException("Bundle elements cannot be null.", nameof(elements));
        TimeTag = timeTag;
        Elements = list;
    }

    /// <summary>When the bundle's contents should take effect.</summary>
    public OscTimeTag TimeTag { get; }

    /// <summary>The contained messages and bundles, in order.</summary>
    public IReadOnlyList<OscPacket> Elements { get; }

    /// <summary>
    /// Enumerates every message in this bundle and its nested bundles, depth first in order,
    /// along with the time tag of the innermost bundle containing it.
    /// </summary>
    public IEnumerable<(OscMessage Message, OscTimeTag TimeTag)> Flatten()
    {
        foreach (var element in Elements)
        {
            switch (element)
            {
                case OscMessage message:
                    yield return (message, TimeTag);
                    break;
                case OscBundle bundle:
                    foreach (var item in bundle.Flatten())
                        yield return item;
                    break;
            }
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"#bundle {TimeTag} ({Elements.Count} elements)";

    internal override void Write(ref OscWriter writer)
    {
        writer.WriteRaw(BundleTag);
        writer.WriteUInt64(TimeTag.Value);
        foreach (var element in Elements)
        {
            var sizeSlot = writer.ReserveInt32();
            var start = writer.Written;
            element.Write(ref writer);
            writer.PatchInt32(sizeSlot, writer.Written - start);
        }
    }

    internal static OscBundle ParseBody(ReadOnlySpan<byte> data)
    {
        if (data.Length < 16 || !data[..8].SequenceEqual(BundleTag))
            throw new OscException("An OSC bundle must start with \"#bundle\" and a time tag.");

        var reader = new OscReader(data[8..]);
        var timeTag = new OscTimeTag(reader.ReadUInt64());
        var elements = new List<OscPacket>();
        while (!reader.IsAtEnd)
        {
            var size = reader.ReadInt32();
            if (size <= 0 || size % 4 != 0)
                throw new OscException($"Bundle element size must be a positive multiple of 4, but was {size}.");
            if (size > reader.Remaining)
                throw new OscException($"Bundle element size {size} exceeds the {reader.Remaining} bytes remaining.");
            elements.Add(Parse(reader.ReadRaw(size)));
        }
        return new OscBundle(timeTag, elements);
    }
}

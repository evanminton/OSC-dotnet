namespace Osc;

/// <summary>
/// An OSC server's address space: a set of OSC methods, each at a fixed address, that incoming
/// messages are dispatched to by address pattern matching.
/// </summary>
/// <remarks>
/// Dispatch runs handlers synchronously on the calling thread. Messages inside a bundle are
/// dispatched in order with the bundle's time tag; it is up to the handler (or a scheduler wrapped
/// around <see cref="Dispatch(OscPacket)"/>) to honour future time tags. Registration and dispatch
/// are thread-safe.
/// </remarks>
public sealed class OscAddressSpace
{
    /// <summary>Characters that may not appear in an OSC method's address: ' ', '#', ',', '/' (inside a part), and the pattern characters.</summary>
    public static ReadOnlySpan<char> ReservedCharacters => " #*,/?[]{}";

    private readonly Lock _lock = new();
    private readonly Dictionary<string, List<OscMethodHandler>> _methods = new(StringComparer.Ordinal);

    /// <summary>Addresses of all registered methods.</summary>
    public IReadOnlyCollection<string> Addresses
    {
        get
        {
            lock (_lock)
                return [.. _methods.Keys];
        }
    }

    /// <summary>Registers <paramref name="handler"/> at <paramref name="address"/>. Several handlers may share one address.</summary>
    /// <returns>A token that unregisters the handler when disposed.</returns>
    /// <exception cref="ArgumentException">The address is not a valid OSC method address.</exception>
    public IDisposable Register(string address, OscMethodHandler handler)
    {
        ValidateMethodAddress(address);
        ArgumentNullException.ThrowIfNull(handler);
        lock (_lock)
        {
            if (!_methods.TryGetValue(address, out var list))
                _methods[address] = list = [];
            list.Add(handler);
        }
        return new Registration(this, address, handler);
    }

    /// <summary>Registers a handler that does not need the time tag.</summary>
    public IDisposable Register(string address, Action<OscMessage> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Register(address, (message, _) => handler(message));
    }

    /// <summary>Returns the addresses of the registered methods that <paramref name="pattern"/> matches.</summary>
    public IReadOnlyList<string> Match(string pattern)
    {
        var compiled = new OscAddressPattern(pattern);
        lock (_lock)
            return [.. _methods.Keys.Where(compiled.IsMatch).Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Dispatches a packet: a message invokes every method its address pattern matches, and a bundle
    /// dispatches each of its elements in order.
    /// </summary>
    /// <returns>The number of handler invocations.</returns>
    public int Dispatch(OscPacket packet) => Dispatch(packet, OscTimeTag.Immediate);

    private int Dispatch(OscPacket packet, OscTimeTag timeTag)
    {
        ArgumentNullException.ThrowIfNull(packet);
        switch (packet)
        {
            case OscBundle bundle:
                var total = 0;
                foreach (var element in bundle.Elements)
                    total += Dispatch(element, bundle.TimeTag);
                return total;
            case OscMessage message:
                var handlers = GetHandlers(message.Address);
                foreach (var handler in handlers)
                    handler(message, timeTag);
                return handlers.Count;
            default:
                throw new ArgumentException($"Unknown packet type {packet.GetType()}.", nameof(packet));
        }
    }

    private List<OscMethodHandler> GetHandlers(string addressPattern)
    {
        var result = new List<OscMethodHandler>();
        lock (_lock)
        {
            if (!OscAddressPattern.ContainsWildcards(addressPattern))
            {
                if (_methods.TryGetValue(addressPattern, out var exact))
                    result.AddRange(exact);
                return result;
            }

            var pattern = new OscAddressPattern(addressPattern);
            foreach (var (address, handlers) in _methods.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (pattern.IsMatch(address))
                    result.AddRange(handlers);
            }
        }
        return result;
    }

    /// <summary>Throws if <paramref name="address"/> is not a valid OSC method address.</summary>
    public static void ValidateMethodAddress(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.Length < 2 || address[0] != '/')
            throw new ArgumentException("An OSC method address must start with '/' and name at least one part.", nameof(address));
        foreach (var part in address[1..].Split('/'))
        {
            if (part.Length == 0)
                throw new ArgumentException($"OSC address \"{address}\" has an empty part.", nameof(address));
            if (part.AsSpan().IndexOfAny(ReservedCharacters) >= 0 || part.Contains('\0'))
                throw new ArgumentException($"OSC address part \"{part}\" contains a reserved character.", nameof(address));
            foreach (var c in part)
            {
                if (c < 0x20 || c > 0x7E)
                    throw new ArgumentException($"OSC address part \"{part}\" must contain only printable ASCII characters.", nameof(address));
            }
        }
    }

    private void Unregister(string address, OscMethodHandler handler)
    {
        lock (_lock)
        {
            if (_methods.TryGetValue(address, out var list) && list.Remove(handler) && list.Count == 0)
                _methods.Remove(address);
        }
    }

    private sealed class Registration(OscAddressSpace owner, string address, OscMethodHandler handler) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.Unregister(address, handler);
        }
    }
}

/// <summary>An OSC method: invoked with the dispatched message and the time tag of its enclosing bundle (or <see cref="OscTimeTag.Immediate"/>).</summary>
public delegate void OscMethodHandler(OscMessage message, OscTimeTag timeTag);

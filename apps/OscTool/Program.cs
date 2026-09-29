using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Osc;

return args switch
{
    ["send", var host, var port, var address, .. var values] => await SendAsync(host, port, address, values),
    ["listen", var port] => await ListenAsync(port),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("""
        Usage:
          osctool send <host> <port> <address> [arg ...]   Send one OSC message over UDP.
          osctool listen <port>                            Print OSC packets received on a UDP port.

        Arguments are sent as int (42), float (0.5), true/false, nil, or string (anything else).
        Prefix a value with s: to force a string, e.g. s:42.
        """);
    return 2;
}

static async Task<int> SendAsync(string host, string portText, string address, string[] values)
{
    if (!TryParsePort(portText, out var port))
        return Usage();
    var message = new OscMessage(address, values.Select(ParseArgument));
    using var udp = new UdpClient();
    await udp.SendAsync(message.ToBytes(), host, port);
    Console.WriteLine($"-> {host}:{port} {message}");
    return 0;
}

static async Task<int> ListenAsync(string portText)
{
    if (!TryParsePort(portText, out var port))
        return Usage();
    using var cancel = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
    using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
    Console.WriteLine($"Listening on UDP {port}. Press Ctrl+C to stop.");
    try
    {
        while (true)
        {
            var result = await udp.ReceiveAsync(cancel.Token);
            Console.WriteLine(Describe(result.RemoteEndPoint, result.Buffer));
        }
    }
    catch (OperationCanceledException)
    {
        return 0;
    }
}

static string Describe(IPEndPoint from, byte[] data)
{
    var time = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
    if (!OscPacket.TryParse(data, out var packet))
        return $"{time} {from} (not an OSC packet, {data.Length} bytes)";
    return packet switch
    {
        OscBundle bundle => $"{time} {from} {bundle}\n" + string.Join("\n",
            bundle.Flatten().Select(e => $"    {e.Message}")),
        _ => $"{time} {from} {packet}",
    };
}

static bool TryParsePort(string text, out int port) =>
    int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is > 0 and <= 65535;

static object? ParseArgument(string text)
{
    if (text.StartsWith("s:", StringComparison.Ordinal))
        return text[2..];
    if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var i))
        return i;
    if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
        return f;
    return text switch
    {
        "true" => true,
        "false" => false,
        "nil" => null,
        _ => text,
    };
}

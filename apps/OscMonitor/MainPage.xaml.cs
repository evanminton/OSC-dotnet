using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Osc;

namespace OscMonitor;

public partial class MainPage : ContentPage
{
	private const int MaxLogLines = 2000;

	private readonly ObservableCollection<string> _log = [];
	private CancellationTokenSource? _listening;

	public MainPage()
	{
		InitializeComponent();
		Log.ItemsSource = _log;
	}

	private async void OnSendClicked(object? sender, EventArgs e)
	{
		try
		{
			if (!TryParsePort(SendPortEntry.Text, out var port))
				throw new FormatException("Send port must be a number from 1 to 65535.");
			var message = new OscMessage(AddressEntry.Text.Trim(), SplitArguments(ArgumentsEntry.Text).Select(ParseArgument));
			using var udp = new UdpClient();
			await udp.SendAsync(message.ToBytes(), HostEntry.Text.Trim(), port);
			Append($"-> {HostEntry.Text.Trim()}:{port} {message}");
		}
		catch (Exception ex)
		{
			Append($"Send failed: {ex.Message}");
		}
	}

	private void OnListenClicked(object? sender, EventArgs e)
	{
		if (_listening is not null)
		{
			_listening.Cancel();
			return;
		}
		if (!TryParsePort(ListenPortEntry.Text, out var port))
		{
			StatusLabel.Text = "Port must be a number from 1 to 65535.";
			return;
		}
		UdpClient udp;
		try
		{
			udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
		}
		catch (SocketException ex)
		{
			StatusLabel.Text = $"Can't listen on {port}: {ex.Message}";
			return;
		}
		_listening = new CancellationTokenSource();
		ListenButton.Text = "Stop";
		ListenPortEntry.IsEnabled = false;
		StatusLabel.Text = $"Listening on UDP {port}";
		_ = ReceiveAsync(udp, _listening.Token);
	}

	private async Task ReceiveAsync(UdpClient udp, CancellationToken cancel)
	{
		using (udp)
		{
			try
			{
				while (true)
				{
					var result = await udp.ReceiveAsync(cancel);
					Append(Describe(result.RemoteEndPoint, result.Buffer));
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (SocketException ex)
			{
				Append($"Receive failed: {ex.Message}");
			}
		}
		_listening?.Dispose();
		_listening = null;
		ListenButton.Text = "Start";
		ListenPortEntry.IsEnabled = true;
		StatusLabel.Text = "Stopped";
	}

	private void OnClearClicked(object? sender, EventArgs e) => _log.Clear();

	private void Append(string line)
	{
		foreach (var part in line.Split('\n'))
			_log.Add(part);
		while (_log.Count > MaxLogLines)
			_log.RemoveAt(0);
	}

	private static string Describe(IPEndPoint from, byte[] data)
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

	private static bool TryParsePort(string? text, out int port) =>
		int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is > 0 and <= 65535;

	/// <summary>Splits on spaces, keeping "double quoted" text together as one string argument.</summary>
	private static IEnumerable<string> SplitArguments(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
			yield break;
		var i = 0;
		while (i < text.Length)
		{
			if (char.IsWhiteSpace(text[i]))
			{
				i++;
			}
			else if (text[i] == '"')
			{
				var end = text.IndexOf('"', i + 1);
				if (end < 0)
					end = text.Length;
				yield return "s:" + text[(i + 1)..end];
				i = end + 1;
			}
			else
			{
				var end = i;
				while (end < text.Length && !char.IsWhiteSpace(text[end]))
					end++;
				yield return text[i..end];
				i = end;
			}
		}
	}

	/// <summary>Reads an argument as int, float, true/false, nil, or string; an "s:" prefix forces a string.</summary>
	private static object? ParseArgument(string text)
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
}

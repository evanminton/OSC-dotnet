# Osc

A .NET 10 class library implementing [Open Sound Control 1.0](https://opensoundcontrol.stanford.edu/spec-1_0.html) and the [OSC 1.1](https://opensoundcontrol.stanford.edu/files/2009-NIME-OSC-1.1.pdf) additions.

- **Messages and bundles**: `OscMessage`, `OscBundle`, encoded with `ToBytes()` / `WriteTo(IBufferWriter<byte>)` and decoded with `OscPacket.Parse` / `TryParse`.
- **Type tags**: all required types (`i f s b`) and the spec's non-standard types (`h t d S c r m T F N I [ ]`), mapped to plain .NET values.
- **Time tags**: `OscTimeTag` (NTP format) with `Immediate` and `DateTime` conversion.
- **Address patterns**: `OscAddressPattern` supports `?`, `*`, `[a-z]`, `[!…]` and `{foo,bar}`; wildcards never cross `/`.
- **OSC 1.1 `//` wildcard**: matches any number of address levels, so `//spherical` matches `/position/spherical`.
- **OSC 1.1 stream framing**: `OscSlip` / `OscSlipDecoder` implement SLIP (RFC 1055) with double END; `stream.WriteOscPacketAsync` / `ReadOscPacketsAsync` use SLIP by default and the 1.0 int32 size prefix via `OscFraming.LengthPrefixed`.
- **OSC 1.1 required types**: `T F N I t` join `i f s b`; `OscTypeTags.UsesOnly(tags, OscTypeTags.Osc11Required)` checks a message sticks to them.
- **Dispatch**: `OscAddressSpace` registers OSC methods and dispatches messages and bundles to every matching method.

## Usage

```csharp
using Osc;

// Encode
var bytes = new OscMessage("/oscillator/4/frequency", 440.0f).ToBytes();
var bundle = new OscBundle(OscTimeTag.FromDateTime(DateTime.UtcNow.AddMilliseconds(50)),
    new OscMessage("/synth/1/note", 60, 0.8f),
    new OscMessage("/synth/1/gate", true));

// Decode
var packet = OscPacket.Parse(bytes);
if (packet is OscMessage m)
    Console.WriteLine($"{m.Address} {m.Get<float>(0)}");

// Dispatch
var space = new OscAddressSpace();
space.Register("/synth/1/note", (msg, timeTag) => Console.WriteLine($"note {msg.Get<int>(0)} at {timeTag}"));
space.Dispatch(OscPacket.Parse(bundle.ToBytes()));
```

### Type mapping

| Tag | .NET type | Tag | .NET type |
|-----|-----------|-----|-----------|
| `i` | `int` | `S` | `OscSymbol` |
| `f` | `float` | `c` | `char` |
| `s` | `string` | `r` | `OscColor` |
| `b` | `byte[]` (or `ReadOnlyMemory<byte>` when encoding) | `m` | `OscMidi` |
| `h` | `long` | `T` / `F` | `bool` |
| `t` | `OscTimeTag` | `N` | `null` |
| `d` | `double` | `I` | `OscImpulse` |
| `[ ]` | `object?[]` (any `IList` when encoding) | | |

### Streams (TCP, serial)

```csharp
using var client = new System.Net.Sockets.TcpClient("localhost", 9000);
var stream = client.GetStream();
await stream.WriteOscPacketAsync(new OscMessage("/ping"));          // SLIP, per OSC 1.1
await foreach (var packet in stream.ReadOscPacketsAsync())
    space.Dispatch(packet);
```

## Notes on the spec

- Strings are written as UTF-8 (a superset of the spec's ASCII); method addresses registered in an `OscAddressSpace` must be printable ASCII.
- A received message with no type tag string and no argument data decodes as a message with no arguments, as the spec asks robust implementations to do. Unknown type tags are rejected with `OscException`.
- `OscAddressSpace.Dispatch` runs handlers synchronously and passes each message its bundle's time tag; scheduling future time tags is left to the caller. This matches OSC 1.1, which deliberately leaves time tag semantics unspecified beyond the format and the "immediately" value.
- `OscPacket` equality compares binary encodings.

## Build and test

```
dotnet test
```

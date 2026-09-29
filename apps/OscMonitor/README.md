# OSC Monitor

A small .NET MAUI app for Windows and macOS (Mac Catalyst) built on the Osc library. It sends OSC messages over UDP and shows the packets it receives on a UDP port, with bundles expanded into their messages.

Arguments are typed like the console tool: `60` is an int, `0.8` a float, `true`/`false` a bool, `nil` a null, and anything else a string. Wrap text in double quotes (or prefix `s:`) to force a string.

## Build a standalone Windows exe

Needs the .NET 10 SDK and the `maui-windows` workload (`dotnet workload install maui-windows`).

```
dotnet publish apps/OscMonitor -f net10.0-windows10.0.19041.0 -c Release -o artifacts/OscMonitor
```

The project is unpackaged (`WindowsPackageType=None`) and self-contained, including the Windows App SDK, so `artifacts/OscMonitor/OscMonitor.exe` runs on Windows 10 1809 or later without installing anything. Copy the whole folder, not just the exe.

## Build a standalone Mac app

Needs the .NET 10 SDK, the `maui-maccatalyst` workload (`dotnet workload install maui-maccatalyst`), and the Xcode version that workload expects.

```
dotnet publish apps/OscMonitor -f net10.0-maccatalyst -c Release -o artifacts/OscMonitor-mac
```

This builds a universal (Apple silicon and Intel) app with the .NET runtime inside it, ad-hoc signed, plus an installer `.pkg` in `artifacts/OscMonitor-mac`. The `.app` itself is at `apps/OscMonitor/bin/Release/net10.0-maccatalyst/osx-arm64/OSC Monitor.app`. The app is sandboxed with network client and server access, so it can send and listen on UDP. To run it on another Mac without a Gatekeeper warning it needs a Developer ID signature and notarization.

If your Xcode is newer than the workload supports, add `-p:ValidateXcodeVersion=false` to build anyway.

The app is not in `Osc.slnx`, so `dotnet test` at the repo root still works on machines without the MAUI workload.

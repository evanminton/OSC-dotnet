# OSC Monitor

A small .NET MAUI Windows app built on the Osc library. It sends OSC messages over UDP and shows the packets it receives on a UDP port, with bundles expanded into their messages.

Arguments are typed like the console tool: `60` is an int, `0.8` a float, `true`/`false` a bool, `nil` a null, and anything else a string. Wrap text in double quotes (or prefix `s:`) to force a string.

## Build a standalone exe

Needs the .NET 10 SDK and the `maui-windows` workload (`dotnet workload install maui-windows`).

```
dotnet publish apps/OscMonitor -c Release -o artifacts/OscMonitor
```

The project is unpackaged (`WindowsPackageType=None`) and self-contained, including the Windows App SDK, so `artifacts/OscMonitor/OscMonitor.exe` runs on Windows 10 1809 or later without installing anything. Copy the whole folder, not just the exe.

The app is not in `Osc.slnx`, so `dotnet test` at the repo root still works on machines without the MAUI workload.

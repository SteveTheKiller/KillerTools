# Architecture

## Product boundaries

`KillerTools.Engine` is a cross-platform .NET 10 library. It contains utility behavior and has no dependency on MCP, KillerUI, or operating system APIs.

The native KillerMCP host references the engine and adds protocol handling, app discovery, app adapters, update notifications, and client registration. The future optional KillerTools desktop app will reference the same engine and use KillerUI on Windows.

## Windows installation

The KillerMCP installer uses the KillerPDF bootstrap pattern. A .NET Framework 4.8 launcher opens without .NET 10, detects `Microsoft.NETCore.App` 10, shows prerequisite status, and directs the user to Microsoft's official runtime download when needed. Silent installation rejects a missing runtime before extraction. A future WinGet manifest must declare the .NET 10 runtime dependency.

The future KillerTools desktop app will require `Microsoft.WindowsDesktop.App` 10 because KillerUI uses WPF. Its default theme remains undecided until the interface can be rendered and compared.

The current Windows, Linux, and macOS packages are framework dependent and require the matching .NET 10 runtime. A future self-contained package would not change the engine API.

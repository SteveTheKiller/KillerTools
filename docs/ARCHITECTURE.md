# Architecture

## Product boundaries

`KillerTools.Engine` is a cross-platform .NET 10 library. It contains utility behavior and has no dependency on MCP, KillerUI, or operating system APIs.

The native KillerMCP host will reference the engine and add protocol handling, app discovery, app adapters, updates, and client registration. The future optional KillerTools desktop app will reference the same engine and use KillerUI on Windows.

## Windows installation

The KillerMCP installer will use the KillerPDF bootstrap pattern. A .NET Framework 4.8 launcher opens without .NET 10, detects `Microsoft.NETCore.App` 10, shows prerequisite status, and directs the user to Microsoft's official runtime download when needed. Silent installation must reject a missing runtime before extraction. WinGet must declare the .NET 10 runtime dependency.

The future KillerTools desktop app will require `Microsoft.WindowsDesktop.App` 10 because KillerUI uses WPF. Its default theme remains undecided until the interface can be rendered and compared.

Linux and macOS packages may declare the .NET 10 runtime as a dependency or use verified self-contained builds. That packaging choice does not change the engine API.

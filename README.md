# KillerTools and KillerMCP

This repository contains the cross-platform .NET 10 KillerTools engine and the native KillerMCP host. KillerMCP gives compatible AI assistants one local connection to all 81 KillerTools utilities and the supported commands exposed by installed Killer apps.

The engine contains platform-neutral utility behavior and does not depend on MCP, a desktop interface, or Windows APIs. The future KillerTools desktop app will use the same engine with KillerUI.

## Install KillerMCP on Windows

Download `KillerMCP-Setup.exe` from the latest GitHub release and run it. Setup:

- Checks for the .NET 10 runtime and opens Microsoft's runtime download when it is missing.
- Installs the native KillerMCP host for the current Windows user.
- Detects supported Killer apps already installed on the computer.
- Registers one `killermcp` connection with Codex, Claude Code, Claude Desktop, Cursor, GitHub Copilot, Gemini CLI, and Windsurf when those clients are found.
- Upgrades the legacy 0.1.x Node based host and supports reinstall and uninstall from the same setup program.

The Windows package requires the .NET 10 runtime. Node and a source checkout are not required.

## Other platforms

Framework dependent packages are built for Windows x64, Linux x64, and macOS arm64. They require the matching .NET 10 runtime. The Windows installer is the supported guided setup experience for 0.2.0.

## Current status

KillerMCP 0.2.0 uses the native engine for the complete KillerTools website catalog. Its runtime also discovers supported installed Killer apps and exposes only the commands advertised by those app versions.

## Build and check

```powershell
pwsh -NoProfile -File scripts\Test-All.ps1
```

The check builds the solution, runs native engine and MCP checks, imports the current implementations from `killer-tools-site`, and compares their output with the native engine. The migration inventory reads the website's authoritative MCP coverage map and reports native operation coverage without maintaining a second list of website tools.

Run the Windows installer test separately on Windows:

```powershell
pwsh -NoProfile -File scripts\Test-WindowsInstaller.ps1
```

That test covers the .NET prerequisite, legacy migration, install, active-host reinstall, client registration, MCP discovery and tool calls, tamper protection, and uninstall.

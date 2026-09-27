# KillerTools

This repository contains the cross-platform .NET 10 engine for the KillerTools utility collection.

The engine contains platform-neutral utility behavior and does not depend on MCP, a desktop interface, or Windows APIs. KillerMCP and the future KillerTools desktop app consume the versioned engine from their own repositories.

## Current status

The native engine covers the complete KillerTools website catalog. Every ported operation is checked against the existing JavaScript implementation.

## Build and check

```powershell
pwsh -NoProfile -File scripts\Test-All.ps1
```

The check builds the engine, runs its native checks, imports the current implementations from `killer-tools-site`, and compares their output with the native engine. The migration inventory reads the website's authoritative coverage map and reports native operation coverage without maintaining a second list.

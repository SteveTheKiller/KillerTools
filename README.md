# KillerTools

KillerTools is the cross-platform .NET 10 engine for the KillerTools utility collection.

The engine contains platform-neutral utility behavior. It does not depend on a desktop interface, MCP, or Windows APIs. KillerMCP and the future KillerTools desktop app will consume the same versioned engine.

## Current status

The native migration is in progress. Each ported operation must match the existing JavaScript implementation before it can replace that implementation.

## Build and check

```powershell
pwsh -NoProfile -File scripts\Test-All.ps1
```

The check builds the solution, runs native checks, imports the current implementations from `killer-tools-site`, and compares their output with the native engine. The migration inventory reads the website's authoritative MCP coverage map and reports native operation coverage without maintaining a second list of website tools.

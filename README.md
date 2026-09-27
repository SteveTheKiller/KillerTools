# KillerTools

KillerTools is the cross-platform .NET 10 engine for the KillerTools utility collection.

The engine contains platform-neutral utility behavior. It does not depend on a desktop interface, MCP, or Windows APIs. KillerMCP and the future KillerTools desktop app will consume the same versioned engine.

## Current status

The native migration is in progress. Each ported operation must match the existing JavaScript implementation before it can replace that implementation.

## Build and check

```powershell
dotnet build KillerTools.slnx
dotnet run --project tests\KillerTools.Engine.Checks\KillerTools.Engine.Checks.csproj
```


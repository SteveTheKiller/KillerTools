# Changelog

## Unreleased

### Added

- Added the native .NET 10 KillerMCP host with all 81 KillerTools utilities and supported installed-app commands.
- Added framework-dependent KillerMCP packages for Windows x64, Linux x64, and macOS arm64.
- Added a KillerUI-styled Windows installer with .NET 10 detection and automatic registration for seven agent clients.
- Added verified legacy migration, reinstall, upgrade, uninstall, tamper protection, and active-host replacement.
- Added native engine, MCP, package, client registration, JavaScript parity, and migration inventory checks.

### Changed

- Replaced the bundled Node runtime with the smaller native .NET host.
- Moved shared KillerTools operations into a cross-platform engine for KillerMCP and the future desktop app.

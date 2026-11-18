# KillerTools Changelog

## 0.2.1 - Unreleased

### Security

- Use authenticated encryption for new ciphertext, generate 160-bit OTP secrets, bound bcrypt verification, and keep PDF certificate checks offline.

- Bound JSON, YAML and XML processing, reject entity expansion, and limit PDF signature inspection.
- QR link checks flag more executable download types, such as .hta, .lnk and .appinstaller, and shorteners reached through a subdomain such as www.bit.ly.

### Fixed

- Report PDF changes after the last signature and distinguish whole-document coverage.
- Updated System.Security.Cryptography.Pkcs to 10.0.13 for the GHSA-846f-f764-76mx and GHSA-2fmv-rv2c-59x7 advisories.
- Removed the ASP.NET Core runtime requirement from user agent parsing.
- QR reading can be cancelled and stops after two seconds on an image it cannot read, instead of running unbounded.

### Changed

- License first-party C# code under MPL 2.0 and retain third-party notices.
- Support standard BIP39 recovery phrase sizes and correct HTML metadata, ObjectId dates, and point-to-point subnet counts.

- Updated the MAC vendor catalog to the current IEEE registry.

### Added

- Expanded firewall port coverage for common MSP platforms, including Fortinet, UniFi, and Calyptix.
- Added Slovenian reference translations while preserving technical identifiers and command values.
- Added PHP2Shell, wp2shell, BlockStrap, and Comment2Shell to the CVE street names and named exploit chains.

## 0.2.0 - 2026-09-27

### Added

- Added the cross-platform .NET 10 engine for the complete KillerTools website catalog.
- Added native engine checks, JavaScript parity checks, and migration inventory checks.

### Changed

- Moved shared KillerTools operations into a versioned engine for KillerMCP and the future desktop app.

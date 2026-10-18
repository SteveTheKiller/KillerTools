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

## Local browser QR reader

`src/KillerTools.Qr.Browser` compiles the same QR reader, link checks and Wi-Fi parser to WebAssembly. Its JavaScript export accepts a base64 grayscale buffer, width and height, then returns decoded results as JSON. It performs no network requests. Images stay in the caller's browser.

`QrLink.Check` scans QR and OCR web links locally. It flags embedded web destinations, nested URL escaping, download filenames in query values, executable extensions disguised by another extension, and the existing address warning signs. Invisible formatting and control characters block opening. Findings explain suspicious structure; no findings do not establish that a website is safe. The scanner never visits destinations, follows redirects, downloads files, or submits content to a reputation service.

Build the runtime and run the same QRCoder corpus under WebAssembly with `powershell -NoProfile -File scripts\Build-QrBrowser.ps1 -ScratchRoot C:\Users\steve\scratch\qr-browser -Checks`. This uses the .NET 10 WebAssembly SDK and Node.js. The shared checks and browser interop must pass before exporting assets.

Omit `-Checks` and add `-Destination C:\Users\steve\killer-tools-site\public\qr-reader-runtime` to export the production runtime. The script hashes every copied asset, uses a content-addressed bundle directory, and writes a manifest for the website's lazy loader. It refuses to overwrite altered bundle assets or a concurrently changed manifest. Production assets do not contain QRCoder or the test corpus.

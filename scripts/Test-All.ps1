param(
    [string]$WebsiteRepository = (Join-Path $PSScriptRoot '..\..\..\killer-tools-site')
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$site = (Resolve-Path $WebsiteRepository).Path
$tsx = if ($env:OS -eq 'Windows_NT') {
    Join-Path $site 'node_modules\.bin\tsx.cmd'
} else {
    Join-Path $site 'node_modules\.bin\tsx'
}

if (-not (Test-Path -LiteralPath $tsx -PathType Leaf)) {
    throw "The KillerTools website dependencies are not installed: $tsx"
}

Push-Location $root
try {
    dotnet build KillerTools.slnx
    if ($LASTEXITCODE -ne 0) { throw 'KillerTools build failed.' }

    dotnet run --project tests\KillerTools.Engine.Checks\KillerTools.Engine.Checks.csproj --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Native engine checks failed.' }

    dotnet run --project tests\KillerTools.Qr.Checks\KillerTools.Qr.Checks.csproj --no-build
    if ($LASTEXITCODE -ne 0) { throw 'QR reader checks failed.' }

    Push-Location $site
    try {
        pnpm -C mcp build:local
        if ($LASTEXITCODE -ne 0) { throw 'JavaScript parity oracle build failed.' }
    }
    finally {
        Pop-Location
    }

    & $tsx scripts\Test-JavaScriptParity.mjs $site
    if ($LASTEXITCODE -ne 0) { throw 'JavaScript parity checks failed.' }

    node scripts\Test-MigrationInventory.mjs $site
    if ($LASTEXITCODE -ne 0) { throw 'Migration inventory check failed.' }
}
finally {
    Pop-Location
}

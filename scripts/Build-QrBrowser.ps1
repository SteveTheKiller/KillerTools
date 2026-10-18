[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ScratchRoot,
    [string]$Destination,
    [switch]$Checks
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$projectName = if ($Checks) { 'KillerTools.Qr.Browser.Checks' } else { 'KillerTools.Qr.Browser' }
$project = if ($Checks) { Join-Path $repo "tests\$projectName\$projectName.csproj" } else { Join-Path $repo "src\$projectName\$projectName.csproj" }
$artifacts = Join-Path ([IO.Path]::GetFullPath($ScratchRoot)) 'qr-browser-build'
& dotnet publish $project -c Release --artifacts-path $artifacts --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Browser QR publish failed.' }
$publish = (& dotnet msbuild $project -nologo -getProperty:PublishDir -p:Configuration=Release "-p:ArtifactsPath=$artifacts" -p:UseArtifactsOutput=true | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot locate the browser publish output.' }
$web = Join-Path $publish 'wwwroot'
if (-not (Test-Path -LiteralPath (Join-Path $web '_framework\dotnet.js'))) { throw "Missing browser runtime under $web" }
if ($Checks) {
    & node (Join-Path $repo 'tests\KillerTools.Qr.Browser.Checks\run.mjs') $web
    if ($LASTEXITCODE -ne 0) { throw 'The browser runtime failed the shared corpus.' }
}
if (-not $Destination) { Write-Output $web; return }
if ($Checks) { throw 'The test corpus cannot be exported as a production reader.' }

$destinationPath = [IO.Path]::GetFullPath($Destination)
$files = @(Get-ChildItem -LiteralPath (Join-Path $web '_framework') -File | Where-Object { $_.Extension -notin '.gz', '.br', '.map', '.pdb' } | Sort-Object Name)
$hashes = [ordered]@{}
foreach ($file in $files) { $hashes[$file.Name] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
$hashInput = ($hashes.GetEnumerator() | ForEach-Object { $_.Key + ':' + $_.Value }) -join "`n"
$sha = [Security.Cryptography.SHA256]::Create()
try { $bundle = -join ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($hashInput)) | ForEach-Object { $_.ToString('x2') }) }
finally { $sha.Dispose() }
$manifestPath = Join-Path $destinationPath 'manifest.json'
$previous = if (Test-Path -LiteralPath $manifestPath) { (Get-Item -LiteralPath $manifestPath).LastWriteTimeUtc.Ticks } else { $null }
$framework = Join-Path $destinationPath "$bundle\_framework"
New-Item -ItemType Directory -Path $framework -Force | Out-Null
foreach ($file in $files) {
    $target = Join-Path $framework $file.Name
    if (Test-Path -LiteralPath $target) {
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hashes[$file.Name]) { throw "Refusing to overwrite changed asset: $target" }
    }
    else { Copy-Item -LiteralPath $file.FullName -Destination $target }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hashes[$file.Name]) { throw "Asset copy failed: $target" }
}
$current = if (Test-Path -LiteralPath $manifestPath) { (Get-Item -LiteralPath $manifestPath).LastWriteTimeUtc.Ticks } else { $null }
if ($current -ne $previous) { throw 'The browser manifest changed during export.' }
$manifest = [ordered]@{ bundle = $bundle; files = $hashes } | ConvertTo-Json -Depth 4
$temporary = Join-Path $destinationPath ('manifest.' + [guid]::NewGuid().ToString('N') + '.tmp')
[IO.File]::WriteAllText($temporary, $manifest + "`n", (New-Object Text.UTF8Encoding($false)))
if (Test-Path -LiteralPath $manifestPath) { [IO.File]::Replace($temporary, $manifestPath, $null) }
else { [IO.File]::Move($temporary, $manifestPath) }
Write-Output "Exported $($files.Count) verified browser QR assets: $bundle"

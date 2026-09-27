$ErrorActionPreference = 'Stop'
$architecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
$platform = if ($IsWindows) { 'win' } elseif ($IsLinux) { 'linux' } elseif ($IsMacOS) { 'osx' } else { throw 'This operating system is not supported.' }
$runtimeIdentifier = "$platform-$architecture"

& (Join-Path $PSScriptRoot 'Build-NativePackage.ps1') -RuntimeIdentifier $runtimeIdentifier
if ($LASTEXITCODE -ne 0) { throw 'Native KillerMCP package verification failed.' }

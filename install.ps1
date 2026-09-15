# Publish K7 Mix into C:\TKHN\K7Mix. Settings stay under
# %LOCALAPPDATA%\K7Mix; mixes it authors stay wherever they already live.

[CmdletBinding()]
param(
    [string]$InstallRoot
)

$ErrorActionPreference = 'Stop'
$repo = $PSScriptRoot
$sync = 'L:\Sync'
if (-not (Test-Path -LiteralPath (Join-Path $sync 'CompanionFamily.ps1'))) {
    $sync = Join-Path $repo '..\..\..\..\Sync'
}

. (Join-Path $sync 'CompanionFamily.ps1')

if ($InstallRoot) {
    Write-Warning "InstallRoot override is no longer the standard. Publishing via the TKHN family."
}

$null = Publish-CompanionApp `
    -App K7Mix `
    -Project (Join-Path $repo 'src\K7Mix\K7Mix.csproj') `
    -RepoRoot $repo `
    -SelfContained

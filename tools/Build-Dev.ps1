param([ValidateSet('Release')][string] $Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$scratch = Join-Path $root '.agents-temp\build-dev'
try {
    & (Join-Path $PSScriptRoot 'Clean-DevOutput.ps1')
    & (Join-Path $PSScriptRoot 'Test.ps1') -Configuration $Configuration -ScratchDirectory (Join-Path $scratch 'tests')
    & (Join-Path $PSScriptRoot 'Clean-DevOutput.ps1') -PreserveMainRelease
    $exe = Join-Path $root 'bin\Release\net10.0-windows\MiniMax.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Developer executable missing: $exe" }
    $unexpected = @(Get-ChildItem -LiteralPath (Join-Path $root 'bin\Release') -Directory |
        Where-Object Name -ne 'net10.0-windows')
    if ($unexpected.Count -gt 0) { throw "Unexpected developer output: $($unexpected.FullName -join ', ')" }
    [pscustomobject]@{ DeveloperExecutable=$exe; FileVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion } | ConvertTo-Json
} finally {
    if (Test-Path -LiteralPath $scratch) { Remove-Item -LiteralPath $scratch -Recurse -Force }
}

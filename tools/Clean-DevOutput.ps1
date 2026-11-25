param([switch] $PreserveMainRelease)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$mainBin = Join-Path $root 'bin'
foreach ($process in Get-Process -Name MiniMax -ErrorAction SilentlyContinue) {
    try {
        if ($process.Path -and $process.Path.StartsWith($mainBin + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Close the developer MiniMax process before cleaning: PID $($process.Id)"
        }
    } finally { $process.Dispose() }
}
$paths = if ($PreserveMainRelease) {
    @('obj','Tests\bin','Tests\obj')
} else {
    @('bin','obj','Tests\bin','Tests\obj')
}
foreach ($relative in $paths) {
    $path = Join-Path $root $relative
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
}
if ($PreserveMainRelease) {
    $releaseRoot = Join-Path $root 'bin\Release'
    if (Test-Path -LiteralPath $releaseRoot) {
        foreach ($directory in Get-ChildItem -LiteralPath $releaseRoot -Directory) {
            if ($directory.Name -ne 'net10.0-windows') { Remove-Item -LiteralPath $directory.FullName -Recurse -Force }
        }
        $rid = Join-Path $releaseRoot 'net10.0-windows\win-x64'
        if (Test-Path -LiteralPath $rid) { Remove-Item -LiteralPath $rid -Recurse -Force }
    }
}

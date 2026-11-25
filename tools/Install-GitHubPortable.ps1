param(
    [string] $Tag = 'latest',
    [string] $Repository = 'yuzhayo/minimax',
    [string] $PortableDirectory = (Join-Path $PSScriptRoot '..\Portable')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$portable = [System.IO.Path]::GetFullPath($PortableDirectory)
if ($portable -eq $root -or -not $portable.StartsWith($root.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Portable directory must stay inside the repository: $portable"
}
foreach ($process in Get-Process -Name MiniMax -ErrorAction SilentlyContinue) {
    try { throw "Close MiniMax before installing Portable: PID $($process.Id)" } finally { $process.Dispose() }
}
$work = Join-Path $root 'artifacts\install'
$download = Join-Path $work 'download'
$stage = Join-Path $work 'stage'
$backup = Join-Path $work 'backup'
if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
New-Item -ItemType Directory -Path $download -Force | Out-Null
try {
    $headers = @{ 'User-Agent'='MiniMax-Portable-Installer'; 'Accept'='application/vnd.github+json' }
    $releaseUri = if ($Tag -eq 'latest') { "https://api.github.com/repos/$Repository/releases/latest" } else {
        "https://api.github.com/repos/$Repository/releases/tags/$Tag"
    }
    $release = Invoke-RestMethod -Uri $releaseUri -Headers $headers
    if ($release.draft -or $release.prerelease) { throw "Release is not a stable published release: $($release.tag_name)" }
    $version = ([string]$release.tag_name).TrimStart('v')
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid release version: $version" }
    $zipName = "MiniMax-v$version-win-x64-portable.zip"
    $zipAsset = @($release.assets | Where-Object name -eq $zipName)
    $shaAsset = @($release.assets | Where-Object name -eq "$zipName.sha256")
    if ($zipAsset.Count -ne 1 -or $shaAsset.Count -ne 1) { throw 'Release ZIP/SHA assets are missing or ambiguous.' }
    $zip = Join-Path $download $zipName
    $sha = "$zip.sha256"
    Invoke-WebRequest -Uri $zipAsset[0].browser_download_url -Headers $headers -OutFile $zip
    Invoke-WebRequest -Uri $shaAsset[0].browser_download_url -Headers $headers -OutFile $sha
    $actual = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    $declared = (Get-Content -LiteralPath $sha -Raw).Trim().Split(' ')[0].ToLowerInvariant()
    if ($actual -ne $declared) { throw "Portable SHA-256 mismatch: $actual vs $declared" }
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $stagePrefix = [System.IO.Path]::GetFullPath($stage).TrimEnd('\') + '\'
        foreach ($entry in $archive.Entries) {
            $destination = [System.IO.Path]::GetFullPath((Join-Path $stage $entry.FullName))
            if (-not $destination.StartsWith($stagePrefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe ZIP path: $($entry.FullName)" }
        }
    } finally { $archive.Dispose() }
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zip,$stage)
    $manifestPath = Join-Path $stage 'release.json'
    $exe = Join-Path $stage 'MiniMax.exe'
    if (-not (Test-Path $manifestPath) -or -not (Test-Path $exe)) { throw 'Extracted release is incomplete.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.version -ne $version -or $manifest.distribution -ne 'portable-self-contained') { throw 'Extracted release manifest mismatch.' }
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion -ne "$version.0") { throw 'Extracted executable version mismatch.' }
    if (Test-Path -LiteralPath $portable) { Move-Item -LiteralPath $portable -Destination $backup }
    try { Move-Item -LiteralPath $stage -Destination $portable }
    catch { if (Test-Path $backup) { Move-Item $backup $portable }; throw }
    if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup -Recurse -Force }
    [pscustomobject]@{ Tag=$release.tag_name; Version=$version; PortableDirectory=$portable; Sha256=$actual } | ConvertTo-Json
} finally {
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}

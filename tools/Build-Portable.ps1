param(
    [string] $Version = (& (Join-Path $PSScriptRoot 'Get-ProjectVersion.ps1')),
    [ValidateSet('win-x64')][string] $Runtime = 'win-x64',
    [string] $PublishDirectory = (Join-Path $PSScriptRoot '..\artifacts\publish\win-x64'),
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\releases')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must use three-part semver, for example 1.0.0.' }
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$prefix = $root.TrimEnd('\') + '\'
function Resolve-RepositoryOutput([string] $Path) {
    $resolved = [System.IO.Path]::GetFullPath($Path)
    if ($resolved -eq $root -or -not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Output must be a repository subfolder: $resolved"
    }
    $resolved
}
$publish = Resolve-RepositoryOutput $PublishDirectory
$output = Resolve-RepositoryOutput $OutputDirectory
if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
New-Item -ItemType Directory -Path $publish -Force | Out-Null
New-Item -ItemType Directory -Path $output -Force | Out-Null
& dotnet publish (Join-Path $root 'MiniMax.csproj') --configuration Release --runtime $Runtime `
    --self-contained true --output $publish --nologo "-p:MiniMaxVersion=$Version" `
    '-p:PublishSingleFile=false' '-p:PublishTrimmed=false' '-p:DebugType=None' '-p:DebugSymbols=false'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$exe = Join-Path $publish 'MiniMax.exe'
foreach ($required in @($exe,(Join-Path $publish 'MiniMax.dll'))) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Portable payload is missing: $required" }
}
if (@(Get-ChildItem -LiteralPath $publish -Recurse -Filter 'e_sqlite3.dll' -File).Count -eq 0) {
    throw 'Portable payload is missing native e_sqlite3.dll.'
}
$versionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
if ($versionInfo.FileVersion -ne "$Version.0" -or -not $versionInfo.ProductVersion.StartsWith($Version, [StringComparison]::Ordinal)) {
    throw "Published version mismatch. File=$($versionInfo.FileVersion), Product=$($versionInfo.ProductVersion), Expected=$Version"
}
$manifest = [ordered]@{ product='MiniMax'; version=$Version; runtime=$Runtime; distribution='portable-self-contained'; mainExecutable='MiniMax.exe' } | ConvertTo-Json
[System.IO.File]::WriteAllText((Join-Path $publish 'release.json'),$manifest,[System.Text.UTF8Encoding]::new($false))
$simulation = Start-Process -FilePath $exe -ArgumentList '--simulate' -NoNewWindow -Wait -PassThru
if ($simulation.ExitCode -ne 0) { throw "Published portable simulation failed with exit code $($simulation.ExitCode)." }
$zipName = "MiniMax-v$Version-$Runtime-portable.zip"
$zip = Join-Path $output $zipName
$shaFile = Join-Path $output "$zipName.sha256"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
if (Test-Path -LiteralPath $shaFile) { Remove-Item -LiteralPath $shaFile -Force }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText($shaFile,"$hash  $zipName`n",[System.Text.UTF8Encoding]::new($false))
$result = [pscustomobject]@{ Version=$Version; Runtime=$Runtime; PortableZip=$zip; Sha256File=$shaFile; Sha256=$hash; FileVersion=$versionInfo.FileVersion; ProductVersion=$versionInfo.ProductVersion }
if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
$publishParent = Split-Path $publish
if ((Test-Path -LiteralPath $publishParent) -and @(Get-ChildItem -LiteralPath $publishParent -Force).Count -eq 0) {
    Remove-Item -LiteralPath $publishParent
}
& (Join-Path $PSScriptRoot 'Clean-DevOutput.ps1') -PreserveMainRelease
$result | ConvertTo-Json

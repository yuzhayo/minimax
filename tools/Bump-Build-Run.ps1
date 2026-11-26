param(
    [string] $Version,
    [switch] $NoRun,
    [switch] $PlanOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$versionFile = Join-Path $root 'version.props'
$buildScript = Join-Path $PSScriptRoot 'Build-Dev.ps1'
$developerExe = Join-Path $root 'bin\Release\net10.0-windows\MiniMax.exe'
Set-Location -LiteralPath $root

function Convert-SemVer([string] $Value) {
    if ($Value -notmatch '^\d+\.\d+\.\d+$') { throw "Version must use three-part semver: $Value" }
    [Version]::Parse($Value)
}

function Get-NextDevelopmentVersion {
    $highest = Convert-SemVer (& (Join-Path $PSScriptRoot 'Get-ProjectVersion.ps1'))
    if (Get-Command git -ErrorAction SilentlyContinue) {
        foreach ($tag in @(git tag --list 'v*' 2>$null)) {
            $parsed = [Version]::new()
            if ([Version]::TryParse($tag.TrimStart('v'), [ref]$parsed) -and
                $parsed.Revision -le 0 -and $parsed -gt $highest) { $highest = $parsed }
        }
    }
    [Version]::new($highest.Major,$highest.Minor,$highest.Build + 1)
}

function Set-ProjectVersion([string] $Target) {
    $content = [System.IO.File]::ReadAllText($versionFile)
    $match = [regex]::Match($content,'(<MiniMaxVersion\b[^>]*>)(\d+\.\d+\.\d+)(</MiniMaxVersion>)')
    if (-not $match.Success) { throw 'MiniMaxVersion was not found in version.props.' }
    $updated = $content.Remove($match.Groups[2].Index,$match.Groups[2].Length).Insert($match.Groups[2].Index,$Target)
    if ($updated -ne $content) {
        [System.IO.File]::WriteAllText($versionFile,$updated,[System.Text.UTF8Encoding]::new($false))
    }
}

function Close-DeveloperMiniMax {
    foreach ($process in Get-Process -Name MiniMax -ErrorAction SilentlyContinue) {
        try {
            if (-not [string]::Equals($process.Path,$developerExe,[StringComparison]::OrdinalIgnoreCase)) { continue }
            if (-not $process.CloseMainWindow()) { throw "Developer MiniMax PID $($process.Id) did not accept a graceful close request." }
            if (-not $process.WaitForExit(10000)) { throw "Developer MiniMax PID $($process.Id) did not close within 10 seconds." }
        } finally { $process.Dispose() }
    }
}

$target = if ([string]::IsNullOrWhiteSpace($Version)) { Get-NextDevelopmentVersion } else { Convert-SemVer $Version }
$targetText = $target.ToString(3)
if ($PlanOnly) {
    [pscustomobject]@{ Mode='development'; Version=$targetText; Build=$true; SaveVersion=$true; Run=(-not $NoRun) }
    return
}

Close-DeveloperMiniMax
$hadVersionOverride = Test-Path Env:MiniMaxVersion
$previousVersionOverride = $env:MiniMaxVersion
try {
    $env:MiniMaxVersion = $targetText
    & $buildScript
    if ($LASTEXITCODE -ne 0) { throw "Developer build failed with exit code $LASTEXITCODE." }
} finally {
    if ($hadVersionOverride) { $env:MiniMaxVersion = $previousVersionOverride }
    else { Remove-Item Env:MiniMaxVersion -ErrorAction SilentlyContinue }
}

if (-not (Test-Path -LiteralPath $developerExe -PathType Leaf)) { throw "Developer executable is missing: $developerExe" }
$fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($developerExe).FileVersion
if ($fileVersion -ne "$targetText.0") { throw "Developer executable version is $fileVersion; expected $targetText.0." }
Set-ProjectVersion $targetText

$processId = $null
if (-not $NoRun) {
    $application = Start-Process -FilePath $developerExe -WorkingDirectory (Split-Path $developerExe) -PassThru
    $null = $application.WaitForInputIdle(10000)
    Start-Sleep -Milliseconds 500
    $application.Refresh()
    if ($application.HasExited) { throw "Developer MiniMax exited with code $($application.ExitCode)." }
    $processId = $application.Id
}

[pscustomobject]@{ Mode='development'; Version=$targetText; FileVersion=$fileVersion; Executable=$developerExe; ProcessId=$processId }

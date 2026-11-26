param(
    [ValidateSet('patch','minor','major')][string] $Bump = 'patch',
    [string] $Message,
    [switch] $PlanOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$prepareScript = Join-Path $PSScriptRoot 'Bump-Build-Run.ps1'
$scratchRoot = Join-Path $root '.agents-temp\production-release-verification'
Set-Location -LiteralPath $root

function Invoke-Native([string] $FileName, [string[]] $Arguments) {
    & $FileName @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$FileName failed with exit code $LASTEXITCODE." }
}

function Wait-ForWorkflowRun([string] $Workflow,[string] $Commit,[string] $Event,[DateTime] $NotBeforeUtc) {
    $deadline = [DateTime]::UtcNow.AddMinutes(2)
    do {
        $json = gh run list --repo yuzhayo/minimax --workflow $Workflow --branch main --commit $Commit `
            --event $Event --limit 10 --json databaseId,createdAt,headSha,status,conclusion
        if ($LASTEXITCODE -ne 0) { throw "Could not read workflow $Workflow." }
        $run = @($json | ConvertFrom-Json) | Where-Object {
            $_.headSha -eq $Commit -and ([DateTime]$_.createdAt).ToUniversalTime() -ge $NotBeforeUtc.AddSeconds(-5)
        } | Sort-Object { [DateTime]$_.createdAt } -Descending | Select-Object -First 1
        if ($run) { return $run }
        Start-Sleep -Seconds 3
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Workflow $Workflow for commit $Commit did not appear within two minutes."
}

function Verify-PublishedRelease([string] $Version,[string] $Commit) {
    if (Test-Path -LiteralPath $scratchRoot) { Remove-Item -LiteralPath $scratchRoot -Recurse -Force }
    New-Item -ItemType Directory -Path $scratchRoot -Force | Out-Null
    try {
        $release = gh release view "v$Version" --repo yuzhayo/minimax --json tagName,targetCommitish,url,isDraft,isPrerelease | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or $release.tagName -ne "v$Version" -or $release.targetCommitish -ne $Commit -or
            $release.isDraft -or $release.isPrerelease) { throw "Published release v$Version does not match commit $Commit." }
        Invoke-Native gh @('release','download',"v$Version",'--repo','yuzhayo/minimax','--dir',$scratchRoot,
            '--pattern',"MiniMax-v$Version-win-x64-portable.zip*",'--clobber')
        $zip = Join-Path $scratchRoot "MiniMax-v$Version-win-x64-portable.zip"
        $sha = "$zip.sha256"
        $actual = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
        $declared = (Get-Content -LiteralPath $sha -Raw).Trim().Split(' ')[0].ToLowerInvariant()
        if ($actual -ne $declared) { throw "Published SHA-256 mismatch: $actual vs $declared" }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $stage = Join-Path $scratchRoot 'stage'
        New-Item -ItemType Directory -Path $stage -Force | Out-Null
        $archive = [IO.Compression.ZipFile]::OpenRead($zip)
        try {
            $prefix = [IO.Path]::GetFullPath($stage).TrimEnd('\') + '\'
            foreach ($entry in $archive.Entries) {
                $destination = [IO.Path]::GetFullPath((Join-Path $stage $entry.FullName))
                if (-not $destination.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe ZIP path: $($entry.FullName)" }
            }
        } finally { $archive.Dispose() }
        [IO.Compression.ZipFile]::ExtractToDirectory($zip,$stage)
        $manifest = Get-Content -LiteralPath (Join-Path $stage 'release.json') -Raw | ConvertFrom-Json
        $exe = Join-Path $stage 'MiniMax.exe'
        if ($manifest.version -ne $Version -or [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion -ne "$Version.0") {
            throw 'Published manifest or executable version is incorrect.'
        }
        $simulation = Start-Process -FilePath $exe -ArgumentList '--simulate' -WorkingDirectory $stage -NoNewWindow -Wait -PassThru
        if ($simulation.ExitCode -ne 0) { throw "Published simulation failed with exit code $($simulation.ExitCode)." }
        [pscustomobject]@{ Url=$release.url; Sha256=$actual }
    } finally {
        if (Test-Path -LiteralPath $scratchRoot) { Remove-Item -LiteralPath $scratchRoot -Recurse -Force }
    }
}

$targetVersion = (& (Join-Path $PSScriptRoot 'Get-ReleaseVersion.ps1') -Bump $Bump).Trim()
if ($targetVersion -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid release target: $targetVersion" }
if ($PlanOnly) {
    [pscustomobject]@{ Mode='production'; Version=$targetVersion; Bump=$Bump; Commit=$true; Push=$true; Release=$true }
    return
}

if ((git branch --show-current).Trim() -ne 'main') { throw 'Production release must run from branch main.' }
Invoke-Native gh @('auth','status')
Invoke-Native git @('fetch','origin','main','--tags')
$head = (git rev-parse HEAD).Trim()
$remote = (git rev-parse origin/main).Trim()
if ($head -ne $remote) { throw "Local HEAD $head does not match origin/main $remote." }

$targetVersion = (& (Join-Path $PSScriptRoot 'Get-ReleaseVersion.ps1') -Bump $Bump).Trim()
$changes = @(git status --short)
Write-Host ''
Write-Host "[MiniMax] Production target: v$targetVersion"
Write-Host '[MiniMax] Current changes:'
if ($changes.Count -eq 0) { Write-Host '  (version bump only)' } else { $changes | ForEach-Object { Write-Host "  $_" } }
Write-Host ''
if ((Read-Host 'Build, stage, commit, push, and publish these changes? Type YES') -ne 'YES') {
    throw 'Cancelled before build; no files were staged.'
}

& $prepareScript -Version $targetVersion -NoRun
if ([string]::IsNullOrWhiteSpace($Message)) { $Message = Read-Host 'Commit message' }
if ([string]::IsNullOrWhiteSpace($Message)) { throw 'Commit message cannot be empty.' }

$changes = @(git status --short)
if ($changes.Count -eq 0) { throw 'No changes exist after version preparation.' }
Write-Host '[MiniMax] Staging:'
$changes | ForEach-Object { Write-Host "  $_" }
Invoke-Native git @('add','-A','--','.')
Invoke-Native git @('diff','--cached','--check')
& git diff --cached --quiet
if ($LASTEXITCODE -eq 0) { throw 'No staged changes exist.' }
if ($LASTEXITCODE -ne 1) { throw 'Could not inspect staged changes.' }

Invoke-Native git @('commit','-m',$Message.Trim())
$commit = (git rev-parse HEAD).Trim()
$ciNotBefore = [DateTime]::UtcNow
Invoke-Native git @('push','origin','main')
$ciRun = Wait-ForWorkflowRun 'ci.yml' $commit 'push' $ciNotBefore
Invoke-Native gh @('run','watch',[string]$ciRun.databaseId,'--repo','yuzhayo/minimax','--exit-status')

$releaseNotBefore = [DateTime]::UtcNow
Invoke-Native gh @('workflow','run','release.yml','--repo','yuzhayo/minimax','--ref','main','-f',"bump=$Bump")
$releaseRun = Wait-ForWorkflowRun 'release.yml' $commit 'workflow_dispatch' $releaseNotBefore
Invoke-Native gh @('run','watch',[string]$releaseRun.databaseId,'--repo','yuzhayo/minimax','--exit-status')
$published = Verify-PublishedRelease $targetVersion $commit

[pscustomobject]@{ Mode='production'; Version=$targetVersion; Commit=$commit; Release=$published.Url; Sha256=$published.Sha256 }

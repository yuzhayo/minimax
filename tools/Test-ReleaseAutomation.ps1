$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Assert([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}

function Read-Required([string] $RelativePath) {
    $path = Join-Path $root $RelativePath
    Assert (Test-Path -LiteralPath $path -PathType Leaf) "Missing release automation file: $RelativePath"
    Get-Content -LiteralPath $path -Raw
}

$developmentBat = Read-Required 'tools\bump-build-run.bat'
$productionBat = Read-Required 'tools\bump-build-commit-push-release.bat'
$developmentScript = Join-Path $PSScriptRoot 'Bump-Build-Run.ps1'
$productionScript = Join-Path $PSScriptRoot 'Bump-Build-Commit-Push-Release.ps1'

foreach ($entry in @(
    @{ Content=$developmentBat; Owner='Bump-Build-Run.ps1'; Name='development BAT' },
    @{ Content=$productionBat; Owner='Bump-Build-Commit-Push-Release.ps1'; Name='production BAT' })) {
    Assert ($entry.Content -match 'where pwsh\.exe') "$($entry.Name) does not validate pwsh.exe."
    Assert ($entry.Content -match [regex]::Escape($entry.Owner)) "$($entry.Name) does not delegate to $($entry.Owner)."
    Assert ($entry.Content -notmatch '(?im)^\s*(git|gh|dotnet)\s') "$($entry.Name) contains workflow policy instead of remaining a thin launcher."
}

foreach ($script in @($developmentScript,$productionScript)) {
    Assert (Test-Path -LiteralPath $script -PathType Leaf) "Missing PowerShell workflow owner: $script"
    $tokens = $null
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($script,[ref]$tokens,[ref]$errors)
    $parserMessages = @($errors | ForEach-Object Message)
    Assert ($errors.Count -eq 0) "PowerShell parser errors in $script`: $($parserMessages -join '; ')"
}

$versionFile = Join-Path $root 'version.props'
$beforeVersion = (Get-FileHash -LiteralPath $versionFile -Algorithm SHA256).Hash
$beforeStatus = @(git -C $root status --porcelain=v1)
if ($LASTEXITCODE -ne 0) { throw 'Could not capture Git status before release automation plan checks.' }

$developmentPlan = & $developmentScript -PlanOnly
$productionPlan = & $productionScript -PlanOnly -Bump patch -Message 'release automation plan test'

$afterVersion = (Get-FileHash -LiteralPath $versionFile -Algorithm SHA256).Hash
$afterStatus = @(git -C $root status --porcelain=v1)
if ($LASTEXITCODE -ne 0) { throw 'Could not capture Git status after release automation plan checks.' }

Assert ($developmentPlan.Mode -eq 'development' -and $developmentPlan.Version -match '^\d+\.\d+\.\d+$') 'Development planning did not return a semantic target version.'
Assert ($productionPlan.Mode -eq 'production' -and $productionPlan.Version -match '^\d+\.\d+\.\d+$') 'Production planning did not return a semantic target version.'
Assert ($productionPlan.Version -eq (& (Join-Path $PSScriptRoot 'Get-ReleaseVersion.ps1') -Bump patch)) 'Production planning does not reuse the repository release-version resolver.'
Assert ($beforeVersion -eq $afterVersion) 'Planning mode changed version.props.'
Assert (($beforeStatus -join "`n") -eq ($afterStatus -join "`n")) 'Planning mode changed the working tree.'

Write-Host 'Release automation plan checks passed.'

param(
    [ValidateSet('Debug', 'Release')][string] $Configuration = 'Release',
    [string] $ScratchDirectory = (Join-Path $PSScriptRoot '..\.agents-temp\ci-tests')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$scratch = [System.IO.Path]::GetFullPath($ScratchDirectory)
if (-not $scratch.StartsWith($root.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Scratch directory must stay inside the repository: $scratch"
}
New-Item -ItemType Directory -Path $scratch -Force | Out-Null
foreach ($project in @('Foundation\AppFoundation.csproj','MiniMax.csproj','Tests\MiniMaxBridgeTests.csproj','SecondHostFixture\SecondHostFixture.csproj')) {
    & dotnet build (Join-Path $root $project) --configuration $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
& dotnet run --no-build --project (Join-Path $root 'Tests\MiniMaxBridgeTests.csproj') --configuration $Configuration -- $scratch
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& dotnet run --no-build --project (Join-Path $root 'SecondHostFixture\SecondHostFixture.csproj') --configuration $Configuration -- --verify
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$app = Join-Path $root "bin\$Configuration\net10.0-windows\MiniMax.exe"
& $app --simulate
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

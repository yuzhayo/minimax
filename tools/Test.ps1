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
foreach ($project in @('MiniMax.csproj','Tests\MiniMaxBridgeTests.csproj')) {
    & dotnet build (Join-Path $root $project) --configuration $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $project with exit code $LASTEXITCODE." }
}
& dotnet run --no-build --project (Join-Path $root 'Tests\MiniMaxBridgeTests.csproj') --configuration $Configuration -- $scratch
if ($LASTEXITCODE -ne 0) { throw "Focused tests failed with exit code $LASTEXITCODE." }
$app = Join-Path $root "bin\$Configuration\net10.0-windows\MiniMax.exe"
$simulation = Start-Process -FilePath $app -ArgumentList '--simulate' -NoNewWindow -Wait -PassThru
if ($simulation.ExitCode -ne 0) { throw "Simulation failed with exit code $($simulation.ExitCode)." }

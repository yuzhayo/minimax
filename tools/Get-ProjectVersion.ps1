param([string] $Project = (Join-Path $PSScriptRoot '..\version.props'))
$ErrorActionPreference = 'Stop'
$content = Get-Content -LiteralPath $Project -Raw
$match = [regex]::Match($content, '<MiniMaxVersion[^>]*>(?<version>\d+\.\d+\.\d+)</MiniMaxVersion>')
if (-not $match.Success) { throw "MiniMaxVersion semver tiga bagian tidak ditemukan di $Project." }
$match.Groups['version'].Value

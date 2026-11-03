$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$configuration = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'config\defaults.json') | ConvertFrom-Json
$json = $configuration.browser | ConvertTo-Json -Depth 10
$outputPath = Join-Path $projectRoot 'extension\defaults.mjs'
[IO.File]::WriteAllText($outputPath, "// Generated from config/defaults.json by scripts/sync-extension-config.ps1`nexport const defaults = $json;`n", [Text.UTF8Encoding]::new($false))

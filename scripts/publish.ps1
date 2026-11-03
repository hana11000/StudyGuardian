$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    & (Join-Path $PSScriptRoot 'sync-extension-config.ps1')
    & (Join-Path $PSScriptRoot 'dotnet.ps1') publish 'src\StudyGuardian.App\StudyGuardian.App.csproj' -c Release -r win-x64 --self-contained true -o 'dist\StudyGuardian-M6'
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $extensionOutput = Join-Path $projectRoot 'dist\StudyGuardian-M6\extension'
    New-Item -ItemType Directory -Force -Path $extensionOutput | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'extension') -File | Copy-Item -Destination $extensionOutput -Force
    Write-Output 'Ready: dist\StudyGuardian-M6\StudyGuardian.App.exe'
} finally { Pop-Location }

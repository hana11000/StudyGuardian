$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    & (Join-Path $PSScriptRoot 'dotnet.ps1') publish 'src\StudyGuardian.App\StudyGuardian.App.csproj' -c Release -r win-x64 --self-contained true -o 'dist\StudyGuardian-M4'
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Output 'Ready: dist\StudyGuardian-M4\StudyGuardian.App.exe'
} finally { Pop-Location }

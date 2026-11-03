param([switch]$Render,[string]$OutputDirectory='.artifacts\m8\preview')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    & (Join-Path $PSScriptRoot 'dotnet.ps1') build src/StudyGuardian.UiPreview/StudyGuardian.UiPreview.csproj -c Release --verbosity minimal
    if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
    $preview=Join-Path $projectRoot 'src\StudyGuardian.UiPreview\bin\Release\net8.0-windows\StudyGuardian.UiPreview.exe'
    if($Render) {
        $renderPath=[IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
        $process=Start-Process -FilePath $preview -ArgumentList @('--render',('"'+$renderPath+'"')) -WindowStyle Hidden -PassThru -Wait
        exit $process.ExitCode
    }
    Start-Process -FilePath $preview
} finally {Pop-Location}

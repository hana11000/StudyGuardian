param([string]$OutputDirectory='dist\StudyGuardian-M7.1',[switch]$SkipArchive)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$output=[IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
if(!$output.StartsWith($projectRoot+'\',[StringComparison]::OrdinalIgnoreCase)) {throw 'Publish inside the project.'}
if(Test-Path -LiteralPath (Join-Path $output 'data')) {throw 'Publish to a clean output folder without data.'}
Push-Location $projectRoot
try {
    & (Join-Path $PSScriptRoot 'sync-extension-config.ps1')
    & (Join-Path $PSScriptRoot 'dotnet.ps1') publish src/StudyGuardian.App/StudyGuardian.App.csproj -c Release -r win-x64 --self-contained true -o $output --verbosity minimal
    if($LASTEXITCODE -ne 0) {exit $LASTEXITCODE}
    & (Join-Path $PSScriptRoot 'dotnet.ps1') publish src/StudyGuardian.Watchdog/StudyGuardian.Watchdog.csproj -c Release -r win-x64 --self-contained true -o (Join-Path $output 'watchdog') --verbosity minimal
    if($LASTEXITCODE -ne 0) {exit $LASTEXITCODE}
    $extensionOutput=Join-Path $output 'extension';New-Item -ItemType Directory -Path $extensionOutput -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'extension') -File | Copy-Item -Destination $extensionOutput -Force
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'packaging') -File | Copy-Item -Destination $output -Force
    $files=@(Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object {$_.Name -ne 'package-files.json'} | ForEach-Object {
        @{path=$_.FullName.Substring($output.Length+1);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
    ConvertTo-Json -InputObject $files | Set-Content -LiteralPath (Join-Path $output 'package-files.json') -Encoding UTF8
    if(!$SkipArchive) {
        $archive=$output+'.zip'
        Compress-Archive -LiteralPath $output -DestinationPath $archive -Force
        (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash+'  '+[IO.Path]::GetFileName($archive) | Set-Content -LiteralPath ($archive+'.sha256') -Encoding ASCII
    }
    Write-Output "Ready: $output\StudyGuardian.App.exe"
} finally {Pop-Location}

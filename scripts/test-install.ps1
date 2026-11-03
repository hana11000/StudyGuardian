$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$testRoot=Join-Path $projectRoot ('.artifacts\install-test-'+[guid]::NewGuid().ToString('N'))
$source=Join-Path $testRoot 'source';$target=Join-Path $testRoot 'target'
New-Item -ItemType Directory -Path $source -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'packaging\Install.ps1') -Destination $source
Set-Content -LiteralPath (Join-Path $source 'StudyGuardian.App.exe') -Value 'synthetic v1'
New-Item -ItemType Directory -Path (Join-Path $source 'config') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $source 'config\defaults.json') -Value '{}'
function Write-Manifest {
    $item=Join-Path $source 'StudyGuardian.App.exe'
    ConvertTo-Json -InputObject @(@{path='StudyGuardian.App.exe';sha256=(Get-FileHash -LiteralPath $item).Hash},@{path='config\defaults.json';sha256=(Get-FileHash -LiteralPath (Join-Path $source 'config\defaults.json')).Hash}) | Set-Content -LiteralPath (Join-Path $source 'package-files.json') -Encoding UTF8
}
Write-Manifest
& (Join-Path $source 'Install.ps1') -Destination $target -NoShortcuts
if(!(Test-Path -LiteralPath (Join-Path $target 'StudyGuardian.App.exe'))) {throw 'Install did not copy app'}
if(!(Test-Path -LiteralPath (Join-Path $target 'config\defaults.json'))) {throw 'Installer did not enumerate all manifest entries'}
New-Item -ItemType Directory -Path (Join-Path $target 'data') | Out-Null
Set-Content -LiteralPath (Join-Path $target 'data\keep.txt') -Value 'synthetic keep'
Set-Content -LiteralPath (Join-Path $source 'StudyGuardian.App.exe') -Value 'synthetic v2';Write-Manifest
& (Join-Path $source 'Install.ps1') -Destination $target -NoShortcuts
if((Get-Content -LiteralPath (Join-Path $target 'data\keep.txt')) -ne 'synthetic keep') {throw 'Upgrade touched data'}
if((Get-Content -LiteralPath (Join-Path $target 'StudyGuardian.App.exe')) -ne 'synthetic v2') {throw 'Upgrade failed'}
$foreign=Join-Path $testRoot 'foreign';New-Item -ItemType Directory -Path $foreign | Out-Null
Set-Content -LiteralPath (Join-Path $foreign 'keep.txt') -Value 'keep'
$rejected=$false;try{& (Join-Path $source 'Install.ps1') -Destination $foreign -NoShortcuts}catch{$rejected=$true}
if(!$rejected){throw 'Unrelated directory accepted'}
$validEntry=@{path='StudyGuardian.App.exe';sha256=(Get-FileHash -LiteralPath (Join-Path $source 'StudyGuardian.App.exe')).Hash}
ConvertTo-Json -InputObject @($validEntry,@{path='..\escaped.txt';sha256='x'}) | Set-Content -LiteralPath (Join-Path $source 'package-files.json') -Encoding UTF8
$rejected=$false;try{& (Join-Path $source 'Install.ps1') -Destination $target -NoShortcuts}catch{$rejected=$true}
if(!$rejected -or (Test-Path -LiteralPath (Join-Path $testRoot 'escaped.txt'))) {throw 'Traversal accepted'}
ConvertTo-Json -InputObject @($validEntry,@{path='data\keep.txt';sha256='x'}) | Set-Content -LiteralPath (Join-Path $source 'package-files.json') -Encoding UTF8
$rejected=$false;try{& (Join-Path $source 'Install.ps1') -Destination $target -NoShortcuts}catch{$rejected=$true}
if(!$rejected){throw 'Data path accepted'}
New-Item -ItemType Directory -Path (Join-Path $source 'data') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $source 'data\keep.txt') -Value 'synthetic overwrite attempt'
$dataHash=(Get-FileHash -LiteralPath (Join-Path $source 'data\keep.txt')).Hash
foreach($alias in @('.\data\keep.txt','./data/keep.txt','data.\keep.txt')) {
    ConvertTo-Json -InputObject @($validEntry,@{path=$alias;sha256=$dataHash}) | Set-Content -LiteralPath (Join-Path $source 'package-files.json') -Encoding UTF8
    $rejected=$false;try{& (Join-Path $source 'Install.ps1') -Destination $target -NoShortcuts}catch{$rejected=$true}
    if(!$rejected -or (Get-Content -LiteralPath (Join-Path $target 'data\keep.txt')) -ne 'synthetic keep'){throw "Normalized data alias accepted: $alias"}
}
Write-Manifest
Set-Content -LiteralPath (Join-Path $source 'StudyGuardian.App.exe') -Value 'corrupted'
$rejected=$false;try{& (Join-Path $source 'Install.ps1') -Destination $target -NoShortcuts}catch{$rejected=$true}
if(!$rejected -or (Get-Content -LiteralPath (Join-Path $target 'StudyGuardian.App.exe')) -ne 'synthetic v2'){throw 'Checksum preflight failed'}
Write-Output 'PASS: install, upgrade, data preservation, foreign directory, traversal, protected data, checksums'

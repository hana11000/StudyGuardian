$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sdkDirectory = Join-Path $projectRoot '.tools\dotnet'
$sdkExe = Join-Path $sdkDirectory 'dotnet.exe'
$sdkVersion = (Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'global.json') | ConvertFrom-Json).sdk.version
if (Test-Path -LiteralPath $sdkExe) {
    & $sdkExe --version
    if ($LASTEXITCODE -eq 0) { Write-Output 'Local .NET SDK is ready.'; exit 0 }
}
$sdkZip = Join-Path $projectRoot '.tools\dotnet-sdk.zip'
New-Item -ItemType Directory -Path $sdkDirectory -Force | Out-Null
$metadata = Invoke-RestMethod -Uri 'https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/8.0/releases.json'
$sdk = @($metadata.releases | ForEach-Object { $_.sdks } | Where-Object { $_.version -eq $sdkVersion })[0]
if (-not $sdk) { throw "SDK $sdkVersion is missing from Microsoft's release metadata." }
$archive = $sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' } | Select-Object -First 1
if (-not $archive -or ([Uri]$archive.url).Host -notin @('builds.dotnet.microsoft.com', 'dotnetcli.blob.core.windows.net')) { throw 'Unexpected SDK source.' }
Invoke-WebRequest -Uri $archive.url -OutFile $sdkZip -UseBasicParsing
if ((Get-FileHash -LiteralPath $sdkZip -Algorithm SHA512).Hash -ne $archive.hash) { throw 'SDK checksum mismatch.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($sdkZip, $sdkDirectory)
Remove-Item -LiteralPath $sdkZip
& $sdkExe --version
exit $LASTEXITCODE

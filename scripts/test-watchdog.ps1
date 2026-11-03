$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$testRoot=Join-Path $projectRoot ('.artifacts\watchdog-test-'+[guid]::NewGuid().ToString('N'))
& (Join-Path $PSScriptRoot 'dotnet.ps1') publish (Join-Path $projectRoot 'tests\StudyGuardian.Watchdog.Fixture\StudyGuardian.Watchdog.Fixture.csproj') -c Release -r win-x64 --self-contained true -o $testRoot --verbosity quiet
if($LASTEXITCODE -ne 0){throw 'Fixture publish failed'}
& (Join-Path $PSScriptRoot 'dotnet.ps1') publish (Join-Path $projectRoot 'src\StudyGuardian.Watchdog\StudyGuardian.Watchdog.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $testRoot 'watchdog') --verbosity quiet
if($LASTEXITCODE -ne 0){throw 'Helper publish failed'}
New-Item -ItemType Directory -Path (Join-Path $testRoot 'config') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'config\defaults.json') -Destination (Join-Path $testRoot 'config')
foreach($mode in @('crash','stop','limit')) {
    Set-Content -LiteralPath (Join-Path $testRoot 'mode.txt') -Value $mode -Encoding ASCII
    $child=Start-Process -FilePath (Join-Path $testRoot 'StudyGuardian.App.exe') -WorkingDirectory $testRoot -WindowStyle Hidden -PassThru
    if(!$child.WaitForExit(10000)){throw 'Fixture did not exit'}
    $waitMilliseconds=2000;if($mode -eq 'limit'){$waitMilliseconds=8000}
    Start-Sleep -Milliseconds $waitMilliseconds
    $launches=@(Get-Content -LiteralPath (Join-Path $testRoot 'launches.jsonl') | ForEach-Object {$_ | ConvertFrom-Json})
    $exits=@(Get-Content -LiteralPath (Join-Path $testRoot 'exits.jsonl') | ForEach-Object {$_ | ConvertFrom-Json})
    $expected=1;if($mode -eq 'crash'){$expected=2};if($mode -eq 'limit'){$expected=4}
    if($launches.Count -ne $expected){throw "Unexpected launch count for $mode : $($launches.Count)"}
    if($mode -eq 'crash') {
        $delay=([datetimeoffset]$launches[1].at-[datetimeoffset]$exits[0].at).TotalSeconds
        if($delay -ge 5 -or $delay -lt 0){throw 'Restart exceeded 5 seconds'}
        Write-Output "PASS: unexpected exit restarted in $delay seconds"
    } else {Write-Output "PASS: $mode produced $expected launches"}
    Move-Item -LiteralPath (Join-Path $testRoot 'launches.jsonl') -Destination (Join-Path $testRoot "launches-$mode.jsonl")
    Move-Item -LiteralPath (Join-Path $testRoot 'exits.jsonl') -Destination (Join-Path $testRoot "exits-$mode.jsonl")
    $child.Dispose()
}
Write-Output "Evidence: $testRoot"

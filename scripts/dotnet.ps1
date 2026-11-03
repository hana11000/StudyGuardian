$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnetPath = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetPath)) {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnetCommand) { throw 'Install .NET 8 SDK or run scripts\setup.ps1 first.' }
    $dotnetPath = $dotnetCommand.Source
}
$env:DOTNET_ROOT = Split-Path -Parent $dotnetPath
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.nuget\packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $projectRoot '.nuget\http-cache'
$forwardArgs = @($args)
if ($forwardArgs.Count -gt 0 -and $forwardArgs[0] -in @('build', 'test', 'restore', 'publish')) {
    # Single-node MSBuild avoids named-pipe restrictions in managed Windows workspaces.
    $forwardArgs += @('-m:1', '-p:UseSharedCompilation=false')
}
& $dotnetPath @forwardArgs
exit $LASTEXITCODE

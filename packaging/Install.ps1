param(
    [string]$Destination=(Join-Path $env:LOCALAPPDATA 'Programs\StudyGuardian'),
    [switch]$NoShortcuts
)
$ErrorActionPreference='Stop'
$sourceRoot=[IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
$installRoot=[IO.Path]::GetFullPath($Destination).TrimEnd('\')
if($installRoot -eq [IO.Path]::GetPathRoot($installRoot).TrimEnd('\') -or
    $installRoot.Equals($sourceRoot,[StringComparison]::OrdinalIgnoreCase) -or
    $installRoot.StartsWith($sourceRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or
    $sourceRoot.StartsWith($installRoot+'\',[StringComparison]::OrdinalIgnoreCase)) {throw 'Choose a separate installation folder.'}
function Assert-NoLinks([string]$path) {
    $cursor=$path
    while($cursor) {
        if(Test-Path -LiteralPath $cursor) {
            $item=Get-Item -LiteralPath $cursor -Force
            if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Linked paths are not supported.'}
        }
        $parent=Split-Path -Parent $cursor
        if($parent -eq $cursor){break};$cursor=$parent
    }
}
Assert-NoLinks $sourceRoot;Assert-NoLinks $installRoot
$marker=Join-Path $installRoot '.studyguardian-install.json'
if((Test-Path -LiteralPath $installRoot) -and @(Get-ChildItem -LiteralPath $installRoot -Force).Count -gt 0) {
    if(!(Test-Path -LiteralPath $marker) -or (Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json).product -ne 'StudyGuardian') {throw 'This folder is not a Study Guardian installation.'}
}
$executable=Join-Path $installRoot 'StudyGuardian.App.exe'
if(@(Get-Process -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $executable -or $_.Path -eq (Join-Path $installRoot 'watchdog\StudyGuardian.Watchdog.exe')}).Count -gt 0) {throw 'Exit Study Guardian before upgrading.'}
$decodedManifest=Get-Content -LiteralPath (Join-Path $sourceRoot 'package-files.json') -Raw | ConvertFrom-Json
$manifest=@($decodedManifest)
if($manifest.Count -eq 0 -or !($manifest.path -contains 'StudyGuardian.App.exe')) {throw 'Package manifest is incomplete.'}
$validated=@()
foreach($entry in $manifest) {
    $relative=[string]$entry.path
    $segments=$relative -split '[\\/]'
    if(@($segments | Where-Object {[string]::IsNullOrEmpty($_) -or $_ -eq '.' -or $_ -eq '..' -or $_.TrimEnd(' ','.') -ne $_}).Count -gt 0) {throw 'Noncanonical package path.'}
    if([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative) -or $relative.Contains(':') -or
        $relative.Split('/\') -contains '..' -or $relative.Split('/\')[0] -ieq 'data') {throw 'Unsafe package path.'}
    $from=[IO.Path]::GetFullPath((Join-Path $sourceRoot $relative));$to=[IO.Path]::GetFullPath((Join-Path $installRoot $relative))
    if(!$from.StartsWith($sourceRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or !$to.StartsWith($installRoot+'\',[StringComparison]::OrdinalIgnoreCase)) {throw 'Package path is outside its folder.'}
    $dataRoot=Join-Path $installRoot 'data'
    if($to.Equals($dataRoot,[StringComparison]::OrdinalIgnoreCase) -or $to.StartsWith($dataRoot+'\',[StringComparison]::OrdinalIgnoreCase)) {throw 'Package may not write data.'}
    Assert-NoLinks $from;Assert-NoLinks $to
    if(!(Test-Path -LiteralPath $from -PathType Leaf) -or (Get-FileHash -LiteralPath $from -Algorithm SHA256).Hash -ine $entry.sha256) {throw 'Package checksum mismatch.'}
    $validated+=@{from=$from;to=$to}
}
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
foreach($file in $validated) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $file.to) -Force | Out-Null
    Copy-Item -LiteralPath $file.from -Destination $file.to -Force
}
@{product='StudyGuardian';version='M7'} | ConvertTo-Json | Set-Content -LiteralPath $marker -Encoding UTF8
if(!$NoShortcuts) {
    $programs=[Environment]::GetFolderPath('Programs');$link=Join-Path $programs 'Study Guardian.lnk'
    $shell=New-Object -ComObject WScript.Shell;$shortcut=$shell.CreateShortcut($link)
    $shortcut.TargetPath=$executable;$shortcut.WorkingDirectory=$installRoot;$shortcut.Save()
}
Write-Output "Installed: $executable"

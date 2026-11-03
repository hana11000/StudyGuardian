@echo off
setlocal
set "GUARDIAN_EXE=%~dp0dist\StudyGuardian-M8\StudyGuardian.App.exe"
if not exist "%GUARDIAN_EXE%" (
  echo Build the app first: powershell -File scripts\publish.ps1
  pause
  exit /b 1
)
start "Study Guardian" "%GUARDIAN_EXE%"

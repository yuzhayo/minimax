@echo off
setlocal

cd /d "%~dp0.."

where pwsh.exe >nul 2>nul
if errorlevel 1 (
  echo [MiniMax] FAILED: pwsh.exe was not found.
  pause
  exit /b 1
)

set "BUMP=%~1"
if not defined BUMP set "BUMP=patch"

pwsh.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Bump-Build-Commit-Push-Release.ps1" -Bump "%BUMP%" -Message "%~2"
if errorlevel 1 (
  echo [MiniMax] FAILED. Review the error above.
  pause
  exit /b 1
)

echo [MiniMax] Commit, push, and release completed.
exit /b 0

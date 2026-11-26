@echo off
setlocal

cd /d "%~dp0.."

where pwsh.exe >nul 2>nul
if errorlevel 1 (
  echo [MiniMax] FAILED: pwsh.exe was not found.
  pause
  exit /b 1
)

pwsh.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Bump-Build-Run.ps1"
if errorlevel 1 (
  echo [MiniMax] FAILED. Review the error above.
  pause
  exit /b 1
)

echo [MiniMax] Development build is running.
exit /b 0

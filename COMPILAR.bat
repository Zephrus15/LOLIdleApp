@echo off
dotnet restore
if errorlevel 1 (
  pause
  exit /b 1
)
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
if errorlevel 1 (
  pause
  exit /b 1
)
echo.
echo Executavel pronto em bin\Release\net8.0-windows\win-x64\publish\LoL Idle.exe
pause

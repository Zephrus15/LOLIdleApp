@echo off
cd /d "%~dp0"

echo Restaurando dependencias...
dotnet restore LoLIdleDesktop.csproj
if errorlevel 1 (
  echo.
  echo Falha ao restaurar as dependencias.
  pause
  exit /b 1
)

echo.
echo Publicando para Windows 64 bits...
dotnet publish LoLIdleDesktop.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
if errorlevel 1 (
  echo.
  echo Falha ao compilar o aplicativo.
  pause
  exit /b 1
)

echo.
echo Executavel pronto em:
echo bin\Release\net8.0-windows\win-x64\publish\LoL Idle.exe
pause

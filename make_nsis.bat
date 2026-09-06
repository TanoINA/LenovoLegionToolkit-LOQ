@echo off
SET PATH=D:\dotnet9;%USERPROFILE%\.dotnet;%PATH%;"%LOCALAPPDATA%\Programs\NSIS";"C:\Program Files (x86)\NSIS";"C:\Program Files\NSIS"

SET VERSION=2.35.4.5
IF NOT "%1"=="" IF /I NOT "%1"=="raw" SET VERSION=%1

for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd"') do set BUILD_DATE=%%i

echo ===================================================
echo Building NSIS Installer for Lenovo Legion Toolkit
echo Version: %VERSION%
echo Build Date: %BUILD_DATE%
echo Target: Lenovo LOQ 15IRX10 and Legion series
echo ===================================================

echo Publishing assemblies with .NET 9...
dotnet publish LenovoLegionToolkit.WPF -c release -o build /p:DebugType=None /p:FileVersion=%VERSION% /p:Version=%VERSION% || exit /b
dotnet publish LenovoLegionToolkit.SpectrumTester -c release -o build /p:DebugType=None /p:FileVersion=%VERSION% /p:Version=%VERSION% || exit /b
dotnet publish LenovoLegionToolkit.Probe -c release -o build /p:DebugType=None /p:FileVersion=%VERSION% /p:Version=%VERSION% || exit /b
dotnet publish LenovoLegionToolkit.CLI -c release -o build /p:DebugType=None /p:FileVersion=%VERSION% /p:Version=%VERSION% || exit /b

echo Packaging identity files...
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build_identity_package.ps1" -Version %VERSION% -OutputDir "build" -UseManifest || exit /b

echo Compiling NSIS Installer...
makensis.exe /DVERSION=%VERSION% /DBUILD_DATE=%BUILD_DATE% make_installer.nsi || exit /b

echo.
echo ===================================================
echo [SUCCESS] NSIS Installer created:
echo build_installer\LenovoLegionToolkitSetup-v%VERSION%_Build%BUILD_DATE%_NSIS.exe
echo ===================================================


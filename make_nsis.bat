@echo off
SET PATH=D:\dotnet9;%USERPROFILE%\.dotnet;%PATH%;"%LOCALAPPDATA%\Programs\NSIS";"C:\Program Files (x86)\NSIS";"C:\Program Files\NSIS"

SET VERSION=2.36.0.0-loq
IF NOT "%1"=="" IF /I NOT "%1"=="raw" SET VERSION=%1

for /f "tokens=1 delims=-" %%a in ("%VERSION%") do set NUMERIC_VERSION=%%a
for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd"') do set BUILD_DATE=%%i

echo ===================================================
echo Building NSIS Installer for Lenovo Legion Toolkit
echo Version: %VERSION%
echo Numeric Version: %NUMERIC_VERSION%
echo Build Date: %BUILD_DATE%
echo Target: Lenovo LOQ 15IRX10 and Legion series
echo ===================================================

echo Cleaning build staging directory...
if exist "build" rd /s /q "build"
mkdir "build"

echo Publishing assemblies with .NET 9...
dotnet publish LenovoLegionToolkit.WPF -c release -o build /p:DebugType=None /p:FileVersion=%NUMERIC_VERSION% /p:Version=%VERSION% || exit /b
dotnet publish LenovoLegionToolkit.SpectrumTester -c release -o build /p:DebugType=None /p:FileVersion=%NUMERIC_VERSION% /p:Version=%VERSION% || exit /b
dotnet publish LenovoLegionToolkit.Probe -c release -o build /p:DebugType=None /p:FileVersion=%NUMERIC_VERSION% /p:Version=%VERSION% || exit /b
dotnet publish LenovoLegionToolkit.CLI -c release -o build /p:DebugType=None /p:FileVersion=%NUMERIC_VERSION% /p:Version=%VERSION% || exit /b

echo Packaging identity files...
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build_identity_package.ps1" -Version %VERSION% -OutputDir "build" -UseManifest || exit /b

echo Compiling NSIS Installer...
makensis.exe /DVERSION=%VERSION% /DNUMERIC_VERSION=%NUMERIC_VERSION% /DBUILD_DATE=%BUILD_DATE% make_installer.nsi || exit /b

echo.
echo ===================================================
echo [SUCCESS] NSIS Installer created:
echo build_installer\LenovoLegionToolkitSetup-v%VERSION%_Build%BUILD_DATE%_NSIS.exe
echo ===================================================


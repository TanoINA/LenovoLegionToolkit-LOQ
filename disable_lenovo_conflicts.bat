@echo off
:: Batch script to disable conflicting Lenovo Legion Space background services on LOQ / Legion
:: Run as Administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Please run this script as Administrator!
    echo Right-click this file and choose "Run as administrator".
    pause
    exit /b 1
)

echo ========================================================
echo Stopping and Disabling Conflicting Lenovo Services...
echo ========================================================

sc config "GAService" start= disabled >nul 2>&1
net stop "GAService" /y >nul 2>&1

sc config "LenovoSmartService" start= disabled >nul 2>&1
net stop "LenovoSmartService" /y >nul 2>&1

sc config "DAService" start= disabled >nul 2>&1
net stop "DAService" /y >nul 2>&1

echo Killing residual SmartEngine / GamingAI processes...
taskkill /F /IM SmartEngineHost64.exe >nul 2>&1
taskkill /F /IM SmartEngineHostN64.exe >nul 2>&1
taskkill /F /IM SmartEngineHostS64.exe >nul 2>&1
taskkill /F /IM GAService.exe >nul 2>&1
taskkill /F /IM GAController.exe >nul 2>&1
taskkill /F /IM GAWorker.exe >nul 2>&1
taskkill /F /IM LenovoSmartService.exe >nul 2>&1
taskkill /F /IM seworker.exe >nul 2>&1
taskkill /F /IM LSDaemon.exe >nul 2>&1
taskkill /F /IM LegionSpace.exe >nul 2>&1

echo.
echo ========================================================
echo [SUCCESS] Lenovo background conflicting services disabled!
echo Custom Mode will now stay active without being reverted.
echo ========================================================
pause

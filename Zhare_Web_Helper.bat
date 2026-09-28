@echo off
title Zhare - Browser Web Portal Connector
color 0B
echo ========================================================
echo             Zhare - High Speed Wi-Fi Transfer
echo ========================================================
echo.
echo Make sure your Windows PC is connected to the same Wi-Fi
echo network (or phone's Mobile Hotspot).
echo.
echo Searching for phone...

set PHONE_IP=192.168.43.1
set PORT=8888

for /f "tokens=3" %%i in ('route print ^| findstr "\<0.0.0.0\>"') do (
    set GATEWAY_IP=%%i
    goto :found_gateway
)

:found_gateway
if not "%GATEWAY_IP%"=="" (
    set TARGET_URL=http://%GATEWAY_IP%:%PORT%
) else (
    set TARGET_URL=http://%PHONE_IP%:%PORT%
)

echo.
echo Opening Zhare Web Portal in your browser:
echo %TARGET_URL%
echo.
start "" "%TARGET_URL%"

echo If the page does not open, launch Zhare.exe directly!
echo.
pause

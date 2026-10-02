@echo off
title Rebuilding Zhare for Windows
echo Compiling HotspotShare.cs with csc.exe into Zhare.exe...

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set WPF="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"

%CSC% /target:winexe /optimize+ /r:%WPF%\PresentationFramework.dll,%WPF%\PresentationCore.dll,%WPF%\WindowsBase.dll,System.dll,System.Xaml.dll /win32icon:"%~dp0..\zhare_icon.ico" /out:"%~dp0..\Zhare.exe" "%~dp0HotspotShare.cs"

if %ERRORLEVEL% equ 0 (
    copy /Y "%~dp0..\Zhare.exe" "%~dp0..\HotspotShare.exe" >nul
    echo.
    echo [SUCCESS] Zhare.exe compiled successfully!
    echo.
) else (
    echo.
    echo [ERROR] Compilation failed.
    echo.
)
pause

@echo off
title Build Zhare Windows Executable
echo ===================================================
echo             Building Zhare for Windows
echo ===================================================
echo.

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set WPF_DIR="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"

if not exist %CSC% (
    echo [ERROR] .NET Framework 4.0/4.8 C# compiler not found at %CSC%
    pause
    exit /b 1
)

echo Compiling windows-pc\HotspotShare.cs into Zhare.exe ...
%CSC% /target:winexe /optimize+ /win32icon:"zhare_icon.ico" /r:%WPF_DIR%\PresentationFramework.dll,%WPF_DIR%\PresentationCore.dll,%WPF_DIR%\WindowsBase.dll,System.dll,System.Xaml.dll,System.Windows.Forms.dll /out:"Zhare.exe" "windows-pc\HotspotShare.cs"

if %ERRORLEVEL% equ 0 (
    echo.
    echo [SUCCESS] Zhare.exe compiled successfully!
    echo Location: %~dp0Zhare.exe
) else (
    echo.
    echo [FAILED] Compilation failed.
)
echo.
pause

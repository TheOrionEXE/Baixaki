@echo off
rem Builds Baixaki.exe from Baixaki.cs using the C# compiler built into Windows.
rem Keep Baixaki.cs and Baixaki.ico in the same folder as this file.
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Could not find the .NET Framework C# compiler.
    pause
    exit /b 1
)
set ICON=
if exist "Baixaki.ico" set ICON=/win32icon:Baixaki.ico
"%CSC%" /nologo /target:winexe /optimize+ %ICON% /out:Baixaki.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll Baixaki.cs
if errorlevel 1 (
    echo.
    echo Build failed. Copy the error lines above and send them to Claude.
    pause
    exit /b 1
)
echo.
echo Done! Open Baixaki.exe (right-click it ^> Send to ^> Desktop for a shortcut).
pause

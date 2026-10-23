@echo off
rem Builds Baixaki.exe using the C# compiler built into Windows.
rem Run from a complete source checkout, including assets and HelpTopics.json.
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Could not find the .NET Framework C# compiler.
    pause
    exit /b 1
)
set ICON=
if exist "assets\baixaki.ico" set ICON=/win32icon:assets\baixaki.ico
if not exist "Version.cs" echo static class AppInfo { public const string Version = "1.0.0"; }> Version.cs
"%CSC%" /nologo /target:winexe /optimize+ %ICON% /out:Baixaki.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Core.dll Baixaki.cs DownloadState.cs DownloadEngine.cs AppUpdater.cs HelpCenter.cs ModernControls.cs UiText.cs /resource:HelpTopics.json,Baixaki.HelpTopics.json Version.cs
if errorlevel 1 (
    echo.
    echo Build failed. Copy the error lines above and review the build log.
    pause
    exit /b 1
)
echo.
echo Done! Open Baixaki.exe (right-click it ^> Send to ^> Desktop for a shortcut).
pause

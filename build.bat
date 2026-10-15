@echo off
rem Compila DesktopPerDesktop.exe. Per installarlo: DesktopPerDesktop.exe --install
cd /d "%~dp0"
if not exist app.ico powershell -NoProfile -ExecutionPolicy Bypass -File make-icon.ps1
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /win32icon:app.ico /out:DesktopPerDesktop.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll DesktopPerDesktop.cs

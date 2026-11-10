@echo off
rem Builds bin\ClaudeTogether.exe (windowed) and bin\claude-together.exe (console).
rem Uses the C# compiler that ships with Windows (.NET Framework 4.x). No SDK needed.
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo csc.exe not found. Install the .NET Framework 4.x developer pack.
  exit /b 1
)
if not exist bin mkdir bin
set "REFS=/r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Management.dll /r:System.Web.Extensions.dll /r:System.Core.dll"
"%CSC%" /nologo /codepage:65001 /target:winexe /out:bin\ClaudeTogether.exe /win32icon:assets\app.ico %REFS% /recurse:src\*.cs || exit /b 1
"%CSC%" /nologo /codepage:65001 /target:exe /define:CLI /out:bin\claude-together.exe /win32icon:assets\app.ico %REFS% /recurse:src\*.cs || exit /b 1
echo Built: bin\ClaudeTogether.exe, bin\claude-together.exe

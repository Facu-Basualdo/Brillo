@echo off
rem Compila Brillo.exe con el compilador de C# que ya trae Windows (.NET Framework 4).
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:winexe /optimize+ /out:Brillo.exe /r:System.Management.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll Brillo.cs

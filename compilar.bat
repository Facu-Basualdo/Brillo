@echo off
rem Compila Brillo.exe y el instalador "Instalar Brillo.exe" con el compilador de C# que ya trae Windows (.NET Framework 4).
rem El instalador lleva Brillo.exe adentro como recurso, asi que se compila al final.
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set OPCIONES=/nologo /codepage:65001 /target:winexe /optimize+ /win32icon:brillo.ico /r:System.Management.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll

"%CSC%" %OPCIONES% /out:Brillo.exe Brillo.cs || exit /b 1
"%CSC%" %OPCIONES% /define:INSTALADOR /main:ProgramaInstalador /resource:Brillo.exe,Brillo.exe "/out:Instalar Brillo.exe" Brillo.cs Instalador.cs || exit /b 1

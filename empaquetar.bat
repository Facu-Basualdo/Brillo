@echo off
rem Arma el paquete MSIX para la Microsoft Store: tienda\Brillo.msix
rem Compila Brillo.exe con /define:TIENDA (inicio automatico por tarea de inicio del paquete) y lo empaqueta
rem con tienda\AppxManifest.xml y los logos de tienda\Assets.
rem Necesita makeappx y makepri: si faltan, correr antes herramientas\bajar-sdk.ps1
cd /d "%~dp0"
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set WINMD=%WINDIR%\System32\WinMetadata
set SDK=
for /d %%d in (herramientas\sdk\paquete\bin\10.*) do set SDK=%%d\x64
if not exist "%SDK%\makeappx.exe" (
    echo Faltan makeappx y makepri. Corre: powershell -ExecutionPolicy Bypass -File herramientas\bajar-sdk.ps1
    exit /b 1
)

set OBJ=tienda\obj
set PAQUETE=%OBJ%\paquete
if exist %OBJ% rmdir /s /q %OBJ%
mkdir %PAQUETE%\Assets

"%FW%\csc.exe" /nologo /codepage:65001 /target:winexe /optimize+ /win32icon:brillo.ico /define:TIENDA ^
    /r:System.Management.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
    /r:"%FW%\System.Runtime.dll" /r:"%WINMD%\Windows.ApplicationModel.winmd" /r:"%WINMD%\Windows.Foundation.winmd" ^
    /out:%PAQUETE%\Brillo.exe Brillo.cs || exit /b 1

copy /y tienda\AppxManifest.xml %PAQUETE%\ >nul
copy /y tienda\Assets\*.png %PAQUETE%\Assets\ >nul

"%SDK%\makepri.exe" new /pr %PAQUETE% /cf tienda\priconfig.xml /mn %PAQUETE%\AppxManifest.xml /of %PAQUETE%\resources.pri /o > %OBJ%\makepri.log || (type %OBJ%\makepri.log & exit /b 1)
"%SDK%\makeappx.exe" pack /d %PAQUETE% /p tienda\Brillo.msix /o > %OBJ%\makeappx.log || (type %OBJ%\makeappx.log & exit /b 1)

echo.
echo Listo: tienda\Brillo.msix (subilo al Centro de partners; la Store lo firma sola)

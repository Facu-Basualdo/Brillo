# Brillo

Programa chico para Windows que ajusta el brillo de las pantallas desde un icono en la barra de tareas (área de notificación), sin usar el botón físico del monitor.

## Hardware del usuario

- **Monitor externo:** LG UltraGear 24GS60F-B (24", 180 Hz). Se controla por **DDC/CI** (código VCP `0x10` = brillo, rango 0–100). Windows lo reporta como "Generic PnP Monitor"; el nombre real ("LG ULTRAGEAR") solo aparece en `WmiMonitorID`.
- **Pantalla de la laptop:** panel interno (`DISPLAY\CMN1616…`). No responde a DDC/CI; se controla por **WMI** (`root\WMI`, clases `WmiMonitorBrightness` / `WmiMonitorBrightnessMethods`).
- Si el LG deja de responder, lo primero es revisar que DDC/CI siga activado en el menú del monitor.

## Archivos

- `Brillo.cs`: todo el código del programa.
- `Instalador.cs`: la ventana del instalador. Se compila **junto con** `Brillo.cs` (reusa `Tema`, `Dpi`, `Native`, `Config`, `Instalacion` y `Flotante.Rellenar/Bordear`) con `/define:INSTALADOR` (para que no se dupliquen los atributos de ensamblado) y `/main:ProgramaInstalador`.
- `compilar.bat`: compila `Brillo.exe` y después `Instalar Brillo.exe` (que lleva `Brillo.exe` adentro con `/resource:Brillo.exe,Brillo.exe`) con `csc.exe` de .NET Framework 4 (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`), que ya viene con Windows. No hay proyecto, NuGet ni SDK. Usa `/codepage:65001` porque los `.cs` están en UTF-8 sin BOM. El `.bat` tiene que quedar en CRLF y sin tildes (si no, `cmd` lo lee mal).
- `empaquetar.bat` + `tienda\`: versión para la **Microsoft Store** (paquete MSIX, que la Store firma sola). Compila `Brillo.cs` con `/define:TIENDA` (referencia `Windows.ApplicationModel.winmd` y `Windows.Foundation.winmd` de `System32\WinMetadata`, más el facade `System.Runtime.dll`), copia `tienda\AppxManifest.xml` y `tienda\Assets\*.png`, arma `resources.pri` con `tienda\priconfig.xml` (sin `<packaging>`, para que quede un solo `.pri`) y genera `tienda\Brillo.msix`. `makeappx`/`makepri` vienen del paquete NuGet oficial `Microsoft.Windows.SDK.BuildTools`, que baja `herramientas\bajar-sdk.ps1` a `herramientas\sdk\` (no se sube). La identidad del manifiesto (`FacuBas.Brillo`, `CN=1198724E-…`, `FacuBas`) es la que asignó el Centro de partners: no cambiarla.
- `brillo.ico`: icono de los dos `.exe` (`/win32icon`). El mismo script genera los logos de `tienda\Assets` (Square44x44 con `scale-*` y `targetsize-*`, Square150x150, StoreLogo). Se genera con `herramientas\crear-icono.ps1` (cuadrado redondeado azul con el glifo de brillo en blanco); solo hace falta correrlo para cambiar el diseño.
- `Brillo.exe` / `Instalar Brillo.exe`: el resultado compilado (no se suben al repo).
- `README.md` (para usuarios, en español) y `LICENSE` (MIT). Si cambia algo visible para el usuario (atajos, opciones, avisos, instalación), actualizar el README.

## Compilar

```bat
compilar.bat
```

Si el programa está corriendo, el `.exe` queda bloqueado: cerrarlo antes (`Stop-Process -Name Brillo`) y luego compilar.

La PC tiene **Smart App Control** activado: a veces bloquea un `.exe` recién compilado ("Una directiva de Control de aplicaciones bloqueó este archivo"). Recompilar suele alcanzar (cada build da un binario distinto). Los `.exe` en la carpeta temporal se bloquean siempre; los de prueba hay que generarlos dentro del repo y borrarlos después.

## Restricciones del código

- El compilador de .NET Framework solo soporta **C# 5**: nada de interpolación `$"..."`, `?.`, `nameof`, miembros con `=>`, `out var`, tuplas ni funciones locales. Usar `delegate { }` / lambdas simples y concatenación de strings.
- Referencias usadas: `System.Management`, `System.Windows.Forms`, `System.Drawing`. Si se agrega otra DLL, sumarla con `/r:` en `compilar.bat`.
- Textos de la interfaz y comentarios en español rioplatense.

## Estructura

- `Native`: P/Invoke a `user32` (monitores, atajos globales, primer plano, DPI), `shcore` (`GetDpiForMonitor`), `dwmapi` (esquinas redondeadas, borde, barra de título oscura) y `dxva2` (DDC/CI).
- `Dpi`: el proceso es *Per-Monitor V2*. La ventana del brillo se dibuja a mano escalada al DPI del monitor donde se abre (el LG está a 100 %, la laptop a 125 %). El menú del icono se crea dentro de `Dpi.ComoSistema(...)` para que Windows lo escale solo. Código que use coordenadas de pantalla para la ventana va dentro de `Dpi.PorMonitor(...)`. No usar la clase `Screen` (cachea coordenadas y se mezcla con los dos modos de DPI): usar `Dpi.Monitor`.
- `Pantalla` (abstracta) → `PantallaDdc` (monitores externos; toma el nombre real de `WmiMonitorID` cruzando `EnumDisplayDevices` con el `InstanceName`) y `PantallaWmi` (panel de la laptop; guarda el `ManagementObject` de los métodos). `Leer()` devuelve 0–100 o -1; `Escribir(int)` devuelve si funcionó. `Deseado` lo escribe la interfaz, `Aplicado` lo maneja el hilo de trabajo.
- `NumerosWindows`: el número de cada pantalla en *Configuración > Pantalla* (el de "Identificar"). Windows no tiene API para eso, y `\\.\DISPLAYn` no sirve: en esta PC coincide de casualidad, pero en general no (y cambia al reconectar). Se imita el criterio del módulo DisplayConfig de PowerShell (MartinGC94): `QueryDisplayConfig(QDC_ALL_PATHS)`, destinos disponibles sin repetir, ordenados por ruta del adaptador y después por tipo de conexión (interna 50, DP embebido 100, DVI 150, DP 200, HDMI 250, resto 300) + `connectorInstance`. Es una aproximación. `DetectarTodas` asigna `Pantalla.Numero` (cruzando `Pantalla.Dispositivo` con la ruta del monitor), ordena las pantallas por ese número y solo agrega " 1", " 2" a nombres repetidos si alguna no tiene número. En esta PC: 1 = laptop (16:10, a la derecha), 2 = LG (16:9, principal).
- **Avisos**: `Pantalla.DetectarTodas(avisos)` anota en una `List<Aviso>` (título + detalle) cada pantalla que Windows ve pero no se puede ajustar, con la causa probable. `PantallaDdc.Diagnosticar` decide según el tipo de conexión de `WmiMonitorConnectionParams.VideoOutputTechnology` (16 = USB/DisplayLink, 15 = Miracast, 17 = virtual; interna = 0x80000000, 6, 11, 13), si `GetPhysicalMonitorsFromHMONITOR` falló (driver genérico) o, si no, DDC/CI apagado o cable/adaptador. El panel interno no genera aviso si anda por WMI (se reconoce también por el `InstanceName` de `WmiMonitorBrightness`). Si estamos en Escritorio remoto, se agrega un aviso aparte. Una lectura DDC fallida en un monitor externo se reintenta una vez a los 60 ms.
- `Trabajador`: hilo aparte que hace **todo** el acceso a DDC y WMI. Escribe los valores pendientes (con 40 ms de pausa entre tandas), vuelve a detectar las pantallas cuando se pide y avisa con `PantallasCambiadas`. Si una escritura falla, vuelve a detectar (como mucho cada 2 s), por si el monitor se reconectó.
- `Config`: opciones guardadas en `HKCU\Software\Brillo` (atajo, paso, tema, control "Todas las pantallas", `Nombres` de las pantallas). "Iniciar con Windows" sigue en la clave `Run`. El programa antes se llamaba *BrilloMonitor*: `Config.Migrar()` pasa la clave vieja `Software\BrilloMonitor` y el valor `BrilloMonitor` de `Run` al nombre nuevo.
- `Tema`: colores oscuro o claro (según `SystemUsesLightTheme` o lo elegido) más el color de énfasis de Windows (`HKCU\Software\Microsoft\Windows\DWM\AccentColor`).
- `Flotante` (abstracta): base de las dos ventanas emergentes. Sin bordes, con esquinas redondeadas (DWM), ubicada junto a la barra de tareas del monitor donde está el mouse y dibujada a mano según su DPI (`E(px)` escala, `Fuente(familia, px)`, `Rellenar` / `Bordear` con esquinas redondeadas). Se esconde con `Deactivate` o si cambia la ventana en primer plano (un `Timer` lo vigila por si nunca llegó a activarse). Las derivadas implementan `Distribuir()` y, si hace falta, `AlMostrar()` / `AlOcultar()`.
- `Ventana` (hereda de `Flotante`): al estilo del mezclador de volumen de Windows. Una cabecera de color con "Todas las pantallas" (o la única pantalla) y el engranaje, y una fila por pantalla (icono con el número de Windows adentro, nombre arriba del deslizador si `Config.Nombres` está activado (la fila pasa de 46 a 58 px), valor). El glifo de la laptop se dibuja más grande (28 px en vez de 22) para que entre el número. Los iconos son glifos de `Segoe Fluent Icons` / `Segoe MDL2 Assets`. También maneja el `NotifyIcon` y el atajo global.
  - Cada vez que se muestra, vuelve a detectar las pantallas y releer el brillo (también ante `DisplaySettingsChanged`).
  - Mientras está abierta, un `Timer` llama cada 1 s a `Trabajador.Releer()` (salvo durante un arrastre), así se ve si el brillo se cambió con los botones del monitor o las teclas de la laptop. `Releer` saltea las pantallas con un cambio pendiente y no lee nada hasta 1,5 s después de la última escritura (algunos monitores todavía informan el valor viejo); solo avisa con `PantallasCambiadas` si algo cambió.
  - Avisos: si hay pantallas para ajustar, una línea al pie (icono de advertencia + resumen + flecha) que se despliega con un clic; los detalles aparecen **encima** de esa línea, así queda bajo el mouse cuando la ventana crece hacia arriba. Si no hay ninguna pantalla, los avisos se muestran directamente.
  - Teclado: flechas ±paso, RePág/AvPág ±10, Inicio/Fin, 1–9 = 10–90 %, 0 = 100 %, Tab cambia de fila, Esc cierra. Rueda: ±paso.
  - Atajo global (`RegisterHotKey`, por defecto Ctrl+Alt+B) para abrir y cerrar. Mientras el panel de configuración está abierto queda desregistrado, y se vuelve a registrar con el evento `Ocultada` del panel.
- `PanelConfig` (hereda de `Flotante`): configuración con el mismo estilo. Cabecera con flecha para volver y tarjetas al estilo de Windows 11: atajo (clic y apretar la combinación; se prueba con `RegisterHotKey` antes de aceptarlo), paso (− / +), tema (segmentos), interruptores para "Todas las pantallas", "Nombres de las pantallas" e "Iniciar con Windows". Los cambios se aplican al instante con el `Action<Config>` que le pasa `Ventana`. Teclado: Tab o ↑↓ para moverse, ←→ para cambiar valores, Espacio/Enter para activar, Retroceso para volver, Esc para cerrar.
- `Programa.Main`: instancia única mediante un `EventWaitHandle` llamado `Brillo_Mostrar`; una segunda copia llama a `AllowSetForegroundWindow` y le avisa a la primera que se muestre. También escucha `Brillo_Salir`: el instalador y el desinstalador lo usan para que la copia abierta se cierre sola (y saque bien su icono) antes de reemplazar o borrar el `.exe`.
- `Instalacion` (en `Brillo.cs`): instalación **por usuario**, sin administrador. Copia a `%LOCALAPPDATA%\Programs\Brillo\Brillo.exe`, crea `Brillo.lnk` en el menú Inicio (y opcionalmente en el escritorio) con el COM `WScript.Shell` por reflexión, escribe el `Run` con la ruta instalada (`Config.CambiarInicio(bool, ruta)`) y la entrada `HKCU\...\Uninstall\Brillo` con `UninstallString = "Brillo.exe" --desinstalar`. `Desinstalar()` borra todo eso más `HKCU\Software\Brillo`, y el propio `.exe` lo borra un `cmd` unos segundos después. La versión está en `Instalacion.VERSION` (se usa también en los atributos de ensamblado y en "Aplicaciones instaladas").
- `Tienda` (en `Brillo.cs`, solo con `TIENDA`): dentro del paquete el valor `Run` del registro no sirve, así que `Config.InicioActivado/CambiarInicio` usan la tarea de inicio `BrilloInicio` del manifiesto (`StartupTask`, viene apagada). Las operaciones asíncronas de WinRT se esperan revisando `Status` (sin `await`/`AsTask`, que pedirían el `Windows.winmd` del SDK). Los tipos de Windows van con nombre completo: un `using Windows.Foundation` choca con `Point`/`Size` de `System.Drawing`. Al abrirse por la tarea de inicio (`AppInstance.GetActivatedEventArgs().Kind == StartupTask`) arranca oculto. Como la Store no deja preguntar al instalar, la primera vez que se abre (`Config.PrimeraVez`, valor `Bienvenida`) pregunta si quiere iniciar con Windows. Sin `--desinstalar` ni migración de BrilloMonitor.
- Versión: al publicar una nueva, subir `Instalacion.VERSION` y `Version` en `tienda\AppxManifest.xml` (formato `x.y.z.0`).
- `VentanaInstalador` (en `Instalador.cs`): ventana normal (con barra de título del color de la cabecera vía DWM), mismo estilo que el resto: tarjetas con interruptores "Iniciar con Windows" (activado por defecto) y "Acceso directo en el escritorio", botones Cancelar / Instalar (o Actualizar si ya está instalado) y una pantalla final con instrucciones. Maneja `WM_DPICHANGED` para rehacer el diseño si se la mueve a otro monitor.

## Uso por línea de comandos

- `Brillo.exe`: abre el icono y la ventana.
- `Brillo.exe --oculto`: solo el icono (así lo lanza el inicio automático).
- `Brillo.exe 40`: pone todas las pantallas al 40 % y sale (no usa la instancia única).
- `Brillo.exe --desinstalar`: pregunta y desinstala (lo llama Windows desde "Aplicaciones instaladas").

"Iniciar con Windows" escribe el valor `Brillo` en `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

## Probar cambios

- Leer el brillo actual del LG: llamar a `GetVCPFeatureAndVCPFeatureReply(h, 0x10, ...)` desde un script de PowerShell con `Add-Type`.
- Leer el de la laptop: `(Get-CimInstance -Namespace root/WMI -ClassName WmiMonitorBrightness).CurrentBrightness`.
- Para escribir por WMI desde PowerShell hay que invocar el método **sobre la instancia**: `Get-CimInstance ... WmiMonitorBrightnessMethods | Invoke-CimMethod -MethodName WmiSetBrightness -Arguments @{Timeout=[uint32]1;Brightness=[byte]N}`. Llamarlo con `-ClassName` falla con "Parámetros de método no válidos".
- Para ver la ventana: simular el atajo con `keybd_event` (Ctrl+Alt+B) desde PowerShell y capturar con `CopyFromScreen`, todo **en el mismo proceso** de PowerShell. Lanzarla con `Start-Process` o abrir un PowerShell nuevo le saca el foco y la ventana se esconde sola (es lo esperado). El proceso que captura tiene que llamar a `SetProcessDpiAwarenessContext(-4)` para que las coordenadas sean reales.
- No simular clics del mouse: el usuario suele estar usando la PC al mismo tiempo.
- Para ver el panel de configuración sin clics: compilar un `.exe` de prueba con `/main:` y una clase aparte que cree `PanelConfig`, le pase la config con `Cargar` y llame a `Mostrar()`. Generarlo dentro del repo (por Smart App Control), capturarlo y borrarlo.
- Instalar o desinstalar en esta PC **cambia la configuración real del usuario** (mueve el `Run` a la ruta instalada; desinstalar borra `HKCU\Software\Brillo`). Preguntar antes, y si se prueba, respaldar esas claves y restaurarlas. Para ver solo la ventana del instalador: abrirlo, capturarlo y cerrarlo con `CloseMainWindow()` sin tocar "Instalar".
- Después de probar, devolver el brillo al valor que tenía el usuario.

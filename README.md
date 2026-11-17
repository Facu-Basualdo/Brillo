# Brillo

Un programa chico para Windows que cambia el brillo de tus pantallas desde un icono en la barra de tareas, sin tener que usar los botones del monitor.

- Un deslizador por pantalla, más uno para mover todas a la vez.
- Funciona con **monitores externos** (por DDC/CI) y con la **pantalla de la laptop**.
- Atajo de teclado global para abrirlo (por defecto **Ctrl + Alt + B**).
- Tema claro u oscuro y el color de énfasis de Windows.
- Instalador de un solo archivo, sin permisos de administrador. También se puede usar el `.exe` suelto, sin instalar.

## Requisitos

- Windows 10 u 11.
- .NET Framework 4 (ya viene con Windows).

## Instalarlo

Abrí **`Instalar Brillo.exe`** y elegí:

- **Iniciar con Windows**: arranca oculto, con el icono junto al reloj (viene activado).
- **Acceso directo en el escritorio** (en el menú Inicio se crea siempre).

Se instala solo para tu usuario, en `%LOCALAPPDATA%\Programs\Brillo`, sin pedir permisos de administrador. Al terminar, Brillo queda abierto.

Para actualizarlo, abrí el instalador nuevo: cierra la versión abierta y la reemplaza, sin perder las opciones.

Para desinstalarlo: *Configuración de Windows > Aplicaciones > Aplicaciones instaladas > Brillo > Desinstalar*. Se borran el programa, los accesos directos, el inicio automático y las opciones.

### La primera vez que lo abrís

El instalador no está firmado digitalmente (un certificado cuesta plata), así que Windows desconfía:

- **"Windows protegió tu PC"** (SmartScreen): tocá *Más información* y después *Ejecutar de todas formas*.
- **"Una directiva de Control de aplicaciones bloqueó este archivo"** (Smart App Control): este bloqueo no tiene botón para saltearlo. Solo se puede instalar desactivando Smart App Control en *Seguridad de Windows > Control de aplicaciones y navegador* (ojo: según la versión de Windows, una vez apagado puede que no se pueda volver a prender sin reinstalar Windows).

Además, Windows 11 esconde los iconos nuevos de la barra de tareas: si no ves el sol junto al reloj, tocá la flechita **^** y arrastralo afuera. También se abre con **Ctrl + Alt + B**.

## Compilarlo

Con el compilador de C# que trae Windows (no hace falta Visual Studio):

```bat
compilar.bat
```

Genera `Brillo.exe` y `Instalar Brillo.exe` (que lleva a `Brillo.exe` adentro). Para pasárselo a alguien alcanza con `Instalar Brillo.exe`. Algunos servicios de mail no dejan adjuntar `.exe` (ni dentro de un `.zip`): en ese caso, compartilo por Google Drive, OneDrive o un *Release* de GitHub.

> Si en tu PC Smart App Control bloquea un `.exe` recién compilado, compilá de nuevo: cada compilación genera un archivo distinto y casi siempre alguna pasa.

### Paquete para la Microsoft Store

```bat
powershell -ExecutionPolicy Bypass -File herramientas\bajar-sdk.ps1   :: solo la primera vez (baja makeappx y makepri)
empaquetar.bat
```

Genera `tienda\Brillo.msix`, que se sube al Centro de partners. La Store lo firma, así que quien lo instala desde ahí no ve avisos de seguridad. En esa versión, *Iniciar con Windows* viene apagado y Brillo lo pregunta la primera vez que se abre.

## Uso

Al abrirlo aparece un icono en el área de notificación (junto al reloj).

- **Clic izquierdo** en el icono o el atajo: abre o cierra la ventana del brillo.
- **Clic derecho**: menú con *Configuración*, *Iniciar con Windows* y *Salir*.

Cada pantalla aparece con su nombre (por ejemplo *LG ULTRAGEAR* o *Pantalla de la laptop*; se puede ocultar en *Configuración*) y, dentro del icono, el mismo número que le pone Windows en *Configuración > Sistema > Pantalla* (el de *Identificar*); las filas van en ese orden. Si cambiás el brillo con los botones del monitor o con las teclas de la laptop mientras la ventana está abierta, se actualiza sola.

En la ventana:

| Tecla | Acción |
|---|---|
| ← → ↑ ↓ / rueda del mouse | Sube o baja según el paso elegido (5 % por defecto) |
| RePág / AvPág | ±10 % |
| Inicio / Fin | 0 % / 100 % |
| 1 … 9 / 0 | 10 % … 90 % / 100 % |
| Tab | Pasa a la pantalla siguiente |
| Esc | Cierra |

En *Configuración* se elige el atajo, el paso, el tema, si se muestra el control "Todas las pantallas", si se muestran los nombres de las pantallas y si arranca con Windows.

### Desde la línea de comandos

```bat
Brillo.exe            :: abre el icono y la ventana
Brillo.exe --oculto   :: solo el icono (así lo abre el inicio automático)
Brillo.exe 40         :: pone todas las pantallas al 40 % y sale
Brillo.exe --desinstalar  :: desinstala (es lo que usa Windows desde "Aplicaciones instaladas")
```

El último sirve para crear accesos directos o tareas programadas (por ejemplo, bajar el brillo a la noche).

## Compatibilidad

Brillo no está hecho para un modelo en particular: busca todas las pantallas conectadas y usa las que dejan cambiar el brillo.

- **Monitores externos:** se controlan por **DDC/CI**, un estándar que tienen casi todos los monitores modernos. A veces viene apagado de fábrica: buscalo en el menú del monitor (suele estar en *Sistema*, *Configuración* u *Otros*).
- **Pantalla de la laptop:** se controla igual que el brillo de Windows, así que funciona si el control de brillo de Windows funciona.

### Si una pantalla no aparece

Si una pantalla está conectada pero no se puede ajustar, Brillo lo dice al pie de la ventana. Hacé clic en el aviso para ver la causa:

| Aviso | Qué pasa | Qué hacer |
|---|---|---|
| *no responde* | El monitor no contestó por DDC/CI. | Activá DDC/CI en el menú del monitor. Si pasa por un adaptador, dock, KVM o hub, probá con un cable directo (HDMI o DisplayPort). Los televisores casi nunca lo soportan. |
| *conectado por USB* | Las pantallas por USB (DisplayLink o similar) no dejan cambiar el brillo desde la PC. | Conectalo por HDMI o DisplayPort, o usá los botones del monitor. |
| *pantalla inalámbrica* | Miracast no deja cambiar el brillo. | — |
| *pantalla virtual* | Es una pantalla creada por un programa (escritorio remoto, captura, etc.). | — |
| *sin acceso* | Windows no da acceso al monitor; suele pasar con el driver de video genérico de Microsoft. | Instalá el driver de la placa de video (NVIDIA, AMD o Intel). |
| *Pantalla de la laptop: sin control de brillo* | Falta el driver de video del fabricante. | Actualizalo desde Windows Update o desde la página del fabricante. |
| *Sesión de Escritorio remoto* | Por Escritorio remoto, Windows no deja cambiar el brillo. | Usalo directamente en la PC. |

## Dónde guarda las cosas

- Programa (si lo instalaste): `%LOCALAPPDATA%\Programs\Brillo\Brillo.exe`.
- Opciones: `HKEY_CURRENT_USER\Software\Brillo`.
- Inicio automático: el valor `Brillo` en `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`.
- Entrada de desinstalación: `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\Brillo`.

Si usás el `.exe` suelto (sin instalar), para sacarlo: desactivá *Iniciar con Windows*, cerralo, borrá el `.exe` y, si querés, la clave de opciones.

> Las versiones anteriores se llamaban *BrilloMonitor*. La primera vez que abrís `Brillo.exe`, pasa solas las opciones y el inicio automático al nombre nuevo.

## Licencia

[MIT](LICENSE).

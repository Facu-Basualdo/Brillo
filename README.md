# Brillo

Un programa chico para Windows que cambia el brillo de tus pantallas desde un icono en la barra de tareas, sin tener que usar los botones del monitor.

- Un deslizador por pantalla, más uno para mover todas a la vez.
- Funciona con **monitores externos** (por DDC/CI) y con la **pantalla de la laptop**.
- Atajo de teclado global para abrirlo (por defecto **Ctrl + Alt + B**).
- Tema claro u oscuro y el color de énfasis de Windows.
- Un solo `.exe` de unos 60 KB. No necesita instalación ni permisos de administrador.

## Requisitos

- Windows 10 u 11.
- .NET Framework 4 (ya viene con Windows).

## Cómo conseguirlo

Compilalo con el compilador de C# que trae Windows (no hace falta Visual Studio):

```bat
compilar.bat
```

Eso genera `Brillo.exe` en la misma carpeta. Copialo donde quieras y ejecutalo.

> Si Windows dice que "una directiva de Control de aplicaciones bloqueó este archivo" (Smart App Control), compilá de nuevo: cada compilación genera un archivo distinto y casi siempre la segunda pasa.

## Uso

Al abrirlo aparece un icono en el área de notificación (junto al reloj).

- **Clic izquierdo** en el icono o el atajo: abre o cierra la ventana del brillo.
- **Clic derecho**: menú con *Configuración*, *Iniciar con Windows* y *Salir*.

En la ventana:

| Tecla | Acción |
|---|---|
| ← → ↑ ↓ / rueda del mouse | Sube o baja según el paso elegido (5 % por defecto) |
| RePág / AvPág | ±10 % |
| Inicio / Fin | 0 % / 100 % |
| 1 … 9 / 0 | 10 % … 90 % / 100 % |
| Tab | Pasa a la pantalla siguiente |
| Esc | Cierra |

En *Configuración* se elige el atajo, el paso, el tema, si se muestra el control "Todas las pantallas" y si arranca con Windows.

### Desde la línea de comandos

```bat
Brillo.exe            :: abre el icono y la ventana
Brillo.exe --oculto   :: solo el icono (así lo abre el inicio automático)
Brillo.exe 40         :: pone todas las pantallas al 40 % y sale
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

- Opciones: `HKEY_CURRENT_USER\Software\Brillo`.
- Inicio automático: el valor `Brillo` en `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`.

Para desinstalarlo: desactivá *Iniciar con Windows*, cerralo, borrá el `.exe` y, si querés, la clave del registro.

> Las versiones anteriores se llamaban *BrilloMonitor*. La primera vez que abrís `Brillo.exe`, pasa solas las opciones y el inicio automático al nombre nuevo.

## Licencia

[MIT](LICENSE).

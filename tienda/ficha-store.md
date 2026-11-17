# Ficha de Brillo en la Microsoft Store (es-AR)

Textos para pegar en el Centro de partners → Envío → Descripción de la Store (Español (Argentina)).

## Descripción

```
Brillo te deja cambiar el brillo de tus pantallas desde la barra de tareas, sin pelearte con los botones del monitor.

Hacé clic en el sol que aparece junto al reloj (o apretá Ctrl + Alt + B) y se abre una ventanita con un deslizador por cada pantalla: el monitor externo, la pantalla de la laptop o todas a la vez.

Funciona con:
• Monitores externos conectados por HDMI, DisplayPort o USB-C, a través de DDC/CI, el estándar que traen casi todos los monitores modernos.
• La pantalla de tu laptop, igual que el control de brillo de Windows.

Si una pantalla no se puede ajustar, Brillo te dice por qué (por ejemplo, que DDC/CI está apagado en el menú del monitor o que está conectada por un adaptador USB) y qué podés hacer.

Pensado para usarlo rápido:
• Rueda del mouse, flechas o números del 1 al 0 para ir de 10 % a 100 %.
• Atajo de teclado global que podés cambiar.
• Opción de iniciar con Windows, oculto, con solo el icono junto al reloj.
• Tema claro u oscuro, con el color de énfasis de Windows.
• Se ve nítido aunque tus pantallas tengan distinta escala.

Liviano, sin publicidad, sin cuentas y sin conexión a internet: no junta ni envía ningún dato.

Para los monitores externos, DDC/CI tiene que estar activado en el menú del monitor (en muchos viene así de fábrica).
```

## Características del producto

```
Un deslizador por pantalla, más uno para mover todas a la vez
Monitores externos por DDC/CI y pantalla de la laptop
Se abre desde el icono junto al reloj o con un atajo de teclado
Rueda del mouse, flechas y números para cambiar el brillo rápido
Avisos claros cuando una pantalla no se puede ajustar, con la causa y qué hacer
Opción de iniciar con Windows
Tema claro u oscuro con el color de énfasis de Windows
Sin publicidad y sin recopilar datos
```

## Términos de búsqueda

```
brillo
brillo monitor
DDC/CI
brightness
monitor externo
bajar brillo
pantalla
```

## Copyright

```
© 2026 FacuBas
```

## Notas para la certificación (Opciones de envío)

```
La app no necesita cuenta ni conexión a internet. Al abrirla aparece un icono de sol en el área de notificación (junto al reloj); con un clic o con Ctrl + Alt + B se abre la ventana de brillo.

Para ajustar un monitor externo hace falta un monitor con DDC/CI activado; la pantalla integrada de una laptop se ajusta por WMI. En una máquina virtual o un equipo sin pantallas compatibles, la app no muestra deslizadores y en su lugar explica por qué ninguna pantalla se puede ajustar: es el comportamiento esperado.

La primera vez que se abre pregunta si se quiere iniciar con Windows (tarea de inicio del paquete, apagada por defecto).
```

## Justificación de runFullTrust (Opciones de envío → capacidades restringidas)

```
Brillo es una aplicación de escritorio que ajusta el brillo de los monitores externos mediante DDC/CI (API de Windows dxva2) y el de la pantalla integrada mediante WMI, y muestra un icono en el área de notificación con un atajo de teclado global. Estas funciones requieren ejecutarse como aplicación de escritorio de plena confianza.
```

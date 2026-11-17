# Genera brillo.ico (el icono de Brillo.exe y del instalador): un cuadrado redondeado azul con el sol de Windows en blanco.
# Uso: powershell -ExecutionPolicy Bypass -File herramientas\crear-icono.ps1
# Solo hace falta correrlo si se quiere cambiar el diseño; el .ico resultante queda en el repo.

param([string]$salida = (Join-Path (Split-Path $PSScriptRoot) "brillo.ico"))

Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class CreadorIcono
{
    static Bitmap Dibujar(int tam)
    {
        var bmp = new Bitmap(tam, tam, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            // Fondo: cuadrado redondeado con un degradé del azul de Windows.
            float margen = tam <= 20 ? 0 : tam * 0.04f;
            var r = new RectangleF(margen, margen, tam - 2 * margen, tam - 2 * margen);
            float d = r.Width * 0.44f;
            using (var camino = new GraphicsPath())
            {
                camino.AddArc(r.X, r.Y, d, d, 180, 90);
                camino.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                camino.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                camino.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                camino.CloseFigure();
                using (var degrade = new LinearGradientBrush(r, Color.FromArgb(38, 150, 240), Color.FromArgb(0, 92, 184), 90f))
                    g.FillPath(degrade, camino);
            }

            // El sol: glifo de brillo de Segoe Fluent Icons (o MDL2), engrosado un poco para que se lea en chico.
            string fuente = "Segoe Fluent Icons";
            try { using (new FontFamily(fuente)) { } } catch { fuente = "Segoe MDL2 Assets"; }
            using (var familia = new FontFamily(fuente))
            using (var sol = new GraphicsPath())
            {
                sol.AddString(((char)0xE706).ToString(), familia, 0, tam, PointF.Empty, StringFormat.GenericTypographic);
                RectangleF b = sol.GetBounds();
                float factor = tam * 0.66f / Math.Max(b.Width, b.Height);
                using (var m = new Matrix())
                {
                    m.Translate(tam / 2f, tam / 2f);
                    m.Scale(factor, factor);
                    m.Translate(-(b.Left + b.Width / 2), -(b.Top + b.Height / 2));
                    sol.Transform(m);
                }
                g.FillPath(Brushes.White, sol);
                using (var lapiz = new Pen(Color.White, Math.Max(0.6f, tam * 0.035f)) { LineJoin = LineJoin.Round })
                    g.DrawPath(lapiz, sol);
            }
        }
        return bmp;
    }

    // Imagen en formato DIB (BMP de 32 bits sin el encabezado de archivo), como la guardan los .ico clásicos.
    static byte[] Dib(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        int filaMascara = ((w + 31) / 32) * 4;
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(40); bw.Write(w); bw.Write(h * 2); bw.Write((short)1); bw.Write((short)32);
            bw.Write(0); bw.Write(w * h * 4 + filaMascara * h); bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
            for (int y = h - 1; y >= 0; y--)
                for (int x = 0; x < w; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    bw.Write(c.B); bw.Write(c.G); bw.Write(c.R); bw.Write(c.A);
                }
            bw.Write(new byte[filaMascara * h]); // máscara AND vacía: la transparencia la da el canal alfa
            return ms.ToArray();
        }
    }

    // PNG de "lienzo" x "lienzo" con el icono de "icono" x "icono" centrado y el resto transparente
    // (los logos del paquete de la Store).
    public static void GuardarPng(string ruta, int lienzo, int icono)
    {
        using (var fondo = new Bitmap(lienzo, lienzo, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(fondo))
            using (var img = Dibujar(icono))
            {
                g.Clear(Color.Transparent);
                g.DrawImageUnscaled(img, (lienzo - icono) / 2, (lienzo - icono) / 2);
            }
            fondo.Save(ruta, ImageFormat.Png);
        }
    }

    public static void Guardar(string ruta, int[] tamanios)
    {
        var imagenes = new List<byte[]>();
        foreach (int t in tamanios)
            using (var bmp = Dibujar(t))
            {
                if (t >= 256) { using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); imagenes.Add(ms.ToArray()); } }
                else imagenes.Add(Dib(bmp));
            }

        using (var fs = File.Create(ruta))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write((short)0); bw.Write((short)1); bw.Write((short)tamanios.Length);
            int desplazamiento = 6 + 16 * tamanios.Length;
            for (int i = 0; i < tamanios.Length; i++)
            {
                int t = tamanios[i];
                bw.Write((byte)(t >= 256 ? 0 : t)); bw.Write((byte)(t >= 256 ? 0 : t));
                bw.Write((byte)0); bw.Write((byte)0); bw.Write((short)1); bw.Write((short)32);
                bw.Write(imagenes[i].Length); bw.Write(desplazamiento);
                desplazamiento += imagenes[i].Length;
            }
            foreach (var img in imagenes) bw.Write(img);
        }
    }
}
'@

[CreadorIcono]::Guardar($salida, [int[]](16, 20, 24, 32, 40, 48, 64, 256))
"Icono guardado en $salida"

# Logos del paquete MSIX (tienda\Assets). makepri elige el tamaño según la escala de la pantalla.
$assets = Join-Path (Split-Path $PSScriptRoot) "tienda\Assets"
New-Item -ItemType Directory -Force $assets | Out-Null
foreach ($escala in 100, 125, 150, 200, 400) {
    $f = $escala / 100
    # Icono de la app (lista de Inicio, barra de tareas): ocupa todo el cuadro.
    [CreadorIcono]::GuardarPng("$assets\Square44x44Logo.scale-$escala.png", [int][Math]::Round(44 * $f), [int][Math]::Round(44 * $f))
    # Mosaico mediano de Inicio: el icono más chico, con aire alrededor.
    [CreadorIcono]::GuardarPng("$assets\Square150x150Logo.scale-$escala.png", [int][Math]::Round(150 * $f), [int][Math]::Round(96 * $f))
    # Logo de la Store y del instalador de paquetes.
    [CreadorIcono]::GuardarPng("$assets\StoreLogo.scale-$escala.png", [int][Math]::Round(50 * $f), [int][Math]::Round(50 * $f))
}
# Tamaños exactos para la barra de tareas y el Explorador (con y sin "placa" de color detrás).
foreach ($t in 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256) {
    [CreadorIcono]::GuardarPng("$assets\Square44x44Logo.targetsize-$t.png", $t, $t)
    [CreadorIcono]::GuardarPng("$assets\Square44x44Logo.targetsize-${t}_altform-unplated.png", $t, $t)
    [CreadorIcono]::GuardarPng("$assets\Square44x44Logo.targetsize-${t}_altform-lightunplated.png", $t, $t)
}
"Logos de la tienda guardados en $assets"

# Logo cuadrado para la ficha de la Store (se sube a mano en el Centro de partners, no va en el paquete).
$logoStore = Join-Path (Split-Path $PSScriptRoot) "tienda\logo-store-1080.png"
[CreadorIcono]::GuardarPng($logoStore, 1080, 960)
"Logo de la ficha guardado en $logoStore"

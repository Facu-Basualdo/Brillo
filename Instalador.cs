// Instalador de Brillo ("Instalar Brillo.exe").
// Trae Brillo.exe adentro como recurso, lo copia a la carpeta del usuario, crea los accesos directos y,
// si se elige, lo deja en el inicio de Windows. No pide permisos de administrador.
// Se compila junto con Brillo.cs (usa Tema, Dpi, Native, Config, Instalacion y los helpers de dibujo de Flotante);
// ver compilar.bat.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Instalador de Brillo")]
[assembly: System.Reflection.AssemblyDescription("Instala Brillo para el usuario actual")]
[assembly: System.Reflection.AssemblyProduct("Brillo")]
[assembly: System.Reflection.AssemblyVersion(Instalacion.VERSION)]
[assembly: System.Reflection.AssemblyFileVersion(Instalacion.VERSION)]

static class ProgramaInstalador
{
    [STAThread]
    static void Main()
    {
        Dpi.Iniciar();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new VentanaInstalador());
    }
}

// Ventana del instalador, con el mismo estilo que la de Brillo: cabecera de color, tarjetas e interruptores.
class VentanaInstalador : Form
{
    const string GLIFO_SOL = "", GLIFO_LISTO = "";
    const int WM_DPICHANGED = 0x02E0;
    const TextFormatFlags LINEA = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine |
                                  TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
    const TextFormatFlags CENTRADO = LINEA | TextFormatFlags.HorizontalCenter;
    const TextFormatFlags PARRAFO = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;

    readonly Tema tema = Tema.Actual(0);
    readonly string fuenteIconos;
    readonly bool yaInstalado;
    float escala = 1f;
    bool alIniciar = true, escritorio, listo, focoVisible;
    string error;
    string sobre, apretado;
    int foco;

    Rectangle rIcono, rTitulo, rSubtitulo, rTexto, rError, rPrincipal, rSecundario;
    Rectangle tarjetaInicio, tarjetaEscritorio, rInicio, rEscritorio;
    int altoCabecera;
    Font fTitulo, fSubtitulo, fTexto, fAyuda, fBoton, fIconoGrande, fIconoListo, fGrande;

    public VentanaInstalador()
    {
        Text = "Instalar Brillo";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        fuenteIconos = ExisteFuente("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
        yaInstalado = Instalacion.Instalado;
        if (yaInstalado)
        {
            try { alIniciar = Config.InicioActivado(); } catch { }
            escritorio = Instalacion.TieneAccesoEscritorio;
        }

        // Tamaño según el DPI del monitor donde está el mouse, centrada en ese monitor.
        Rectangle trabajo, total;
        int dpi;
        Dpi.Monitor(Cursor.Position, out trabajo, out total, out dpi);
        escala = dpi / 96f;
        Distribuir();
        if (!trabajo.IsEmpty)
            Location = new Point(trabajo.Left + (trabajo.Width - Width) / 2, trabajo.Top + (trabajo.Height - Height) / 2);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            // Barra de título oscura y del mismo color que la cabecera (Windows 11; en Windows 10 se ignora).
            int oscuro = tema.Oscuro ? 1 : 0;
            Native.DwmSetWindowAttribute(Handle, 20, ref oscuro, 4);
            int titulo = Colorref(tema.Cabecera), texto = Colorref(tema.TextoCabecera), borde = Colorref(tema.Borde);
            Native.DwmSetWindowAttribute(Handle, 35, ref titulo, 4);
            Native.DwmSetWindowAttribute(Handle, 36, ref texto, 4);
            Native.DwmSetWindowAttribute(Handle, 34, ref borde, 4);
        }
        catch { }
    }

    static int Colorref(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_DPICHANGED)
        {
            // Se movió a un monitor con otra escala: rehacemos el diseño y usamos la posición que sugiere Windows.
            escala = (int)(m.WParam.ToInt64() & 0xFFFF) / 96f;
            var sugerido = (Native.RECT)Marshal.PtrToStructure(m.LParam, typeof(Native.RECT));
            Distribuir();
            Location = new Point(sugerido.Left, sugerido.Top);
            return;
        }
        base.WndProc(ref m);
    }

    // ---------- Diseño ----------

    int E(float v) { return (int)Math.Round(v * escala); }

    Font Fuente(string familia, float px)
    {
        return new Font(familia, px * escala, FontStyle.Regular, GraphicsUnit.Pixel);
    }

    void Distribuir()
    {
        foreach (var f in new[] { fTitulo, fSubtitulo, fTexto, fAyuda, fBoton, fIconoGrande, fIconoListo, fGrande })
            if (f != null) f.Dispose();
        fIconoListo = Fuente(fuenteIconos, 20);
        fTitulo = Fuente("Segoe UI Semibold", 24);
        fSubtitulo = Fuente("Segoe UI", 13);
        fTexto = Fuente("Segoe UI", 14);
        fAyuda = Fuente("Segoe UI", 12);
        fBoton = Fuente("Segoe UI", 14);
        fIconoGrande = Fuente(fuenteIconos, 34);
        fGrande = Fuente("Segoe UI Semibold", 17);

        int ancho = E(440);
        altoCabecera = E(104);
        rIcono = new Rectangle(E(24), E(28), E(48), E(48));
        rTitulo = Rectangle.FromLTRB(E(88), E(24), ancho - E(20), E(58));
        rSubtitulo = Rectangle.FromLTRB(E(88), E(58), ancho - E(20), E(80));

        int y = altoCabecera + E(20);
        rTexto = new Rectangle(E(24), y, ancho - E(48), E(40));
        y += E(48);
        tarjetaInicio = new Rectangle(E(16), y, ancho - E(32), E(64));
        y += E(68);
        tarjetaEscritorio = new Rectangle(E(16), y, ancho - E(32), E(64));
        rInicio = new Rectangle(tarjetaInicio.Right - E(16) - E(40), tarjetaInicio.Top + E(22), E(40), E(20));
        rEscritorio = new Rectangle(tarjetaEscritorio.Right - E(16) - E(40), tarjetaEscritorio.Top + E(22), E(40), E(20));
        y += E(64) + E(10);
        rError = new Rectangle(E(24), y, ancho - E(48), E(36));
        y += E(44);

        rPrincipal = new Rectangle(ancho - E(24) - E(120), y, E(120), E(36));
        rSecundario = new Rectangle(rPrincipal.Left - E(8) - E(110), y, E(110), E(36));
        y += E(36) + E(20);

        ClientSize = new Size(ancho, y);
        Invalidate();
    }

    // ---------- Dibujo ----------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(tema.Fondo);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using (var b = new SolidBrush(tema.Cabecera)) g.FillRectangle(b, 0, 0, ClientSize.Width, altoCabecera);
        TextRenderer.DrawText(g, GLIFO_SOL, fIconoGrande, rIcono, tema.TextoCabecera, CENTRADO);
        TextRenderer.DrawText(g, "Brillo", fTitulo, rTitulo, tema.TextoCabecera, LINEA | TextFormatFlags.Left);
        TextRenderer.DrawText(g, "El brillo de tus pantallas, desde la barra de tareas", fSubtitulo, rSubtitulo,
            tema.TextoCabecera, LINEA | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        if (listo) DibujarListo(g);
        else DibujarOpciones(g);

        if (focoVisible)
            Flotante.Bordear(g, Rectangle.Inflate(RectFoco(), E(3), E(3)), E(6), tema.Foco, Math.Max(2, E(2)));
    }

    void DibujarOpciones(Graphics g)
    {
        string texto = yaInstalado
            ? "Brillo ya está instalado. Podés actualizarlo o cambiar las opciones."
            : "Se instala solo para tu usuario y no pide permisos de administrador.";
        TextRenderer.DrawText(g, texto, fAyuda, rTexto, tema.TextoSuave, PARRAFO);

        Tarjeta(g, tarjetaInicio, "Iniciar con Windows", "Arranca oculto, con el icono junto al reloj");
        Interruptor(g, rInicio, alIniciar, sobre == "inicio");
        Tarjeta(g, tarjetaEscritorio, "Acceso directo en el escritorio", "En el menú Inicio se crea siempre");
        Interruptor(g, rEscritorio, escritorio, sobre == "escritorio");

        if (error != null) TextRenderer.DrawText(g, error, fAyuda, rError, tema.Error, PARRAFO);

        Boton(g, rSecundario, "Cancelar", "secundario", false);
        Boton(g, rPrincipal, yaInstalado ? "Actualizar" : "Instalar", "principal", true);
    }

    void DibujarListo(Graphics g)
    {
        var rCheck = new Rectangle(E(24), altoCabecera + E(24), E(28), E(28));
        TextRenderer.DrawText(g, GLIFO_LISTO, fIconoListo, rCheck, tema.Acento, CENTRADO);
        TextRenderer.DrawText(g, "Listo, Brillo quedó instalado", fGrande,
            Rectangle.FromLTRB(rCheck.Right + E(10), rCheck.Top, ClientSize.Width - E(24), rCheck.Bottom), tema.Texto, LINEA | TextFormatFlags.Left);

        string atajo = "Ctrl + Alt + B";
        try { var c = Config.Cargar(); atajo = Config.TextoAtajo(c.AtajoMods, c.AtajoTecla); } catch { }
        string texto =
            "Ya está abierto: es el sol que aparece junto al reloj. Hacé clic en él para cambiar el brillo, " +
            "o usá el atajo " + atajo + ".\n\n" +
            "Si no ves el sol, tocá la flechita ^ de la barra de tareas y arrastralo afuera para que quede siempre a la vista.\n\n" +
            "Para desinstalarlo: Configuración de Windows > Aplicaciones > Aplicaciones instaladas > Brillo.";
        var r = Rectangle.FromLTRB(E(24), rCheck.Bottom + E(14), ClientSize.Width - E(24), rPrincipal.Top - E(10));
        TextRenderer.DrawText(g, texto, fAyuda, r, tema.Texto, PARRAFO);

        Boton(g, rPrincipal, "Cerrar", "principal", true);
    }

    void Tarjeta(Graphics g, Rectangle t, string titulo, string ayuda)
    {
        Flotante.Rellenar(g, t, E(6), tema.Tarjeta);
        Flotante.Bordear(g, t, E(6), tema.BordeTarjeta, 1);
        int izquierda = t.Left + E(16), limite = t.Right - E(16) - E(40) - E(12);
        int medio = t.Top + t.Height / 2;
        const TextFormatFlags flags = LINEA | TextFormatFlags.Left | TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(g, titulo, fTexto, Rectangle.FromLTRB(izquierda, medio - E(21), limite, medio), tema.Texto, flags);
        TextRenderer.DrawText(g, ayuda, fAyuda, Rectangle.FromLTRB(izquierda, medio + E(1), limite, medio + E(19)), tema.TextoSuave, flags);
    }

    void Boton(Graphics g, Rectangle r, string texto, string id, bool principal)
    {
        bool encima = sobre == id;
        if (principal)
        {
            Flotante.Rellenar(g, r, E(4), encima ? tema.AcentoActivo : tema.Acento);
            TextRenderer.DrawText(g, texto, fBoton, r, tema.SobreAcento, CENTRADO);
        }
        else
        {
            Flotante.Rellenar(g, r, E(4), encima ? tema.ControlHover : tema.Control);
            Flotante.Bordear(g, r, E(4), tema.BordeControl, 1);
            TextRenderer.DrawText(g, texto, fBoton, r, tema.Texto, CENTRADO);
        }
    }

    // Interruptor al estilo de Windows 11 (igual que en el panel de configuración).
    void Interruptor(Graphics g, Rectangle r, bool prendido, bool encima)
    {
        float radio = r.Height / 2f;
        float d = encima ? E(14) : E(12);
        float cy = r.Top + r.Height / 2f;
        float cx;
        Color perilla;
        if (prendido)
        {
            Flotante.Rellenar(g, r, radio, encima ? tema.AcentoActivo : tema.Acento);
            cx = r.Right - radio;
            perilla = tema.SobreAcento;
        }
        else
        {
            if (encima) Flotante.Rellenar(g, r, radio, tema.ControlHover);
            Flotante.Bordear(g, r, radio, tema.TextoSuave, 1);
            cx = r.Left + radio;
            perilla = tema.TextoSuave;
        }
        using (var b = new SolidBrush(perilla)) g.FillEllipse(b, cx - d / 2, cy - d / 2, d, d);
    }

    // ---------- Acciones ----------

    string[] Controles()
    {
        return listo ? new[] { "principal" } : new[] { "inicio", "escritorio", "secundario", "principal" };
    }

    Rectangle RectFoco()
    {
        switch (Controles()[Math.Min(foco, Controles().Length - 1)])
        {
            case "inicio": return rInicio;
            case "escritorio": return rEscritorio;
            case "secundario": return rSecundario;
            default: return rPrincipal;
        }
    }

    string Objetivo(Point p)
    {
        if (rPrincipal.Contains(p)) return "principal";
        if (listo) return null;
        if (rSecundario.Contains(p)) return "secundario";
        if (tarjetaInicio.Contains(p)) return "inicio";        // como en Windows, toda la tarjeta prende y apaga
        if (tarjetaEscritorio.Contains(p)) return "escritorio";
        return null;
    }

    void Accion(string id)
    {
        switch (id)
        {
            case "inicio": alIniciar = !alIniciar; break;
            case "escritorio": escritorio = !escritorio; break;
            case "secundario": Close(); return;
            case "principal":
                if (listo) { Close(); return; }
                Instalar();
                break;
        }
        Invalidate();
    }

    void Instalar()
    {
        error = null;
        Cursor = Cursors.WaitCursor;
        Refresh();
        try
        {
            byte[] programa;
            using (var s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Brillo.exe"))
            {
                if (s == null) throw new InvalidOperationException("el instalador está incompleto (no trae Brillo.exe).");
                using (var ms = new System.IO.MemoryStream()) { s.CopyTo(ms); programa = ms.ToArray(); }
            }
            Instalacion.Instalar(programa, alIniciar, escritorio);
            System.Diagnostics.Process.Start(Instalacion.Ejecutable);
            listo = true;
            foco = 0;
        }
        catch (Exception ex)
        {
            error = "No se pudo instalar: " + ex.Message;
        }
        Cursor = Cursors.Default;
        Invalidate();
    }

    // ---------- Mouse y teclado ----------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        apretado = Objetivo(e.Location);
        focoVisible = false;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        string id = Objetivo(e.Location);
        if (id != null && id == apretado) Accion(id);
        apretado = null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        string id = Objetivo(e.Location);
        if (id != sobre)
        {
            sobre = id;
            Cursor = id != null ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        sobre = null;
        Invalidate();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys tecla = keyData & Keys.KeyCode;
        bool shift = (keyData & Keys.Shift) != 0;
        string[] controles = Controles();
        switch (tecla)
        {
            case Keys.Escape: Close(); return true;
            case Keys.Tab:
                foco = (foco + (shift ? controles.Length - 1 : 1)) % controles.Length;
                focoVisible = true;
                break;
            case Keys.Enter:
                Accion(focoVisible ? controles[foco] : "principal");
                return true;
            case Keys.Space:
                if (focoVisible) Accion(controles[foco]);
                return true;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
        Invalidate();
        return true;
    }

    static bool ExisteFuente(string nombre)
    {
        try { using (new FontFamily(nombre)) return true; }
        catch { return false; }
    }
}

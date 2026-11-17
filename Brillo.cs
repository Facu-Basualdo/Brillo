// Brillo - ajusta el brillo de los monitores externos (DDC/CI) y de la pantalla de la laptop (WMI).
// Uso:
//   Brillo.exe          -> icono en la barra de tareas y ventana con un deslizador por pantalla
//   Brillo.exe --oculto -> solo el icono (así lo lanza el inicio automático)
//   Brillo.exe 40       -> pone todas las pantallas al 40% y sale (útil para accesos directos)
//   Brillo.exe --desinstalar -> desinstala (lo llama Windows desde "Aplicaciones instaladas")

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

#if !INSTALADOR
[assembly: System.Reflection.AssemblyTitle("Brillo")]
[assembly: System.Reflection.AssemblyDescription("Ajusta el brillo de las pantallas desde la barra de tareas")]
[assembly: System.Reflection.AssemblyProduct("Brillo")]
[assembly: System.Reflection.AssemblyVersion(Instalacion.VERSION)]
[assembly: System.Reflection.AssemblyFileVersion(Instalacion.VERSION)]
#endif

static class Native
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc cb, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool EnumDisplayDevices(string device, uint indice, ref DISPLAY_DEVICE dd, uint flags);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern bool AllowSetForegroundWindow(int processId);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vk);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr contexto);

    [DllImport("user32.dll")]
    public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr contexto);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr hMonitor, int tipo, out uint dpiX, out uint dpiY);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hWnd, int atributo, ref int valor, int tamanio);

    [DllImport("dxva2.dll")]
    public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint count);

    [DllImport("dxva2.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint count, [Out] PHYSICAL_MONITOR[] monitors);

    [DllImport("dxva2.dll")]
    public static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hMonitor, byte code, IntPtr type, out uint current, out uint max);

    [DllImport("dxva2.dll")]
    public static extern bool SetVCPFeature(IntPtr hMonitor, byte code, uint value);

    [DllImport("dxva2.dll")]
    public static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);
}

// Manejo de DPI. El proceso es "por monitor" para que la ventana del brillo se vea nítida en las dos
// pantallas (dibujamos todo a mano según el DPI de cada una). Los menús y el diálogo de configuración
// se crean en modo "DPI del sistema" y Windows los escala solo.
static class Dpi
{
    static readonly IntPtr POR_MONITOR = new IntPtr(-4);   // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
    static readonly IntPtr SISTEMA = new IntPtr(-2);       // DPI_AWARENESS_CONTEXT_SYSTEM_AWARE

    public static void Iniciar()
    {
        try { if (Native.SetProcessDpiAwarenessContext(POR_MONITOR)) return; } catch { }
        try { Native.SetProcessDPIAware(); } catch { }
    }

    public static void ComoSistema(Action a) { Con(SISTEMA, a); }
    public static void PorMonitor(Action a) { Con(POR_MONITOR, a); }

    static void Con(IntPtr contexto, Action a)
    {
        IntPtr anterior = IntPtr.Zero;
        try { anterior = Native.SetThreadDpiAwarenessContext(contexto); } catch { }
        try { a(); }
        finally
        {
            if (anterior != IntPtr.Zero)
                try { Native.SetThreadDpiAwarenessContext(anterior); } catch { }
        }
    }

    // Área de trabajo, tamaño total y DPI del monitor que contiene el punto.
    public static void Monitor(Point p, out Rectangle trabajo, out Rectangle total, out int dpi)
    {
        trabajo = total = Rectangle.Empty;
        dpi = 96;
        var pt = new Native.POINT { X = p.X, Y = p.Y };
        IntPtr h = Native.MonitorFromPoint(pt, 2); // MONITOR_DEFAULTTONEAREST
        var info = new Native.MONITORINFOEX();
        info.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFOEX));
        if (Native.GetMonitorInfo(h, ref info))
        {
            trabajo = Rectangle.FromLTRB(info.rcWork.Left, info.rcWork.Top, info.rcWork.Right, info.rcWork.Bottom);
            total = Rectangle.FromLTRB(info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Right, info.rcMonitor.Bottom);
        }
        try
        {
            uint x, y;
            if (Native.GetDpiForMonitor(h, 0, out x, out y) == 0 && x > 0) dpi = (int)x;
        }
        catch { }
    }
}

abstract class Pantalla
{
    public string Nombre;
    public string Clave;                // identifica la pantalla entre una detección y la siguiente
    public bool EsLaptop;
    public volatile int Deseado = -1;   // lo que pidió el usuario (lo escribe la interfaz)
    public int Aplicado = -1;           // lo último que se escribió o leyó (solo lo toca el hilo de trabajo)

    public abstract int Leer();                     // 0-100, o -1 si falla
    public abstract bool Escribir(int porcentaje);
    public virtual void Liberar() { }

    // Si se pasa una lista de avisos, ahí se anotan las pantallas que se encontraron pero no se pueden ajustar.
    public static List<Pantalla> DetectarTodas(List<Aviso> avisos)
    {
        var laptop = PantallaWmi.Detectar();
        string instanciaLaptop = laptop.Count > 0 ? ((PantallaWmi)laptop[0]).Instancia : null;
        var lista = PantallaDdc.Detectar(instanciaLaptop, laptop.Count > 0, avisos);
        lista.AddRange(laptop);

        if (avisos != null && SystemInformation.TerminalServerSession)
            avisos.Insert(0, new Aviso("Sesión de Escritorio remoto",
                "Estás usando esta PC por Escritorio remoto, y desde ahí Windows no deja cambiar el brillo de sus pantallas."));
        return lista;
    }
}

// Una pantalla que Windows ve pero que no se puede ajustar, con la explicación para el usuario.
class Aviso
{
    public readonly string Titulo, Detalle;
    public Aviso(string titulo, string detalle) { Titulo = titulo; Detalle = detalle; }
}

// Lo que Windows informa de un monitor conectado (por WMI).
class InfoMonitor
{
    public string Instancia;    // "DISPLAY\GSM5C89\5&2f15793d&0&UID4354_0"
    public string Nombre = "";  // el nombre real, por ejemplo "LG ULTRAGEAR"
    public long Conexion = -1;  // VideoOutputTechnology (D3DKMDT_VIDEO_OUTPUT_TECHNOLOGY); -1 si no se sabe
}

class PantallaDdc : Pantalla
{
    const byte VCP_BRILLO = 0x10;
    readonly IntPtr handle;
    uint maximo = 100;

    public PantallaDdc(IntPtr h) { handle = h; }

    public override int Leer()
    {
        uint cur, max;
        if (!Native.GetVCPFeatureAndVCPFeatureReply(handle, VCP_BRILLO, IntPtr.Zero, out cur, out max) || max == 0)
            return -1;
        maximo = max;
        return (int)Math.Round(cur * 100.0 / max);
    }

    public override bool Escribir(int porcentaje)
    {
        uint valor = (uint)Math.Round(porcentaje * maximo / 100.0);
        return Native.SetVCPFeature(handle, VCP_BRILLO, valor);
    }

    public override void Liberar() { Native.DestroyPhysicalMonitor(handle); }

    // Devuelve los monitores externos que responden a DDC/CI, con su nombre real si se puede.
    // Los que no responden se anotan en "avisos" (si no es null) con la causa más probable.
    // instanciaLaptop: el InstanceName del panel que se maneja por WMI (o null); hayLaptop: si ese panel anda.
    public static List<Pantalla> Detectar(string instanciaLaptop, bool hayLaptop, List<Aviso> avisos)
    {
        var hmons = new List<IntPtr>();
        Native.MonitorEnumProc cb = delegate(IntPtr h, IntPtr d, IntPtr r, IntPtr x) { hmons.Add(h); return true; };
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero);
        GC.KeepAlive(cb);

        var datos = InfoWmi();
        var lista = new List<Pantalla>();
        foreach (IntPtr hmon in hmons)
        {
            var info = new Native.MONITORINFOEX();
            info.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFOEX));
            string adaptador = Native.GetMonitorInfo(hmon, ref info) ? info.szDevice : null;

            uint n;
            Native.PHYSICAL_MONITOR[] fisicos = null;
            if (Native.GetNumberOfPhysicalMonitorsFromHMONITOR(hmon, out n) && n > 0)
            {
                fisicos = new Native.PHYSICAL_MONITOR[n];
                if (!Native.GetPhysicalMonitorsFromHMONITOR(hmon, n, fisicos)) fisicos = null;
            }
            if (fisicos == null)
            {
                // Windows no nos dio acceso al monitor: lo anotamos igual para explicar por qué no aparece.
                string id0 = adaptador != null ? IdDispositivo(adaptador, 0) : null;
                Diagnosticar(avisos, Buscar(datos, id0), EsInterna(Buscar(datos, id0), id0, instanciaLaptop), hayLaptop, true);
                continue;
            }

            for (int j = 0; j < fisicos.Length; j++)
            {
                string id = adaptador != null ? IdDispositivo(adaptador, j) : null;
                InfoMonitor d = Buscar(datos, id);
                bool interna = EsInterna(d, id, instanciaLaptop);

                var p = new PantallaDdc(fisicos[j].hPhysicalMonitor);
                int v = p.Leer();
                // Algunos monitores no contestan a la primera (por ejemplo, recién despiertos): probamos otra vez.
                if (v < 0 && !interna) { Thread.Sleep(60); v = p.Leer(); }
                if (v < 0)
                {
                    p.Liberar();
                    Diagnosticar(avisos, d, interna, hayLaptop, false);
                    continue;
                }

                p.Nombre = d != null && d.Nombre != "" ? d.Nombre : "Monitor externo";
                p.Clave = "ddc|" + (id ?? (adaptador + "|" + j));
                p.Aplicado = v;
                p.Deseado = v;
                lista.Add(p);
            }
        }

        // Si hay dos monitores con el mismo nombre, los numeramos.
        for (int i = 0; i < lista.Count; i++)
        {
            int iguales = 0, orden = 0;
            for (int k = 0; k < lista.Count; k++)
                if (lista[k].Nombre == lista[i].Nombre) { iguales++; if (k <= i) orden++; }
            if (iguales > 1) lista[i].Nombre += " " + orden;
        }
        return lista;
    }

    // "\\?\DISPLAY#GSM5C89#5&2f15793d&0&UID4354#{e6f0...}" -> "DISPLAY\GSM5C89\5&2f15793d&0&UID4354",
    // que es el comienzo del InstanceName de WmiMonitorID.
    static string IdDispositivo(string adaptador, int indice)
    {
        var dd = new Native.DISPLAY_DEVICE();
        dd.cb = Marshal.SizeOf(typeof(Native.DISPLAY_DEVICE));
        if (!Native.EnumDisplayDevices(adaptador, (uint)indice, ref dd, 1) || string.IsNullOrEmpty(dd.DeviceID))
            return null;
        string s = dd.DeviceID;
        if (s.StartsWith(@"\\?\")) s = s.Substring(4);
        int llave = s.LastIndexOf("#{");
        if (llave > 0) s = s.Substring(0, llave);
        return s.Replace('#', '\\');
    }

    // Nombre real y tipo de conexión de cada monitor conectado.
    static List<InfoMonitor> InfoWmi()
    {
        var r = new List<InfoMonitor>();
        try
        {
            using (var s = new ManagementObjectSearcher("root\\WMI", "SELECT InstanceName, UserFriendlyName FROM WmiMonitorID"))
                foreach (ManagementObject o in s.Get())
                {
                    string instancia = o["InstanceName"] as string;
                    if (instancia == null) continue;
                    var letras = o["UserFriendlyName"] as ushort[];
                    string nombre = "";
                    if (letras != null)
                        foreach (ushort c in letras) { if (c == 0) break; nombre += (char)c; }
                    r.Add(new InfoMonitor { Instancia = instancia, Nombre = nombre.Trim() });
                }
        }
        catch { }
        try
        {
            using (var s = new ManagementObjectSearcher("root\\WMI", "SELECT InstanceName, VideoOutputTechnology FROM WmiMonitorConnectionParams"))
                foreach (ManagementObject o in s.Get())
                {
                    string instancia = o["InstanceName"] as string;
                    object tec = o["VideoOutputTechnology"];
                    if (instancia == null || tec == null) continue;
                    InfoMonitor m = null;
                    foreach (var x in r) if (string.Equals(x.Instancia, instancia, StringComparison.OrdinalIgnoreCase)) m = x;
                    if (m == null) { m = new InfoMonitor { Instancia = instancia }; r.Add(m); }
                    m.Conexion = Convert.ToInt64(tec);
                }
        }
        catch { }
        return r;
    }

    static InfoMonitor Buscar(List<InfoMonitor> datos, string id)
    {
        if (id == null) return null;
        foreach (var d in datos)
            if (d.Instancia.StartsWith(id + "_", StringComparison.OrdinalIgnoreCase)) return d;
        return null;
    }

    // Tipos de conexión (D3DKMDT_VIDEO_OUTPUT_TECHNOLOGY) que nos interesan.
    const long LVDS = 6, DP_INTEGRADO = 11, UDI_INTEGRADO = 13, MIRACAST = 15, USB = 16, VIRTUAL = 17, INTERNA = 0x80000000L;

    static bool EsInterna(InfoMonitor d, string id, string instanciaLaptop)
    {
        if (id != null && instanciaLaptop != null && instanciaLaptop.StartsWith(id + "_", StringComparison.OrdinalIgnoreCase))
            return true;
        if (d == null) return false;
        return d.Conexion == INTERNA || d.Conexion == LVDS || d.Conexion == DP_INTEGRADO || d.Conexion == UDI_INTEGRADO;
    }

    // Anota por qué una pantalla que Windows ve no se puede ajustar.
    static void Diagnosticar(List<Aviso> avisos, InfoMonitor d, bool interna, bool hayLaptop, bool sinAcceso)
    {
        if (avisos == null) return;
        if (interna)
        {
            // El panel de la laptop nunca responde a DDC/CI; solo es un problema si tampoco anda por WMI.
            if (!hayLaptop)
                avisos.Add(new Aviso("Pantalla de la laptop: sin control de brillo",
                    "Windows no deja cambiar su brillo. Suele pasar cuando falta el driver de video del fabricante: " +
                    "actualizalo desde Windows Update o desde la página del fabricante de la laptop."));
            return;
        }

        string nombre = d != null && d.Nombre != "" ? d.Nombre : "Monitor externo";
        long conexion = d != null ? d.Conexion : -1;
        if (conexion == USB)
            avisos.Add(new Aviso(nombre + ": conectado por USB",
                "Las pantallas conectadas por USB (DisplayLink o similar) no dejan cambiar el brillo desde la PC. " +
                "Conectalo por HDMI o DisplayPort, o usá los botones del monitor."));
        else if (conexion == MIRACAST)
            avisos.Add(new Aviso(nombre + ": pantalla inalámbrica",
                "Las pantallas conectadas sin cables (Miracast) no dejan cambiar el brillo desde la PC."));
        else if (conexion == VIRTUAL)
            avisos.Add(new Aviso(nombre + ": pantalla virtual",
                "Es una pantalla creada por un programa (escritorio remoto, captura o similar): no tiene un brillo propio para ajustar."));
        else if (sinAcceso)
            avisos.Add(new Aviso(nombre + ": sin acceso",
                "Windows no da acceso al monitor. Suele pasar con el driver de video genérico de Microsoft: " +
                "instalá el driver de la placa de video (NVIDIA, AMD o Intel)."));
        else
            avisos.Add(new Aviso(nombre + " no responde",
                "Revisá que DDC/CI esté activado en el menú del monitor. Si está conectado con un adaptador, dock, " +
                "KVM o hub, probá con un cable directo. Los televisores casi nunca lo permiten."));
    }
}

class PantallaWmi : Pantalla
{
    ManagementObject metodos;   // se guarda para no repetir la consulta en cada cambio
    public string Instancia;    // InstanceName del panel, para reconocerlo entre los monitores

    public PantallaWmi() { Nombre = "Pantalla de la laptop"; Clave = "wmi"; EsLaptop = true; }

    public override int Leer()
    {
        try
        {
            using (var s = new ManagementObjectSearcher("root\\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness"))
                foreach (ManagementObject o in s.Get())
                    return Convert.ToInt32(o["CurrentBrightness"]);
        }
        catch { }
        return -1;
    }

    public override bool Escribir(int porcentaje)
    {
        for (int intento = 0; intento < 2; intento++)
        {
            try
            {
                if (metodos == null) metodos = BuscarMetodos();
                if (metodos == null) return false;
                metodos.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)porcentaje });
                return true;
            }
            catch { metodos = null; }
        }
        return false;
    }

    public override void Liberar()
    {
        if (metodos != null) { metodos.Dispose(); metodos = null; }
    }

    static ManagementObject BuscarMetodos()
    {
        using (var s = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods"))
            foreach (ManagementObject o in s.Get())
                return o;
        return null;
    }

    public static List<Pantalla> Detectar()
    {
        var lista = new List<Pantalla>();
        var p = new PantallaWmi();
        try
        {
            using (var s = new ManagementObjectSearcher("root\\WMI", "SELECT InstanceName, CurrentBrightness FROM WmiMonitorBrightness"))
                foreach (ManagementObject o in s.Get())
                {
                    p.Instancia = o["InstanceName"] as string;
                    int v = Convert.ToInt32(o["CurrentBrightness"]);
                    p.Aplicado = v;
                    p.Deseado = v;
                    lista.Add(p);
                    break;
                }
        }
        catch { }
        return lista;
    }
}

// Hace todo el acceso a las pantallas (DDC y WMI) en un hilo aparte, para que la ventana no se trabe.
class Trabajador
{
    readonly object cerrojo = new object();
    readonly Queue<Action> tareas = new Queue<Action>();
    readonly AutoResetEvent senal = new AutoResetEvent(false);
    List<Pantalla> pantallas = new List<Pantalla>();
    DateTime ultimaDeteccion = DateTime.MinValue;

    // Se llama desde el hilo de trabajo cada vez que se vuelven a detectar las pantallas,
    // con las que se pueden ajustar y los avisos de las que no.
    public Action<List<Pantalla>, List<Aviso>> PantallasCambiadas;

    public Trabajador()
    {
        var hilo = new Thread(Bucle);
        hilo.IsBackground = true;
        hilo.Start();
    }

    public void Encolar(Action a)
    {
        lock (cerrojo) tareas.Enqueue(a);
        senal.Set();
    }

    // Avisa que hay valores nuevos para escribir.
    public void Despertar() { senal.Set(); }

    // Vuelve a buscar las pantallas (por si se conectó o reconectó alguna) y relee el brillo.
    public void Redetectar() { Encolar(DetectarAhora); }

    public List<Pantalla> DetectarYEsperar(out List<Aviso> avisosEncontrados)
    {
        var listo = new ManualResetEvent(false);
        List<Pantalla> resultado = null;
        List<Aviso> encontrados = null;
        Encolar(delegate
        {
            DetectarAhora();
            resultado = new List<Pantalla>(pantallas);
            encontrados = new List<Aviso>(avisos);
            listo.Set();
        });
        listo.WaitOne(15000);
        avisosEncontrados = encontrados ?? new List<Aviso>();
        return resultado ?? new List<Pantalla>();
    }

    List<Aviso> avisos = new List<Aviso>();

    void DetectarAhora()
    {
        var nuevosAvisos = new List<Aviso>();
        var nuevas = Pantalla.DetectarTodas(nuevosAvisos);
        avisos = nuevosAvisos;
        // Si había un cambio que todavía no se pudo aplicar, lo pasamos a la pantalla nueva.
        foreach (var n in nuevas)
            foreach (var v in pantallas)
                if (v.Clave == n.Clave && v.Deseado >= 0 && v.Deseado != v.Aplicado) n.Deseado = v.Deseado;
        foreach (var v in pantallas) v.Liberar();
        pantallas = nuevas;
        ultimaDeteccion = DateTime.Now;

        var avisar = PantallasCambiadas;
        if (avisar != null) avisar(new List<Pantalla>(nuevas), new List<Aviso>(nuevosAvisos));
    }

    void Bucle()
    {
        while (true)
        {
            senal.WaitOne();
            while (true)
            {
                Action tarea = null;
                lock (cerrojo) if (tareas.Count > 0) tarea = tareas.Dequeue();
                if (tarea != null) { try { tarea(); } catch { } continue; }
                if (!AplicarPendientes()) break;
                Thread.Sleep(40); // pausa corta para no saturar el bus DDC mientras se arrastra
            }
        }
    }

    // Escribe los valores que cambiaron. Devuelve true si escribió algo.
    bool AplicarPendientes()
    {
        bool escribio = false;
        foreach (var p in pantallas)
        {
            int d = p.Deseado;
            if (d < 0 || d == p.Aplicado) continue;
            escribio = true;
            if (p.Escribir(d)) { p.Aplicado = d; continue; }

            // No respondió: puede que el monitor se haya reconectado. Lo buscamos de nuevo
            // (como mucho cada 2 segundos); si ya lo hicimos hace poco, nos damos por vencidos.
            if ((DateTime.Now - ultimaDeteccion).TotalSeconds > 2) { DetectarAhora(); return true; }
            p.Aplicado = d;
        }
        return escribio;
    }
}

class Config
{
    public const int MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8;
    public const string CLAVE = @"Software\Brillo";
    const string CLAVE_INICIO = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string NOMBRE_APP = "Brillo";
    const string CLAVE_VIEJA = @"Software\BrilloMonitor", NOMBRE_VIEJO = "BrilloMonitor";   // nombres de antes

    public int AtajoMods = MOD_CONTROL | MOD_ALT;
    public int AtajoTecla = (int)Keys.B;
    public int Paso = 5;
    public bool Maestro = true;   // mostrar un deslizador para todas las pantallas a la vez
    public int Tema = 0;          // 0 = como Windows, 1 = oscuro, 2 = claro

    public Config Copiar() { return (Config)MemberwiseClone(); }

    // Antes el programa se llamaba BrilloMonitor: pasamos sus opciones y su inicio automático al nombre nuevo.
    static void Migrar()
    {
        try
        {
            bool habia;
            using (var vieja = Registry.CurrentUser.OpenSubKey(CLAVE_VIEJA))
            {
                habia = vieja != null;
                if (habia)
                    using (var nueva = Registry.CurrentUser.CreateSubKey(CLAVE))
                        foreach (string n in vieja.GetValueNames())
                            if (nueva.GetValue(n) == null) nueva.SetValue(n, vieja.GetValue(n), vieja.GetValueKind(n));
            }
            if (habia) Registry.CurrentUser.DeleteSubKeyTree(CLAVE_VIEJA, false);
        }
        catch { }
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(CLAVE_INICIO, true))
                if (k != null && k.GetValue(NOMBRE_VIEJO) != null)
                {
                    k.DeleteValue(NOMBRE_VIEJO, false);
                    k.SetValue(NOMBRE_APP, "\"" + Application.ExecutablePath + "\" --oculto");
                }
        }
        catch { }
    }

    public static Config Cargar()
    {
#if !TIENDA
        Migrar();   // la versión de la Store nunca se llamó BrilloMonitor
#endif
        var c = new Config();
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(CLAVE))
                if (k != null)
                {
                    c.AtajoMods = LeerEntero(k, "AtajoMods", c.AtajoMods);
                    c.AtajoTecla = LeerEntero(k, "AtajoTecla", c.AtajoTecla);
                    c.Paso = LeerEntero(k, "Paso", c.Paso);
                    c.Maestro = LeerEntero(k, "Maestro", 1) != 0;
                    c.Tema = LeerEntero(k, "Tema", c.Tema);
                }
        }
        catch { }
        c.Paso = Math.Max(1, Math.Min(25, c.Paso));
        if (c.Tema < 0 || c.Tema > 2) c.Tema = 0;
        return c;
    }

    static int LeerEntero(RegistryKey k, string nombre, int porDefecto)
    {
        object v = k.GetValue(nombre);
        return v is int ? (int)v : porDefecto;
    }

    public void Guardar()
    {
        try
        {
            using (var k = Registry.CurrentUser.CreateSubKey(CLAVE))
            {
                k.SetValue("AtajoMods", AtajoMods, RegistryValueKind.DWord);
                k.SetValue("AtajoTecla", AtajoTecla, RegistryValueKind.DWord);
                k.SetValue("Paso", Paso, RegistryValueKind.DWord);
                k.SetValue("Maestro", Maestro ? 1 : 0, RegistryValueKind.DWord);
                k.SetValue("Tema", Tema, RegistryValueKind.DWord);
            }
        }
        catch { }
    }

#if TIENDA
    // En la versión de la Microsoft Store el inicio automático es una "tarea de inicio" del paquete.
    public static bool InicioActivado() { return Tienda.InicioActivado(); }
    public static void CambiarInicio(bool activar) { Tienda.CambiarInicio(activar); }
#else
    public static bool InicioActivado()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(CLAVE_INICIO))
            return k != null && k.GetValue(NOMBRE_APP) != null;
    }

    public static void CambiarInicio(bool activar)
    {
        CambiarInicio(activar, Application.ExecutablePath);
    }
#endif

    // Marca guardada la primera vez que se abre (la versión de la Store pregunta ahí por el inicio automático).
    public static bool PrimeraVez()
    {
        try
        {
            using (var k = Registry.CurrentUser.CreateSubKey(CLAVE))
            {
                if (k.GetValue("Bienvenida") != null) return false;
                k.SetValue("Bienvenida", 1, RegistryValueKind.DWord);
                return true;
            }
        }
        catch { return false; }
    }

    // El instalador lo usa con la ruta del programa instalado (no con la suya).
    public static void CambiarInicio(bool activar, string ejecutable)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(CLAVE_INICIO))
        {
            if (activar) k.SetValue(NOMBRE_APP, "\"" + ejecutable + "\" --oculto");
            else k.DeleteValue(NOMBRE_APP, false);
        }
    }

    public static string TextoModificadores(int mods)
    {
        string s = "";
        if ((mods & MOD_CONTROL) != 0) s += "Ctrl + ";
        if ((mods & MOD_ALT) != 0) s += "Alt + ";
        if ((mods & MOD_SHIFT) != 0) s += "Shift + ";
        if ((mods & MOD_WIN) != 0) s += "Win + ";
        return s;
    }

    public static string TextoAtajo(int mods, int tecla)
    {
        if (tecla == 0) return "Ninguno";
        return TextoModificadores(mods) + NombreTecla((Keys)tecla);
    }

    static string NombreTecla(Keys k)
    {
        if (k >= Keys.D0 && k <= Keys.D9) return ((int)(k - Keys.D0)).ToString();
        if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return "Num " + (int)(k - Keys.NumPad0);
        switch (k)
        {
            case Keys.Up: return "Flecha arriba";
            case Keys.Down: return "Flecha abajo";
            case Keys.Left: return "Flecha izquierda";
            case Keys.Right: return "Flecha derecha";
            case Keys.PageUp: return "RePág";
            case Keys.PageDown: return "AvPág";
            case Keys.Space: return "Espacio";
            case Keys.Delete: return "Supr";
            case Keys.Home: return "Inicio";
            case Keys.End: return "Fin";
            case Keys.Oemcomma: return ",";
            case Keys.OemPeriod: return ".";
            case Keys.OemMinus: return "-";
            case Keys.Oemplus: return "+";
        }
        return k.ToString();
    }
}

// Colores de la ventana, según el tema de Windows y el color de énfasis.
class Tema
{
    public bool Oscuro;
    public Color Fondo, Cabecera, Texto, TextoCabecera, Pista, Acento, AcentoActivo, Borde, Burbuja, Foco, Resalte, Menu, MenuElegido;
    public Color TextoSuave, Tarjeta, BordeTarjeta, Control, ControlHover, BordeControl, Error, Aviso, SobreAcento, HoverCabecera;

    public static Tema Actual(int preferencia)
    {
        bool oscuro = preferencia == 1 || (preferencia == 0 && !SistemaClaro());
        Color acento = LeerAcento();
        var t = new Tema();
        t.Oscuro = oscuro;
        t.Acento = acento;
        if (oscuro)
        {
            t.Fondo = Color.FromArgb(32, 32, 32);
            t.Cabecera = Mezclar(acento, t.Fondo, 0.5);
            t.Texto = Color.FromArgb(235, 235, 235);
            t.TextoCabecera = Color.White;
            t.Pista = Color.FromArgb(100, 255, 255, 255);
            t.AcentoActivo = Mezclar(acento, Color.White, 0.75);
            t.Borde = Color.FromArgb(60, 60, 60);
            t.Burbuja = Color.FromArgb(48, 48, 48);
            t.Foco = Color.FromArgb(200, 255, 255, 255);
            t.Resalte = Color.FromArgb(40, 255, 255, 255);
            t.Menu = Color.FromArgb(43, 43, 43);
            t.MenuElegido = Color.FromArgb(65, 65, 65);
            t.TextoSuave = Color.FromArgb(165, 165, 165);
            t.Tarjeta = Color.FromArgb(43, 43, 43);
            t.BordeTarjeta = Color.FromArgb(50, 50, 50);
            t.Control = Color.FromArgb(58, 58, 58);
            t.ControlHover = Color.FromArgb(70, 70, 70);
            t.BordeControl = Color.FromArgb(78, 78, 78);
            t.Error = Color.FromArgb(255, 153, 164);
            t.Aviso = Color.FromArgb(252, 225, 0);
            t.HoverCabecera = Color.FromArgb(45, 255, 255, 255);
        }
        else
        {
            t.Fondo = Color.FromArgb(243, 243, 243);
            t.Cabecera = Mezclar(acento, t.Fondo, 0.22);
            t.Texto = Color.FromArgb(25, 25, 25);
            t.TextoCabecera = Color.Black;
            t.Pista = Color.FromArgb(110, 0, 0, 0);
            t.AcentoActivo = Mezclar(acento, Color.Black, 0.8);
            t.Borde = Color.FromArgb(205, 205, 205);
            t.Burbuja = Color.White;
            t.Foco = Color.FromArgb(200, 0, 0, 0);
            t.Resalte = Color.FromArgb(30, 0, 0, 0);
            t.Menu = Color.FromArgb(249, 249, 249);
            t.MenuElegido = Color.FromArgb(225, 225, 225);
            t.TextoSuave = Color.FromArgb(95, 95, 95);
            t.Tarjeta = Color.FromArgb(251, 251, 251);
            t.BordeTarjeta = Color.FromArgb(229, 229, 229);
            t.Control = Color.White;
            t.ControlHover = Color.FromArgb(245, 245, 245);
            t.BordeControl = Color.FromArgb(205, 205, 205);
            t.Error = Color.FromArgb(196, 43, 28);
            t.Aviso = Color.FromArgb(157, 93, 0);
            t.HoverCabecera = Color.FromArgb(30, 0, 0, 0);
        }
        t.SobreAcento = Color.White;
        return t;
    }

    static Color Mezclar(Color a, Color b, double t)
    {
        return Color.FromArgb((int)(a.R * t + b.R * (1 - t)), (int)(a.G * t + b.G * (1 - t)), (int)(a.B * t + b.B * (1 - t)));
    }

    public static bool SistemaClaro()
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                object v = k == null ? null : k.GetValue("SystemUsesLightTheme");
                return v is int && (int)v == 1;
            }
        }
        catch { return false; }
    }

    static Color LeerAcento()
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
            {
                object v = k == null ? null : k.GetValue("AccentColor");
                if (v is int)
                {
                    int c = (int)v; // 0xAABBGGRR
                    return Color.FromArgb(c & 0xFF, (c >> 8) & 0xFF, (c >> 16) & 0xFF);
                }
            }
        }
        catch { }
        return Color.FromArgb(0, 120, 212);
    }
}

// Menú del icono con los colores del tema.
class RenderMenu : ToolStripProfessionalRenderer
{
    readonly Tema t;
    public RenderMenu(Tema t) : base(new ColoresMenu(t)) { this.t = t; RoundedEdges = false; }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = t.Texto;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var r = e.ImageRectangle;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = new Pen(t.Texto, Math.Max(1.5f, r.Height / 9f)))
            e.Graphics.DrawLines(p, new[] {
                new PointF(r.Left + r.Width * 0.2f, r.Top + r.Height * 0.52f),
                new PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.74f),
                new PointF(r.Left + r.Width * 0.8f, r.Top + r.Height * 0.28f) });
    }
}

class ColoresMenu : ProfessionalColorTable
{
    readonly Tema t;
    public ColoresMenu(Tema t) { this.t = t; UseSystemColors = false; }
    public override Color ToolStripDropDownBackground { get { return t.Menu; } }
    public override Color ImageMarginGradientBegin { get { return t.Menu; } }
    public override Color ImageMarginGradientMiddle { get { return t.Menu; } }
    public override Color ImageMarginGradientEnd { get { return t.Menu; } }
    public override Color MenuBorder { get { return t.Borde; } }
    public override Color MenuItemBorder { get { return t.MenuElegido; } }
    public override Color MenuItemSelected { get { return t.MenuElegido; } }
    public override Color MenuItemSelectedGradientBegin { get { return t.MenuElegido; } }
    public override Color MenuItemSelectedGradientEnd { get { return t.MenuElegido; } }
    public override Color SeparatorDark { get { return t.Borde; } }
    public override Color SeparatorLight { get { return t.Menu; } }
}

class Fila
{
    public Pantalla Pantalla;   // null en la cabecera de "Todas las pantallas"
    public string Nombre;
    public int Valor;
    public bool Grande;         // va en la cabecera
    public bool EsMaestro;      // mueve todas las pantallas a la vez
    public string Glifo;
    public Rectangle Area, Titulo, Icono, Pista, Zona, Texto;
    public int Centro;

    public bool TieneDeslizador { get { return Pantalla != null || EsMaestro; } }
}

// Base de las ventanas emergentes (brillo y configuración): sin bordes, con esquinas redondeadas,
// ubicadas junto a la barra de tareas, dibujadas a mano según el DPI del monitor donde se abren
// y que se esconden al hacer clic afuera.
abstract class Flotante : Form
{
    const int WM_DPICHANGED = 0x02E0;
    protected const TextFormatFlags LINEA = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine |
                                            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
    protected const TextFormatFlags CENTRADO = LINEA | TextFormatFlags.HorizontalCenter;

    protected Tema tema;
    protected int dpi = 96;
    protected float escala = 1f;
    protected readonly string fuenteIconos;
    Rectangle areaTrabajo, areaMonitor;
    readonly System.Windows.Forms.Timer vigilante = new System.Windows.Forms.Timer();
    IntPtr primerPlanoAlMostrar;

    public DateTime OcultadaEn = DateTime.MinValue;
    public bool Saliendo;
    public event EventHandler Ocultada;

    protected Flotante()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        fuenteIconos = ExisteFuente("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
        tema = Tema.Actual(0);

        // Por si la ventana no logra ponerse al frente (y entonces no recibe Deactivate),
        // la escondemos igual cuando el usuario se va a otra ventana.
        vigilante.Interval = 200;
        vigilante.Tick += delegate { VigilarPrimerPlano(); };

        Deactivate += delegate { Ocultar(); };
        FormClosing += delegate(object s, FormClosingEventArgs e)
        {
            if (!Saliendo && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Ocultar(); }
        };
    }

    protected abstract int PreferenciaTema { get; }
    protected abstract void Distribuir();          // calcula el tamaño y las posiciones según "escala"
    protected virtual void AlMostrar() { }
    protected virtual void AlOcultar() { }

    // Crea la ventana sin mostrarla (en el modo de DPI por monitor del hilo principal).
    public void Crear() { if (!IsHandleCreated) CreateHandle(); }

    public void Mostrar()
    {
        Dpi.PorMonitor(delegate
        {
            tema = Tema.Actual(PreferenciaTema);
            AplicarDwm();
            int nuevoDpi;
            Dpi.Monitor(Cursor.Position, out areaTrabajo, out areaMonitor, out nuevoDpi);
            if (nuevoDpi != dpi) { dpi = nuevoDpi; escala = dpi / 96f; Distribuir(); }
            AlMostrar();
            Ubicar();
            primerPlanoAlMostrar = Native.GetForegroundWindow();
            Show();
            Activate();
            Native.SetForegroundWindow(Handle);
            Invalidate();
            vigilante.Start();
        });
    }

    public void Ocultar()
    {
        if (!Visible) return;
        OcultadaEn = DateTime.Now;
        vigilante.Stop();
        AlOcultar();
        Hide();
        var aviso = Ocultada;
        if (aviso != null) aviso(this, EventArgs.Empty);
    }

    // Vuelve a calcular el tamaño y, si está a la vista, la reubica.
    protected void Redistribuir()
    {
        Distribuir();
        if (Visible) Dpi.PorMonitor(Ubicar);
        Invalidate();
    }

    // Abajo a la derecha del monitor donde está el mouse, o del lado donde esté la barra de tareas.
    void Ubicar()
    {
        if (areaTrabajo.IsEmpty) return;
        int margen = E(12);
        int x = areaTrabajo.Right - Width - margen;
        int y = areaTrabajo.Bottom - Height - margen;
        if (areaTrabajo.Left > areaMonitor.Left) x = areaTrabajo.Left + margen;
        if (areaTrabajo.Top > areaMonitor.Top) y = areaTrabajo.Top + margen;
        Location = new Point(x, y);
    }

    void VigilarPrimerPlano()
    {
        if (!Visible) { vigilante.Stop(); return; }
        IntPtr actual = Native.GetForegroundWindow();
        if (actual == IntPtr.Zero || actual == Handle) { if (actual == Handle) primerPlanoAlMostrar = Handle; return; }
        if (actual != primerPlanoAlMostrar) Ocultar();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x20000;   // CS_DROPSHADOW
            cp.ExStyle |= 0x80;         // WS_EX_TOOLWINDOW: no aparece en Alt+Tab
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AplicarDwm();
    }

    // Esquinas redondeadas y borde fino de Windows 11 (en Windows 10 no hace nada).
    protected void AplicarDwm()
    {
        if (!IsHandleCreated) return;
        try
        {
            int redondo = 2; // DWMWCP_ROUND
            Native.DwmSetWindowAttribute(Handle, 33, ref redondo, 4);
            int borde = tema.Borde.R | (tema.Borde.G << 8) | (tema.Borde.B << 16);
            Native.DwmSetWindowAttribute(Handle, 34, ref borde, 4);
        }
        catch { }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_DPICHANGED) return; // el tamaño lo calculamos nosotros en Mostrar
        base.WndProc(ref m);
    }

    // ---------- Ayudas para dibujar ----------

    protected int E(float v) { return (int)Math.Round(v * escala); }

    protected Font Fuente(string familia, float px)
    {
        return new Font(familia, px * escala, FontStyle.Regular, GraphicsUnit.Pixel);
    }

    public static GraphicsPath Redondeado(RectangleF r, float radio)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radio * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Rellenar(Graphics g, RectangleF r, float radio, Color c)
    {
        using (var b = new SolidBrush(c))
        using (var camino = Redondeado(r, radio))
            g.FillPath(b, camino);
    }

    public static void Bordear(Graphics g, RectangleF r, float radio, Color c, float grosor)
    {
        var adentro = RectangleF.Inflate(r, -grosor / 2, -grosor / 2);
        using (var p = new Pen(c, grosor))
        using (var camino = Redondeado(adentro, Math.Max(0, radio - grosor / 2)))
            g.DrawPath(p, camino);
    }

    protected static bool ExisteFuente(string nombre)
    {
        try { using (var f = new FontFamily(nombre)) return true; }
        catch { return false; }
    }
}

// Ventana emergente con un deslizador por pantalla, al estilo de los controles de volumen de Windows.
class Ventana : Flotante
{
    const int ID_ATAJO = 1, ID_PRUEBA = 2;
    const int WM_HOTKEY = 0x0312;
    const string GLIFO_SOL = "", GLIFO_MONITOR = "", GLIFO_LAPTOP = "", GLIFO_AJUSTES = "";

    readonly Trabajador trabajador;
    readonly PanelConfig panel;
    Config config;
    List<Fila> filas = new List<Fila>();
    Fila arrastrando, sobreDeslizador, sobreIcono;
    int foco;
    bool focoVisible, sobreAjustes, apretoAjustes, atajoRegistrado;
    int acumuladoRueda;
    int altoCabecera;
    Rectangle rAjustes, rMensaje;
    Font fTitulo, fValorGrande, fValor, fIconoGrande, fIcono, fAjustes, fBurbuja, fMensaje, fAvisoTitulo, fGlifoAviso, fFlecha;

    // Pantallas que se ven pero no se pueden ajustar. Con pantallas para ajustar, se muestran como una
    // línea al pie que se despliega con un clic; sin ninguna, se muestran directamente.
    const string GLIFO_AVISO = "", GLIFO_ARRIBA = "", GLIFO_ABAJO = "";
    List<Aviso> avisos = new List<Aviso>();
    bool avisosAbiertos, sobreAvisos, apretoAvisos;
    int lineaAvisos;
    Rectangle rAvisos;
    readonly List<Rectangle> rAvisoTitulo = new List<Rectangle>(), rAvisoDetalle = new List<Rectangle>();

    NotifyIcon icono;
    ContextMenuStrip menu;
    ToolStripMenuItem itemInicio;

    public Ventana(Trabajador trabajador, List<Pantalla> pantallas, List<Aviso> avisosIniciales, Config cfg)
    {
        this.trabajador = trabajador;
        config = cfg;
        Text = "Brillo";
        tema = Tema.Actual(config.Tema);
        avisos = avisosIniciales;
        ArmarFilas(pantallas);
        Distribuir();

        panel = new PanelConfig(AplicarConfig, AtajoDisponible, delegate { Mostrar(); });
        // Mientras el panel está abierto el atajo queda libre (para poder elegir otro); al cerrarlo se vuelve a registrar.
        panel.Ocultada += delegate { if (!Saliendo) RegistrarAtajo(config.AtajoMods, config.AtajoTecla); };

        trabajador.PantallasCambiadas = delegate(List<Pantalla> l, List<Aviso> a) { Invocar(delegate { RecibirPantallas(l, a); }); };
        SystemEvents.DisplaySettingsChanged += delegate { Invocar(delegate { if (Visible) trabajador.Redetectar(); }); };
        // Si se pasa la barra de tareas de oscura a clara (o al revés), el icono cambia de color.
        SystemEvents.UserPreferenceChanged += delegate { Invocar(ActualizarIconoBandeja); };

        // El icono y su menú se crean en modo "DPI del sistema" para que Windows los escale solo.
        Dpi.ComoSistema(CrearIcono);
    }

    protected override int PreferenciaTema { get { return config.Tema; } }

    // Crea las ventanas (sin mostrarlas) y registra el atajo de teclado.
    public void Preparar()
    {
        Crear();
        panel.Crear();
        if (!RegistrarAtajo(config.AtajoMods, config.AtajoTecla))
            icono.ShowBalloonTip(6000, "Brillo",
                "No se pudo usar el atajo " + Config.TextoAtajo(config.AtajoMods, config.AtajoTecla) +
                " porque lo usa otro programa. Podés cambiarlo en Configuración.", ToolTipIcon.Warning);
    }

    public void Invocar(Action a)
    {
        try { if (IsHandleCreated && !IsDisposed) BeginInvoke(a); } catch { }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == ID_ATAJO)
        {
            if (Visible) Ocultar(); else Mostrar();
            return;
        }
        base.WndProc(ref m);
    }

    protected override void AlMostrar()
    {
        // Si se midieron antes de que existiera la ventana, volvemos a medir los textos de los avisos.
        if (avisos.Count > 0) Distribuir();
        panel.Ocultar();
        foco = PrimeraConDeslizador();
        focoVisible = false;
        acumuladoRueda = 0;
        // Relee el brillo (y las pantallas), por si se cambió con el botón del monitor o se conectó otra.
        trabajador.Redetectar();
    }

    protected override void AlOcultar()
    {
        arrastrando = null;
        Capture = false;
        ActualizarTextoIcono();
    }

    // ---------- Icono de la barra de tareas ----------

    void CrearIcono()
    {
        menu = new ContextMenuStrip();
        menu.ShowImageMargin = false;
        menu.ShowCheckMargin = true;
        var abrir = new ToolStripMenuItem("Abrir", null, delegate { Mostrar(); });
        abrir.Font = new Font(menu.Font, FontStyle.Bold);
        itemInicio = new ToolStripMenuItem("Iniciar con Windows", null, delegate { Config.CambiarInicio(!Config.InicioActivado()); });
        menu.Items.Add(abrir);
        menu.Items.Add("Configuración", null, delegate { AbrirConfiguracion(); });
        menu.Items.Add(itemInicio);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, delegate { Salir(); });
        menu.Opening += delegate
        {
            itemInicio.Checked = Config.InicioActivado();
            menu.Renderer = new RenderMenu(Tema.Actual(config.Tema));
        };

        icono = new NotifyIcon();
        icono.Icon = CrearIconoBandeja(fuenteIconos, SystemInformation.SmallIconSize.Width, Tema.SistemaClaro());
        icono.Text = "Brillo";
        icono.ContextMenuStrip = menu;
        icono.Visible = true;
        icono.MouseClick += delegate(object s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (Visible || panel.Visible) { Ocultar(); panel.Ocultar(); return; }
            // Si una ventana se acaba de esconder por este mismo clic, no la volvemos a abrir (funciona como interruptor).
            DateTime ultima = OcultadaEn > panel.OcultadaEn ? OcultadaEn : panel.OcultadaEn;
            if ((DateTime.Now - ultima).TotalMilliseconds < 300) return;
            Mostrar();
        };
        ActualizarTextoIcono();
    }

    void ActualizarTextoIcono()
    {
        if (icono == null) return;
        string s = "Brillo";
        foreach (var f in filas)
            if (f.Pantalla != null) s += "\n" + f.Nombre + ": " + f.Valor + " %";
        if (s.Length > 63) s = s.Substring(0, 62) + "…";
        try { icono.Text = s; } catch { }
    }

    bool RegistrarAtajo(int mods, int tecla)
    {
        DesregistrarAtajo();
        if (tecla == 0) return true;
        atajoRegistrado = Native.RegisterHotKey(Handle, ID_ATAJO, (uint)(mods | 0x4000 /* MOD_NOREPEAT */), (uint)tecla);
        return atajoRegistrado;
    }

    void DesregistrarAtajo()
    {
        if (atajoRegistrado) Native.UnregisterHotKey(Handle, ID_ATAJO);
        atajoRegistrado = false;
    }

    // Prueba si se puede usar un atajo (sin dejarlo registrado).
    bool AtajoDisponible(int mods, int tecla)
    {
        if (tecla == 0) return true;
        bool libre = Native.RegisterHotKey(Handle, ID_PRUEBA, (uint)(mods | 0x4000), (uint)tecla);
        if (libre) Native.UnregisterHotKey(Handle, ID_PRUEBA);
        return libre;
    }

    public void Salir()
    {
        Saliendo = true;
        panel.Saliendo = true;
        DesregistrarAtajo();
        icono.Visible = false;
        icono.Dispose();
        Application.Exit();
    }

    // ---------- Configuración ----------

    void AbrirConfiguracion()
    {
        if (panel.Visible) { panel.Activate(); return; }
        Ocultar();
        DesregistrarAtajo();
        panel.Cargar(config.Copiar());
        Invocar(panel.Mostrar);
    }

    // El panel llama a esto cada vez que cambia una opción.
    void AplicarConfig(Config nueva)
    {
        bool cambioMaestro = nueva.Maestro != config.Maestro;
        config = nueva;
        config.Guardar();
        tema = Tema.Actual(config.Tema);
        if (cambioMaestro) { ArmarFilas(PantallasActuales()); Distribuir(); }
        Invalidate();
    }

    // ---------- Filas ----------

    List<Pantalla> PantallasActuales()
    {
        var l = new List<Pantalla>();
        foreach (var f in filas) if (f.Pantalla != null) l.Add(f.Pantalla);
        return l;
    }

    void RecibirPantallas(List<Pantalla> nuevas, List<Aviso> nuevosAvisos)
    {
        var actuales = PantallasActuales();
        bool mismas = actuales.Count == nuevas.Count;
        for (int i = 0; mismas && i < nuevas.Count; i++) mismas = actuales[i].Clave == nuevas[i].Clave;

        bool mismosAvisos = avisos.Count == nuevosAvisos.Count;
        for (int i = 0; mismosAvisos && i < avisos.Count; i++) mismosAvisos = avisos[i].Titulo == nuevosAvisos[i].Titulo;
        avisos = nuevosAvisos;
        if (!mismosAvisos && mismas) Redistribuir();

        if (mismas)
        {
            // Mismas pantallas: solo actualizamos los valores (sin cortar un arrastre en curso).
            foreach (var f in filas)
            {
                if (f.Pantalla == null) continue;
                var n = nuevas[actuales.IndexOf(f.Pantalla)];
                if (f == arrastrando) n.Deseado = f.Valor;
                else f.Valor = Limitar(n.Deseado);
                f.Pantalla = n;
            }
            ActualizarMaestro();
            if (arrastrando != null) trabajador.Despertar();
        }
        else
        {
            ArmarFilas(nuevas);
            Redistribuir();
        }
        ActualizarTextoIcono();
        Invalidate();
    }

    void ArmarFilas(List<Pantalla> pantallas)
    {
        arrastrando = sobreDeslizador = sobreIcono = null;
        Capture = false;
        filas = new List<Fila>();
        if (pantallas.Count == 1)
        {
            var p = pantallas[0];
            filas.Add(new Fila { Pantalla = p, Nombre = p.Nombre, Grande = true, Glifo = GLIFO_SOL });
        }
        else if (pantallas.Count > 1)
        {
            filas.Add(new Fila { EsMaestro = config.Maestro, Nombre = config.Maestro ? "Todas las pantallas" : "Brillo", Grande = true, Glifo = GLIFO_SOL });
            foreach (var p in pantallas)
                filas.Add(new Fila { Pantalla = p, Nombre = p.Nombre, Glifo = p.EsLaptop ? GLIFO_LAPTOP : GLIFO_MONITOR });
        }
        foreach (var f in filas)
            if (f.Pantalla != null) f.Valor = Limitar(f.Pantalla.Deseado);
        ActualizarMaestro();
        if (foco >= filas.Count || foco < 0 || !filas[foco].TieneDeslizador) foco = PrimeraConDeslizador();
        ActualizarTextoIcono();
    }

    void ActualizarMaestro()
    {
        Fila maestro = null;
        int suma = 0, n = 0;
        foreach (var f in filas)
        {
            if (f.EsMaestro) maestro = f;
            if (f.Pantalla != null) { suma += f.Valor; n++; }
        }
        if (maestro != null && n > 0) maestro.Valor = (int)Math.Round((double)suma / n);
    }

    int PrimeraConDeslizador()
    {
        for (int i = 0; i < filas.Count; i++) if (filas[i].TieneDeslizador) return i;
        return 0;
    }

    void CambiarValor(Fila f, int v)
    {
        v = Limitar(v);
        if (f.Valor == v && !f.EsMaestro) return;
        f.Valor = v;
        if (f.EsMaestro)
        {
            foreach (var r in filas)
                if (r.Pantalla != null) { r.Valor = v; r.Pantalla.Deseado = v; }
        }
        else if (f.Pantalla != null)
        {
            f.Pantalla.Deseado = v;
            ActualizarMaestro();
        }
        trabajador.Despertar();
        Invalidate();
    }

    static int Limitar(int v) { return Math.Max(0, Math.Min(100, v)); }

    // ---------- Diseño ----------

    protected override void Distribuir()
    {
        foreach (var f in new[] { fTitulo, fValorGrande, fValor, fIconoGrande, fIcono, fAjustes, fBurbuja, fMensaje, fAvisoTitulo, fGlifoAviso, fFlecha })
            if (f != null) f.Dispose();
        fTitulo = Fuente("Segoe UI", 14);
        fValorGrande = Fuente("Segoe UI", 26);
        fValor = Fuente("Segoe UI", 17);
        fIconoGrande = Fuente(fuenteIconos, 22);
        fIcono = Fuente(fuenteIconos, 22);
        fAjustes = Fuente(fuenteIconos, 15);
        fBurbuja = Fuente("Segoe UI", 13);
        fMensaje = Fuente("Segoe UI", 14);
        fAvisoTitulo = Fuente(ExisteFuente("Segoe UI Semibold") ? "Segoe UI Semibold" : "Segoe UI", 14);
        fGlifoAviso = Fuente(fuenteIconos, 18);
        fFlecha = Fuente(fuenteIconos, 12);

        int ancho = E(368);
        int y;
        rAjustes = new Rectangle(ancho - E(42), E(6), E(34), E(32));
        rMensaje = rAvisos = Rectangle.Empty;

        if (filas.Count == 0)
        {
            altoCabecera = E(44);
            y = altoCabecera;
            if (avisos.Count == 0)
            {
                rMensaje = new Rectangle(E(16), altoCabecera + E(12), ancho - E(32), E(64));
                y = rMensaje.Bottom + E(12);
            }
        }
        else
        {
            var cab = filas[0];
            cab.Titulo = new Rectangle(E(16), E(8), rAjustes.Left - E(20), E(28));
            if (cab.TieneDeslizador) { UbicarFila(cab, E(72), ancho); altoCabecera = E(110); }
            else altoCabecera = E(44);
            cab.Area = new Rectangle(0, 0, ancho, altoCabecera);

            y = altoCabecera;
            if (filas.Count > 1)
            {
                y += E(6);
                for (int i = 1; i < filas.Count; i++)
                {
                    filas[i].Area = new Rectangle(0, y, ancho, E(46));
                    UbicarFila(filas[i], y + E(23), ancho);
                    y += E(46);
                }
                y += E(6);
            }
        }
        y = DistribuirAvisos(y, ancho);
        Size = new Size(ancho, y);
        Invalidate();
    }

    // Ubica los avisos debajo de las filas y devuelve la nueva altura. La línea que los despliega va abajo
    // de todo, para que al abrirlos (la ventana crece hacia arriba) quede debajo del mouse.
    int DistribuirAvisos(int y, int ancho)
    {
        rAvisoTitulo.Clear();
        rAvisoDetalle.Clear();
        lineaAvisos = y;
        if (avisos.Count == 0) return y;

        bool conFilas = filas.Count > 0;
        if (!conFilas || avisosAbiertos)
        {
            const TextFormatFlags flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            int izquierda = E(68), anchoTexto = ancho - izquierda - E(16);
            var limite = new Size(anchoTexto, int.MaxValue);
            y += E(14);
            // Medimos con el contexto de la propia ventana: el de la pantalla puede tener otro DPI
            // y cortar las líneas en otro lugar que al dibujar.
            Graphics g = IsHandleCreated ? CreateGraphics() : null;
            try
            {
                foreach (var a in avisos)
                {
                    Size t = g != null ? TextRenderer.MeasureText(g, a.Titulo, fAvisoTitulo, limite, flags)
                                       : TextRenderer.MeasureText(a.Titulo, fAvisoTitulo, limite, flags);
                    rAvisoTitulo.Add(new Rectangle(izquierda, y, anchoTexto, t.Height));
                    y += t.Height + E(2);
                    Size d = g != null ? TextRenderer.MeasureText(g, a.Detalle, fBurbuja, limite, flags)
                                       : TextRenderer.MeasureText(a.Detalle, fBurbuja, limite, flags);
                    rAvisoDetalle.Add(new Rectangle(izquierda, y, anchoTexto, d.Height));
                    y += d.Height + E(14);
                }
            }
            finally { if (g != null) g.Dispose(); }
        }
        if (conFilas)
        {
            rAvisos = new Rectangle(E(6), y + E(4), ancho - E(12), E(40));
            y = rAvisos.Bottom + E(6);
        }
        return y;
    }

    string ResumenAvisos()
    {
        if (avisos.Count == 1) return avisos[0].Titulo;
        return avisos.Count + " pantallas no se pueden ajustar";
    }

    void AlternarAvisos()
    {
        avisosAbiertos = !avisosAbiertos;
        Redistribuir();
    }

    void UbicarFila(Fila f, int cy, int ancho)
    {
        int tam = E(34);
        f.Centro = cy;
        f.Icono = new Rectangle(E(16), cy - tam / 2, tam, tam);
        int grosor = Math.Max(2, E(2));
        f.Pista = new Rectangle(E(68), cy - grosor / 2, ancho - E(68) - E(70), grosor);
        f.Zona = Rectangle.FromLTRB(f.Pista.Left - E(8), cy - E(17), f.Pista.Right + E(8), cy + E(17));
        f.Texto = Rectangle.FromLTRB(f.Pista.Right + E(6), cy - E(20), ancho - E(8), cy + E(20));
    }

    int XDeValor(Fila f, int v) { return f.Pista.Left + (int)Math.Round(f.Pista.Width * v / 100.0); }
    int ValorDeX(Fila f, int x) { return Limitar((int)Math.Round((x - f.Pista.Left) * 100.0 / f.Pista.Width)); }

    // ---------- Dibujo ----------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(tema.Fondo);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using (var b = new SolidBrush(tema.Cabecera)) g.FillRectangle(b, 0, 0, Width, altoCabecera);

        if (filas.Count == 0)
        {
            TextRenderer.DrawText(g, "Brillo", fTitulo, new Rectangle(E(16), E(8), Width, E(28)), tema.TextoCabecera, LINEA | TextFormatFlags.Left);
            if (avisos.Count == 0)
                TextRenderer.DrawText(g, "No se encontró ninguna pantalla.\nSi tenés un monitor conectado, revisá que DDC/CI esté activado en su menú.",
                    fMensaje, rMensaje, tema.Texto, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
        }
        else
        {
            var cab = filas[0];
            TextRenderer.DrawText(g, cab.Nombre, fTitulo, cab.Titulo, tema.TextoCabecera, LINEA | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            for (int i = 0; i < filas.Count; i++)
                if (filas[i].TieneDeslizador) DibujarFila(g, filas[i], i);
        }

        // Botón de configuración.
        if (sobreAjustes) Rellenar(g, rAjustes, E(4), tema.HoverCabecera);
        TextRenderer.DrawText(g, GLIFO_AJUSTES, fAjustes, rAjustes, tema.TextoCabecera, CENTRADO);

        DibujarAvisos(g);
        if (sobreIcono != null && arrastrando == null) DibujarBurbuja(g, sobreIcono);
    }

    void DibujarAvisos(Graphics g)
    {
        if (avisos.Count == 0) return;
        bool conFilas = filas.Count > 0;
        if (conFilas)
            using (var b = new SolidBrush(tema.Borde)) g.FillRectangle(b, 0, lineaAvisos, Width, Math.Max(1, E(1)));

        const TextFormatFlags flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        for (int i = 0; i < rAvisoTitulo.Count && i < avisos.Count; i++)
        {
            // Sin la línea de resumen, cada aviso lleva su propio icono.
            if (!conFilas)
                TextRenderer.DrawText(g, GLIFO_AVISO, fGlifoAviso, new Rectangle(E(16), rAvisoTitulo[i].Top - E(6), E(34), E(32)), tema.Aviso, CENTRADO);
            TextRenderer.DrawText(g, avisos[i].Titulo, fAvisoTitulo, rAvisoTitulo[i], tema.Texto, flags);
            TextRenderer.DrawText(g, avisos[i].Detalle, fBurbuja, rAvisoDetalle[i], tema.TextoSuave, flags);
        }

        if (!rAvisos.IsEmpty)
        {
            if (sobreAvisos) Rellenar(g, rAvisos, E(4), tema.Resalte);
            var glifo = new Rectangle(E(16), rAvisos.Top + (rAvisos.Height - E(34)) / 2, E(34), E(34));
            TextRenderer.DrawText(g, GLIFO_AVISO, fGlifoAviso, glifo, tema.Aviso, CENTRADO);
            var flecha = new Rectangle(rAvisos.Right - E(36), rAvisos.Top, E(30), rAvisos.Height);
            TextRenderer.DrawText(g, avisosAbiertos ? GLIFO_ABAJO : GLIFO_ARRIBA, fFlecha, flecha, tema.TextoSuave, CENTRADO);
            var texto = Rectangle.FromLTRB(E(68), rAvisos.Top, flecha.Left - E(4), rAvisos.Bottom);
            TextRenderer.DrawText(g, ResumenAvisos(), fMensaje, texto, tema.Texto, LINEA | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    void DibujarFila(Graphics g, Fila f, int indice)
    {
        Color texto = f.Grande ? tema.TextoCabecera : tema.Texto;

        if (focoVisible && indice == foco)
            Rellenar(g, Rectangle.FromLTRB(E(6), f.Centro - E(21), Width - E(6), f.Centro + E(21)), E(4), tema.Resalte);

        TextRenderer.DrawText(g, f.Glifo, f.Grande ? fIconoGrande : fIcono, f.Icono, texto, CENTRADO);

        int x = XDeValor(f, f.Valor);
        using (var b = new SolidBrush(tema.Pista)) g.FillRectangle(b, x, f.Pista.Top, f.Pista.Right - x, f.Pista.Height);
        using (var b = new SolidBrush(tema.Acento)) g.FillRectangle(b, f.Pista.Left, f.Pista.Top, x - f.Pista.Left, f.Pista.Height);

        bool activa = arrastrando == f || (arrastrando == null && sobreDeslizador == f);
        Rellenar(g, new RectangleF(x - E(4), f.Centro - E(12), E(8), E(24)), E(4), activa ? tema.AcentoActivo : tema.Acento);

        TextRenderer.DrawText(g, f.Valor.ToString(), f.Grande ? fValorGrande : fValor, f.Texto, texto, CENTRADO);
    }

    // Cartelito con el nombre de la pantalla al pasar el mouse por su icono.
    void DibujarBurbuja(Graphics g, Fila f)
    {
        const TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        Size t = TextRenderer.MeasureText(g, f.Nombre, fBurbuja, Size.Empty, flags);
        var r = new Rectangle(f.Icono.Right + E(6), f.Centro - (t.Height + E(12)) / 2, t.Width + E(20), t.Height + E(12));
        if (r.Right > Width - E(6)) r.X = Width - E(6) - r.Width;
        Rellenar(g, r, E(4), tema.Burbuja);
        Bordear(g, r, E(4), tema.Borde, 1);
        TextRenderer.DrawText(g, f.Nombre, fBurbuja, new Point(r.X + E(10), r.Y + E(6)), tema.Texto, flags);
    }

    // ---------- Mouse y teclado ----------

    Fila DeslizadorEn(Point p)
    {
        foreach (var f in filas) if (f.TieneDeslizador && f.Zona.Contains(p)) return f;
        return null;
    }

    Fila FilaEn(Point p)
    {
        foreach (var f in filas) if (f.TieneDeslizador && f.Area.Contains(p)) return f;
        return null;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        focoVisible = false;
        apretoAjustes = rAjustes.Contains(e.Location);
        apretoAvisos = rAvisos.Contains(e.Location);
        var f = DeslizadorEn(e.Location);
        if (f != null)
        {
            arrastrando = f;
            foco = filas.IndexOf(f);
            Capture = true;
            CambiarValor(f, ValorDeX(f, e.X));
        }
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (arrastrando != null) { CambiarValor(arrastrando, ValorDeX(arrastrando, e.X)); return; }

        var deslizador = DeslizadorEn(e.Location);
        Fila icono = null;
        foreach (var f in filas) if (!f.Grande && f.TieneDeslizador && f.Icono.Contains(e.Location)) icono = f;
        bool ajustes = rAjustes.Contains(e.Location);
        bool enAvisos = rAvisos.Contains(e.Location);
        if (deslizador != sobreDeslizador || icono != sobreIcono || ajustes != sobreAjustes || enAvisos != sobreAvisos)
        {
            sobreDeslizador = deslizador;
            sobreIcono = icono;
            sobreAjustes = ajustes;
            sobreAvisos = enAvisos;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (arrastrando != null) { arrastrando = null; Capture = false; ActualizarTextoIcono(); Invalidate(); }
        if (apretoAjustes && rAjustes.Contains(e.Location)) AbrirConfiguracion();
        if (apretoAvisos && rAvisos.Contains(e.Location)) AlternarAvisos();
        apretoAjustes = apretoAvisos = false;
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture && arrastrando != null) { arrastrando = null; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        sobreDeslizador = sobreIcono = null;
        sobreAjustes = sobreAvisos = false;
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        acumuladoRueda += e.Delta;
        int pasos = acumuladoRueda / 120;   // las touchpads mandan pasos más chicos: los vamos juntando
        if (pasos == 0) return;
        acumuladoRueda -= pasos * 120;
        var f = FilaEn(e.Location);
        if (f == null && foco < filas.Count) f = filas[foco];
        if (f != null && f.TieneDeslizador) CambiarValor(f, f.Valor + pasos * config.Paso);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys tecla = keyData & Keys.KeyCode;
        bool shift = (keyData & Keys.Shift) != 0;
        Fila f = foco < filas.Count && filas[foco].TieneDeslizador ? filas[foco] : null;

        if (tecla == Keys.Escape) { Ocultar(); return true; }
        if (tecla == Keys.Tab) { MoverFoco(shift ? -1 : 1); focoVisible = true; Invalidate(); return true; }
        if (f == null) return base.ProcessCmdKey(ref msg, keyData);

        int v;
        switch (tecla)
        {
            case Keys.Right: case Keys.Up: v = f.Valor + config.Paso; break;
            case Keys.Left: case Keys.Down: v = f.Valor - config.Paso; break;
            case Keys.PageUp: v = f.Valor + 10; break;
            case Keys.PageDown: v = f.Valor - 10; break;
            case Keys.Home: v = 0; break;
            case Keys.End: v = 100; break;
            default:
                // 1..9 = 10..90 %, 0 = 100 %
                if (tecla >= Keys.D0 && tecla <= Keys.D9) { int n = tecla - Keys.D0; v = n == 0 ? 100 : n * 10; break; }
                if (tecla >= Keys.NumPad0 && tecla <= Keys.NumPad9) { int n = tecla - Keys.NumPad0; v = n == 0 ? 100 : n * 10; break; }
                return base.ProcessCmdKey(ref msg, keyData);
        }
        focoVisible = true;
        CambiarValor(f, v);
        Invalidate();
        return true;
    }

    void MoverFoco(int direccion)
    {
        if (filas.Count == 0) return;
        int i = foco;
        for (int vueltas = 0; vueltas < filas.Count; vueltas++)
        {
            i = (i + direccion + filas.Count) % filas.Count;
            if (filas[i].TieneDeslizador) { foco = i; return; }
        }
    }

    // Icono de la bandeja: el glifo de brillo de Windows, monocromo como los demás iconos del sistema
    // (blanco con la barra de tareas oscura, negro con la clara).
    static Icon CrearIconoBandeja(string fuente, int tam, bool barraClara)
    {
        using (var bmp = new Bitmap(tam, tam))
        {
            using (var g = Graphics.FromImage(bmp))
            using (var familia = new FontFamily(fuente))
            using (var camino = new GraphicsPath())
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                camino.AddString(GLIFO_SOL, familia, (int)FontStyle.Regular, tam, PointF.Empty, StringFormat.GenericTypographic);
                // Lo centramos y lo ajustamos al cuadro según el contorno real del glifo.
                RectangleF b = camino.GetBounds();
                float factor = tam * 0.88f / Math.Max(b.Width, b.Height);
                using (var m = new Matrix())
                {
                    m.Translate(tam / 2f, tam / 2f);
                    m.Scale(factor, factor);
                    m.Translate(-(b.Left + b.Width / 2), -(b.Top + b.Height / 2));
                    camino.Transform(m);
                }
                // El glifo de brillo tiene trazos más finos que los del parlante o la red:
                // le sumamos un contorno para que quede del mismo peso que los demás iconos.
                Color color = barraClara ? Color.Black : Color.White;
                using (var pincel = new SolidBrush(color)) g.FillPath(pincel, camino);
                using (var lapiz = new Pen(color, tam * 0.04f) { LineJoin = LineJoin.Round }) g.DrawPath(lapiz, camino);
            }
            IntPtr h = bmp.GetHicon();
            try { using (var temporal = Icon.FromHandle(h)) return (Icon)temporal.Clone(); }
            finally { Native.DestroyIcon(h); }
        }
    }

    void ActualizarIconoBandeja()
    {
        if (icono == null) return;
        Icon anterior = icono.Icon;
        Dpi.ComoSistema(delegate
        {
            icono.Icon = CrearIconoBandeja(fuenteIconos, SystemInformation.SmallIconSize.Width, Tema.SistemaClaro());
        });
        if (anterior != null) anterior.Dispose();
    }
}

// Panel de configuración con el mismo estilo que la ventana del brillo. Los cambios se aplican al instante.
class PanelConfig : Flotante
{
    const string GLIFO_VOLVER = "", GLIFO_MENOS = "", GLIFO_MAS = "";
    const int ATAJO = 0, PASO = 1, TEMA = 2, MAESTRO = 3, INICIO = 4, OPCIONES = 5;
    static readonly string[] TEMAS = { "Windows", "Oscuro", "Claro" };

    readonly Action<Config> aplicar;
    readonly Func<int, int, bool> atajoDisponible;
    readonly Action volver;
    Config config = new Config();
    bool inicio, capturando, focoVisible;
    string parcial, error;     // texto mientras se aprieta la combinación, y mensaje de error
    string sobre, apretado;    // control bajo el mouse y control donde se apretó
    int foco;

    int altoCabecera;
    Rectangle rVolver, rTitulo, rAtajo, rMenos, rMas, rPaso, rSegmentos, rMaestro, rInicio;
    readonly Rectangle[] tarjetas = new Rectangle[OPCIONES];
    readonly Rectangle[] rSegmento = new Rectangle[3];
    Font fTitulo, fTexto, fAyuda, fControl, fGlifo;

    public PanelConfig(Action<Config> aplicar, Func<int, int, bool> atajoDisponible, Action volver)
    {
        this.aplicar = aplicar;
        this.atajoDisponible = atajoDisponible;
        this.volver = volver;
        Text = "Brillo - Configuración";
        Distribuir();
    }

    protected override int PreferenciaTema { get { return config.Tema; } }

    public void Cargar(Config c) { config = c; }

    protected override void AlMostrar()
    {
        inicio = Config.InicioActivado();
        capturando = false;
        parcial = error = null;
        sobre = apretado = null;
        foco = ATAJO;
        focoVisible = false;
    }

    protected override void AlOcultar() { capturando = false; }

    void Aplicar()
    {
        aplicar(config.Copiar());
        tema = Tema.Actual(config.Tema);
        AplicarDwm();
        Invalidate();
    }

    // ---------- Diseño ----------

    protected override void Distribuir()
    {
        foreach (var f in new[] { fTitulo, fTexto, fAyuda, fControl, fGlifo })
            if (f != null) f.Dispose();
        fTitulo = Fuente("Segoe UI", 14);
        fTexto = Fuente("Segoe UI", 14);
        fAyuda = Fuente("Segoe UI", 12);
        fControl = Fuente("Segoe UI", 13);
        fGlifo = Fuente(fuenteIconos, 12);

        int ancho = E(368);
        altoCabecera = E(48);
        rVolver = new Rectangle(E(8), E(8), E(32), E(32));
        rTitulo = Rectangle.FromLTRB(rVolver.Right + E(8), 0, ancho - E(16), altoCabecera);

        int y = altoCabecera + E(8);
        for (int i = 0; i < OPCIONES; i++)
        {
            tarjetas[i] = new Rectangle(E(8), y, ancho - E(16), E(60));
            y += E(64);
        }

        int derecha = tarjetas[0].Right - E(14);
        int alto = E(32);
        rAtajo = new Rectangle(derecha - E(150), Medio(ATAJO) - alto / 2, E(150), alto);
        rMas = new Rectangle(derecha - E(32), Medio(PASO) - alto / 2, E(32), alto);
        rPaso = new Rectangle(rMas.Left - E(52), rMas.Top, E(52), alto);
        rMenos = new Rectangle(rPaso.Left - E(32), rMas.Top, E(32), alto);
        int anchoSegmento = E(66);
        rSegmentos = new Rectangle(derecha - anchoSegmento * 3, Medio(TEMA) - alto / 2, anchoSegmento * 3, alto);
        for (int k = 0; k < 3; k++)
            rSegmento[k] = new Rectangle(rSegmentos.Left + k * anchoSegmento, rSegmentos.Top, anchoSegmento, alto);
        rMaestro = new Rectangle(derecha - E(40), Medio(MAESTRO) - E(10), E(40), E(20));
        rInicio = new Rectangle(derecha - E(40), Medio(INICIO) - E(10), E(40), E(20));

        Size = new Size(ancho, y + E(4));
        Invalidate();
    }

    int Medio(int opcion) { return tarjetas[opcion].Top + tarjetas[opcion].Height / 2; }

    Rectangle RectControl(int opcion)
    {
        switch (opcion)
        {
            case ATAJO: return rAtajo;
            case PASO: return Rectangle.FromLTRB(rMenos.Left, rMenos.Top, rMas.Right, rMas.Bottom);
            case TEMA: return rSegmentos;
            case MAESTRO: return rMaestro;
            default: return rInicio;
        }
    }

    // ---------- Dibujo ----------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(tema.Fondo);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Cabecera, igual que la de la ventana del brillo.
        using (var b = new SolidBrush(tema.Cabecera)) g.FillRectangle(b, 0, 0, Width, altoCabecera);
        if (sobre == "volver") Rellenar(g, rVolver, E(4), tema.HoverCabecera);
        TextRenderer.DrawText(g, GLIFO_VOLVER, fGlifo, rVolver, tema.TextoCabecera, CENTRADO);
        TextRenderer.DrawText(g, "Configuración", fTitulo, rTitulo, tema.TextoCabecera, LINEA | TextFormatFlags.Left);

        string ayudaAtajo = error ?? (capturando ? "Retroceso: ninguno" : "Hacé clic para cambiarlo");
        Tarjeta(g, ATAJO, "Atajo para abrir", ayudaAtajo, error != null);
        Tarjeta(g, PASO, "Paso", "Rueda del mouse y flechas", false);
        Tarjeta(g, TEMA, "Tema", null, false);
        Tarjeta(g, MAESTRO, "Todas las pantallas", "Un control que las mueve juntas", false);
        Tarjeta(g, INICIO, "Iniciar con Windows", "Arranca solo con el icono", false);

        // Atajo
        Rellenar(g, rAtajo, E(4), sobre == "atajo" && !capturando ? tema.ControlHover : tema.Control);
        if (capturando) Bordear(g, rAtajo, E(4), tema.Acento, Math.Max(2, E(2)));
        else Bordear(g, rAtajo, E(4), tema.BordeControl, 1);
        string textoAtajo = capturando ? (parcial ?? "Apretá las teclas…") : Config.TextoAtajo(config.AtajoMods, config.AtajoTecla);
        TextRenderer.DrawText(g, textoAtajo, fControl, rAtajo, capturando && parcial == null ? tema.TextoSuave : tema.Texto,
            CENTRADO | TextFormatFlags.EndEllipsis);

        // Paso
        Boton(g, rMenos, GLIFO_MENOS, "menos", config.Paso > 1);
        Boton(g, rMas, GLIFO_MAS, "mas", config.Paso < 25);
        TextRenderer.DrawText(g, config.Paso + " %", fControl, rPaso, tema.Texto, CENTRADO);

        // Tema
        Rellenar(g, rSegmentos, E(4), tema.Control);
        Bordear(g, rSegmentos, E(4), tema.BordeControl, 1);
        for (int k = 0; k < 3; k++)
        {
            var r = Rectangle.Inflate(rSegmento[k], -E(3), -E(3));
            bool elegido = k == config.Tema;
            if (elegido) Rellenar(g, r, E(3), tema.Acento);
            else if (sobre == "tema" + k) Rellenar(g, r, E(3), tema.ControlHover);
            TextRenderer.DrawText(g, TEMAS[k], fControl, rSegmento[k], elegido ? tema.SobreAcento : tema.Texto, CENTRADO);
        }

        Interruptor(g, rMaestro, config.Maestro, sobre == "maestro");
        Interruptor(g, rInicio, inicio, sobre == "inicio");

        if (focoVisible)
            Bordear(g, Rectangle.Inflate(RectControl(foco), E(3), E(3)), E(6), tema.Foco, Math.Max(2, E(2)));
    }

    void Tarjeta(Graphics g, int opcion, string titulo, string ayuda, bool esError)
    {
        var t = tarjetas[opcion];
        Rellenar(g, t, E(6), tema.Tarjeta);
        Bordear(g, t, E(6), tema.BordeTarjeta, 1);
        int izquierda = t.Left + E(16);
        int limite = RectControl(opcion).Left - E(12);
        int medio = Medio(opcion);
        const TextFormatFlags flags = LINEA | TextFormatFlags.Left | TextFormatFlags.EndEllipsis;
        if (ayuda == null)
        {
            TextRenderer.DrawText(g, titulo, fTexto, Rectangle.FromLTRB(izquierda, medio - E(12), limite, medio + E(12)), tema.Texto, flags);
            return;
        }
        TextRenderer.DrawText(g, titulo, fTexto, Rectangle.FromLTRB(izquierda, medio - E(21), limite, medio), tema.Texto, flags);
        TextRenderer.DrawText(g, ayuda, fAyuda, Rectangle.FromLTRB(izquierda, medio + E(1), limite, medio + E(19)),
            esError ? tema.Error : tema.TextoSuave, flags);
    }

    void Boton(Graphics g, Rectangle r, string glifo, string id, bool habilitado)
    {
        Rellenar(g, r, E(4), sobre == id && habilitado ? tema.ControlHover : tema.Control);
        Bordear(g, r, E(4), tema.BordeControl, 1);
        TextRenderer.DrawText(g, glifo, fGlifo, r, habilitado ? tema.Texto : tema.TextoSuave, CENTRADO);
    }

    // Interruptor al estilo de Windows 11.
    void Interruptor(Graphics g, Rectangle r, bool prendido, bool encima)
    {
        float radio = r.Height / 2f;
        float d = encima ? E(14) : E(12);
        float cy = r.Top + r.Height / 2f;
        float cx;
        Color perilla;
        if (prendido)
        {
            Rellenar(g, r, radio, encima ? tema.AcentoActivo : tema.Acento);
            cx = r.Right - radio;
            perilla = tema.SobreAcento;
        }
        else
        {
            if (encima) Rellenar(g, r, radio, tema.ControlHover);
            Bordear(g, r, radio, tema.TextoSuave, 1);
            cx = r.Left + radio;
            perilla = tema.TextoSuave;
        }
        using (var b = new SolidBrush(perilla)) g.FillEllipse(b, cx - d / 2, cy - d / 2, d, d);
    }

    // ---------- Acciones ----------

    string Objetivo(Point p)
    {
        if (rVolver.Contains(p)) return "volver";
        if (rAtajo.Contains(p)) return "atajo";
        if (rMenos.Contains(p)) return "menos";
        if (rMas.Contains(p)) return "mas";
        for (int k = 0; k < 3; k++) if (rSegmento[k].Contains(p)) return "tema" + k;
        if (tarjetas[MAESTRO].Contains(p)) return "maestro";   // como en Windows, toda la tarjeta prende y apaga
        if (tarjetas[INICIO].Contains(p)) return "inicio";
        return null;
    }

    void Accion(string id)
    {
        switch (id)
        {
            case "volver": Ocultar(); volver(); return;
            case "atajo": capturando = !capturando; parcial = error = null; foco = ATAJO; break;
            case "menos": CambiarPaso(-1); foco = PASO; break;
            case "mas": CambiarPaso(1); foco = PASO; break;
            case "maestro": config.Maestro = !config.Maestro; foco = MAESTRO; Aplicar(); break;
            case "inicio":
                try { Config.CambiarInicio(!inicio); } catch { }
                inicio = Config.InicioActivado();
                foco = INICIO;
                break;
            default:
                if (id.StartsWith("tema")) { CambiarTema(id[4] - '0'); foco = TEMA; }
                break;
        }
        Invalidate();
    }

    void CambiarPaso(int delta)
    {
        int v = Math.Max(1, Math.Min(25, config.Paso + delta));
        if (v == config.Paso) return;
        config.Paso = v;
        Aplicar();
    }

    void CambiarTema(int t)
    {
        t = Math.Max(0, Math.Min(2, t));
        if (t == config.Tema) return;
        config.Tema = t;
        Aplicar();
    }

    // Toma la combinación que se está apretando mientras se elige el atajo.
    bool Capturar(Keys keyData)
    {
        Keys tecla = keyData & Keys.KeyCode;
        int mods = 0;
        if ((keyData & Keys.Control) != 0) mods |= Config.MOD_CONTROL;
        if ((keyData & Keys.Alt) != 0) mods |= Config.MOD_ALT;
        if ((keyData & Keys.Shift) != 0) mods |= Config.MOD_SHIFT;
        if (Native.GetAsyncKeyState(0x5B) < 0 || Native.GetAsyncKeyState(0x5C) < 0) mods |= Config.MOD_WIN;

        if (mods == 0 && tecla == Keys.Escape) { capturando = false; parcial = null; }
        else if (mods == 0 && (tecla == Keys.Back || tecla == Keys.Delete)) FijarAtajo(0, 0);
        else if (tecla == Keys.ControlKey || tecla == Keys.ShiftKey || tecla == Keys.Menu || tecla == Keys.LWin || tecla == Keys.RWin)
            parcial = Config.TextoModificadores(mods) + "…";
        else if ((mods & (Config.MOD_CONTROL | Config.MOD_ALT | Config.MOD_WIN)) == 0) { error = "Usá Ctrl, Alt o Win"; parcial = null; }
        else if (!atajoDisponible(mods, (int)tecla)) { error = "Ya lo usa otro programa"; parcial = null; }
        else FijarAtajo(mods, (int)tecla);
        Invalidate();
        return true;
    }

    void FijarAtajo(int mods, int tecla)
    {
        config.AtajoMods = mods;
        config.AtajoTecla = tecla;
        capturando = false;
        parcial = error = null;
        Aplicar();
    }

    // ---------- Mouse y teclado ----------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        apretado = Objetivo(e.Location);
        focoVisible = false;
        if (capturando && apretado != "atajo") { capturando = false; parcial = null; }
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
        if (id != sobre) { sobre = id; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        sobre = null;
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (tarjetas[PASO].Contains(e.Location)) CambiarPaso(Math.Sign(e.Delta));
        else if (tarjetas[TEMA].Contains(e.Location)) CambiarTema(config.Tema - Math.Sign(e.Delta));
        Invalidate();
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (capturando && parcial != null) { parcial = null; Invalidate(); }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (capturando) return Capturar(keyData);
        Keys tecla = keyData & Keys.KeyCode;
        bool shift = (keyData & Keys.Shift) != 0;
        switch (tecla)
        {
            case Keys.Escape: Ocultar(); return true;
            case Keys.Back: Ocultar(); volver(); return true;
            case Keys.Tab: foco = (foco + (shift ? OPCIONES - 1 : 1)) % OPCIONES; break;
            case Keys.Up: foco = (foco + OPCIONES - 1) % OPCIONES; break;
            case Keys.Down: foco = (foco + 1) % OPCIONES; break;
            case Keys.Left:
            case Keys.Right:
                int d = tecla == Keys.Right ? 1 : -1;
                if (foco == PASO) CambiarPaso(d);
                else if (foco == TEMA) CambiarTema(config.Tema + d);
                break;
            case Keys.Enter:
            case Keys.Space:
                if (foco == ATAJO) { capturando = true; parcial = error = null; }
                else if (foco == MAESTRO) Accion("maestro");
                else if (foco == INICIO) Accion("inicio");
                break;
            default: return base.ProcessCmdKey(ref msg, keyData);
        }
        focoVisible = true;
        Invalidate();
        return true;
    }
}

// Instalación por usuario, sin permisos de administrador: el programa va a %LOCALAPPDATA%\Programs\Brillo,
// con acceso directo en el menú Inicio y entrada en "Aplicaciones instaladas" de Windows.
// La usan el instalador (Instalador.cs) y "Brillo.exe --desinstalar".
static class Instalacion
{
    public const string VERSION = "1.0.0";
    public const string EVENTO_SALIR = "Brillo_Salir";   // la copia abierta lo escucha y se cierra
    const string CLAVE_DESINSTALAR = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Brillo";

    public static string Carpeta
    {
        get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Brillo"); }
    }

    public static string Ejecutable { get { return System.IO.Path.Combine(Carpeta, "Brillo.exe"); } }

    static string AccesoMenu
    {
        get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Brillo.lnk"); }
    }

    static string AccesoEscritorio
    {
        get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Brillo.lnk"); }
    }

    public static bool Instalado { get { return System.IO.File.Exists(Ejecutable); } }
    public static bool TieneAccesoEscritorio { get { return System.IO.File.Exists(AccesoEscritorio); } }

    // Le pide a la copia abierta que se cierre sola (así saca bien su icono de la bandeja).
    // Si es una versión vieja que no entiende el aviso, la cierra a la fuerza.
    public static void CerrarCopiaAbierta()
    {
        try
        {
            EventWaitHandle aviso;
            if (EventWaitHandle.TryOpenExisting(EVENTO_SALIR, out aviso))
                using (aviso) aviso.Set();
        }
        catch { }

        int propio = System.Diagnostics.Process.GetCurrentProcess().Id;
        DateTime limite = DateTime.Now.AddSeconds(5);
        foreach (var p in System.Diagnostics.Process.GetProcessesByName("Brillo"))
        {
            if (p.Id == propio) continue;
            try
            {
                int espera = (int)Math.Max(0, (limite - DateTime.Now).TotalMilliseconds);
                if (!p.WaitForExit(espera)) { p.Kill(); p.WaitForExit(2000); }
            }
            catch { }
        }
    }

    public static void Instalar(byte[] programa, bool alIniciar, bool escritorio)
    {
        CerrarCopiaAbierta();
        System.IO.Directory.CreateDirectory(Carpeta);
        System.IO.File.WriteAllBytes(Ejecutable, programa);

        CrearAcceso(AccesoMenu);
        if (escritorio) CrearAcceso(AccesoEscritorio);
        else Borrar(AccesoEscritorio);

        Config.CambiarInicio(alIniciar, Ejecutable);

        using (var k = Registry.CurrentUser.CreateSubKey(CLAVE_DESINSTALAR))
        {
            k.SetValue("DisplayName", "Brillo");
            k.SetValue("DisplayVersion", VERSION);
            k.SetValue("DisplayIcon", Ejecutable);
            k.SetValue("Publisher", "Brillo");
            k.SetValue("InstallLocation", Carpeta);
            k.SetValue("UninstallString", "\"" + Ejecutable + "\" --desinstalar");
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            k.SetValue("EstimatedSize", Math.Max(1, programa.Length / 1024), RegistryValueKind.DWord);
        }
    }

    // Borra todo lo que dejó el instalador, más las opciones guardadas.
    public static void Desinstalar()
    {
        CerrarCopiaAbierta();
        try { Config.CambiarInicio(false); } catch { }
        Borrar(AccesoMenu);
        Borrar(AccesoEscritorio);
        try { Registry.CurrentUser.DeleteSubKeyTree(CLAVE_DESINSTALAR, false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(Config.CLAVE, false); } catch { }

        // El .exe que está corriendo no se puede borrar a sí mismo: lo borra cmd unos segundos después.
        string exe = Application.ExecutablePath;
        if (string.Equals(System.IO.Path.GetDirectoryName(exe), Carpeta, StringComparison.OrdinalIgnoreCase))
        {
            var info = new System.Diagnostics.ProcessStartInfo("cmd.exe",
                "/c ping 127.0.0.1 -n 3 > nul & del /f /q \"" + exe + "\" & rmdir \"" + Carpeta + "\"");
            info.CreateNoWindow = true;
            info.UseShellExecute = false;
            info.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            System.Diagnostics.Process.Start(info);
        }
    }

    // Crea un acceso directo (.lnk) a Brillo.exe con el objeto COM WScript.Shell (sin referencias extra).
    static void CrearAcceso(string ruta)
    {
        Type tipo = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(tipo);
        try
        {
            object acceso = tipo.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { ruta });
            Type t = acceso.GetType();
            const System.Reflection.BindingFlags PONER = System.Reflection.BindingFlags.SetProperty;
            t.InvokeMember("TargetPath", PONER, null, acceso, new object[] { Ejecutable });
            t.InvokeMember("WorkingDirectory", PONER, null, acceso, new object[] { Carpeta });
            t.InvokeMember("Description", PONER, null, acceso, new object[] { "Ajustá el brillo de tus pantallas" });
            t.InvokeMember("IconLocation", PONER, null, acceso, new object[] { Ejecutable + ",0" });
            t.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, acceso, null);
            Marshal.FinalReleaseComObject(acceso);
        }
        finally { Marshal.FinalReleaseComObject(shell); }
    }

    static void Borrar(string ruta)
    {
        try { if (System.IO.File.Exists(ruta)) System.IO.File.Delete(ruta); } catch { }
    }
}

#if TIENDA
// Versión de la Microsoft Store (paquete MSIX, ver tienda\AppxManifest.xml). Dentro del paquete el registro
// "Run" no sirve para arrancar con Windows: se usa la tarea de inicio "BrilloInicio" declarada en el manifiesto.
// Los tipos de Windows van con el nombre completo: un "using Windows.Foundation" chocaría con Point y Size de System.Drawing.
static class Tienda
{
    const string TAREA = "BrilloInicio";

    // Espera una operación asíncrona de Windows sin "await" (que con este compilador pediría el SDK).
    static T Esperar<T>(Windows.Foundation.IAsyncOperation<T> op)
    {
        DateTime limite = DateTime.Now.AddSeconds(5);
        while (op.Status == Windows.Foundation.AsyncStatus.Started && DateTime.Now < limite) Thread.Sleep(10);
        return op.GetResults();
    }

    static Windows.ApplicationModel.StartupTask Tarea()
    {
        try { return Esperar(Windows.ApplicationModel.StartupTask.GetAsync(TAREA)); }
        catch { return null; }
    }

    public static bool InicioActivado()
    {
        var t = Tarea();
        return t != null && (t.State == Windows.ApplicationModel.StartupTaskState.Enabled ||
                             t.State == Windows.ApplicationModel.StartupTaskState.EnabledByPolicy);
    }

    public static void CambiarInicio(bool activar)
    {
        var t = Tarea();
        if (t == null) return;
        if (!activar) { t.Disable(); return; }
        var estado = Esperar(t.RequestEnableAsync());
        // Si el usuario lo apagó desde el Administrador de tareas o Configuración, Windows no deja que el
        // programa lo vuelva a prender: abrimos la página de aplicaciones de inicio para que lo haga ahí.
        if (estado == Windows.ApplicationModel.StartupTaskState.DisabledByUser)
            try { System.Diagnostics.Process.Start("ms-settings:startupapps"); } catch { }
    }

    // ¿Lo abrió Windows al iniciar sesión (y no el usuario)?
    public static bool AbiertoAlIniciar()
    {
        try
        {
            var a = Windows.ApplicationModel.AppInstance.GetActivatedEventArgs();
            return a != null && a.Kind == Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
        }
        catch { return false; }
    }
}
#endif

static class Programa
{
    [STAThread]
    static void Main(string[] args)
    {
#if !TIENDA
        if (Array.IndexOf(args, "--desinstalar") >= 0)
        {
            // Lo llama Windows desde "Aplicaciones instaladas".
            Dpi.Iniciar();
            Application.EnableVisualStyles();
            if (MessageBox.Show("¿Querés desinstalar Brillo?\n\nSe borran el programa, sus accesos directos, " +
                    "el inicio automático y las opciones guardadas.", "Desinstalar Brillo",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Instalacion.Desinstalar();
                MessageBox.Show("Brillo se desinstaló.", "Brillo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return;
        }
#endif

        int fijo = -1;
        if (args.Length == 0 || !int.TryParse(args[0].TrimEnd('%'), out fijo)) fijo = -1;

        if (fijo >= 0)
        {
            foreach (var p in Pantalla.DetectarTodas(null)) { p.Escribir(Math.Min(100, fijo)); p.Liberar(); }
            return;
        }

        // Si ya hay una copia corriendo, le pedimos que se muestre y salimos.
        bool nueva;
        var senal = new EventWaitHandle(false, EventResetMode.AutoReset, "Brillo_Mostrar", out nueva);
        if (!nueva)
        {
            // Le damos permiso a la otra copia para ponerse al frente (si no, Windows se lo niega).
            try { Native.AllowSetForegroundWindow(-1); } catch { }
            senal.Set();
            return;
        }

        Dpi.Iniciar();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var config = Config.Cargar();
        var trabajador = new Trabajador();
        List<Aviso> avisos;
        var pantallas = trabajador.DetectarYEsperar(out avisos);
        var ventana = new Ventana(trabajador, pantallas, avisos, config);
        ventana.Preparar();

        var hilo = new Thread(delegate()
        {
            while (true) { senal.WaitOne(); ventana.Invocar(ventana.Mostrar); }
        });
        hilo.IsBackground = true;
        hilo.Start();

        // El instalador y el desinstalador nos piden que nos cerremos antes de reemplazar o borrar el .exe.
        var salir = new EventWaitHandle(false, EventResetMode.AutoReset, Instalacion.EVENTO_SALIR);
        var hiloSalir = new Thread(delegate()
        {
            salir.WaitOne();
            ventana.Invocar(ventana.Salir);
        });
        hiloSalir.IsBackground = true;
        hiloSalir.Start();

        bool oculto = Array.IndexOf(args, "--oculto") >= 0;
#if TIENDA
        // La tarea de inicio del paquete no pasa argumentos: preguntamos a Windows cómo nos abrió.
        oculto = oculto || Tienda.AbiertoAlIniciar();
        // La Store no deja preguntar nada al instalar, así que lo preguntamos la primera vez que se abre.
        if (Config.PrimeraVez() && !oculto)
        {
            if (MessageBox.Show("¿Querés que Brillo se abra solo al iniciar Windows?\n\n" +
                    "Queda como un icono junto al reloj. Lo podés cambiar cuando quieras en Configuración.",
                    "Brillo", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                Config.CambiarInicio(true);
        }
#endif
        if (!oculto) ventana.Mostrar();
        Application.Run();
        GC.KeepAlive(senal);
        GC.KeepAlive(salir);
    }
}

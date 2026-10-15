// DesktopPerDesktop - una cartella Desktop diversa per ogni desktop virtuale di Windows.
// Compilare con build.bat (usa il compilatore C# incluso in .NET Framework, nessun SDK richiesto).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// Nome e versione mostrati da Windows (Gestione attività, Impostazioni > App > Avvio)
[assembly: System.Reflection.AssemblyTitle("Desktop per Desktop")]
[assembly: System.Reflection.AssemblyProduct("Desktop per Desktop")]
[assembly: System.Reflection.AssemblyDescription("Una cartella Desktop diversa per ogni desktop virtuale")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

static class Native
{
    public static Guid FOLDERID_Desktop = new Guid("B4BFCC3A-DB2C-424C-B029-7FE99A87C641");

    [DllImport("shell32.dll")]
    public static extern int SHSetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken,
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath);

    [DllImport("shell32.dll")]
    public static extern int SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, string dwItem1, IntPtr dwItem2);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string cls, string title);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    public static string GetDesktopPath()
    {
        IntPtr p;
        if (SHGetKnownFolderPath(ref FOLDERID_Desktop, 0, IntPtr.Zero, out p) != 0) return null;
        string s = Marshal.PtrToStringUni(p);
        Marshal.FreeCoTaskMem(p);
        return s;
    }

    public static bool SetDesktopPath(string path)
    {
        string old = GetDesktopPath();
        int hr = SHSetKnownFolderPath(ref FOLDERID_Desktop, 0, IntPtr.Zero, path);
        if (hr != 0) return false;
        RefreshDesktop(old, path);
        return true;
    }

    static void RefreshDesktop(string oldPath, string newPath)
    {
        const int SHCNE_UPDATEDIR = 0x00001000;
        const uint SHCNF_PATHW = 0x0005;
        if (oldPath != null) SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, oldPath, IntPtr.Zero);
        SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, newPath, IntPtr.Zero);
        PressF5();
    }

    // Equivalente di premere F5 sul desktop
    static void PressF5()
    {
        IntPtr defView = FindDesktopDefView();
        if (defView != IntPtr.Zero) PostMessage(defView, 0x0111 /*WM_COMMAND*/, (IntPtr)0x7103, IntPtr.Zero);
    }

    // Aggiornamento pesante (Explorer ricarica tutte le icone): solo se quello normale non basta
    public static void ForceRefresh()
    {
        SHChangeNotify(0x08000000 /*SHCNE_ASSOCCHANGED*/, 0, null, IntPtr.Zero);
        PressF5();
    }

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool RedrawWindow(IntPtr hWnd, IntPtr rect, IntPtr rgn, uint flags);

    static IntPtr frozenList = IntPtr.Zero;

    // Sospende il disegno delle icone mentre Explorer carica la nuova cartella.
    // (Nasconderle non va bene: sotto non c'è lo sfondo e lo schermo diventa nero.)
    public static void FreezeIcons()
    {
        if (frozenList != IntPtr.Zero) return;
        IntPtr dv = FindDesktopDefView();
        IntPtr list = dv == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(dv, IntPtr.Zero, "SysListView32", null);
        if (list == IntPtr.Zero) return;
        SendMessage(list, 0x000B /*WM_SETREDRAW*/, IntPtr.Zero, IntPtr.Zero);
        frozenList = list;
    }

    public static void UnfreezeIcons()
    {
        if (frozenList == IntPtr.Zero) return;
        SendMessage(frozenList, 0x000B /*WM_SETREDRAW*/, (IntPtr)1, IntPtr.Zero);
        RedrawWindow(frozenList, IntPtr.Zero, IntPtr.Zero, 0x0001 | 0x0004 | 0x0080 /*INVALIDATE|ERASE|ALLCHILDREN*/);
        frozenList = IntPtr.Zero;
    }

    static IntPtr FindDesktopDefView()
    {
        IntPtr progman = FindWindow("Progman", null);
        IntPtr dv = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (dv != IntPtr.Zero) return dv;
        IntPtr worker = IntPtr.Zero;
        while ((worker = FindWindowEx(IntPtr.Zero, worker, "WorkerW", null)) != IntPtr.Zero)
        {
            dv = FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (dv != IntPtr.Zero) return dv;
        }
        return IntPtr.Zero;
    }
}

// ---- Posizioni delle icone: interfacce COM della vista del desktop di Explorer ----

[StructLayout(LayoutKind.Sequential)]
struct POINT { public int X, Y; }

[ComImport, Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")]
class CShellWindows { }

[ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
interface IShellWindows
{
    void _get_Count(); void _Item(); void _NewEnum(); void _Register(); void _RegisterPending();
    void _Revoke(); void _OnNavigate(); void _OnActivated();
    [return: MarshalAs(UnmanagedType.IDispatch)]
    object FindWindowSW(ref object pvarLoc, ref object pvarLocRoot, int swClass, out int phwnd, int swfwOptions);
}

[ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IServiceProvider
{
    [return: MarshalAs(UnmanagedType.IUnknown)]
    object QueryService(ref Guid guidService, ref Guid riid);
}

[ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IShellBrowser
{
    void _GetWindow(); void _ContextSensitiveHelp(); void _InsertMenusSB(); void _SetMenuSB(); void _RemoveMenusSB();
    void _SetStatusTextSB(); void _EnableModelessSB(); void _TranslateAcceleratorSB(); void _BrowseObject();
    void _GetViewStateStream(); void _GetControlWindow(); void _SendControlMsg();
    [return: MarshalAs(UnmanagedType.IUnknown)]
    object QueryActiveShellView();
}

[ComImport, Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IFolderView
{
    void _GetCurrentViewMode(); void _SetCurrentViewMode();
    [return: MarshalAs(UnmanagedType.IUnknown)]
    object GetFolder(ref Guid riid);
    IntPtr Item(int index);
    int ItemCount(uint flags);
    void _Items(); void _GetSelectionMarkedItem(); void _GetFocusedItem();
    POINT GetItemPosition(IntPtr pidl);
    void _GetSpacing(); void _GetDefaultSpacing(); void _GetAutoArrange(); void _SelectItem();
    void SelectAndPositionItems(uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
        [MarshalAs(UnmanagedType.LPArray)] POINT[] apt, uint flags);
}

[ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IShellFolder
{
    void _ParseDisplayName(); void _EnumObjects(); void _BindToObject(); void _BindToStorage(); void _CompareIDs();
    void _CreateViewObject(); void _GetAttributesOf(); void _GetUIObjectOf();
    void GetDisplayNameOf(IntPtr pidl, uint flags, IntPtr pName);
}

static class IconLayout
{
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    static extern int StrRetToBuf(IntPtr pstr, IntPtr pidl, StringBuilder buf, int cch);

    static IFolderView GetDesktopView()
    {
        try
        {
            IShellWindows sw = (IShellWindows)new CShellWindows();
            object loc = 0 /*CSIDL_DESKTOP*/, root = null;
            int hwnd;
            object disp = sw.FindWindowSW(ref loc, ref root, 8 /*SWC_DESKTOP*/, out hwnd, 1 /*SWFO_NEEDDISPATCH*/);
            Guid sid = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837"); // SID_STopLevelBrowser
            Guid iid = typeof(IShellBrowser).GUID;
            IShellBrowser browser = (IShellBrowser)((IServiceProvider)disp).QueryService(ref sid, ref iid);
            return (IFolderView)browser.QueryActiveShellView();
        }
        catch { return null; }
    }

    static string NameOf(IShellFolder folder, IntPtr pidl)
    {
        IntPtr strret = Marshal.AllocCoTaskMem(512);
        try
        {
            folder.GetDisplayNameOf(pidl, 0x8001 /*SHGDN_INFOLDER|SHGDN_FORPARSING*/, strret);
            StringBuilder sb = new StringBuilder(1024);
            return StrRetToBuf(strret, pidl, sb, sb.Capacity) == 0 ? sb.ToString() : null;
        }
        catch { return null; }
        finally { Marshal.FreeCoTaskMem(strret); }
    }

    // Visita ogni icona della vista corrente: (nome, pidl)
    static void ForEachItem(IFolderView view, Action<string, IntPtr> action)
    {
        Guid iidFolder = typeof(IShellFolder).GUID;
        IShellFolder folder = (IShellFolder)view.GetFolder(ref iidFolder);
        int count = view.ItemCount(2 /*SVGIO_ALLVIEW*/);
        for (int i = 0; i < count; i++)
        {
            IntPtr pidl = view.Item(i);
            try
            {
                string name = NameOf(folder, pidl);
                if (name != null) action(name, pidl);
            }
            finally { Marshal.FreeCoTaskMem(pidl); }
        }
    }

    // Nomi delle icone attualmente mostrate sul desktop (null se la vista non è disponibile)
    public static HashSet<string> CurrentNames()
    {
        IFolderView view = GetDesktopView();
        if (view == null) return null;
        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try { ForEachItem(view, delegate(string name, IntPtr pidl) { names.Add(name); }); }
        catch { return null; }
        return names;
    }

    public static Dictionary<string, Point> Capture()
    {
        IFolderView view = GetDesktopView();
        if (view == null) return null;
        Dictionary<string, Point> result = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
        try
        {
            ForEachItem(view, delegate(string name, IntPtr pidl) {
                POINT p = view.GetItemPosition(pidl);
                result[name] = new Point(p.X, p.Y);
            });
        }
        catch { return null; }
        return result;
    }

    // Riposiziona tutte le icone in un'unica chiamata.
    // Restituisce il numero di icone presenti nella vista (-1 se la vista non è disponibile).
    public static int Apply(Dictionary<string, Point> layout)
    {
        IFolderView view = GetDesktopView();
        if (view == null) return -1;
        try
        {
            Guid iidFolder = typeof(IShellFolder).GUID;
            IShellFolder folder = (IShellFolder)view.GetFolder(ref iidFolder);
            int count = view.ItemCount(2 /*SVGIO_ALLVIEW*/);
            if (layout == null || layout.Count == 0) return count;
            List<IntPtr> pidls = new List<IntPtr>();
            List<POINT> pts = new List<POINT>();
            try
            {
                for (int i = 0; i < count; i++)
                {
                    IntPtr pidl = view.Item(i);
                    string name = NameOf(folder, pidl);
                    Point p;
                    if (name == null || !layout.TryGetValue(name, out p)) { Marshal.FreeCoTaskMem(pidl); continue; }
                    POINT pt; pt.X = p.X; pt.Y = p.Y;
                    pidls.Add(pidl); pts.Add(pt);
                }
                if (pidls.Count > 0)
                    view.SelectAndPositionItems((uint)pidls.Count, pidls.ToArray(), pts.ToArray(), 0x80 /*SVSI_POSITIONITEM*/);
            }
            finally { foreach (IntPtr p in pidls) Marshal.FreeCoTaskMem(p); }
            return count;
        }
        catch { return -1; }
    }
}

// Posizioni salvate per ogni cartella Desktop, in %APPDATA%\DesktopPerDesktop\layout.txt
class LayoutStore
{
    static readonly string FilePath = Path.Combine(Config.Dir, "layout.txt");
    Dictionary<string, Dictionary<string, Point>> data =
        new Dictionary<string, Dictionary<string, Point>>(StringComparer.OrdinalIgnoreCase);

    public LayoutStore()
    {
        if (!File.Exists(FilePath)) return;
        foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
        {
            string[] f = line.Split('\t');
            int x, y;
            if (f.Length != 4 || !int.TryParse(f[2], out x) || !int.TryParse(f[3], out y)) continue;
            Get(f[0], true)[f[1]] = new Point(x, y);
        }
    }

    public Dictionary<string, Point> Get(string folder, bool create)
    {
        Dictionary<string, Point> d;
        if (!data.TryGetValue(folder, out d) && create)
        {
            d = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
            data[folder] = d;
        }
        return d;
    }

    // Aggiorna le posizioni della cartella; restituisce true se qualcosa è cambiato
    public bool Update(string folder, Dictionary<string, Point> captured)
    {
        if (captured == null || captured.Count == 0) return false;
        Dictionary<string, Point> d = Get(folder, true);
        bool changed = false;
        foreach (KeyValuePair<string, Point> kv in captured)
        {
            Point old;
            if (!d.TryGetValue(kv.Key, out old) || old != kv.Value) { d[kv.Key] = kv.Value; changed = true; }
        }
        return changed;
    }

    public void Save()
    {
        Directory.CreateDirectory(Config.Dir);
        List<string> lines = new List<string>();
        foreach (KeyValuePair<string, Dictionary<string, Point>> folder in data)
            foreach (KeyValuePair<string, Point> kv in folder.Value)
                lines.Add(folder.Key + "\t" + kv.Key + "\t" + kv.Value.X + "\t" + kv.Value.Y);
        File.WriteAllLines(FilePath, lines.ToArray(), Encoding.UTF8);
    }
}

static class VirtualDesktops
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";

    static byte[] ReadBinary(string subKey, string name)
    {
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(subKey))
        {
            if (k == null) return null;
            return k.GetValue(name) as byte[];
        }
    }

    static string SessionKey()
    {
        return @"Software\Microsoft\Windows\CurrentVersion\Explorer\SessionInfo\" +
               Process.GetCurrentProcess().SessionId + @"\VirtualDesktops";
    }

    static Guid ToGuid(byte[] b, int offset)
    {
        byte[] g = new byte[16];
        Array.Copy(b, offset, g, 0, 16);
        return new Guid(g);
    }

    public static Guid Current()
    {
        byte[] b = ReadBinary(Key, "CurrentVirtualDesktop");
        if (b == null || b.Length < 16) b = ReadBinary(SessionKey(), "CurrentVirtualDesktop");
        if (b == null || b.Length < 16) return Guid.Empty;
        return ToGuid(b, 0);
    }

    public static List<Guid> All()
    {
        List<Guid> list = new List<Guid>();
        byte[] b = ReadBinary(Key, "VirtualDesktopIDs");
        if (b == null) b = ReadBinary(SessionKey(), "VirtualDesktopIDs");
        if (b == null) return list;
        for (int i = 0; i + 16 <= b.Length; i += 16) list.Add(ToGuid(b, i));
        return list;
    }

    public static string Name(Guid id)
    {
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Key + @"\Desktops\" + id.ToString("B").ToUpper()))
        {
            string n = k == null ? null : k.GetValue("Name") as string;
            if (!string.IsNullOrEmpty(n)) return n;
        }
        int idx = All().IndexOf(id);
        return idx >= 0 ? "Desktop " + (idx + 1) : "Desktop";
    }

    public static int Index(Guid id)
    {
        return All().IndexOf(id);
    }
}

class Config
{
    public static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopPerDesktop");
    static readonly string FilePath = Path.Combine(Dir, "config.txt");

    public string Original;
    public int TouchpadSign = 1; // verso dello swipe, calibrato automaticamente
    public Dictionary<Guid, string> Map = new Dictionary<Guid, string>();

    public static Config Load()
    {
        Config c = new Config();
        if (File.Exists(FilePath))
        {
            foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                Guid g;
                if (k == "original") c.Original = v;
                else if (k == "touchpadSign") c.TouchpadSign = v == "-1" ? -1 : 1;
                else if (Guid.TryParse(k, out g) && v.Length > 0) c.Map[g] = v;
            }
        }
        if (string.IsNullOrEmpty(c.Original))
        {
            // Primo avvio: la cartella Desktop attuale diventa quella "originale"
            c.Original = Native.GetDesktopPath();
            c.Save();
        }
        return c;
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        List<string> lines = new List<string>();
        lines.Add("original=" + Original);
        lines.Add("touchpadSign=" + TouchpadSign);
        foreach (KeyValuePair<Guid, string> kv in Map) lines.Add(kv.Key.ToString("B").ToUpper() + "=" + kv.Value);
        File.WriteAllLines(FilePath, lines.ToArray(), Encoding.UTF8);
    }

    public string PathFor(Guid id)
    {
        string p;
        return Map.TryGetValue(id, out p) ? p : Original;
    }
}

// Installazione per l'utente corrente: menu Start, App installate, App di avvio
static class Installer
{
    public const string AppName = "DesktopPerDesktop";
    public const string DisplayName = "Desktop per Desktop";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName;

    public static readonly string InstallDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\" + AppName);
    public static readonly string InstalledExe = Path.Combine(InstallDir, AppName + ".exe");
    static readonly string ShortcutPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), DisplayName + ".lnk");

    // Windows (Impostazioni > App > Avvio, Gestione attività) segna le app disattivate
    // in StartupApproved\Run: il primo byte dispari significa "disattivata".
    public static bool IsStartupEnabled()
    {
        using (RegistryKey run = Registry.CurrentUser.OpenSubKey(RunKey))
            if (run == null || run.GetValue(AppName) == null) return false;
        using (RegistryKey ap = Registry.CurrentUser.OpenSubKey(ApprovedKey))
        {
            byte[] b = ap == null ? null : ap.GetValue(AppName) as byte[];
            return b == null || b.Length == 0 || (b[0] & 1) == 0;
        }
    }

    public static void SetStartup(bool enabled)
    {
        using (RegistryKey run = Registry.CurrentUser.CreateSubKey(RunKey))
            run.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
        byte[] state = new byte[12];
        state[0] = (byte)(enabled ? 2 : 3);
        if (!enabled) Array.Copy(BitConverter.GetBytes(DateTime.Now.ToFileTime()), 0, state, 4, 8);
        using (RegistryKey ap = Registry.CurrentUser.CreateSubKey(ApprovedKey))
            ap.SetValue(AppName, state, RegistryValueKind.Binary);
    }

    static void StopOtherInstances()
    {
        int me = Process.GetCurrentProcess().Id;
        foreach (Process p in Process.GetProcessesByName(AppName))
        {
            if (p.Id == me) continue;
            try { p.Kill(); p.WaitForExit(3000); } catch { }
        }
    }

    static void CreateShortcut(string lnkPath, string target)
    {
        Type t = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(t);
        object lnk = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
        Type lt = lnk.GetType();
        lt.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, lnk, new object[] { target });
        lt.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, lnk, new object[] { Path.GetDirectoryName(target) });
        lt.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, lnk, new object[] { "Una cartella Desktop diversa per ogni desktop virtuale" });
        lt.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, lnk, new object[] { target + ",0" });
        lt.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, lnk, null);
    }

    public static void Install()
    {
        StopOtherInstances();
        Directory.CreateDirectory(InstallDir);
        if (!string.Equals(Path.GetFullPath(Application.ExecutablePath), InstalledExe, StringComparison.OrdinalIgnoreCase))
            File.Copy(Application.ExecutablePath, InstalledExe, true);

        CreateShortcut(ShortcutPath, InstalledExe);

        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            k.SetValue("DisplayName", DisplayName);
            k.SetValue("DisplayIcon", InstalledExe + ",0");
            k.SetValue("DisplayVersion", "1.0");
            k.SetValue("Publisher", AppName);
            k.SetValue("InstallLocation", InstallDir);
            k.SetValue("UninstallString", "\"" + InstalledExe + "\" --uninstall");
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            k.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024) + 1, RegistryValueKind.DWord);
        }

        // Avvio automatico attivo di default, puntando alla copia installata
        using (RegistryKey run = Registry.CurrentUser.CreateSubKey(RunKey))
            run.SetValue(AppName, "\"" + InstalledExe + "\"");
        using (RegistryKey ap = Registry.CurrentUser.CreateSubKey(ApprovedKey))
            ap.SetValue(AppName, new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);

        Process.Start(InstalledExe);
    }

    public static void Uninstall()
    {
        if (MessageBox.Show("Disinstallare " + DisplayName + "?\n\nIl Desktop tornerà quello originale. " +
            "Le cartelle che hai assegnato ai desktop e i loro file NON vengono toccati.",
            DisplayName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        StopOtherInstances();
        Config c = Config.Load();
        if (!string.IsNullOrEmpty(c.Original)) Native.SetDesktopPath(c.Original);

        using (RegistryKey run = Registry.CurrentUser.OpenSubKey(RunKey, true))
            if (run != null) run.DeleteValue(AppName, false);
        using (RegistryKey ap = Registry.CurrentUser.OpenSubKey(ApprovedKey, true))
            if (ap != null) ap.DeleteValue(AppName, false);
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
        try { File.Delete(ShortcutPath); } catch { }

        if (MessageBox.Show("Eliminare anche le impostazioni salvate (cartelle assegnate e posizioni delle icone)?",
            DisplayName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            try { Directory.Delete(Config.Dir, true); } catch { }
        }

        // L'eseguibile non può cancellarsi da solo mentre è in esecuzione
        ProcessStartInfo psi = new ProcessStartInfo("cmd.exe",
            "/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"" + InstallDir + "\"");
        psi.WindowStyle = ProcessWindowStyle.Hidden;
        psi.CreateNoWindow = true;
        Process.Start(psi);
    }
}

// Legge il touchpad di precisione (Raw Input HID) e riconosce lo swipe orizzontale
// a quattro dita mentre è ancora in corso, prima che Windows completi il cambio di desktop.
class TouchpadWatcher : NativeWindow
{
    public event Action<int> Swipe; // -1 = dita verso sinistra, +1 = verso destra
    public Action<string> Debug;

    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTDEVICE { public ushort UsagePage, Usage; public uint Flags; public IntPtr Target; }

    [DllImport("user32.dll")]
    static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);
    [DllImport("user32.dll")]
    static extern uint GetRawInputData(IntPtr hRawInput, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("user32.dll")]
    static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint command, IntPtr data, ref uint size);
    [DllImport("hid.dll")]
    static extern int HidP_GetCaps(IntPtr preparsed, byte[] caps);
    [DllImport("hid.dll")]
    static extern int HidP_GetValueCaps(int reportType, byte[] caps, ref ushort length, IntPtr preparsed);
    [DllImport("hid.dll")]
    static extern int HidP_GetUsageValue(int reportType, ushort page, ushort link, ushort usage,
        out uint value, IntPtr preparsed, byte[] report, uint reportLength);
    [DllImport("hid.dll")]
    static extern int HidP_GetUsages(int reportType, ushort page, ushort link, ushort[] usages,
        ref uint length, IntPtr preparsed, byte[] report, uint reportLength);

    const int HIDP_STATUS_SUCCESS = 0x00110000;
    const double Threshold = 0.07; // frazione della larghezza del touchpad

    class Device
    {
        public IntPtr Preparsed;
        public List<ushort> Fingers = new List<ushort>(); // link collection di ogni dito
        public ushort CountLink;
        public bool HasCount;
        public int XMin, XMax = 1, YMin, YMax = 1;
    }

    Dictionary<IntPtr, Device> devices = new Dictionary<IntPtr, Device>();
    List<PointF> frame = new List<PointF>();
    int frameExpected, frameSeen;
    bool tracking, fired;
    int trackedFingers;
    double startX, startY;

    public TouchpadWatcher()
    {
        CreateHandle(new CreateParams());
        RAWINPUTDEVICE d;
        d.UsagePage = 0x0D; d.Usage = 0x05; // Digitizer / Touch Pad
        d.Flags = 0x00000100;               // RIDEV_INPUTSINK: riceve l'input anche in background
        d.Target = Handle;
        RegisterRawInputDevices(new RAWINPUTDEVICE[] { d }, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x00FF /*WM_INPUT*/) { try { OnInput(m.LParam); } catch { } }
        base.WndProc(ref m);
    }

    void OnInput(IntPtr hRaw)
    {
        uint header = (uint)(8 + IntPtr.Size * 2), size = 0;
        GetRawInputData(hRaw, 0x10000003 /*RID_INPUT*/, IntPtr.Zero, ref size, header);
        if (size == 0) return;
        IntPtr buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(hRaw, 0x10000003, buf, ref size, header) != size) return;
            if (Marshal.ReadInt32(buf, 0) != 2 /*RIM_TYPEHID*/) return;
            Device dev = GetDevice(Marshal.ReadIntPtr(buf, 8));
            if (dev == null || dev.Fingers.Count == 0) return;
            int sizeHid = Marshal.ReadInt32(buf, (int)header), count = Marshal.ReadInt32(buf, (int)header + 4);
            byte[] report = new byte[sizeHid];
            for (int i = 0; i < count; i++)
            {
                Marshal.Copy(IntPtr.Add(buf, (int)header + 8 + i * sizeHid), report, 0, sizeHid);
                OnReport(dev, report);
            }
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    Device GetDevice(IntPtr hDevice)
    {
        Device dev;
        if (devices.TryGetValue(hDevice, out dev)) return dev;
        dev = new Device();
        devices[hDevice] = dev;
        uint size = 0;
        GetRawInputDeviceInfo(hDevice, 0x20000005 /*RIDI_PREPARSEDDATA*/, IntPtr.Zero, ref size);
        if (size == 0) return dev;
        dev.Preparsed = Marshal.AllocHGlobal((int)size);
        GetRawInputDeviceInfo(hDevice, 0x20000005, dev.Preparsed, ref size);

        byte[] caps = new byte[64];
        if (HidP_GetCaps(dev.Preparsed, caps) != HIDP_STATUS_SUCCESS) return dev;
        ushort n = BitConverter.ToUInt16(caps, 48); // NumberInputValueCaps
        byte[] vc = new byte[72 * Math.Max(1, (int)n)];
        if (HidP_GetValueCaps(0 /*HidP_Input*/, vc, ref n, dev.Preparsed) != HIDP_STATUS_SUCCESS) return dev;
        for (int i = 0; i < n; i++)
        {
            int b = i * 72;
            ushort page = BitConverter.ToUInt16(vc, b), link = BitConverter.ToUInt16(vc, b + 6);
            ushort usage = BitConverter.ToUInt16(vc, b + 56);
            int min = BitConverter.ToInt32(vc, b + 40), max = BitConverter.ToInt32(vc, b + 44);
            if (page == 0x01 && usage == 0x30) { if (!dev.Fingers.Contains(link)) dev.Fingers.Add(link); dev.XMin = min; dev.XMax = max; }
            else if (page == 0x01 && usage == 0x31) { dev.YMin = min; dev.YMax = max; }
            else if (page == 0x0D && usage == 0x54) { dev.CountLink = link; dev.HasCount = true; }
        }
        if (dev.XMax <= dev.XMin) dev.XMax = dev.XMin + 1;
        if (dev.YMax <= dev.YMin) dev.YMax = dev.YMin + 1;
        return dev;
    }

    bool TipDown(Device dev, ushort link, byte[] report)
    {
        ushort[] usages = new ushort[16];
        uint len = 16;
        if (HidP_GetUsages(0, 0x0D, link, usages, ref len, dev.Preparsed, report, (uint)report.Length) != HIDP_STATUS_SUCCESS)
            return false;
        for (int i = 0; i < len; i++) if (usages[i] == 0x42 /*Tip Switch*/) return true;
        return false;
    }

    void OnReport(Device dev, byte[] report)
    {
        uint len = (uint)report.Length, v;
        // Il "contact count" è presente solo nel primo report di ogni fotogramma
        if (dev.HasCount && HidP_GetUsageValue(0, 0x0D, dev.CountLink, 0x54, out v, dev.Preparsed, report, len) == HIDP_STATUS_SUCCESS && v > 0)
        {
            frame.Clear();
            frameExpected = (int)v;
            frameSeen = 0;
        }
        foreach (ushort link in dev.Fingers)
        {
            if (frameSeen >= frameExpected) break;
            frameSeen++;
            uint x, y;
            if (HidP_GetUsageValue(0, 0x01, link, 0x30, out x, dev.Preparsed, report, len) != HIDP_STATUS_SUCCESS) continue;
            if (HidP_GetUsageValue(0, 0x01, link, 0x31, out y, dev.Preparsed, report, len) != HIDP_STATUS_SUCCESS) continue;
            if (!TipDown(dev, link, report)) continue;
            frame.Add(new PointF((float)(((int)x - dev.XMin) / (double)(dev.XMax - dev.XMin)),
                                 (float)(((int)y - dev.YMin) / (double)(dev.YMax - dev.YMin))));
        }
        if (frameSeen >= frameExpected && frameExpected > 0) { OnFrame(); frameExpected = 0; }
    }

    void OnFrame()
    {
        int n = frame.Count;
        if (n < 4) { tracking = false; return; }
        double ax = 0, ay = 0;
        foreach (PointF p in frame) { ax += p.X; ay += p.Y; }
        ax /= n; ay /= n;
        if (!tracking || n != trackedFingers)
        {
            tracking = true; fired = false; trackedFingers = n;
            if (Debug != null) Debug(n + " dita sul touchpad");
            startX = ax; startY = ay;
            return;
        }
        if (fired) return;
        double dx = ax - startX, dy = ay - startY;
        if (Math.Abs(dx) > Threshold && Math.Abs(dx) > 1.5 * Math.Abs(dy))
        {
            fired = true;
            if (Debug != null) Debug("swipe riconosciuto, dx=" + dx.ToString("0.000"));
            if (Swipe != null) Swipe(dx > 0 ? 1 : -1);
        }
    }
}

class TrayApp : ApplicationContext
{
    const string AppName = Installer.AppName;

    Config config;
    NotifyIcon tray;
    System.Windows.Forms.Timer timer;
    Guid lastDesktop = Guid.Empty;
    string lastApplied;
    ToolStripMenuItem startupItem;
    IntPtr iconHandle = IntPtr.Zero;
    SettingsForm settings;
    LayoutStore layouts = new LayoutStore();
    System.Windows.Forms.Timer restoreTimer;
    int restoreTicks, restoreStable, restoreLastCount;
    bool restoreLoaded;

    // Registro dei tempi in %APPDATA%\DesktopPerDesktop\debug.log
    List<string> logLines = new List<string>();

    void Log(string msg)
    {
        logLines.Add(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg);
        if (logLines.Count > 200) FlushLog();
    }

    void FlushLog()
    {
        if (logLines.Count == 0) return;
        try
        {
            string path = Path.Combine(Config.Dir, "debug.log");
            FileInfo fi = new FileInfo(path);
            if (fi.Exists && fi.Length > 200000) fi.Delete();
            logLines.Add("");
            File.AppendAllText(path, string.Join(Environment.NewLine, logLines.ToArray()) + Environment.NewLine, Encoding.UTF8);
        }
        catch { }
        logLines.Clear();
    }
    Control invoker;

    delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    HookProc hookProc; // tenuto in un campo perché il garbage collector non lo elimini
    IntPtr hookHandle;

    [DllImport("user32.dll")]
    static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")]
    static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandle(string name);

    static bool Down(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

    IntPtr KeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        int msg = (int)wParam;
        if (nCode >= 0 && (msg == 0x0100 || msg == 0x0104)) // WM_KEYDOWN / WM_SYSKEYDOWN
        {
            int vk = Marshal.ReadInt32(lParam);
            if ((vk == 0x25 || vk == 0x27) && (Down(0x5B) || Down(0x5C)) && Down(0x11))
            {
                int dir = vk == 0x25 ? -1 : 1;
                invoker.BeginInvoke(new Action(delegate { PreSwitch(dir, false); }));
            }
        }
        return CallNextHookEx(hookHandle, nCode, wParam, lParam);
    }

    // Previsione in corso: finché Windows non conferma, il registro indica ancora il desktop di partenza
    Guid predictedFrom = Guid.Empty;
    int predictedDir;
    bool predictedByTouch;
    DateTime predictedUntil = DateTime.MinValue, calibrateUntil = DateTime.MinValue;
    TouchpadWatcher touchpad;

    void OnTouchpadSwipe(int fingers)
    {
        // Di default: dita verso sinistra = desktop successivo (a destra).
        // Se Windows va dalla parte opposta, TouchpadSign si corregge da solo.
        PreSwitch((fingers < 0 ? 1 : -1) * config.TouchpadSign, true);
    }

    // Prevede il desktop di destinazione e cambia cartella prima che Windows aggiorni il registro
    void PreSwitch(int dir, bool touch)
    {
        List<Guid> ids = VirtualDesktops.All();
        int idx = ids.IndexOf(lastDesktop);
        if (idx < 0) idx = ids.IndexOf(VirtualDesktops.Current());
        int next = idx + dir;
        if (idx < 0 || next < 0 || next >= ids.Count) return;
        Log((touch ? "previsione touchpad" : "previsione tastiera") + ": desktop " + (next + 1));
        predictedFrom = ids[idx];
        predictedDir = dir;
        predictedByTouch = touch;
        predictedUntil = DateTime.Now.AddMilliseconds(touch ? 2500 : 1000);
        calibrateUntil = DateTime.Now.AddSeconds(5);
        lastDesktop = ids[next];
        string target = config.PathFor(lastDesktop);
        if (!string.Equals(target, lastApplied, StringComparison.OrdinalIgnoreCase))
        {
            try { Directory.CreateDirectory(target); SwitchTo(target); } catch { }
        }
        UpdateIcon(lastDesktop);
    }

    [DllImport("advapi32.dll")]
    static extern int RegNotifyChangeKeyValue(Microsoft.Win32.SafeHandles.SafeRegistryHandle hKey,
        bool watchSubtree, int filter, Microsoft.Win32.SafeHandles.SafeWaitHandle hEvent, bool async);

    void WatchRegistry()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops"))
            using (AutoResetEvent ev = new AutoResetEvent(false))
            {
                if (k == null) return;
                while (true)
                {
                    if (RegNotifyChangeKeyValue(k.Handle, false, 4 /*REG_NOTIFY_CHANGE_LAST_SET*/,
                        ev.SafeWaitHandle, true) != 0) return;
                    ev.WaitOne();
                    try { invoker.BeginInvoke(new Action(delegate { Check(false); })); } catch { }
                }
            }
        }
        catch { } // in caso di errore resta il controllo periodico del timer
    }
    DateTime noCaptureUntil = DateTime.MinValue;
    int ticksSinceCapture;

    public TrayApp()
    {
        config = Config.Load();
        invoker = new Control();
        IntPtr unused = invoker.Handle; // crea la finestra sul thread dell'interfaccia

        lastApplied = Native.GetDesktopPath();

        restoreTimer = new System.Windows.Forms.Timer();
        restoreTimer.Interval = 30;
        restoreTimer.Tick += delegate {
            // Explorer ricarica le icone in modo asincrono: aspetta che compaiano i file
            // della nuova cartella, riapplica le posizioni finché il numero di icone è
            // stabile, poi sblocca il disegno (massimo ~1,8 secondi)
            ++restoreTicks;
            if (!restoreLoaded && ViewShows(lastApplied))
            {
                restoreLoaded = true;
                Log("cartella caricata da Explorer");
            }
            int count = IconLayout.Apply(layouts.Get(lastApplied, false));
            if (restoreLoaded && count == restoreLastCount) restoreStable++; else restoreStable = 0;
            restoreLastCount = count;
            if (!restoreLoaded && restoreTicks == 10)
            {
                // L'aggiornamento leggero non è bastato: usa quello completo
                Log("aggiornamento leggero insufficiente: ForceRefresh");
                Native.ForceRefresh();
            }
            if ((restoreLoaded && restoreStable >= 2) || restoreTicks >= 60)
            {
                restoreTimer.Stop();
                Native.UnfreezeIcons();
                Log("icone mostrate (" + count + " icone, " + restoreTicks + " giri)");
                FlushLog();
            }
        };

        // Intercetta Win+Ctrl+Freccia per cambiare cartella già durante l'animazione
        hookProc = KeyboardHook;
        using (Process p = Process.GetCurrentProcess())
        using (ProcessModule m = p.MainModule)
            hookHandle = SetWindowsHookEx(13 /*WH_KEYBOARD_LL*/, hookProc, GetModuleHandle(m.ModuleName), 0);

        // Notifica immediata dal registro quando cambia il desktop virtuale
        Thread watcher = new Thread(WatchRegistry);
        watcher.IsBackground = true;
        watcher.Start();

        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("Configura cartelle...", null, delegate { ShowSettings(); });
        menu.Items.Add("Apri cartella di questo desktop", null, delegate {
            Process.Start("explorer.exe", "\"" + config.PathFor(VirtualDesktops.Current()) + "\"");
        });
        menu.Items.Add(new ToolStripSeparator());
        startupItem = new ToolStripMenuItem("Avvia con Windows", null, delegate { ToggleStartup(); });
        menu.Items.Add(startupItem);
        // Lo stato può essere cambiato anche da Impostazioni o Gestione attività
        menu.Opening += delegate { startupItem.Checked = Installer.IsStartupEnabled(); };
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Esci (ripristina Desktop originale)", null, delegate { Quit(); });

        tray = new NotifyIcon();
        tray.ContextMenuStrip = menu;
        tray.Visible = true;
        tray.DoubleClick += delegate { ShowSettings(); };

        SystemEvents.SessionEnding += delegate { RestoreOriginal(); };

        // Swipe a quattro dita sul touchpad
        try { touchpad = new TouchpadWatcher(); touchpad.Swipe += OnTouchpadSwipe; touchpad.Debug = Log; } catch { }

        timer = new System.Windows.Forms.Timer();
        timer.Interval = 200;
        timer.Tick += delegate { Check(false); };

        // All'accesso lascia qualche secondo a Explorer prima di interrogarlo
        System.Windows.Forms.Timer startup = new System.Windows.Forms.Timer();
        startup.Interval = 3000;
        startup.Tick += delegate {
            startup.Stop();
            startup.Dispose();
            CaptureLayout(true);
            timer.Start();
            Check(true);
        };
        startup.Start();
    }

    // true se la vista del desktop contiene già i file della cartella indicata
    static bool ViewShows(string folder)
    {
        string[] entries;
        try { entries = Directory.GetFileSystemEntries(folder); } catch { return true; }
        if (entries.Length == 0) return true;
        HashSet<string> names = IconLayout.CurrentNames();
        if (names == null) return true;
        foreach (string e in entries)
        {
            string name = Path.GetFileName(e);
            if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
            if (names.Contains(name)) return true;
        }
        return false;
    }

    // Salva le posizioni delle icone della cartella attualmente mostrata
    void CaptureLayout(bool force)
    {
        if (lastApplied == null || (!force && DateTime.Now < noCaptureUntil)) return;
        if (layouts.Update(lastApplied, IconLayout.Capture())) layouts.Save();
    }

    void SwitchTo(string target)
    {
        CaptureLayout(false);
        restoreTimer.Stop();
        Native.FreezeIcons();
        Log("cambio cartella -> " + target);
        if (!Native.SetDesktopPath(target)) { Native.UnfreezeIcons(); return; }
        Log("SetDesktopPath completato");
        restoreLoaded = false;
        lastApplied = target;
        noCaptureUntil = DateTime.Now.AddSeconds(3);
        restoreTicks = 0; restoreStable = 0; restoreLastCount = -1;
        restoreTimer.Stop();
        IconLayout.Apply(layouts.Get(target, false));
        restoreTimer.Start();
    }

    public void Check(bool force)
    {
        Guid cur = VirtualDesktops.Current();
        if (!force && cur == lastDesktop)
        {
            predictedFrom = Guid.Empty; // previsione confermata da Windows
            // Ogni ~3 secondi memorizza eventuali spostamenti fatti a mano
            if (++ticksSinceCapture >= 15) { ticksSinceCapture = 0; CaptureLayout(false); }
            return;
        }
        // Cambio già anticipato (tastiera o touchpad) ma Windows non l'ha ancora confermato
        if (!force && cur == predictedFrom && DateTime.Now < predictedUntil) return;

        // Lo swipe ha portato dalla parte opposta rispetto alla previsione: inverti il verso
        if (predictedByTouch && predictedFrom != Guid.Empty && DateTime.Now < calibrateUntil)
        {
            List<Guid> ids = VirtualDesktops.All();
            int from = ids.IndexOf(predictedFrom);
            if (from >= 0 && ids.IndexOf(cur) == from - predictedDir)
            {
                config.TouchpadSign = -config.TouchpadSign;
                config.Save();
            }
        }
        predictedFrom = Guid.Empty;
        lastDesktop = cur;
        Log("Windows ha cambiato desktop: " + (VirtualDesktops.Index(cur) + 1));

        string target = config.PathFor(cur);
        if (!string.Equals(target, lastApplied, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                Directory.CreateDirectory(target);
                SwitchTo(target);
            }
            catch (Exception ex)
            {
                tray.ShowBalloonTip(3000, AppName, "Impossibile usare " + target + ": " + ex.Message, ToolTipIcon.Error);
            }
        }
        UpdateIcon(cur);
    }

    void UpdateIcon(Guid cur)
    {
        int idx = VirtualDesktops.Index(cur);
        string label = idx >= 0 ? (idx + 1).ToString() : "?";
        using (Bitmap bmp = new Bitmap(32, 32))
        using (Graphics g = Graphics.FromImage(bmp))
        using (Font f = new Font("Segoe UI", label.Length > 1 ? 14 : 18, FontStyle.Bold, GraphicsUnit.Pixel))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (Brush bg = new SolidBrush(Color.FromArgb(0, 120, 212))) g.FillRectangle(bg, 2, 2, 28, 28);
            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            g.DrawString(label, f, Brushes.White, new RectangleF(0, 1, 32, 32), sf);
            IntPtr h = bmp.GetHicon();
            tray.Icon = Icon.FromHandle(h);
            if (iconHandle != IntPtr.Zero) Native.DestroyIcon(iconHandle);
            iconHandle = h;
        }
        string text = VirtualDesktops.Name(cur) + "\n" + config.PathFor(cur);
        tray.Text = text.Length > 63 ? text.Substring(0, 60) + "..." : text;
    }

    void ShowSettings()
    {
        if (settings != null && !settings.IsDisposed) { settings.Activate(); return; }
        settings = new SettingsForm(config);
        settings.FormClosed += delegate {
            if (settings.DialogResult == DialogResult.OK) { config.Save(); Check(true); }
            else config = Config.Load();
        };
        settings.Show();
    }

    void ToggleStartup()
    {
        Installer.SetStartup(!Installer.IsStartupEnabled());
        startupItem.Checked = Installer.IsStartupEnabled();
    }

    void RestoreOriginal()
    {
        if (string.IsNullOrEmpty(config.Original)) return;
        CaptureLayout(false);
        if (string.Equals(config.Original, lastApplied, StringComparison.OrdinalIgnoreCase)) return;
        if (!Native.SetDesktopPath(config.Original)) return;
        lastApplied = config.Original;
        // Attende che Explorer ricarichi le icone, poi rimette le posizioni
        for (int i = 0; i < 3; i++)
        {
            DateTime until = DateTime.Now.AddMilliseconds(500);
            while (DateTime.Now < until) { Application.DoEvents(); Thread.Sleep(20); }
            IconLayout.Apply(layouts.Get(config.Original, false));
        }
    }

    void Quit()
    {
        timer.Stop();
        restoreTimer.Stop();
        Native.UnfreezeIcons();
        if (hookHandle != IntPtr.Zero) UnhookWindowsHookEx(hookHandle);
        RestoreOriginal();
        tray.Visible = false;
        ExitThread();
    }
}

class SettingsForm : Form
{
    Config config;
    DataGridView grid;
    List<Guid> ids;

    public SettingsForm(Config c)
    {
        config = c;
        Text = "DesktopPerDesktop - cartelle per desktop";
        Width = 820; Height = 420;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        Label info = new Label();
        info.Dock = DockStyle.Top; info.Height = 44; info.Padding = new Padding(8, 8, 8, 0);
        info.Text = "Desktop originale: " + config.Original +
                    "\nI desktop senza una cartella assegnata usano il Desktop originale.";

        grid = new DataGridView();
        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.Columns.Add("num", "#");
        grid.Columns.Add("name", "Desktop");
        grid.Columns.Add("path", "Cartella");
        DataGridViewButtonColumn browse = new DataGridViewButtonColumn();
        browse.HeaderText = ""; browse.Text = "Scegli..."; browse.UseColumnTextForButtonValue = true;
        grid.Columns.Add(browse);
        DataGridViewButtonColumn reset = new DataGridViewButtonColumn();
        reset.HeaderText = ""; reset.Text = "Originale"; reset.UseColumnTextForButtonValue = true;
        grid.Columns.Add(reset);
        grid.Columns[0].FillWeight = 8; grid.Columns[1].FillWeight = 25; grid.Columns[2].FillWeight = 67;
        grid.Columns[3].FillWeight = 15; grid.Columns[4].FillWeight = 15;
        for (int i = 0; i < 3; i++) grid.Columns[i].ReadOnly = true;
        grid.CellContentClick += OnCellClick;

        FlowLayoutPanel buttons = new FlowLayoutPanel();
        buttons.Dock = DockStyle.Bottom; buttons.Height = 44; buttons.FlowDirection = FlowDirection.RightToLeft;
        buttons.Padding = new Padding(6);
        Button ok = new Button(); ok.Text = "Salva"; ok.Width = 90;
        ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };
        Button cancel = new Button(); cancel.Text = "Annulla"; cancel.Width = 90;
        cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
        Button auto = new Button(); auto.Text = "Crea cartelle automaticamente"; auto.Width = 210;
        auto.Click += delegate { AutoAssign(); };
        Button refresh = new Button(); refresh.Text = "Aggiorna elenco"; refresh.Width = 120;
        refresh.Click += delegate { Fill(); };
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel); buttons.Controls.Add(auto); buttons.Controls.Add(refresh);

        Controls.Add(grid); Controls.Add(buttons); Controls.Add(info);
        AcceptButton = ok; CancelButton = cancel;
        Fill();
    }

    void Fill()
    {
        ids = VirtualDesktops.All();
        grid.Rows.Clear();
        for (int i = 0; i < ids.Count; i++)
        {
            string p;
            string shown = config.Map.TryGetValue(ids[i], out p) ? p : "(Desktop originale)";
            grid.Rows.Add((i + 1).ToString(), VirtualDesktops.Name(ids[i]), shown);
        }
    }

    void OnCellClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        Guid id = ids[e.RowIndex];
        if (e.ColumnIndex == 3)
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Cartella per " + VirtualDesktops.Name(id);
                dlg.ShowNewFolderButton = true;
                string p;
                if (config.Map.TryGetValue(id, out p) && Directory.Exists(p)) dlg.SelectedPath = p;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (IsInside(dlg.SelectedPath, config.Original))
                {
                    MessageBox.Show(this, "Scegli una cartella che non stia dentro il Desktop originale, " +
                        "altrimenti comparirebbe come icona sugli altri desktop.", "Attenzione",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                config.Map[id] = dlg.SelectedPath;
            }
        }
        else if (e.ColumnIndex == 4) config.Map.Remove(id);
        else return;
        Fill();
    }

    static bool IsInside(string child, string parent)
    {
        string c = Path.GetFullPath(child).TrimEnd('\\') + "\\";
        string p = Path.GetFullPath(parent).TrimEnd('\\') + "\\";
        return c.StartsWith(p, StringComparison.OrdinalIgnoreCase) && c.Length > p.Length;
    }

    void AutoAssign()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktops");
        for (int i = 1; i < ids.Count; i++) // il primo desktop resta sul Desktop originale
        {
            if (config.Map.ContainsKey(ids[i])) continue;
            string name = VirtualDesktops.Name(ids[i]);
            foreach (char ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
            string p = Path.Combine(root, name);
            Directory.CreateDirectory(p);
            config.Map[ids[i]] = p;
        }
        Fill();
    }
}

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--install") { Application.EnableVisualStyles(); Installer.Install(); return; }
        if (args.Length > 0 && args[0] == "--uninstall") { Application.EnableVisualStyles(); Installer.Uninstall(); return; }
        if (args.Length > 0 && args[0] == "--dump-layout")
        {
            // Diagnostica: scrive le posizioni attuali delle icone, senza cambiare nulla
            Dictionary<string, Point> l = IconLayout.Capture();
            List<string> lines = new List<string>();
            if (l == null) lines.Add("vista del desktop non trovata");
            else foreach (KeyValuePair<string, Point> kv in l) lines.Add(kv.Key + "\t" + kv.Value.X + "\t" + kv.Value.Y);
            File.WriteAllLines(args.Length > 1 ? args[1] : "layout-dump.txt", lines.ToArray(), Encoding.UTF8);
            return;
        }
        bool created;
        using (Mutex m = new Mutex(true, @"Local\DesktopPerDesktop", out created))
        {
            if (!created) { MessageBox.Show("DesktopPerDesktop è già in esecuzione (icona nella barra delle applicazioni)."); return; }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApp());
        }
    }
}

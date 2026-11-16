// Same detection mechanism as Disactivity's MIT-licensed slave: an off-screen Win32 window.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

static class ActivityHost {
    delegate IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam);
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct WindowClass { public uint style; public WindowProc procedure; public int classExtra, windowExtra; public IntPtr instance, icon, cursor, brush; public string menu, name; }
    [StructLayout(LayoutKind.Sequential)] struct Message { public IntPtr window; public uint message; public UIntPtr wparam; public IntPtr lparam; public uint time; public int x,y; public uint extra; }
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern ushort RegisterClassW(ref WindowClass data);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr CreateWindowExW(uint extended, string name, string title, uint style, int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] static extern int GetMessageW(out Message message,IntPtr window,uint min,uint max);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessageW(ref Message message);
    [DllImport("user32.dll")] static extern IntPtr DefWindowProcW(IntPtr window,uint message,IntPtr wparam,IntPtr lparam);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandleW(string name);
    static WindowProc procedure = delegate(IntPtr h,uint m,IntPtr w,IntPtr l) { return (m == 0x10 || m == 2) ? IntPtr.Zero : DefWindowProcW(h,m,w,l); };
    [STAThread] static void Main(string[] args) {
        int parentId; if (args.Length != 1 || !Int32.TryParse(args[0], out parentId)) return;
        Process parent; try { parent = Process.GetProcessById(parentId); } catch { return; }
        var monitor = new Thread(() => { try { parent.WaitForExit(); } catch { } Environment.Exit(0); }); monitor.IsBackground = true; monitor.Start();
        string name = "DiscordVPNActivity";
        var instance = GetModuleHandleW(null);
        var wc = new WindowClass { name=name, instance=instance, procedure=procedure };
        RegisterClassW(ref wc);
        string title = System.IO.Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
        var window = CreateWindowExW(0x08040000, name, title, 0x80CF0000, -32000,-32000,1,1,IntPtr.Zero,IntPtr.Zero,instance,IntPtr.Zero);
        ShowWindow(window,7);
        Message msg; while (GetMessageW(out msg,IntPtr.Zero,0,0)>0) { TranslateMessage(ref msg); DispatchMessageW(ref msg); }
    }
}

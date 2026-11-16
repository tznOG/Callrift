using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace Callrift {
    public static class WindowFrame {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        public static void Attach(Window window) {
            WindowChrome.SetWindowChrome(window, new WindowChrome {
                CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false, NonClientFrameEdges = NonClientFrameEdges.None
            });
            window.SourceInitialized += (s,e) => Update(window);
        }
        public static void Update(Window window) {
            var handle = new WindowInteropHelper(window).Handle; if (handle == IntPtr.Zero) return;
            Color background = ((SolidColorBrush)window.Resources["Sidebar"]).Color;
            int dark = background.R < 128 ? 1 : 0;
            int color = background.R | (background.G << 8) | (background.B << 16);
            try {
                DwmSetWindowAttribute(handle, 20, ref dark, 4);
                DwmSetWindowAttribute(handle, 34, ref color, 4);
                DwmSetWindowAttribute(handle, 35, ref color, 4);
            } catch (DllNotFoundException) { }
        }
    }
}

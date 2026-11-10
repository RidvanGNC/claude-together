// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClaudeTogether
{
    /// <summary>Windows-only visual touches: dark title bar and dark scroll bars. Silently skipped elsewhere.</summary>
    static class UiWin
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string subApp, string subIdList);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        public static void DarkTitle(IntPtr handle)
        {
            if (!Win.IsWindows) return;
            try
            {
                int on = 1;
                DwmSetWindowAttribute(handle, 20, ref on, 4);
                DwmSetWindowAttribute(handle, 19, ref on, 4);
                int caption = 0x0015110F;
                DwmSetWindowAttribute(handle, 35, ref caption, 4);
                int text = 0x00EFEDEC;
                DwmSetWindowAttribute(handle, 36, ref text, 4);
                int border = 0x003A2F2A;
                DwmSetWindowAttribute(handle, 34, ref border, 4);
            }
            catch (Exception) { }
        }

        public static void DarkScroll(Control c)
        {
            if (!Win.IsWindows) return;
            EventHandler apply = delegate
            {
                try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch (Exception) { }
            };
            if (c.IsHandleCreated) apply(c, EventArgs.Empty);
            c.HandleCreated += apply;
        }
    }
}

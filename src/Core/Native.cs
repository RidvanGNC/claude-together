// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Runtime.InteropServices;

namespace ClaudeTogether
{
    static class Win
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CreateSymbolicLink(string link, string target, int flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CreateHardLink(string newFile, string existing, IntPtr security);

        public static bool IsWindows { get { return Environment.OSVersion.Platform == PlatformID.Win32NT; } }
    }
}

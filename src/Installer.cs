// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ClaudeTogether
{
    /// <summary>
    /// Per-user install for the portable zip: copies the executables, adds them to PATH and creates a
    /// Start Menu shortcut. No admin rights needed. Steps aside when the MSI package is installed.
    /// </summary>
    static class Installer
    {
        public const string CliExe = "claude-together.exe";
        public const string GuiExe = "ClaudeTogether.exe";

        public static readonly string InstallDir = Path.Combine(Paths.Local, "Programs", "ClaudeTogether");
        static readonly string ShortcutPath = Path.Combine(Paths.Roaming, "Microsoft", "Windows", "Start Menu", "Programs", "Claude Together.lnk");

        /// <summary>Key the MSI writes for its components (see installer/ClaudeTogether.wxs).</summary>
        const string MsiKey = @"Software\ClaudeTogether\Components";

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);

        /// <summary>True when the MSI package owns the install folder, the PATH entry and the shortcut.</summary>
        public static bool MsiInstalled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(MsiKey))
                    return key != null && key.ValueCount > 0;
            }
            catch (Exception) { return false; }
        }

        static void RequireNoMsi()
        {
            if (MsiInstalled())
                throw new InvalidOperationException(Loc.T("Claude Together was installed with the MSI package. Update it with a newer MSI, or remove it in Settings > Apps."));
        }

        public static void Install(Action<string> log)
        {
            RequireNoMsi();
            string from = AppDomain.CurrentDomain.BaseDirectory;
            Directory.CreateDirectory(InstallDir);
            foreach (string f in new[] { CliExe, GuiExe })
            {
                string src = Path.Combine(from, f);
                string dst = Path.Combine(InstallDir, f);
                if (!File.Exists(src)) continue;
                if (!string.Equals(Path.GetFullPath(src), Path.GetFullPath(dst), StringComparison.OrdinalIgnoreCase)) File.Copy(src, dst, true);
                log("  " + dst);
            }

            if (SetUserPath(true)) log(Loc.T("Added to PATH: {0}", InstallDir));

            string gui = Path.Combine(InstallDir, GuiExe);
            if (File.Exists(gui))
            {
                CreateShortcut(gui);
                log(Loc.T("Start Menu shortcut created: Claude Together"));
            }
        }

        public static void Uninstall(Action<string> log)
        {
            RequireNoMsi();
            if (SetUserPath(false)) log(Loc.T("Removed from PATH: {0}", InstallDir));
            if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
            if (Directory.Exists(InstallDir)) log(Loc.T("Files were left in place; delete this folder to finish: {0}", InstallDir));
        }

        static bool SameDir(string a, string b)
        {
            return string.Equals(a.Trim().TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds or removes the install folder in the user's PATH. Returns false when nothing had to change.
        /// Reads the raw registry value, so entries such as %USERPROFILE%\bin are written back unexpanded.
        /// </summary>
        static bool SetUserPath(bool add)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey("Environment"))
            {
                string path = key.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
                List<string> parts = path.Split(';').Where(p => p.Length > 0).ToList();
                if (parts.Any(p => SameDir(p, InstallDir)) == add) return false;
                if (add) parts.Add(InstallDir);
                else parts.RemoveAll(p => SameDir(p, InstallDir));
                RegistryValueKind kind = path.Length > 0 ? key.GetValueKind("Path") : RegistryValueKind.ExpandString;
                key.SetValue("Path", string.Join(";", parts.ToArray()), kind);
            }
            // Tell running programs (Explorer, new terminals) that the environment changed.
            try
            {
                IntPtr result;
                SendMessageTimeout(new IntPtr(0xFFFF), 0x001A, IntPtr.Zero, "Environment", 0x0002, 1000, out result);
            }
            catch (Exception) { }
            return true;
        }

        static void CreateShortcut(string target)
        {
            Type shell = Type.GetTypeFromProgID("WScript.Shell");
            object sh = Activator.CreateInstance(shell);
            object link = shell.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { ShortcutPath });
            Type lt = link.GetType();
            lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { target });
            lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { Path.GetDirectoryName(target) });
            lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
        }
    }
}

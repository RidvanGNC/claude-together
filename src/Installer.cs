// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ClaudeTogether
{
    /// <summary>Per-user install: copies the executables, adds them to PATH and creates a Start Menu shortcut. No admin rights needed.</summary>
    static class Installer
    {
        public const string CliExe = "claude-together.exe";
        public const string GuiExe = "ClaudeTogether.exe";

        public static readonly string InstallDir = Path.Combine(Paths.Local, "Programs", "ClaudeTogether");
        static readonly string ShortcutPath = Path.Combine(Paths.Roaming, "Microsoft", "Windows", "Start Menu", "Programs", "Claude Together.lnk");

        public static void Install(Action<string> log)
        {
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

            string path = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
            if (!path.Split(';').Contains(InstallDir, StringComparer.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("Path", path.TrimEnd(';') + ";" + InstallDir, EnvironmentVariableTarget.User);
                log(Loc.T("Added to PATH: {0}", InstallDir));
            }

            string gui = Path.Combine(InstallDir, GuiExe);
            if (File.Exists(gui))
            {
                CreateShortcut(gui);
                log(Loc.T("Start Menu shortcut created: Claude Together"));
            }
        }

        public static void Uninstall(Action<string> log)
        {
            string path = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
            string[] parts = path.Split(';').Where(p => p.Length > 0 && !p.Equals(InstallDir, StringComparison.OrdinalIgnoreCase)).ToArray();
            Environment.SetEnvironmentVariable("Path", string.Join(";", parts), EnvironmentVariableTarget.User);
            log(Loc.T("Removed from PATH: {0}", InstallDir));
            if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
            log(Loc.T("Files were left in place; delete this folder to finish: {0}", InstallDir));
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

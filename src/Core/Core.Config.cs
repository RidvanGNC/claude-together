// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace ClaudeTogether
{
    static partial class Core
    {
        static readonly object logLock = new object();

        public static string Stamp() { return DateTime.Now.ToString("yyyyMMdd-HHmmss"); }

        public static void Log(string text)
        {
            try
            {
                lock (logLock)
                {
                    Directory.CreateDirectory(Paths.Root);
                    File.AppendAllText(Paths.LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch (Exception) { }
        }

        static JavaScriptSerializer Json()
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            s.RecursionLimit = 500;
            return s;
        }

        public static string ToJson(object value) { return Json().Serialize(value); }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        /// <summary>Reads profiles.json. Copies it from the older location on first run.</summary>
        public static Config Load()
        {
            Config c = new Config();
            try
            {
                if (!File.Exists(Paths.ConfigFile) && File.Exists(Paths.LegacyConfig))
                {
                    Directory.CreateDirectory(Paths.Root);
                    File.Copy(Paths.LegacyConfig, Paths.ConfigFile);
                }
            }
            catch (Exception ex) { Log("Config migration failed: " + ex.Message); }

            if (!File.Exists(Paths.ConfigFile)) { c.Stage = 0; return c; }
            c.Exists = true;
            try
            {
                Dictionary<string, object> root = Json().DeserializeObject(File.ReadAllText(Paths.ConfigFile)) as Dictionary<string, object>;
                if (root == null) return c;

                object acc;
                if (root.TryGetValue("accounts", out acc) && acc != null)
                {
                    List<object> items = new List<object>();
                    if (acc is Dictionary<string, object>) items.Add(acc);
                    else if (!(acc is string) && acc is System.Collections.IEnumerable)
                        foreach (object o in (System.Collections.IEnumerable)acc) items.Add(o);

                    foreach (object o in items)
                    {
                        Dictionary<string, object> d = o as Dictionary<string, object>;
                        string name = d == null ? "" : Str(d, "name");
                        if (name.Length == 0) continue;
                        string mode = Str(d, "desktop");
                        bool primary = mode == Account.PrimaryMode || mode == "normal";
                        c.Accounts.Add(new Account { Name = name, Desktop = primary ? Account.PrimaryMode : Account.SecondaryMode });
                    }
                }
                object stage;
                if (root.TryGetValue("stage", out stage) && stage != null) c.Stage = Convert.ToInt32(stage);
                c.BackupDir = Str(root, "backupDir");
            }
            catch (Exception ex) { Log("Could not read config: " + ex.Message); }
            return c;
        }

        public static void Save(Config c)
        {
            Directory.CreateDirectory(Paths.Root);
            List<object> accounts = new List<object>();
            foreach (Account a in c.Accounts)
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["name"] = a.Name;
                d["desktop"] = a.Desktop;
                accounts.Add(d);
            }
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["accounts"] = accounts;
            root["stage"] = c.Stage;
            root["backupDir"] = c.BackupDir ?? "";
            File.WriteAllText(Paths.ConfigFile, Json().Serialize(root), new UTF8Encoding(false));
            c.Exists = true;
        }

        /// <summary>Returns an error message, or null when the name is acceptable.</summary>
        public static string ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Loc.T("Enter a name.");
            name = name.Trim();
            if (!Regex.IsMatch(name, "^[A-Za-z0-9_-]{1,24}$")) return Loc.T("Use only letters, digits, - and _ (max 24 characters).");
            if (name.Equals("shared", StringComparison.OrdinalIgnoreCase)) return Loc.T("This name is reserved.");
            return null;
        }

        public static Account Find(Config cfg, string name)
        {
            return cfg.Accounts.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}

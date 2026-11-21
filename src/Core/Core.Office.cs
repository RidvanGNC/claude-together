// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.IO;
using System.Text;

namespace ClaudeTogether
{
    static partial class Core
    {
        /// <summary>Reads office.json (desks, decorations, floor, wallpaper). A missing or damaged file gives the default office.</summary>
        public static OfficeState LoadOffice()
        {
            try
            {
                if (File.Exists(Paths.OfficeFile)) return OfficeState.FromJson(File.ReadAllText(Paths.OfficeFile));
            }
            catch (Exception ex) { Log("Could not read the office: " + ex.Message); }
            return OfficeState.Default();
        }

        /// <summary>Writes office.json through a temporary file, so an interrupted save cannot leave half a file.</summary>
        public static void SaveOffice(OfficeState state)
        {
            try
            {
                Directory.CreateDirectory(Paths.Root);
                string tmp = Paths.OfficeFile + ".tmp";
                File.WriteAllText(tmp, state.ToJson(), new UTF8Encoding(false));
                File.Copy(tmp, Paths.OfficeFile, true);
                File.Delete(tmp);
            }
            catch (Exception ex) { Log("Could not save the office: " + ex.Message); }
        }
    }
}

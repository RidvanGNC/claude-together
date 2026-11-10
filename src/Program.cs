// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;

namespace ClaudeTogether
{
    /// <summary>
    /// One source, two executables: built with CLI defined it is claude-together.exe (console),
    /// otherwise it is ClaudeTogether.exe (windowed, no console).
    /// </summary>
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
#if CLI
            return Cli.Run(args);
#else
            return GuiHost.Run();
#endif
        }
    }
}

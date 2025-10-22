namespace DayZManager
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Any(a => string.Equals(a, "--stop-elevated", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    foreach (var n in new[] { "DayZServer_x64", "DayZ_x64", "DayZDiag_x64", "DayZ_BE" })
                    {
                        try { foreach (var p in System.Diagnostics.Process.GetProcessesByName(n)) { p.Kill(true); p.WaitForExit(250); } }
                        catch { /* ignore per-process errors */ }
                    }
                }
                catch { /* best effort */ }
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}

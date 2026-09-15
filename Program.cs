using PowerSave.Infra;
using PowerSave.UI;

namespace PowerSave;

internal static class Program
{
    const string MutexName = "PowerSaveStudio.SingleInstance.v1";

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Any(a => a.Trim().TrimStart('-', '/').Equals("help", StringComparison.OrdinalIgnoreCase) || a.Trim() == "?" || a.Trim() == "/?" || a.Trim() == "-h"))
        {
            // Attach console if launched from terminal, else fallback to MessageBox
            try
            {
                if (Native.AttachConsole(Native.ATTACH_PARENT_PROCESS) || Native.AllocConsole())
                {
                    Console.WriteLine("PowerSave Studio v1.0 — usage:");
                    Console.WriteLine("  PowerSave.exe --apply=ultrasave|powersave|ultraperf [--tray]");
                    Console.WriteLine("  PowerSave.exe --tray               start minimized to tray");
                    Console.WriteLine("  PowerSave.exe --help               show this help");
                    Console.WriteLine("  Aliases: ultrasave|ups|us|ultraeco | powersave|balanced|save|ps|eco | ultraperf|perf|up");
                    Native.FreeConsole();
                }
                else
                {
                    MessageBox.Show("PowerSave Studio v1.0\n\nUsage:\n  PowerSave.exe --apply=ultrasave|powersave|ultraperf [--tray]\n  PowerSave.exe --tray\n  PowerSave.exe --help",
                        "PowerSave", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch { }
            return 0;
        }

        var cli = CliOptions.Parse(args);

        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            IpcClient.TrySend(cli.ApplyMode is not null ? $"MODE:{cli.ApplyMode}" : "SHOW");
            return 0;
        }

        Logger.Init();
        Logger.Info($"PowerSave starting (elevated relaunch: {cli.ElevatedRelaunch})");

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) =>
        {
            try { Logger.Error("UI thread exception", e.Exception); Logger.Flush(); } catch { }
#if DEBUG
            MessageBox.Show(e.Exception.ToString(), "PowerSave — UI error", MessageBoxButtons.OK, MessageBoxIcon.Error);
#endif
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try { Logger.Error("unhandled domain exception", e.ExceptionObject as Exception); Logger.Flush(); } catch { }
        };
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            try { Logger.Error("task exception", e.Exception); Logger.Flush(); } catch { }
            e.SetObserved();
        };

        try
        {
            using var context = new AppContext(cli);
            Application.Run(context);
        }
        catch (Exception ex)
        {
            Logger.Error("fatal", ex);
            try { Logger.Flush(); } catch { }
#if !DEBUG
            MessageBox.Show($"PowerSave encountered an error and needs to restart.\n\n{ex.Message}\n\nDetails logged to %LOCALAPPDATA%\\PowerSave\\powersave.log",
                "PowerSave", MessageBoxButtons.OK, MessageBoxIcon.Error);
#endif
            return 1;
        }
        finally
        {
            AnimEngine.Shutdown();
            Logger.Shutdown();
        }

        return 0;
    }
}

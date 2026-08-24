using PowerSave.Core;
using PowerSave.Infra;
using PowerSave.UI;

namespace PowerSave;

public sealed class AppContext : ApplicationContext
{
    readonly MainForm _form;
    readonly IpcServer _ipc = new();

    public AppContext(CliOptions cli) : base()
    {
        var settings = SettingsStore.Load();
        _form = new MainForm(settings, cli);
        MainForm = _form;

        _ipc.MessageReceived += line =>
        {
            try
            {
                if (line.StartsWith("MODE:", StringComparison.Ordinal))
                {
                    var key = ModeKeys.Normalize(line[5..]);
                    if (key is not null && !_form.IsDisposed && _form.IsHandleCreated)
                    {
                        try { _form.BeginInvoke(new Action(() => { if (!_form.IsDisposed) _form.ApplyFromExternal(key); })); } catch { }
                    }
                }
                else if (line == "SHOW")
                {
                    if (!_form.IsDisposed && _form.IsHandleCreated)
                    {
                        try { _form.BeginInvoke(new Action(() => { if (!_form.IsDisposed) _form.ShowFromTray(); })); } catch { }
                    }
                }
            }
            catch { }
        };
        _ipc.Start();

        // Avoid flash when starting minimized — don't Show(), let tray handle it
        if (cli.StartMinimized)
        {
            // Ensure handle is created so IPC BeginInvoke works, but stay hidden
            _form.Load += (_, _) =>
            {
                try { _form.BeginInvoke(new Action(() => { if (!_form.IsDisposed) _form.HideToTray(silent: true); })); } catch { }
            };
            // Create handle without making visible
            var handle = _form.Handle;
            _form.BeginInvoke(new Action(() => Logger.Info("started minimized to tray")));
        }
        else
        {
            _form.Show();
        }

        _form.FormClosed += (_, _) =>
        {
            try { _ipc.Dispose(); } catch { }
            ExitThread();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _ipc.Dispose(); } catch { }
            try { _form.Dispose(); } catch { }
        }
        base.Dispose(disposing);
    }
}

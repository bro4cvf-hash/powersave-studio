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

        // IMPORTANT: MainForm is intentionally never assigned here.
        // Application.Run() force-shows ApplicationContext.MainForm, which would flash the
        // window when starting minimized to tray. Instead we Show() explicitly (normal
        // start) or keep the form hidden (tray start), and drive shutdown from FormClosed.
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

        if (cli.StartMinimized)
        {
            // Create the handle (without making the form visible) so IPC BeginInvoke works.
            // Form.Load never fires for a form that is never shown, so the startup path
            // (battery timer, tweaks, mode detection, --apply) is invoked explicitly.
            _ = _form.Handle;
            _form.BeginInvoke(new Action(() => _ = _form.RunStartupAsync()));
            Logger.Info("started minimized to tray");
        }
        else
        {
            _form.Show(); // fires Load -> MainForm.RunStartupAsync
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

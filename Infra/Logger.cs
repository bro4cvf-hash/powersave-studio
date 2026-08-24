namespace PowerSave.Infra;

public enum LogLevel { Info, Warn, Error }

public static class Paths
{
    public static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerSave");

    public static string EnsureDataDir()
    {
        Directory.CreateDirectory(DataDir);
        return DataDir;
    }
}

public static class Logger
{
    static readonly object _lock = new();
    static readonly Queue<string> _pending = new();
    static System.Threading.Timer? _flushTimer;
    static string _path = "";

    public static string LogPath => _path;

    public static void Init()
    {
        try
        {
            _path = Path.Combine(Paths.EnsureDataDir(), "powersave.log");
            RotateIfNeeded();
            _flushTimer = new System.Threading.Timer(_ => Flush(), null, 2000, 4000);
        }
        catch { }
    }

    static void RotateIfNeeded()
    {
        try
        {
            if (File.Exists(_path) && new FileInfo(_path).Length > 1_000_000)
            {
                var old = _path + ".old";
                if (File.Exists(old)) File.Delete(old);
                File.Move(_path, old);
            }
        }
        catch { }
    }

    public static void Info(string message) => Write(LogLevel.Info, message);
    public static void Warn(string message) => Write(LogLevel.Warn, message);
    public static void Error(string message, Exception? ex = null) =>
        Write(LogLevel.Error, ex is null ? message : $"{message} :: {ex}");

    static void Write(LogLevel level, string message)
    {
        lock (_lock)
        {
            if (_path.Length == 0) return;
            // Truncate huge messages to keep log readable
            if (message.Length > 1200) message = message[..1200] + "…";
            _pending.Enqueue($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}");
            // Prevent unbounded queue (e.g., tight loop)
            if (_pending.Count > 800) _pending.Dequeue();
        }
    }

    public static void Flush()
    {
        string[] items;
        lock (_lock)
        {
            if (_pending.Count == 0 || _path.Length == 0) return;
            items = _pending.ToArray();
            _pending.Clear();
        }
        try { File.AppendAllLines(_path, items); } catch { }
    }
}

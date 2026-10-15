using System.Globalization;
using System.Text;
using ExplorerOrganizer.Core.Engine;

namespace ExplorerOrganizer.Core.Services;

/// <summary>Thread-safe activity log: in-memory ring for the UI + one tab-separated file per day.</summary>
public sealed class ActivityLog
{
    public const int MemoryLimit = 5000;
    private readonly string? _directory;
    private readonly object _lock = new();
    private readonly List<OperationRecord> _entries = new();

    public ActivityLog(string? directory)
    {
        _directory = directory;
        if (directory == null) return;
        try { Directory.CreateDirectory(directory); } catch (IOException) { _directory = null; }
        if (_directory != null) CleanupOldFiles(directory, TimeSpan.FromDays(60));
    }

    public event EventHandler<IReadOnlyList<OperationRecord>>? Added;

    public IReadOnlyList<OperationRecord> Snapshot() { lock (_lock) return _entries.ToList(); }

    public void Add(IReadOnlyList<OperationRecord> records)
    {
        if (records.Count == 0) return;
        lock (_lock)
        {
            _entries.AddRange(records);
            if (_entries.Count > MemoryLimit) _entries.RemoveRange(0, _entries.Count - MemoryLimit);
            if (_directory != null)
            {
                try
                {
                    var file = Path.Combine(_directory, $"activity-{DateTime.Now:yyyy-MM-dd}.log");
                    File.AppendAllLines(file, records.Select(ToLine), Encoding.UTF8);
                }
                catch (IOException) { /* logging must never break file processing */ }
            }
        }
        Added?.Invoke(this, records);
    }

    public void Info(string message, string trigger = "") =>
        Add(new[] { new OperationRecord { Kind = OperationKind.Error, Status = OperationStatus.Unchanged, Message = message, Trigger = trigger } });

    public void Error(string message, string trigger = "") =>
        Add(new[] { new OperationRecord { Kind = OperationKind.Error, Status = OperationStatus.Failed, Message = message, Trigger = trigger } });

    /// <summary>Exports records as CSV (by extension) or tab-separated text.</summary>
    public static void Export(IEnumerable<OperationRecord> records, string file)
    {
        bool csv = file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        string[] header = { "Time", "Status", "Action", "Rule", "Trigger", "Source", "Target", "Message" };
        sb.AppendLine(csv ? string.Join(';', header) : string.Join('\t', header));
        foreach (var r in records)
        {
            var cols = new[] { r.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), r.Status.ToString(), r.Kind.ToString(), r.RuleName, r.Trigger, r.Source, r.Target, r.Message };
            sb.AppendLine(csv ? string.Join(';', cols.Select(CsvEscape)) : string.Join('\t', cols.Select(c => c.Replace('\t', ' ').Replace('\n', ' '))));
        }
        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string ToLine(OperationRecord r) => string.Join('\t',
        r.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), r.Status, r.Kind, r.RuleName, r.Trigger, r.Source, r.Target,
        r.Message.Replace('\t', ' ').Replace('\n', ' '));

    private static string CsvEscape(string s) =>
        s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    private static void CleanupOldFiles(string dir, TimeSpan age)
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "activity-*.log"))
                if (File.GetLastWriteTime(f) < DateTime.Now - age) File.Delete(f); // our own log files only
        }
        catch { }
    }
}

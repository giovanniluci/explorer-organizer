using ExplorerOrganizer.Core.Engine;

namespace ExplorerOrganizer.Core.Services;

public sealed class Statistics
{
    public long FilesOrganized { get; set; }
    public long Moved { get; set; }
    public long Copied { get; set; }
    public long Renamed { get; set; }
    public long Recycled { get; set; }
    public long Errors { get; set; }
    public long BytesOrganized { get; set; }
    public DateTime? LastRun { get; set; }
    public string TodayDate { get; set; } = "";
    public long Today { get; set; }

    public void Apply(RunResult result, DateTime now)
    {
        var today = now.ToString("yyyy-MM-dd");
        if (TodayDate != today) { TodayDate = today; Today = 0; }
        var done = result.Records.Where(r => r.Status == Engine.OperationStatus.Done).ToList();
        int files = done.Select(r => r.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        FilesOrganized += files;
        Today += files;
        Moved += done.Count(r => r.Kind == OperationKind.Move);
        Copied += done.Count(r => r.Kind == OperationKind.Copy);
        Renamed += done.Count(r => r.Kind == OperationKind.Rename);
        Recycled += done.Count(r => r.Kind is OperationKind.Recycle or OperationKind.Delete);
        Errors += result.Failed;
        foreach (var r in done.Where(r => r.Kind is OperationKind.Move or OperationKind.Copy))
        {
            try { BytesOrganized += new FileInfo(r.Target).Length; } catch { }
        }
        if (result.Matched > 0) LastRun = now;
    }
}

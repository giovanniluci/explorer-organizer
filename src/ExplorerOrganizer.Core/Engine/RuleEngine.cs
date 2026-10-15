using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Platform;

namespace ExplorerOrganizer.Core.Engine;

public sealed class RunOptions
{
    /// <summary>Preview mode: nothing is touched, records have status Planned.</summary>
    public bool Simulate { get; init; }
    /// <summary>Label of what started the run (drop, watch, scan, schedule, context menu...).</summary>
    public string Trigger { get; init; } = "manual";
    /// <summary>Asked at most once per run before any permanent deletion. Null/false =&gt; the file goes to the Recycle Bin instead.</summary>
    public Func<bool>? ConfirmPermanentDelete { get; init; }
    public CancellationToken Cancellation { get; init; }
}

public sealed class RunResult
{
    public List<OperationRecord> Records { get; } = new();
    public UndoBatch? Undo { get; set; }
    public int Scanned { get; set; }
    public int Matched { get; set; }
    public int Failed => Records.Count(r => r.Status == OperationStatus.Failed);
    public int Changed => Records.Count(r => r.Status is OperationStatus.Done or OperationStatus.Planned);
}

/// <summary>
/// Applies a profile to a list of files and folders: for each item the first enabled matching rule wins and its actions run in order.
/// A folder handled by a rule is processed as a whole: the items inside it are not processed again.
/// The same code path runs in simulation (preview) and for real, so the preview is faithful.
/// </summary>
public sealed class RuleEngine
{
    private readonly IMetadataProvider _metadata;
    private readonly IContentReader _content;
    private readonly IRecycleBin _recycleBin;
    private readonly Func<DateTime> _clock;

    public RuleEngine(IMetadataProvider metadata, IContentReader content, IRecycleBin recycleBin, Func<DateTime>? clock = null)
    {
        _metadata = metadata;
        _content = content;
        _recycleBin = recycleBin;
        _clock = clock ?? (() => DateTime.Now);
    }

    public IRecycleBin RecycleBin => _recycleBin;

    /// <summary>Returns the rule that would handle the file (or null). Used by the editor "test on file" button.</summary>
    public Rule? FindRule(Profile profile, string path, out FileContext context)
    {
        var ctx = new FileContext(path, null, _metadata, _content);
        context = ctx;
        var now = _clock();
        foreach (var rule in profile.Rules.Where(r => r.Enabled && ConditionEvaluator.AppliesTo(r, ctx)))
        {
            context.Captures.Clear();
            if (ConditionEvaluator.Evaluate(rule.Condition, context, now)) return rule;
        }
        return null;
    }

    public RunResult Run(IEnumerable<FileInput> files, Profile profile, RunOptions options)
    {
        var result = new RunResult();
        IFileOps ops = options.Simulate ? new SimulatedFileOps() : new RealFileOps(_recycleBin);
        var run = new RunState();
        var now = _clock();
        bool? permanentAllowed = null;
        var rules = profile.Rules.Where(r => r.Enabled).ToList();
        // folders first (outermost first), so a folder handled as a whole claims its content before the files inside it
        var ordered = files.Select(f => (Input: f, IsFolder: Directory.Exists(f.Path)))
            .Where(x => x.IsFolder || File.Exists(x.Input.Path))
            .OrderBy(x => x.IsFolder ? 0 : 1)
            .ThenBy(x => x.IsFolder ? x.Input.Path.Length : 0)
            .Select(x => x.Input)
            .ToList();
        var claimed = new List<string>();

        foreach (var input in ordered)
        {
            if (options.Cancellation.IsCancellationRequested) break;
            if (!File.Exists(input.Path) && !Directory.Exists(input.Path)) continue;
            var full = Path.GetFullPath(input.Path);
            if (claimed.Any(c => IsInside(full, c))) continue;
            result.Scanned++;

            var ctx = new FileContext(input.Path, input.RootFolder, _metadata, _content);
            Rule? rule = null;
            foreach (var r in rules)
            {
                if (!ConditionEvaluator.AppliesTo(r, ctx)) continue;
                ctx.Captures.Clear();
                try
                {
                    if (ConditionEvaluator.Evaluate(r.Condition, ctx, now)) { rule = r; break; }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    result.Records.Add(Record(options, r, ctx, OperationKind.Error, OperationStatus.Failed, ctx.OriginalPath, "", ex.Message));
                    break; // unreadable file: do not try lower-priority rules on it
                }
            }
            if (rule == null) continue;
            result.Matched++;
            if (ctx.IsFolder) claimed.Add(ctx.OriginalPath);

            foreach (var action in rule.Actions)
            {
                bool proceed;
                try
                {
                    proceed = Execute(action, ctx, rule, ops, run, now, options, result, ref permanentAllowed);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TemplateException
                                               or ArgumentException or NotSupportedException or InvalidDataException
                                               or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException)
                {
                    var msg = ex is IOException io && RealFileOps.IsInUse(io) ? Loc.T("Err.FileInUse") : ex.Message;
                    result.Records.Add(Record(options, rule, ctx, KindOf(action), OperationStatus.Failed, ctx.LogicalPath, "", msg));
                    proceed = false;
                }
                if (!proceed) break;
            }
        }

        if (ops is RealFileOps real && real.UndoSteps.Count > 0)
        {
            result.Undo = new UndoBatch
            {
                Time = now,
                FileCount = result.Matched,
                HasIrreversibleSteps = real.HasIrreversibleSteps,
                Steps = real.UndoSteps,
                Description = string.Format(Loc.T("Undo.BatchDescription"), result.Matched, profile.Name, Loc.T("Trigger." + options.Trigger)),
            };
        }
        return result;
    }

    private bool Execute(RuleAction action, FileContext ctx, Rule rule, IFileOps ops, RunState run, DateTime now,
        RunOptions options, RunResult result, ref bool? permanentAllowed)
    {
        if (ctx.IsFolder && action is CopyAction or CompressAction or ExtractAction)
        {
            result.Records.Add(Record(options, rule, ctx, KindOf(action), OperationStatus.Skipped, ctx.LogicalPath, "", Loc.T("Msg.NotForFolders")));
            return true;
        }
        switch (action)
        {
            case MoveAction m:
                {
                    var dir = TokenRenderer.RenderPath(m.Destination, ctx, run, now);
                    var oldDir = ctx.Folder;
                    bool ok = MoveTo(ctx, Path.Combine(dir, ctx.FileName), m.OnConflict, OperationKind.Move, rule, ops, options, result);
                    if (ok && m.RemoveEmptySourceFolder && !ops.Simulated) ops.RemoveEmptyFolders(oldDir, ctx.RootFolder);
                    return ok;
                }
            case SortIntoSubfoldersAction s:
                {
                    var baseDir = TokenRenderer.RenderPath(s.BaseFolder, ctx, run, now);
                    var sub = SortSubfolder(s, ctx);
                    return MoveTo(ctx, Path.Combine(baseDir, sub, ctx.FileName), s.OnConflict, OperationKind.Move, rule, ops, options, result);
                }
            case RenameAction r:
                {
                    var name = TokenRenderer.RenderFileName(r.NewName, ctx, run, now);
                    return MoveTo(ctx, Path.Combine(ctx.Folder, name), r.OnConflict, OperationKind.Rename, rule, ops, options, result);
                }
            case CopyAction c:
                {
                    var dir = TokenRenderer.RenderPath(c.Destination, ctx, run, now);
                    var target = Path.Combine(dir, ctx.FileName);
                    if (SamePath(target, ctx.LogicalPath))
                    {
                        result.Records.Add(Record(options, rule, ctx, OperationKind.Copy, OperationStatus.Unchanged, ctx.LogicalPath, target, Loc.T("Msg.SameFolder")));
                        return true;
                    }
                    var resolved = ConflictResolver.Resolve(target, c.OnConflict, ops.Exists, out bool overwrite);
                    if (resolved == null) return Skipped(options, rule, ctx, OperationKind.Copy, target, result);
                    ops.EnsureDirectory(Path.GetDirectoryName(resolved)!);
                    ops.Copy(ctx.LogicalPath, resolved, overwrite);
                    result.Records.Add(Record(options, rule, ctx, OperationKind.Copy, Done(ops), ctx.LogicalPath, resolved, overwrite ? Loc.T("Msg.Overwritten") : ""));
                    return true;
                }
            case DeleteAction d:
                {
                    var path = ctx.LogicalPath;
                    var oldDir = ctx.Folder;
                    if (d.Permanent)
                    {
                        if (ops.Simulated)
                        {
                            result.Records.Add(Record(options, rule, ctx, OperationKind.Delete, OperationStatus.Planned, path, "", Loc.T("Msg.PermanentNeedsConfirm")));
                            ops.DeletePermanently(path);
                            return false;
                        }
                        permanentAllowed ??= options.ConfirmPermanentDelete?.Invoke() ?? false;
                        if (permanentAllowed == true)
                        {
                            ops.DeletePermanently(path);
                            result.Records.Add(Record(options, rule, ctx, OperationKind.Delete, OperationStatus.Done, path, "", Loc.T("Msg.DeletedPermanently")));
                            if (d.RemoveEmptySourceFolder) ops.RemoveEmptyFolders(oldDir, ctx.RootFolder);
                            return false;
                        }
                        // not confirmed: fall back to the Recycle Bin (never delete without confirmation)
                    }
                    if (!_recycleBin.IsSupported(path))
                    {
                        result.Records.Add(Record(options, rule, ctx, OperationKind.Recycle, OperationStatus.Failed, path, "", Loc.T("Err.RecycleUnsupportedLeft")));
                        return false;
                    }
                    ops.Recycle(path);
                    result.Records.Add(Record(options, rule, ctx, OperationKind.Recycle, Done(ops), path, "",
                        d.Permanent ? Loc.T("Msg.PermanentNotConfirmed") : ""));
                    if (d.RemoveEmptySourceFolder && !ops.Simulated) ops.RemoveEmptyFolders(oldDir, ctx.RootFolder);
                    return false;
                }
            case CompressAction z:
                {
                    var zipPath = TokenRenderer.RenderPath(z.ArchivePath, ctx, run, now);
                    if (!zipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) zipPath += ".zip";
                    var resolved = ConflictResolver.Resolve(zipPath, z.OnConflict, ops.Exists, out bool overwrite);
                    if (resolved == null) return Skipped(options, rule, ctx, OperationKind.Compress, zipPath, result);
                    ops.EnsureDirectory(Path.GetDirectoryName(resolved)!);
                    ops.CreateZip(ctx.LogicalPath, resolved, overwrite);
                    result.Records.Add(Record(options, rule, ctx, OperationKind.Compress, Done(ops), ctx.LogicalPath, resolved, ""));
                    if (!z.DeleteSource) return true;
                    if (!_recycleBin.IsSupported(ctx.LogicalPath))
                    {
                        result.Records.Add(Record(options, rule, ctx, OperationKind.Recycle, OperationStatus.Failed, ctx.LogicalPath, "", Loc.T("Err.RecycleUnsupportedLeft")));
                        return false;
                    }
                    ops.Recycle(ctx.LogicalPath);
                    result.Records.Add(Record(options, rule, ctx, OperationKind.Recycle, Done(ops), ctx.LogicalPath, "", ""));
                    return false;
                }
            case ExtractAction x:
                {
                    if (!ctx.Extension.Equals("zip", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Records.Add(Record(options, rule, ctx, OperationKind.Extract, OperationStatus.Skipped, ctx.LogicalPath, "", Loc.T("Msg.NotZip")));
                        return true;
                    }
                    var dest = TokenRenderer.RenderPath(x.Destination, ctx, run, now);
                    // read from the physical archive: in simulation it has not moved
                    int n = ops.ExtractZip(ops.Simulated ? ctx.PhysicalPath : ctx.LogicalPath, dest, x.OnConflict);
                    result.Records.Add(Record(options, rule, ctx, OperationKind.Extract, Done(ops), ctx.LogicalPath, dest, string.Format(Loc.T("Msg.FilesExtracted"), n)));
                    if (!x.DeleteArchive) return true;
                    if (!_recycleBin.IsSupported(ctx.LogicalPath))
                    {
                        result.Records.Add(Record(options, rule, ctx, OperationKind.Recycle, OperationStatus.Failed, ctx.LogicalPath, "", Loc.T("Err.RecycleUnsupportedLeft")));
                        return false;
                    }
                    ops.Recycle(ctx.LogicalPath);
                    result.Records.Add(Record(options, rule, ctx, OperationKind.Recycle, Done(ops), ctx.LogicalPath, "", ""));
                    return false;
                }
            case CreateShortcutAction l:
                {
                    var dir = TokenRenderer.RenderPath(l.Destination, ctx, run, now);
                    var name = TokenRenderer.RenderFileName(l.ShortcutName, ctx, run, now);
                    var lnk = Path.Combine(dir, name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? name : name + ".lnk");
                    var resolved = ConflictResolver.Resolve(lnk, l.OnConflict, ops.Exists, out bool overwrite);
                    if (resolved == null) return Skipped(options, rule, ctx, OperationKind.Shortcut, lnk, result);
                    ops.EnsureDirectory(Path.GetDirectoryName(resolved)!);
                    ops.CreateShortcut(ctx.LogicalPath, resolved, overwrite);
                    result.Records.Add(Record(options, rule, ctx, OperationKind.Shortcut, Done(ops), ctx.LogicalPath, resolved, ""));
                    return true;
                }
            case OpenWithAction o:
                {
                    if (string.IsNullOrWhiteSpace(o.Application)) throw new TemplateException(Loc.T("Err.NoApplication"));
                    var app = Environment.ExpandEnvironmentVariables(TokenRenderer.Render(o.Application, ctx, run, now, RenderMode.Raw));
                    var args = TokenRenderer.Render(o.Arguments, ctx, run, now, RenderMode.Raw);
                    ops.Launch(app, args);
                    result.Records.Add(Record(options, rule, ctx, OperationKind.OpenWith, Done(ops), ctx.LogicalPath, app, args));
                    return true;
                }
            default:
                return false;
        }
    }

    private static bool MoveTo(FileContext ctx, string target, ConflictPolicy policy, OperationKind kind, Rule rule,
        IFileOps ops, RunOptions options, RunResult result)
    {
        var source = ctx.LogicalPath;
        target = Path.GetFullPath(target);
        if (string.Equals(source, target, StringComparison.Ordinal))
        {
            result.Records.Add(Record(options, rule, ctx, kind, OperationStatus.Unchanged, source, target, Loc.T("Msg.AlreadyInPlace")));
            return true;
        }
        if (ctx.IsFolder)
        {
            // never move a folder into itself or into one of its subfolders
            var targetDir = Path.GetDirectoryName(target) ?? "";
            if (SamePath(targetDir, source))
            {
                result.Records.Add(Record(options, rule, ctx, kind, OperationStatus.Unchanged, source, targetDir, Loc.T("Msg.FolderIsDestination")));
                return false;
            }
            if (IsInside(target, source))
            {
                result.Records.Add(Record(options, rule, ctx, kind, OperationStatus.Skipped, source, target, Loc.T("Msg.FolderIntoItself")));
                return false;
            }
        }

        string? resolved;
        bool overwrite = false;
        if (SamePath(source, target)) resolved = target; // case-only rename
        else resolved = ConflictResolver.Resolve(target, policy, ops.Exists, out overwrite, ctx.IsFolder);
        if (resolved == null) return Skipped(options, rule, ctx, kind, target, result);

        ops.EnsureDirectory(Path.GetDirectoryName(resolved)!);
        ops.Move(source, resolved, overwrite);
        result.Records.Add(Record(options, rule, ctx, kind, Done(ops), source, resolved,
            overwrite ? Loc.T("Msg.Overwritten") : resolved != target ? Loc.T("Msg.RenamedOnConflict") : ""));
        ctx.LogicalPath = resolved;
        if (!ops.Simulated) ctx.PhysicalPath = resolved;
        return true;
    }

    private static string SortSubfolder(SortIntoSubfoldersAction s, FileContext ctx)
    {
        switch (s.Mode)
        {
            case SortMode.ByType:
                return FileCategories.DisplayName(FileCategories.FromExtension(ctx.Extension));
            case SortMode.ByExtension:
                return ctx.Extension.Length == 0 ? Loc.T("Sort.NoExtension") : TokenRenderer.SanitizeSegment(ctx.Extension.ToUpperInvariant());
            case SortMode.ByFirstLetter:
                {
                    var n = ctx.NameWithoutExtension.TrimStart();
                    return n.Length > 0 && char.IsLetterOrDigit(n[0]) ? char.ToUpper(n[0], Loc.Culture).ToString() : "#";
                }
            default:
                {
                    var date = ConditionEvaluator.GetDate(s.DateSource, ctx) ?? ctx.Info.LastWriteTime;
                    var formatted = TokenRenderer.FormatDate(date, s.DateFormat);
                    return string.Join('\\', formatted.Split('\\').Select(seg => TokenRenderer.SanitizeSegment(seg)));
                }
        }
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="path"/> is strictly inside <paramref name="folder"/>.</summary>
    public static bool IsInside(string path, string folder)
    {
        var f = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + "\\";
        return Path.GetFullPath(path).StartsWith(f, StringComparison.OrdinalIgnoreCase);
    }

    private static OperationStatus Done(IFileOps ops) => ops.Simulated ? OperationStatus.Planned : OperationStatus.Done;

    private static bool Skipped(RunOptions options, Rule rule, FileContext ctx, OperationKind kind, string target, RunResult result)
    {
        result.Records.Add(Record(options, rule, ctx, kind, OperationStatus.Skipped, ctx.LogicalPath, target, Loc.T("Msg.SkippedExists")));
        return false;
    }

    private static OperationKind KindOf(RuleAction a) => a switch
    {
        MoveAction or SortIntoSubfoldersAction => OperationKind.Move,
        CopyAction => OperationKind.Copy,
        RenameAction => OperationKind.Rename,
        DeleteAction => OperationKind.Recycle,
        CompressAction => OperationKind.Compress,
        ExtractAction => OperationKind.Extract,
        CreateShortcutAction => OperationKind.Shortcut,
        OpenWithAction => OperationKind.OpenWith,
        _ => OperationKind.Error,
    };

    private static OperationRecord Record(RunOptions o, Rule rule, FileContext ctx, OperationKind kind, OperationStatus status,
        string source, string target, string message) => new()
        {
            Trigger = o.Trigger,
            RuleName = rule.Name,
            FilePath = ctx.OriginalPath,
            IsFolder = ctx.IsFolder,
            Kind = kind,
            Status = status,
            Source = source,
            Target = target,
            Message = ctx.IsFolder ? string.Join(" · ", new[] { ctx.FolderContent.Summary, message }.Where(m => m.Length > 0)) : message,
        };
}

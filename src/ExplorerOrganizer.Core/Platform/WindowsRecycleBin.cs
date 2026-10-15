using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace ExplorerOrganizer.Core.Platform;

/// <summary>
/// Recycle Bin access through the Shell. Restore works by reading the "$I" index files that
/// Windows writes next to each recycled "$R" item in &lt;drive&gt;\$Recycle.Bin\&lt;user SID&gt;.
/// </summary>
public sealed class WindowsRecycleBin : IRecycleBin
{
    private const int FO_DELETE = 3;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;
    private const ushort FOF_WANTNUKEWARNING = 0x4000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public int wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

    public bool IsSupported(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            if (full.StartsWith(@"\\", StringComparison.Ordinal)) return false; // UNC: no Recycle Bin, Shell would delete permanently
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root)) return false;
            var drive = new DriveInfo(root);
            return drive.DriveType == DriveType.Fixed && full.Length < 260;
        }
        catch
        {
            return false;
        }
    }

    public RecycledItem Recycle(string path)
    {
        var full = Path.GetFullPath(path);
        if (!IsSupported(full))
            throw new IOException(string.Format(Localization.Loc.T("Err.RecycleUnsupported"), full));

        bool isDir = Directory.Exists(full);
        var when = DateTime.UtcNow;
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = full + "\0\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI | FOF_WANTNUKEWARNING,
        };
        int rc = SHFileOperation(ref op);
        if (rc != 0 || op.fAnyOperationsAborted)
            throw new IOException(string.Format(Localization.Loc.T("Err.RecycleFailed"), rc));
        if (File.Exists(full) || Directory.Exists(full))
            throw new IOException(string.Format(Localization.Loc.T("Err.RecycleFailed"), "exists"));
        return new RecycledItem(full, when, isDir);
    }

    public void Restore(RecycledItem item)
    {
        var root = Path.GetPathRoot(item.OriginalPath) ?? throw new IOException("Invalid path");
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new IOException("No user SID");
        var binDir = Path.Combine(root, "$Recycle.Bin", sid);
        if (!Directory.Exists(binDir)) throw new IOException(Localization.Loc.T("Err.RecycleItemNotFound"));

        string? bestInfo = null;
        DateTime bestTime = DateTime.MinValue;
        foreach (var info in Directory.EnumerateFiles(binDir, "$I*"))
        {
            byte[] data;
            try { data = File.ReadAllBytes(info); } catch { continue; }
            if (!TryParseInfoFile(data, out var original, out var deletedUtc)) continue;
            if (!string.Equals(original, item.OriginalPath, StringComparison.OrdinalIgnoreCase)) continue;
            if (deletedUtc < item.DeletedAtUtc.AddMinutes(-2)) continue;
            if (deletedUtc > bestTime) { bestTime = deletedUtc; bestInfo = info; }
        }
        if (bestInfo == null) throw new IOException(Localization.Loc.T("Err.RecycleItemNotFound"));

        var dataPath = Path.Combine(binDir, "$R" + Path.GetFileName(bestInfo)[2..]);
        if (File.Exists(item.OriginalPath) || Directory.Exists(item.OriginalPath))
            throw new IOException(string.Format(Localization.Loc.T("Err.TargetExists"), item.OriginalPath));
        Directory.CreateDirectory(Path.GetDirectoryName(item.OriginalPath)!);
        if (Directory.Exists(dataPath)) Directory.Move(dataPath, item.OriginalPath);
        else File.Move(dataPath, item.OriginalPath);
        try { File.Delete(bestInfo); } catch { /* index file left behind: harmless, Explorer ignores orphans */ }
    }

    /// <summary>Parses a $I file (format v1: Vista–8.1, v2: Windows 10+).</summary>
    internal static bool TryParseInfoFile(byte[] data, out string originalPath, out DateTime deletedUtc)
    {
        originalPath = "";
        deletedUtc = default;
        if (data.Length < 28) return false;
        long version = BitConverter.ToInt64(data, 0);
        long fileTime = BitConverter.ToInt64(data, 16);
        try { deletedUtc = DateTime.FromFileTimeUtc(fileTime); } catch { return false; }
        if (version == 2)
        {
            int chars = BitConverter.ToInt32(data, 24);
            if (chars <= 0 || 28 + chars * 2 > data.Length) return false;
            originalPath = Encoding.Unicode.GetString(data, 28, chars * 2).TrimEnd('\0');
            return originalPath.Length > 0;
        }
        if (version == 1)
        {
            int len = Math.Min(520, data.Length - 24);
            originalPath = Encoding.Unicode.GetString(data, 24, len);
            int nul = originalPath.IndexOf('\0');
            if (nul >= 0) originalPath = originalPath[..nul];
            return originalPath.Length > 0;
        }
        return false;
    }
}

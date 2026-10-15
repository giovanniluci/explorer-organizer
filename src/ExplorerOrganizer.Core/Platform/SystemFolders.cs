using System.Runtime.InteropServices;

namespace ExplorerOrganizer.Core.Platform;

public static class SystemFolders
{
    private static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out IntPtr path);

    public static string Downloads
    {
        get
        {
            try
            {
                if (SHGetKnownFolderPath(DownloadsId, 0, IntPtr.Zero, out var p) == 0)
                {
                    try { return Marshal.PtrToStringUni(p)!; }
                    finally { Marshal.FreeCoTaskMem(p); }
                }
            }
            catch { /* fall through */ }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }

    public static string? Get(string token) => token.ToLowerInvariant() switch
    {
        "desktop" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        "documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "downloads" => Downloads,
        "pictures" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "music" => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        "videos" => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        "home" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        _ => null,
    };
}

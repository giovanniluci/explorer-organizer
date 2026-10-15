using ExplorerOrganizer.Core.Model;

namespace ExplorerOrganizer.Core.Engine;

public static class FileCategories
{
    private static readonly Dictionary<string, FileCategory> Map = Build();

    private static Dictionary<string, FileCategory> Build()
    {
        var d = new Dictionary<string, FileCategory>(StringComparer.OrdinalIgnoreCase);
        void Add(FileCategory c, string list) { foreach (var e in list.Split(' ')) d[e] = c; }
        Add(FileCategory.Image, "jpg jpeg jpe jfif png gif bmp tif tiff webp heic heif avif svg ico raw cr2 cr3 nef arw dng orf rw2 psd");
        Add(FileCategory.Video, "mp4 m4v mkv avi mov wmv flv webm mpg mpeg 3gp ts mts m2ts vob");
        Add(FileCategory.Audio, "mp3 wav flac aac m4a ogg oga opus wma aiff aif mid midi");
        Add(FileCategory.Document, "pdf doc docx docm dot dotx odt rtf txt md xls xlsx xlsm ods csv ppt pptx pptm odp epub mobi xps oxps pages numbers key one msg eml");
        Add(FileCategory.Archive, "zip rar 7z tar gz tgz bz2 xz zst cab iso img lz lzma");
        Add(FileCategory.Code, "cs js ts jsx tsx py java c cpp h hpp go rs rb php html htm css scss json xml yaml yml sql sh ps1 bat cmd ipynb");
        Add(FileCategory.Executable, "exe msi msix msixbundle appx appxbundle com scr");
        return d;
    }

    public static FileCategory FromExtension(string? ext)
    {
        if (string.IsNullOrEmpty(ext)) return FileCategory.Other;
        return Map.TryGetValue(ext.TrimStart('.'), out var c) ? c : FileCategory.Other;
    }

    /// <summary>Localized folder name for a category ("Immagini" / "Images").</summary>
    public static string DisplayName(FileCategory c) => Localization.Loc.T("Category." + c);
}

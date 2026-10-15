using System.Runtime.InteropServices;

namespace ExplorerOrganizer.Core.Platform;

/// <summary>
/// Reads file metadata (EXIF, ID3, Office/PDF document properties...) through the Windows
/// Property System, the same source Explorer uses for its "Details" columns. No third-party parsers.
/// </summary>
public sealed class ShellMetadataProvider : IMetadataProvider
{
    private const int GPS_DEFAULT = 0;
    private const int GPS_BESTEFFORT = 0x40;
    private const ushort VT_FILETIME = 64;
    private static readonly Guid IID_IPropertyStore = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public long int64;
        [FieldOffset(8)] public IntPtr ptr;
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PROPERTYKEY key);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        [PreserveSig] int Commit();
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHGetPropertyStoreFromParsingName(string path, IntPtr bindCtx, int flags, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore? store);

    [DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int PSGetPropertyKeyFromName(string name, out PROPERTYKEY key);

    [DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int PropVariantToStringAlloc(ref PROPVARIANT pv, out IntPtr str);

    [DllImport("ole32.dll", PreserveSig = true)]
    private static extern int PropVariantClear(ref PROPVARIANT pv);

    public object? Get(string path, string canonicalName)
    {
        if (PSGetPropertyKeyFromName(canonicalName, out var key) != 0) return null;
        IPropertyStore? store = null;
        try
        {
            var iid = IID_IPropertyStore;
            int hr = SHGetPropertyStoreFromParsingName(path, IntPtr.Zero, GPS_DEFAULT, ref iid, out store);
            if (hr != 0 || store == null)
            {
                hr = SHGetPropertyStoreFromParsingName(path, IntPtr.Zero, GPS_BESTEFFORT, ref iid, out store);
                if (hr != 0 || store == null) return null;
            }
            if (store.GetValue(ref key, out var pv) != 0) return null;
            try
            {
                if (pv.vt == 0) return null;
                if (pv.vt == VT_FILETIME)
                    return pv.int64 <= 0 ? null : DateTime.FromFileTimeUtc(pv.int64).ToLocalTime();
                if (PropVariantToStringAlloc(ref pv, out var psz) != 0 || psz == IntPtr.Zero) return null;
                try
                {
                    var s = Marshal.PtrToStringUni(psz);
                    if (canonicalName.Equals("System.Media.Duration", StringComparison.OrdinalIgnoreCase)
                        && ulong.TryParse(s, out var ticks))
                        return TimeSpan.FromTicks((long)ticks).ToString(@"hh\.mm\.ss");
                    return string.IsNullOrEmpty(s) ? null : s;
                }
                finally { Marshal.FreeCoTaskMem(psz); }
            }
            finally { PropVariantClear(ref pv); }
        }
        catch (Exception)
        {
            return null; // broken property handlers must never crash the engine
        }
        finally
        {
            if (store != null) Marshal.ReleaseComObject(store);
        }
    }
}

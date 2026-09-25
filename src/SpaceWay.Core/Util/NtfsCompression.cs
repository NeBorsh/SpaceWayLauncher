using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace SpaceWay.Core.Util;

/// <summary>
/// Enables transparent NTFS compression for a directory, the same flag as
/// "Compress contents to save disk space" in folder properties.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class NtfsCompression
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint OpenExisting = 3;

    /// <summary>Open a directory rather than a file.</summary>
    private const uint FileFlagBackupSemantics = 0x02000000;

    private const uint FsctlSetCompression = 0x9C040;
    private const short CompressionFormatDefault = 1;

    public static void Enable(string directory)
    {
        using var handle = CreateFileW(
            directory,
            GenericRead | GenericWrite,
            FileShareRead,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);

        if (handle.IsInvalid)
            throw new IOException($"CreateFileW: {Marshal.GetLastPInvokeError()}");

        var format = CompressionFormatDefault;
        if (!DeviceIoControl(
                handle, FsctlSetCompression, ref format, sizeof(short),
                IntPtr.Zero, 0, out _, IntPtr.Zero))
        {
            throw new IOException($"FSCTL_SET_COMPRESSION: {Marshal.GetLastPInvokeError()}");
        }
    }

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        ref short inBuffer,
        int inBufferSize,
        IntPtr outBuffer,
        int outBufferSize,
        out uint bytesReturned,
        IntPtr overlapped);
}

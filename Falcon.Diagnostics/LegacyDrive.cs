using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Falcon.Diagnostics;

internal static class LegacyDrive
{
    public static void Remove()
    {
        using var registry = Registry.CurrentUser.OpenSubKey(@"Software\Falcon Diagnostics", writable: true);
        if (registry?.GetValue("ExplorerDrive") is not string claim || claim.Length < 3 ||
            claim[0] is < 'E' or > 'Z' || claim[1] != ':')
            return;

        string dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Falcon Diagnostics", "Donnees Falcon");
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        string recorded = claim[2..];
        if (!SamePath(recorded, dataPath) && !SamePath(recorded, directory.FullName) &&
            !(directory.Parent is DirectoryInfo parent && SamePath(recorded, parent.FullName)))
            return;

        string drive = claim[..2];
        var mapped = new char[32768];
        if (QueryDosDeviceW(drive, mapped, mapped.Length) != 0 &&
            new string(mapped, 0, Array.IndexOf(mapped, '\0')).Equals(recorded, StringComparison.OrdinalIgnoreCase))
        {
            const uint exactRemoval = 1 | 2 | 4;
            if (!DefineDosDeviceW(exactRemoval, drive, recorded))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Impossible de retirer l’ancien lecteur Falcon.");
            SHChangeNotify(0x80, 5, drive + @"\", IntPtr.Zero);
        }
        registry.DeleteValue("ExplorerDrive", false);
    }

    static bool SamePath(string recorded, string folder) =>
        recorded.Equals(@"\??\" + Path.TrimEndingDirectorySeparator(folder), StringComparison.OrdinalIgnoreCase);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DefineDosDeviceW(uint flags, string name, string path);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern uint QueryDosDeviceW(string name, [Out] char[] target, int length);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern void SHChangeNotify(uint eventId, uint flags, string item, IntPtr other);
}

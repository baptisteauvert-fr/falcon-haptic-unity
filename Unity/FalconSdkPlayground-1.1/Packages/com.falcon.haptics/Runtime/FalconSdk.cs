using System;
using System.Collections.Generic;
using System.IO;

namespace Falcon.Haptics
{
    public static class FalconSdk
    {
        public static string Find()
        {
            var configured = Environment.GetEnvironmentVariable("FALCON_DHD_DLL");
            if (IsLibrary(configured))
                return configured;

            var roots = new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root))
                    continue;
                foreach (var folder in SdkDirectories(root))
                {
                    foreach (var subfolder in new[] { "bin", "lib", "" })
                    {
                        var dll = Path.Combine(folder, subfolder, "dhd64.dll");
                        if (File.Exists(dll))
                            return dll;
                    }
                }
            }

            var system = Path.Combine(Environment.SystemDirectory, "dhd64.dll");
            return File.Exists(system) ? system : null;
        }

        public static bool IsLibrary(string path) =>
            !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) &&
            string.Equals(Path.GetFileName(path), "dhd64.dll", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(path);

        static IEnumerable<string> SdkDirectories(string programFiles)
        {
            string[] folders;
            string vendor = Path.Combine(programFiles, "Force Dimension");
            try
            {
                folders = Directory.Exists(vendor)
                    ? Directory.GetDirectories(vendor)
                    : Array.Empty<string>();
            }
            catch (UnauthorizedAccessException)
            {
                folders = Array.Empty<string>();
            }
            catch (IOException)
            {
                folders = Array.Empty<string>();
            }
            foreach (var folder in folders)
                yield return folder;
            yield return vendor;
        }
    }
}

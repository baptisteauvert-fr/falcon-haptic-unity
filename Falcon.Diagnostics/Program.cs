using System.Diagnostics;
using System.Text.Json;
using Falcon.Haptics;

namespace Falcon.Diagnostics;

internal static class Program
{
    internal const string StartupArgument = "--watch";
    internal const string SdkDownload = "https://forcedimension.com/software";

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == FalconSetup.RepairArgument)
        {
            if (args.Length != 3)
                return 87;
            try { return FalconSetup.RepairUsb(args[1], args[2]); }
            catch (InvalidOperationException) { return 170; } // ERROR_BUSY
            catch (UnauthorizedAccessException) { return 5; }
            catch (IOException) { return 2; }
        }
        if (args.Contains("--diagnostic"))
            return WriteDiagnostic(args);

        using var singleton = new Mutex(true, @"Local\Falcon.Diagnostics", out bool first);
        using var showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Falcon.Diagnostics.Show");
        if (!first)
        {
            if (!args.Contains(StartupArgument))
                showRequest.Set();
            return 0;
        }

        try { LegacyDrive.Remove(); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or UnauthorizedAccessException or IOException)
        {
            Debug.WriteLine(ex.Message);
        }
        ApplicationConfiguration.Initialize();
        using var window = new DiagnosticWindow(showRequest);
        window.Start(args.Contains(StartupArgument));
        Application.Run();
        return 0;
    }

    static int WriteDiagnostic(string[] args)
    {
        int index = Array.IndexOf(args, "--diagnostic");
        if (index + 1 >= args.Length)
            return 2;

        var usb = WindowsFalcon.Inspect();
        string path = FalconSdk.Find(), sdk = null, firmware = null, error = null;
        FalconSample? sample = null;
        try
        {
            if (path != null)
            {
                using var device = new FalconDevice(path);
                sdk = device.SdkVersion;
                if (usb.Started && args.Contains("--read"))
                {
                    device.Connect();
                    firmware = device.FirmwareVersion;
                    sample = device.Read();
                }
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        var output = new { usb, path, sdk, firmware, sample, error };
        File.WriteAllText(args[index + 1], JsonSerializer.Serialize(output,
            new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
        return 0;
    }

    internal static void OpenSdkDownload() =>
        Process.Start(new ProcessStartInfo(SdkDownload) { UseShellExecute = true });
}

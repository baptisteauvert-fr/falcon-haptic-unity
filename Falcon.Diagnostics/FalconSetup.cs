using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Falcon.Haptics;

namespace Falcon.Diagnostics;

internal enum SetupNeed { None, Sdk, Driver }

internal static class FalconSetup
{
    internal const string RepairArgument = "--repair-falcon-usb";
    const int RebootRequired = 3010;
    const int DriverChanged = 0x20000001;
    const uint ForceInstall = 1;
    const string VendorFolder = "sdk-3.17.7";
    const string InstallerName = "sdk-3.17.7-win-x64.exe";
    const string InstallerUrl = "https://downloads.forcedimension.com/sdk/" + InstallerName;
    const long InstallerLength = 84_446_656;

    // SHA-256 of the official installer and USB package whose vendor signatures
    // were verified on the validation machine. Review the pins on SDK upgrades.
    const string InstallerHash = "1CCBAEC254D416850128E42D8498B09591FF435FC8DCF95CCEF3F1F1D4D5451D";
    const string DriverInfHash = "D607ECF61E9C0FDD24A145FC1C6526ABD864AE7230E508CF28E2B7533F187E96";
    const string DriverCatalogHash = "C26FBE37E5F30FA0AFAD93004F7B583EF37B1CE81D20DF0CB4698CE501380FDB";
    static readonly HttpClient Http = new();

    public static SetupNeed GetNeed(UsbFalcon usb, string sdk) =>
        string.IsNullOrEmpty(sdk) ? SetupNeed.Sdk :
        NeedsDriver(usb) ? SetupNeed.Driver : SetupNeed.None;

    static bool NeedsDriver(UsbFalcon usb) => usb.Present && (usb.Problem == 28 || usb.UsesLegacyDriver);

    public static async Task<bool> InstallAsync(SetupNeed need, string sdk, IProgress<string> status)
    {
        if (need == SetupNeed.None)
            return false;
        if (RuntimeInformation.OSArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("La réparation a été conçue pour Windows x64 sur processeur Intel ou AMD.");
        if (need == SetupNeed.Sdk)
        {
            if (await InstallSdkAsync(status))
                return true;
            sdk = FalconSdk.Find();
            if (sdk == null)
                throw new InvalidOperationException("Windows ne retrouve pas dhd64.dll. Sélectionnez-le dans le dossier bin du SDK.");
        }

        var device = WindowsFalcon.Inspect(requireSingleFalcon: true);
        if (!NeedsDriver(device))
            return false;

        string inf = FindDriver(sdk);
        if (inf == null)
        {
            if (need == SetupNeed.Driver)
            {
                if (await InstallSdkAsync(status))
                    return true;
                inf = FindDriver(FalconSdk.Find() ?? sdk);
            }
            if (inf == null)
                throw new InvalidOperationException("Le pilote signé Force Dimension est introuvable. Vérifiez l’installation du SDK officiel 3.17.7 x64.");
        }

        status.Report("Association du pilote Force Dimension au Falcon · confirmez la demande de Windows.");
        var repair = Elevated(Environment.ProcessPath ?? throw new IOException("Exécutable du diagnostic introuvable."));
        repair.ArgumentList.Add(RepairArgument);
        repair.ArgumentList.Add(device.Instance);
        repair.ArgumentList.Add(inf);
        int code = await ExitCodeAsync(repair);
        if (code == RebootRequired)
            return true;
        if (code == DriverChanged)
            throw new InvalidOperationException("Le Falcon ou son pilote a changé pendant la réparation. Laissez un seul Falcon branché puis actualisez.");
        if (code != 0)
            throw new Win32Exception(code, $"Windows a refusé le pilote Force Dimension (0x{code:X8}) · {new Win32Exception(code).Message}");
        return false;
    }

    internal static int RepairUsb(string expectedInstance, string inf)
    {
        if (RuntimeInformation.OSArchitecture != Architecture.X64)
            return 1633; // ERROR_INSTALL_PLATFORM_UNSUPPORTED
        if (!Path.IsPathFullyQualified(inf) || !IsOfficialDriver(inf))
            return 2; // ERROR_FILE_NOT_FOUND

        var device = WindowsFalcon.Inspect(requireSingleFalcon: true);
        if (!string.Equals(expectedInstance, device.Instance, StringComparison.OrdinalIgnoreCase))
            return DriverChanged;
        if (!NeedsDriver(device))
            return device.Started && device.UsesWinUsb ? 0 : DriverChanged;

        // Override ranking only for the known incompatible FTDI binding.
        if (!UpdateDriverForPlugAndPlayDevicesW(IntPtr.Zero, WindowsFalcon.HardwareId, inf,
            device.UsesLegacyDriver ? ForceInstall : 0, out bool reboot))
            return Marshal.GetLastWin32Error();
        return reboot ? RebootRequired : 0;
    }

    [DllImport("newdev.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool UpdateDriverForPlugAndPlayDevicesW(IntPtr parent, string hardwareId, string inf,
        uint flags, [MarshalAs(UnmanagedType.Bool)] out bool reboot);

    static async Task<bool> InstallSdkAsync(IProgress<string> status)
    {
        string offline = Path.Combine(AppContext.BaseDirectory, InstallerName);
        if (File.Exists(offline))
        {
            VerifyInstaller(offline);
            status.Report("Installation du SDK · confirmez la demande de Windows.");
            return await RunSdkInstallerAsync(offline);
        }

        DirectoryInfo temp = Directory.CreateTempSubdirectory("FalconSdk-");
        string downloaded = Path.Combine(temp.FullName, InstallerName);
        try
        {
            status.Report("Téléchargement du SDK officiel Force Dimension…");
            using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            using (var response = await Http.GetAsync(InstallerUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is long announced && announced != InstallerLength)
                    throw new IOException("La taille de l’installateur officiel a changé. Utilisez le site Force Dimension.");

                using var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                using var output = File.Create(downloaded);
                var buffer = new byte[81920];
                int count;
                while ((count = await input.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) != 0)
                {
                    if (output.Length + count > InstallerLength)
                        throw new IOException("Le téléchargement dépasse la taille attendue.");
                    await output.WriteAsync(buffer.AsMemory(0, count), timeout.Token).ConfigureAwait(false);
                }
            }

            VerifyInstaller(downloaded);
            status.Report("Installation du SDK · confirmez la demande de Windows.");
            return await RunSdkInstallerAsync(downloaded).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                File.Delete(downloaded);
                temp.Delete();
            }
            catch (IOException ex) { Debug.WriteLine(ex.Message); }
            catch (UnauthorizedAccessException ex) { Debug.WriteLine(ex.Message); }
        }
    }

    static string FindDriver(string sdk)
    {
        string installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Force Dimension", VendorFolder);
        string selected = new FileInfo(sdk).Directory?.Parent?.FullName;
        foreach (string root in new[] { installed, selected })
        {
            if (string.IsNullOrEmpty(root))
                continue;
            string folder = Path.Combine(root, "drivers", "usb");
            string inf = Path.Combine(folder, "fdhusb.inf");
            if (IsOfficialDriver(inf))
                return inf;
        }
        return null;
    }

    static bool IsOfficialDriver(string inf) =>
        string.Equals(Path.GetFileName(inf), "fdhusb.inf", StringComparison.OrdinalIgnoreCase) &&
        HasHash(inf, DriverInfHash) && HasHash(Path.ChangeExtension(inf, ".cat"), DriverCatalogHash);

    static void VerifyInstaller(string path)
    {
        if (new FileInfo(path).Length != InstallerLength || !HasHash(path, InstallerHash))
            throw new IOException("Impossible de vérifier l’installateur. Téléchargez la version officielle 3.17.7 x64.");
    }

    static bool HasHash(string path, string expected)
    {
        if (!File.Exists(path))
            return false;
        using var file = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file)).Equals(expected, StringComparison.Ordinal);
    }

    static ProcessStartInfo Elevated(string path) => new(path) { UseShellExecute = true, Verb = "runas" };

    static async Task<bool> RunSdkInstallerAsync(string path)
    {
        int code = await ExitCodeAsync(Elevated(path)).ConfigureAwait(false);
        if (code != 0 && code != RebootRequired)
            throw new InvalidOperationException($"L’installation s’est arrêtée (code {code}).");
        return code == RebootRequired;
    }

    static async Task<int> ExitCodeAsync(ProcessStartInfo options)
    {
        using Process process = Process.Start(options) ?? throw new IOException("Windows n’a pas ouvert l’installateur.");
        await process.WaitForExitAsync().ConfigureAwait(false);
        return process.ExitCode;
    }
}

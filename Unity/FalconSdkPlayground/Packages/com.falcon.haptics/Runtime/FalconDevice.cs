using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using System.Threading;

namespace Falcon.Haptics
{
    public readonly struct FalconSample
    {
        public readonly double X, Y, Z;
        public readonly uint Buttons;

        public FalconSample(double x, double y, double z, uint buttons)
        {
            X = x;
            Y = y;
            Z = z;
            Buttons = buttons;
        }
    }

    // One session owns one handle. Open, sample and dispose it on the same thread.
    public sealed partial class FalconDevice : IFalconBackend
    {
        static FalconDevice sessionOwner;
        readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        const int FalconType = 60;
        const uint DllLoadDirAndDefaultDirs = 0x00001100;
        IntPtr library;
        int id = -1;
        DeviceCall close;
        DeviceCall getType;
        Count count;
        DeviceCall open;
        Position getPosition;
        Button getButton;
        EnableForce enableForce;
        VersionString firmware;
        TextResult getSdkVersion;
        TextResult getError;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Count();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DeviceCall(sbyte id);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnableForce(byte enabled, sbyte id);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Position(out double x, out double y, out double z, sbyte id);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Button(int index, sbyte id);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr TextResult();
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        delegate int VersionString(StringBuilder buffer, UIntPtr size, sbyte id);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32.dll", ExactSpelling = true)]
        static extern bool FreeLibrary(IntPtr module);

        public FalconDevice(string path)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                RuntimeInformation.ProcessArchitecture != Architecture.X64)
                throw new PlatformNotSupportedException("Force Dimension DHD requiert Windows x64.");

            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || !File.Exists(path))
                throw new FileNotFoundException("Sélectionnez le fichier dhd64.dll du SDK Force Dimension.");

            library = LoadLibraryExW(Path.GetFullPath(path), IntPtr.Zero, DllLoadDirAndDefaultDirs);
            if (library == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Impossible de charger le SDK DHD x64 ou l’une de ses dépendances.");

            try
            {
                count = Export<Count>("dhdGetDeviceCount");
                open = Export<DeviceCall>("dhdOpenID");
                close = Export<DeviceCall>("dhdClose");
                getType = Export<DeviceCall>("dhdGetSystemType");
                enableForce = Export<EnableForce>("dhdEnableForce");
                getPosition = Export<Position>("dhdGetPosition");
                getButton = Export<Button>("dhdGetButton");
                firmware = Export<VersionString>("dhdGetVersionStr");
                getSdkVersion = Export<TextResult>("dhdGetSDKVersionStr");
                getError = Export<TextResult>("dhdErrorGetLastStr");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public string SdkVersion
        {
            get
            {
                EnsureLoaded();
                return Marshal.PtrToStringAnsi(getSdkVersion()) ?? "Indisponible";
            }
        }

        public string FirmwareVersion
        {
            get
            {
                var buffer = new StringBuilder(256);
                return firmware(buffer, new UIntPtr((uint)buffer.Capacity), DeviceId()) >= 0 && buffer.Length > 0
                    ? buffer.ToString()
                    : "Indisponible";
            }
        }

        public void Connect()
        {
            EnsureLoaded();
            if (id >= 0)
                return;

            if (Interlocked.CompareExchange(ref sessionOwner, this, null) != null)
                throw new InvalidOperationException("Une autre session possède déjà le Falcon dans ce processus.");
            try
            {

                int devices = count();
                if (devices <= 0)
                    throw Failure("Aucun périphérique vu par le SDK DHD");

                Exception lastOpenError = null;
                for (int index = 0; index < devices && index <= sbyte.MaxValue; index++)
                {
                    int opened = open((sbyte)index);
                    if (opened < 0)
                    {
                        lastOpenError = Failure("Ouverture du Falcon");
                        continue;
                    }
                    id = opened;
                    try
                    {
                        if (getType(DeviceId()) != FalconType)
                        {
                            Disconnect();
                            continue;
                        }
                        if (enableForce(0, DeviceId()) < 0)
                            throw Failure("Désactivation du retour de force");
                        return;
                    }
                    catch (Exception connectError)
                    {
                        try { Disconnect(); }
                        catch (Exception cleanupError)
                        {
                            throw new FalconCleanupException("Échec de connexion et de fermeture du Falcon.",
                                new[] { connectError, cleanupError });
                        }
                        throw;
                    }
                }
                throw lastOpenError ?? new InvalidOperationException("Aucun Novint Falcon parmi les périphériques DHD.");
            }
            finally
            {
                if (id < 0) Interlocked.CompareExchange(ref sessionOwner, null, this);
            }
        }

        public FalconSample Read()
        {
            sbyte deviceId = DeviceId();
            if (getPosition(out double x, out double y, out double z, deviceId) < 0)
                throw Failure("Lecture de la position");

            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z))
                throw new InvalidOperationException("Le SDK a fourni une position invalide.");

            uint buttons = 0;
            for (int index = 0; index < 4; index++)
            {
                int pressed = getButton(index, deviceId);
                if (pressed < 0)
                    throw Failure("Lecture des boutons");
                if (pressed == 1)
                    buttons |= 1u << index;
            }

            return new FalconSample(x, y, z, buttons);
        }

        static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        void Disconnect()
        {
            if (id < 0)
                return;
            sbyte deviceId = DeviceId();
            if (close(deviceId) < 0)
                throw Failure("Fermeture du Falcon");
            id = -1;
        }

        public void Dispose()
        {
            if (library == IntPtr.Zero)
                return;
            EnsureLoaded();
            var failures = new List<Exception>();
            try
            {
                try { EndHaptics(); } catch (Exception ex) { failures.Add(ex); }
                try { Disconnect(); } catch (Exception ex) { failures.Add(ex); }
            }
            finally
            {
                // A failed close may leave native activity alive. Quarantine its handle and DLL.
                if (id < 0)
                {
                    if (!FreeLibrary(library))
                        failures.Add(new InvalidOperationException("Le déchargement de DHD a échoué."));
                    library = IntPtr.Zero;
                    forceEnabled = false;
                    Interlocked.CompareExchange(ref sessionOwner, null, this);
                }
            }
            if (failures.Count != 0) throw new FalconCleanupException("Nettoyage du Falcon incomplet.", failures);
        }

        void EnsureLoaded()
        {
            if (library == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(FalconDevice));
            if (Thread.CurrentThread.ManagedThreadId != ownerThread)
                throw new InvalidOperationException("La session DHD doit rester sur son thread propriétaire.");
        }

        void IFalconBackend.BeginHaptics(HapticMode mode) => BeginHaptics(mode);
        void IFalconBackend.ApplyHaptics(System.Numerics.Vector3 force) => ApplyHaptics(force);
        void IFalconBackend.EndHaptics() => EndHaptics();

        sbyte DeviceId()
        {
            EnsureLoaded();
            if (id < 0)
                throw new InvalidOperationException("Le Falcon est fermé.");
            return checked((sbyte)id);
        }

        T Export<T>(string name) where T : Delegate
        {
            IntPtr address = GetProcAddress(library, name);
            if (address == IntPtr.Zero)
                throw new EntryPointNotFoundException("Le SDK ne fournit pas " + name + ".");
            return Marshal.GetDelegateForFunctionPointer<T>(address);
        }

        InvalidOperationException Failure(string operation)
        {
            return new InvalidOperationException(operation + " — " +
                (Marshal.PtrToStringAnsi(getError()) ?? "Erreur DHD"));
        }
    }
}


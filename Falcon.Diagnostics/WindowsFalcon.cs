using System.Runtime.InteropServices;
using System.Text;

namespace Falcon.Diagnostics;

internal readonly record struct UsbFalcon(string Instance, uint Status, uint Problem, string Service, string DriverVersion)
{
    public bool Present => Instance is not null;
    public bool Started => Present && Problem == 0 && (Status & 8) != 0;
    public bool UsesLegacyDriver => string.Equals(Service, "FTDIBUS", StringComparison.OrdinalIgnoreCase);
    public bool UsesWinUsb => string.Equals(Service, "WinUSB", StringComparison.OrdinalIgnoreCase);
}

internal static class WindowsFalcon
{
    internal const string HardwareId = @"USB\VID_0403&PID_CB48";
    const string Prefix = HardwareId + "\\";
    const uint PresentUsbEnumerator = 0x101;
    const uint BufferSmall = 0x1A;

    [StructLayout(LayoutKind.Sequential)]
    struct PropertyKey
    {
        public Guid Format;
        public uint Id;
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern uint CM_Get_Device_ID_List_SizeW(out uint length, string filter, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern uint CM_Get_Device_ID_ListW(string filter, [Out] char[] ids, uint length, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern uint CM_Locate_DevNodeW(out uint node, string id, uint flags);
    [DllImport("cfgmgr32.dll", ExactSpelling = true)]
    static extern uint CM_Get_DevNode_Status(out uint status, out uint problem, uint node, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern uint CM_Get_DevNode_Registry_PropertyW(uint node, uint property, out uint type, [Out] byte[] buffer, ref uint bytes, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern uint CM_Get_DevNode_PropertyW(uint node, ref PropertyKey key, out uint type, [Out] byte[] buffer, ref uint bytes, uint flags);

    public static UsbFalcon Inspect(bool requireSingleFalcon = false)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            uint result = CM_Get_Device_ID_List_SizeW(out uint length, "USB", PresentUsbEnumerator);
            if (result != 0)
                throw new InvalidOperationException($"Inventaire USB indisponible (0x{result:X}).");

            char[] ids = new char[length];
            result = CM_Get_Device_ID_ListW("USB", ids, length, PresentUsbEnumerator);
            if (result == BufferSmall)
                continue;
            if (result != 0)
                throw new InvalidOperationException($"Inventaire USB indisponible (0x{result:X}).");

            UsbFalcon detected = default;
            bool found = false;
            foreach (string instance in new string(ids).Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!instance.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (requireSingleFalcon && found)
                    throw new InvalidOperationException("Laissez un seul Falcon branché pendant la réparation.");
                found = true;
                if (CM_Locate_DevNodeW(out uint node, instance, 0) != 0 ||
                    CM_Get_DevNode_Status(out uint status, out uint problem, node, 0) != 0)
                    continue;
                byte[] buffer = new byte[512];
                uint bytes = (uint)buffer.Length;
                string service = CM_Get_DevNode_Registry_PropertyW(node, 5, out _, buffer, ref bytes, 0) == 0
                    ? Decode(buffer, bytes) : null;
                var versionKey = new PropertyKey { Format = new Guid("a8b865dd-2e3d-4094-ad97-e593a70c75d6"), Id = 3 };
                bytes = (uint)buffer.Length;
                string version = CM_Get_DevNode_PropertyW(node, ref versionKey, out _, buffer, ref bytes, 0) == 0
                    ? Decode(buffer, bytes) : null;
                detected = new UsbFalcon(instance, status, problem, service, version);
                if (!requireSingleFalcon)
                    return detected;
            }
            return detected;
        }
        throw new InvalidOperationException("Les périphériques USB changent. Réessayez.");
    }

    static string Decode(byte[] buffer, uint bytes) => Encoding.Unicode
        .GetString(buffer, 0, checked((int)bytes)).TrimEnd('\0');
}

using System;
using System.Runtime.InteropServices;

namespace WinDivert
{
    public static class WinDivertNative
    {
        public enum WinDivertPacketFlags : byte
        {
            Sniffed = 1 << 0,
            Outbound = 1 << 1,
            Loopback = 1 << 2,
            Impostor = 1 << 3,
            IPv6 = 1 << 4,
            IPChecksum = 1 << 5,
            TCPChecksum = 1 << 6,
            UDPChecksum = 1 << 7,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Address
        {
            public long Timestamp;
            public byte Layer;
            public byte Event;
            public WinDivertPacketFlags Flags;
            public uint IfIdx;
            public uint SubIfIdx;
        }

        public enum WinDivertLayer : uint
        {
            Network = 0,
            Forward = 1,
            Flow = 2,
            Socket = 3,
            Reflect = 4
        }

        const string WINDIVERT_DLL = "WinDivert.dll";

        [DllImport(WINDIVERT_DLL, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern IntPtr WinDivertOpen([MarshalAs(UnmanagedType.LPStr)] string filter, WinDivertLayer layer, short priority, ulong flags);

        [DllImport(WINDIVERT_DLL, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern bool WinDivertRecv(IntPtr handle, IntPtr pPacket, uint packetLen, out uint readLen, out Address pAddr);

        [DllImport(WINDIVERT_DLL, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern bool WinDivertClose(IntPtr handle);

        [DllImport(WINDIVERT_DLL, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern bool WinDivertHelperCompileFilter([MarshalAs(UnmanagedType.LPStr)] string filter, WinDivertLayer layer, IntPtr obj, uint objLen, out IntPtr errorStr, out uint errorPos);

        [DllImport(WINDIVERT_DLL, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern bool WinDivertSend(IntPtr handle, IntPtr pPacket, uint packetLen, out uint pSendLen, ref Address pAddr);

        [DllImport(WINDIVERT_DLL, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern uint WinDivertHelperCalcChecksums(IntPtr pPacket, uint packetLen, ulong flags);
    }
}
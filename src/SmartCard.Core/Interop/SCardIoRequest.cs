using System.Runtime.InteropServices;

namespace SmartCard.WinUI.Interop;

/// <summary>
/// Structure defining protocol control information for SCardTransmit.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SCardIoRequest
{
    public uint dwProtocol;
    public uint cbPciLength;

    public static SCardIoRequest CreateT1() => new()
    {
        dwProtocol = WinSCardNative.SCARD_PROTOCOL_T1,
        cbPciLength = (uint)Marshal.SizeOf<SCardIoRequest>()
    };

    public static SCardIoRequest CreateT0() => new()
    {
        dwProtocol = WinSCardNative.SCARD_PROTOCOL_T0,
        cbPciLength = (uint)Marshal.SizeOf<SCardIoRequest>()
    };
}

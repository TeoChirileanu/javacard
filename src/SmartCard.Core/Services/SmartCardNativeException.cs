using SmartCard.WinUI.Interop;

namespace SmartCard.WinUI.Services;

/// <summary>
/// PC/SC call that returned a failure code from winscard.dll.
/// </summary>
public sealed class SmartCardNativeException : Exception
{
    public SmartCardNativeException(int nativeStatus)
        : base($"PC/SC error 0x{nativeStatus:X8}")
    {
        NativeStatus = nativeStatus;
    }

    public int NativeStatus { get; }

    public static bool IsReaderLoss(int nativeStatus) =>
        nativeStatus == WinSCardNative.SCARD_E_NO_READERS_AVAILABLE
        || nativeStatus == WinSCardNative.SCARD_E_READER_UNAVAILABLE;
}

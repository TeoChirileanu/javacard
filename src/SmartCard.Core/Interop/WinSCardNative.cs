using System;
using System.Runtime.InteropServices;

namespace SmartCard.WinUI.Interop;

/// <summary>
/// Source-generated P/Invoke wrappers for Windows winscard.dll (PC/SC Resource Manager).
/// Compatible with .NET 11 Native AOT.
/// </summary>
public static partial class WinSCardNative
{
    private const string WinSCardDll = "winscard.dll";

    public const uint SCARD_SCOPE_USER = 0;
    public const uint SCARD_SCOPE_SYSTEM = 2;

    public const uint SCARD_PROTOCOL_UNDEFINED = 0x00000000;
    public const uint SCARD_PROTOCOL_T0        = 0x00000001;
    public const uint SCARD_PROTOCOL_T1        = 0x00000002;

    public const uint SCARD_SHARE_SHARED    = 0x00000002;
    public const uint SCARD_SHARE_EXCLUSIVE = 0x00000001;
    public const uint SCARD_SHARE_DIRECT    = 0x00000003;

    public const uint SCARD_LEAVE_CARD   = 0;
    public const uint SCARD_RESET_CARD   = 1;
    public const uint SCARD_UNPOWER_CARD = 2;

    public const int SCARD_S_SUCCESS              = 0;
    public const int SCARD_E_NO_SMARTCARD         = unchecked((int)0x8010000C);
    public const int SCARD_E_UNKNOWN_READER       = unchecked((int)0x80100009);
    public const int SCARD_E_NO_READERS_AVAILABLE = unchecked((int)0x8010002E);
    public const int SCARD_E_READER_UNAVAILABLE   = unchecked((int)0x80100017);

    public const uint SCARD_ATTR_ATR_STRING = 0x00090303;

    [LibraryImport(WinSCardDll, EntryPoint = "SCardEstablishContext")]
    public static partial int SCardEstablishContext(
        uint dwScope,
        nint pvReserved1,
        nint pvReserved2,
        out nint phContext
    );

    [LibraryImport(WinSCardDll, EntryPoint = "SCardReleaseContext")]
    public static partial int SCardReleaseContext(
        nint hContext
    );

    [LibraryImport(WinSCardDll, EntryPoint = "SCardListReadersW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int SCardListReaders(
        nint hContext,
        string? mszGroups,
        Span<char> mszReaders,
        ref uint pcchReaders
    );

    [LibraryImport(WinSCardDll, EntryPoint = "SCardConnectW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int SCardConnect(
        nint hContext,
        string szReader,
        uint dwShareMode,
        uint dwPreferredProtocols,
        out nint phCard,
        out uint pdwActiveProtocol
    );

    [LibraryImport(WinSCardDll, EntryPoint = "SCardDisconnect")]
    public static partial int SCardDisconnect(
        nint hCard,
        uint dwDisposition
    );

    [LibraryImport(WinSCardDll, EntryPoint = "SCardGetAttrib")]
    public static partial int SCardGetAttrib(
        nint hCard,
        uint dwAttrId,
        byte[] pbAttr,
        ref uint pcbAttrLen
    );

    [LibraryImport(WinSCardDll, EntryPoint = "SCardTransmit")]
    public static partial int SCardTransmit(
        nint hCard,
        in SCardIoRequest pioSendPci,
        ReadOnlySpan<byte> pbSendBuffer,
        uint cbSendLength,
        nint pioRecvPci,
        Span<byte> pbRecvBuffer,
        ref uint pcbRecvLength
    );
}

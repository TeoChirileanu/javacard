using System;

namespace SmartCard.WinUI.Models;

/// <summary>
/// Binding identifiers shared by the applet contract and the host.
/// </summary>
public static class SmartCardProtocol
{
    /// <summary>Applet AID, 8 bytes: F0 73 70 65 63 01 01 00.</summary>
    public static readonly byte[] AppletAid = { 0xF0, 0x73, 0x70, 0x65, 0x63, 0x01, 0x01, 0x00 };

    public const ushort SuccessStatusWord = 0x9000;

    /// <summary>Persistent NVM buffer length in octets.</summary>
    public const int NvmCapacity = 128;

    public const string Spr332ReaderName = "SCM Microsystems Inc. SPRx32 USB Smart Card Reader 0";
}

/// <summary>
/// Information about an enumerated PC/SC smart card reader.
/// </summary>
public sealed record SmartCardReaderInfo(
    string Name,
    bool IsConnected,
    string? CardAtr
);

/// <summary>
/// Result of an application-level smart card operation.
/// </summary>
public sealed record CardOperationResult(
    bool Success,
    string Message,
    string? Data,
    ushort StatusWord
);

/// <summary>
/// Low-level APDU Command representation.
/// </summary>
public readonly record struct ApduCommand(byte Cla, byte Ins, byte P1, byte P2, ReadOnlyMemory<byte> Data = default, byte? Le = null)
{
    public byte[] ToBytes()
    {
        int length = 4 + (Data.IsEmpty ? 0 : 1 + Data.Length) + (Le.HasValue ? 1 : 0);
        byte[] bytes = new byte[length];
        bytes[0] = Cla;
        bytes[1] = Ins;
        bytes[2] = P1;
        bytes[3] = P2;
        int idx = 4;
        if (!Data.IsEmpty)
        {
            bytes[idx++] = (byte)Data.Length;
            Data.Span.CopyTo(bytes.AsSpan(idx));
            idx += Data.Length;
        }
        if (Le.HasValue)
        {
            bytes[idx] = Le.Value;
        }
        return bytes;
    }
}

/// <summary>
/// Low-level APDU Response representation.
/// </summary>
public readonly record struct ApduResponse(ReadOnlyMemory<byte> Data, ushort StatusWord)
{
    public bool IsSuccess => StatusWord == 0x9000;
}

/// <summary>
/// One step of a GlobalPlatform CAP install, as a share of measured wall time.
/// </summary>
public readonly record struct InstallProgress(int Percent, string Stage);

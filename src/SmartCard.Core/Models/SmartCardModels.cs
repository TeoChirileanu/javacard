using System;

namespace SmartCard.WinUI.Models;

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

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SmartCard.WinUI.Interop;
using SmartCard.WinUI.Models;

namespace SmartCard.WinUI.Services;

/// <summary>
/// Implements PC/SC smart card interactions using Native AOT P/Invoke to winscard.dll,
/// with built-in ISO 7816-4 virtual emulation fallback when applet is not yet flashed to EEPROM.
/// </summary>
public sealed class SmartCardService : ISmartCardService
{
    public const string SimulatorReaderName = "[Simulator] Java Card Virtual Reader (ISO 7816-4)";

    private nint _context = nint.Zero;
    private nint _card = nint.Zero;
    private uint _activeProtocol = WinSCardNative.SCARD_PROTOCOL_UNDEFINED;
    private string? _currentReader;

    private bool _isEmulated = false;
    private bool _isVirtualOnly = false;
    private readonly byte[] _emulatedMemory = new byte[128];
    private int _emulatedDataLength = 0;

    // Standard AID for BerStorageApplet: F0 73 70 65 63 01 01 00
    private static readonly byte[] AppletAid = { 0xF0, 0x73, 0x70, 0x65, 0x63, 0x01, 0x01, 0x00 };

    public bool IsConnected => (_card != nint.Zero) || _isVirtualOnly;
    public bool IsEmulated => _isEmulated;
    public string? CurrentReader => _currentReader;

    public ValueTask InitializeAsync()
    {
        if (_context != nint.Zero)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            int rc = WinSCardNative.SCardEstablishContext(WinSCardNative.SCARD_SCOPE_USER, nint.Zero, nint.Zero, out _context);
            if (rc != WinSCardNative.SCARD_S_SUCCESS)
            {
                _context = nint.Zero;
            }
        }
        catch
        {
            _context = nint.Zero;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<string>> GetAvailableReadersAsync()
    {
        List<string> readers = new();

        if (_context != nint.Zero)
        {
            uint charCount = 0;
            int rc = WinSCardNative.SCardListReaders(_context, null, Span<char>.Empty, ref charCount);
            if (rc == WinSCardNative.SCARD_S_SUCCESS && charCount > 1)
            {
                char[] buffer = new char[charCount];
                rc = WinSCardNative.SCardListReaders(_context, null, buffer.AsSpan(), ref charCount);
                if (rc == WinSCardNative.SCARD_S_SUCCESS)
                {
                    int start = 0;
                    for (int i = 0; i < charCount; i++)
                    {
                        if (buffer[i] == '\0')
                        {
                            if (i > start)
                            {
                                readers.Add(new string(buffer, start, i - start));
                            }
                            start = i + 1;
                        }
                    }
                }
            }
        }

        // Add the virtual simulator reader option
        readers.Add(SimulatorReaderName);

        return ValueTask.FromResult<IReadOnlyList<string>>(readers);
    }

    public ValueTask<bool> ConnectAsync(string readerName)
    {
        if (string.IsNullOrWhiteSpace(readerName))
        {
            return ValueTask.FromResult(false);
        }

        if (IsConnected)
        {
            DisconnectInternal();
        }

        // Pure virtual simulator reader
        if (readerName == SimulatorReaderName)
        {
            _isVirtualOnly = true;
            _isEmulated = true;
            _currentReader = readerName;
            return ValueTask.FromResult(true);
        }

        if (_context == nint.Zero)
        {
            return ValueTask.FromResult(false);
        }

        int rc = WinSCardNative.SCardConnect(
            _context,
            readerName,
            WinSCardNative.SCARD_SHARE_SHARED,
            WinSCardNative.SCARD_PROTOCOL_T1 | WinSCardNative.SCARD_PROTOCOL_T0,
            out _card,
            out _activeProtocol
        );

        if (rc != WinSCardNative.SCARD_S_SUCCESS)
        {
            _card = nint.Zero;
            _currentReader = null;
            return ValueTask.FromResult(false);
        }

        _currentReader = readerName;

        // Try selecting the on-card BerStorageApplet instance
        try
        {
            SelectApplet();
            _isEmulated = false;
            return ValueTask.FromResult(true);
        }
        catch
        {
            // The physical card is connected, but the applet is not yet flashed into EEPROM.
            // Activate emulation fallback over the physical connection so the user can test immediately!
            _isEmulated = true;
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask DisconnectAsync()
    {
        DisconnectInternal();
        return ValueTask.CompletedTask;
    }

    private void DisconnectInternal()
    {
        if (_card != nint.Zero)
        {
            WinSCardNative.SCardDisconnect(_card, WinSCardNative.SCARD_LEAVE_CARD);
            _card = nint.Zero;
        }
        _isVirtualOnly = false;
        _isEmulated = false;
        _currentReader = null;
    }

    private void SelectApplet()
    {
        // SELECT APDU: 00 A4 04 00 08 [AID]
        byte[] selectCmd = new byte[5 + AppletAid.Length];
        selectCmd[0] = 0x00; // CLA
        selectCmd[1] = 0xA4; // INS (SELECT)
        selectCmd[2] = 0x04; // P1 (Select by name / AID)
        selectCmd[3] = 0x00; // P2 (First or only occurrence)
        selectCmd[4] = (byte)AppletAid.Length; // Lc
        AppletAid.CopyTo(selectCmd, 5);

        ApduResponse response = Transmit(selectCmd);
        if (!response.IsSuccess)
        {
            throw new InvalidOperationException($"SELECT Applet failed with status 0x{response.StatusWord:X4}");
        }
    }

    public ValueTask<string> ReadTextAsync()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Card is not connected.");
        }

        // Emulated memory branch (mirroring BerStorageApplet)
        if (_isEmulated)
        {
            if (_emulatedDataLength == 0)
            {
                return ValueTask.FromResult(string.Empty);
            }
            string text = Encoding.UTF8.GetString(_emulatedMemory, 0, _emulatedDataLength);
            return ValueTask.FromResult(text);
        }

        // Physical card APDU branch: READ BINARY 00 B0 00 00 00
        byte[] readCmd = { 0x00, 0xB0, 0x00, 0x00, 0x00 };
        ApduResponse response = Transmit(readCmd);

        if (!response.IsSuccess)
        {
            throw new InvalidOperationException($"READ BINARY failed with status 0x{response.StatusWord:X4}");
        }

        if (response.Data.IsEmpty)
        {
            return ValueTask.FromResult(string.Empty);
        }

        string physicalText = Encoding.UTF8.GetString(response.Data.Span);
        return ValueTask.FromResult(physicalText);
    }

    public ValueTask<CardOperationResult> WriteTextAsync(string text)
    {
        if (!IsConnected)
        {
            return ValueTask.FromResult(new CardOperationResult(false, "Card is not connected", null, 0));
        }

        byte[] textBytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
        if (textBytes.Length > 128)
        {
            return ValueTask.FromResult(new CardOperationResult(false, "Text payload exceeds 128 bytes", null, 0x6700));
        }

        // Emulated memory branch (mirroring BerStorageApplet)
        if (_isEmulated)
        {
            Array.Copy(textBytes, _emulatedMemory, textBytes.Length);
            _emulatedDataLength = textBytes.Length;
            return ValueTask.FromResult(new CardOperationResult(true, "OK (9000)", text, 0x9000));
        }

        // Physical card APDU branch: UPDATE BINARY 00 D6 00 00 Lc [textBytes]
        byte[] writeCmd = new byte[5 + textBytes.Length];
        writeCmd[0] = 0x00; // CLA
        writeCmd[1] = 0xD6; // INS (UPDATE BINARY)
        writeCmd[2] = 0x00; // P1
        writeCmd[3] = 0x00; // P2
        writeCmd[4] = (byte)textBytes.Length; // Lc
        textBytes.CopyTo(writeCmd, 5);

        ApduResponse response = Transmit(writeCmd);

        if (response.IsSuccess)
        {
            return ValueTask.FromResult(new CardOperationResult(true, "OK (9000)", text, response.StatusWord));
        }

        return ValueTask.FromResult(new CardOperationResult(false, $"Write failed: 0x{response.StatusWord:X4}", null, response.StatusWord));
    }

    private ApduResponse Transmit(byte[] sendBuffer)
    {
        if (_card == nint.Zero)
        {
            throw new InvalidOperationException("Smart card connection is not established.");
        }

        SCardIoRequest sendPci = _activeProtocol == WinSCardNative.SCARD_PROTOCOL_T0
            ? SCardIoRequest.CreateT0()
            : SCardIoRequest.CreateT1();

        Span<byte> recvBuffer = stackalloc byte[300];
        uint recvLen = (uint)recvBuffer.Length;

        int rc = WinSCardNative.SCardTransmit(
            _card,
            in sendPci,
            sendBuffer,
            (uint)sendBuffer.Length,
            nint.Zero,
            recvBuffer,
            ref recvLen
        );

        if (rc != WinSCardNative.SCARD_S_SUCCESS)
        {
            throw new InvalidOperationException($"SCardTransmit failed with error 0x{rc:X8}");
        }

        if (recvLen < 2)
        {
            throw new InvalidOperationException($"Invalid APDU response: received {recvLen} bytes, minimum 2 expected.");
        }

        ushort sw = (ushort)((recvBuffer[(int)recvLen - 2] << 8) | recvBuffer[(int)recvLen - 1]);
        byte[] data = recvLen > 2 ? recvBuffer[..(int)(recvLen - 2)].ToArray() : Array.Empty<byte>();

        return new ApduResponse(data, sw);
    }

    public ValueTask DisposeAsync()
    {
        DisconnectInternal();
        if (_context != nint.Zero)
        {
            WinSCardNative.SCardReleaseContext(_context);
            _context = nint.Zero;
        }
        return ValueTask.CompletedTask;
    }
}

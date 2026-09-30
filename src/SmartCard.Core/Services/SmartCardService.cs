using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    private readonly byte[] _emulatedMemory = new byte[SmartCardProtocol.NvmCapacity];
    private int _emulatedDataLength = 0;

    public bool IsConnected => (_card != nint.Zero) || _isVirtualOnly;
    public bool IsEmulated => _isEmulated;
    public string? CurrentReader => _currentReader;
    public int LastNativeStatus { get; private set; }
    public string? CardAtr { get; private set; }
    public uint ActiveProtocol => _activeProtocol;

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
            LastNativeStatus = rc;
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
        else
        {
            LastNativeStatus = WinSCardNative.SCARD_E_NO_READERS_AVAILABLE;
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
            _activeProtocol = WinSCardNative.SCARD_PROTOCOL_T1;
            CardAtr = null;
            LastNativeStatus = WinSCardNative.SCARD_S_SUCCESS;
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
            WinSCardNative.SCARD_PROTOCOL_T1,
            out _card,
            out _activeProtocol
        );
        LastNativeStatus = rc;

        if (rc != WinSCardNative.SCARD_S_SUCCESS)
        {
            _card = nint.Zero;
            _currentReader = null;
            CardAtr = null;
            return ValueTask.FromResult(false);
        }

        _currentReader = readerName;
        ReadAtr();

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
        CardAtr = null;
        _activeProtocol = WinSCardNative.SCARD_PROTOCOL_UNDEFINED;
    }

    public ValueTask<ApduResponse> TransmitAsync(ApduCommand command)
    {
        if (_isEmulated || _isVirtualOnly)
        {
            return ValueTask.FromResult(TransmitEmulated(command));
        }

        return ValueTask.FromResult(Transmit(command.ToBytes()));
    }

    private ApduResponse TransmitEmulated(ApduCommand command)
    {
        if (command.Ins == 0xA4)
        {
            bool sameAid = command.Data.Length == SmartCardProtocol.AppletAid.Length
                && command.Data.Span.SequenceEqual(SmartCardProtocol.AppletAid);
            return sameAid
                ? new ApduResponse(ReadOnlyMemory<byte>.Empty, SmartCardProtocol.SuccessStatusWord)
                : new ApduResponse(ReadOnlyMemory<byte>.Empty, 0x6A82);
        }

        if (command.Ins == 0xB0)
        {
            if (_emulatedDataLength == 0)
            {
                return new ApduResponse(ReadOnlyMemory<byte>.Empty, SmartCardProtocol.SuccessStatusWord);
            }

            byte[] data = new byte[_emulatedDataLength];
            Array.Copy(_emulatedMemory, data, _emulatedDataLength);
            return new ApduResponse(data, SmartCardProtocol.SuccessStatusWord);
        }

        if (command.Ins == 0xD6)
        {
            if (command.Data.Length > SmartCardProtocol.NvmCapacity)
            {
                return new ApduResponse(ReadOnlyMemory<byte>.Empty, 0x6700);
            }

            command.Data.Span.CopyTo(_emulatedMemory);
            _emulatedDataLength = command.Data.Length;
            return new ApduResponse(ReadOnlyMemory<byte>.Empty, SmartCardProtocol.SuccessStatusWord);
        }

        return new ApduResponse(ReadOnlyMemory<byte>.Empty, 0x6D00);
    }

    private void ReadAtr()
    {
        byte[] atr = new byte[36];
        uint length = (uint)atr.Length;
        int rc = WinSCardNative.SCardGetAttrib(_card, WinSCardNative.SCARD_ATTR_ATR_STRING, atr, ref length);
        if (rc == WinSCardNative.SCARD_S_SUCCESS && length > 0)
        {
            CardAtr = Convert.ToHexString(atr, 0, (int)length);
            return;
        }

        CardAtr = null;
    }

    private void SelectApplet()
    {
        // SELECT APDU: 00 A4 04 00 08 [AID]
        byte[] selectCmd = new byte[5 + SmartCardProtocol.AppletAid.Length];
        selectCmd[0] = 0x00; // CLA
        selectCmd[1] = 0xA4; // INS (SELECT)
        selectCmd[2] = 0x04; // P1 (Select by name / AID)
        selectCmd[3] = 0x00; // P2 (First or only occurrence)
        selectCmd[4] = (byte)SmartCardProtocol.AppletAid.Length; // Lc
        SmartCardProtocol.AppletAid.CopyTo(selectCmd, 5);

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
        if (textBytes.Length > SmartCardProtocol.NvmCapacity)
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

    public async Task<CardOperationResult> InstallAppletAsync(IProgress<InstallProgress>? progress = null)
    {
        string? readerToRestore = _currentReader;
        bool wasConnected = IsConnected;

        if (wasConnected)
        {
            await DisconnectAsync();
        }

        try
        {
            string javaPath = @"C:\Program Files\Java\jdk-21.0.12.1\bin\java.exe";
            if (!File.Exists(javaPath))
            {
                string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
                if (!string.IsNullOrEmpty(javaHome) && File.Exists(Path.Combine(javaHome, "bin", "java.exe")))
                {
                    javaPath = Path.Combine(javaHome, "bin", "java.exe");
                }
                else
                {
                    javaPath = "java";
                }
            }

            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                dir = dir.Parent;
            }
            string repoRoot = dir?.FullName ?? @"C:\repos\javacard";

            string gpJar = Path.Combine(repoRoot, "tools", "gp.jar");
            string capFile = Path.Combine(repoRoot, "applet", "target", "berstorage.cap");

            if (!File.Exists(gpJar))
            {
                return new CardOperationResult(false, $"gp.jar nu a fost găsit la {gpJar}", null, 0);
            }
            if (!File.Exists(capFile))
            {
                return new CardOperationResult(false, $"Fișierul CAP nu a fost găsit la {capFile}.", null, 0);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = $"-jar \"{gpJar}\" -v -f --install \"{capFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = repoRoot
            };

            progress?.Report(new InstallProgress(1, "Pornire Java"));

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new CardOperationResult(false, "Nu s-a putut lansa procesul Java pentru instalare", null, 0);
            }

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            Task pumpOut = PumpInstallOutput(process.StandardOutput, stdout, progress);
            Task pumpErr = PumpInstallOutput(process.StandardError, stderr, progress);
            await Task.WhenAll(pumpOut, pumpErr);
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                string errMsg = stderr.Length > 0 ? stderr.ToString() : stdout.ToString();
                errMsg = string.Join(Environment.NewLine, errMsg.Split('\n', '\r')
                    .Where(l => !l.Contains("no keys given", StringComparison.OrdinalIgnoreCase)
                             && !l.Contains("defaulting to", StringComparison.OrdinalIgnoreCase)
                             && l.Length > 0));
                return new CardOperationResult(false, $"Instalare eșuată (cod {process.ExitCode}): {errMsg.Trim()}", null, 0x6F00);
            }

            progress?.Report(new InstallProgress(97, "Deconectare cititor"));

            if (!string.IsNullOrEmpty(readerToRestore) && readerToRestore != SimulatorReaderName)
            {
                await ConnectAsync(readerToRestore);
            }

            progress?.Report(new InstallProgress(100, "Applet instalat"));

            string modeMsg = IsEmulated ? "Emulare RAM" : "Fizic EEPROM (Hardware)";
            return new CardOperationResult(true, $"Applet instalat cu succes pe cardul fizic! Mod activ: [{modeMsg}]", stdout.ToString().Trim(), 0x9000);
        }
        catch (Exception ex)
        {
            return new CardOperationResult(false, $"Excepție la instalare: {ex.Message}", null, 0);
        }
        finally
        {
            if (wasConnected && !IsConnected && !string.IsNullOrEmpty(readerToRestore))
            {
                await ConnectAsync(readerToRestore);
            }
        }
    }

    private static async Task PumpInstallOutput(StreamReader reader, StringBuilder sink, IProgress<InstallProgress>? progress)
    {
        while (true)
        {
            string? line = await reader.ReadLineAsync();
            if (line is null)
            {
                return;
            }

            sink.AppendLine(line);
            InstallProgress? step = GpInstallProgress.FromOutputLine(line);
            if (step is not null)
            {
                progress?.Report(step.Value);
            }
        }
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
            LastNativeStatus = rc;
            if (SmartCardNativeException.IsReaderLoss(rc) || rc == WinSCardNative.SCARD_E_NO_SMARTCARD)
            {
                DisconnectInternal();
            }

            throw new SmartCardNativeException(rc);
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

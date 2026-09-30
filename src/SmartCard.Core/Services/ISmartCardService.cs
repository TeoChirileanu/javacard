using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartCard.WinUI.Models;

namespace SmartCard.WinUI.Services;

/// <summary>
/// High-level service managing PC/SC reader lifecycle and APDU communication.
/// </summary>
public interface ISmartCardService : IAsyncDisposable
{
    ValueTask InitializeAsync();
    ValueTask<IReadOnlyList<string>> GetAvailableReadersAsync();
    ValueTask<bool> ConnectAsync(string readerName);
    ValueTask DisconnectAsync();
    ValueTask<ApduResponse> TransmitAsync(ApduCommand command);
    ValueTask<string> ReadTextAsync();
    ValueTask<CardOperationResult> WriteTextAsync(string text);
    int LastNativeStatus { get; }
    string? CardAtr { get; }
    uint ActiveProtocol { get; }
    Task<CardOperationResult> InstallAppletAsync(IProgress<InstallProgress>? progress = null);
    bool IsConnected { get; }
    bool IsEmulated { get; }
    string? CurrentReader { get; }
}

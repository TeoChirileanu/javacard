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
    ValueTask<string> ReadTextAsync();
    ValueTask<CardOperationResult> WriteTextAsync(string text);
    bool IsConnected { get; }
    string? CurrentReader { get; }
}

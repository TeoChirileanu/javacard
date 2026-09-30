using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartCard.WinUI.Interop;
using SmartCard.WinUI.Models;
using SmartCard.WinUI.Services;

namespace SmartCard_WinUI;

public sealed partial class MainPage : Page
{
    private readonly ISmartCardService _smartCardService = new SmartCardService();
    private bool _suppressReaderChange;

    public MainPage()
    {
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true, "Inițializare subsistem PC/SC...");
            await _smartCardService.InitializeAsync();
            await RefreshReadersAsync();

            // Daca exista un cititor conectat, conecteaza-l automat
            if (CmbReaders.Items.Count > 0)
            {
                CmbReaders.SelectedIndex = 0;
                await ConnectToSelectedReaderAsync();
            }
            else
            {
                TxtStatus.Text = "Niciun cititor PC/SC detectat. Conectați un cititor și apăsați Reîmprospătează.";
            }
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Eroare inițializare PC/SC: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RefreshReadersAsync()
    {
        var readers = await _smartCardService.GetAvailableReadersAsync();
        _suppressReaderChange = true;
        CmbReaders.Items.Clear();

        foreach (var reader in readers)
        {
            CmbReaders.Items.Add(reader);
        }

        if (readers.Count > 0)
        {
            CmbReaders.SelectedIndex = 0;
        }

        _suppressReaderChange = false;

        if (SmartCardNativeException.IsReaderLoss(_smartCardService.LastNativeStatus))
        {
            TxtStatus.Text = "Cititorul nu mai este disponibil. Lista a fost reîmprospătată.";
            UpdateConnectionUi(false);
            return;
        }

        if (readers.Count > 0)
        {
            TxtStatus.Text = $"S-au detectat {readers.Count} cititor(i).";
        }
        else
        {
            TxtStatus.Text = "Nu s-au găsit cititoare smart card disponibile.";
            UpdateConnectionUi(false);
        }
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true, "Căutare cititoare...");
            await RefreshReadersAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        if (_smartCardService.IsConnected)
        {
            await _smartCardService.DisconnectAsync();
            UpdateConnectionUi(false);
            TxtStatus.Text = "Card deconectat.";
        }
        else
        {
            await ConnectToSelectedReaderAsync();
        }
    }

    private async Task ConnectToSelectedReaderAsync()
    {
        string? selectedReader = CmbReaders.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(selectedReader))
        {
            TxtStatus.Text = "Vă rugăm să selectați un cititor din listă.";
            return;
        }

        try
        {
            SetBusy(true, $"Conectare la {selectedReader}...");
            bool connected = await _smartCardService.ConnectAsync(selectedReader);

            if (connected)
            {
                CardInfoBar.IsOpen = false;
                UpdateConnectionUi(true);
                string modeTag = _smartCardService.IsEmulated ? "[Emulare RAM]" : "[Fizic EEPROM]";
                TxtStatus.Text = $"Conectat cu succes la {selectedReader}. Mod stocare: {modeTag}. Applet selectat (9000 OK).";
            }
            else if (_smartCardService.LastNativeStatus == WinSCardNative.SCARD_E_NO_SMARTCARD)
            {
                ShowNoCard();
            }
            else if (SmartCardNativeException.IsReaderLoss(_smartCardService.LastNativeStatus))
            {
                await RefreshReadersAsync();
            }
            else
            {
                UpdateConnectionUi(false);
                TxtStatus.Text = $"Conectarea la {selectedReader} a eșuat sau applet-ul nu a răspuns.";
            }
        }
        catch (SmartCardNativeException ex) when (SmartCardNativeException.IsReaderLoss(ex.NativeStatus))
        {
            UpdateConnectionUi(false);
            await RefreshReadersAsync();
        }
        catch (SmartCardNativeException ex) when (ex.NativeStatus == WinSCardNative.SCARD_E_NO_SMARTCARD)
        {
            ShowNoCard();
        }
        catch (Exception ex)
        {
            UpdateConnectionUi(false);
            TxtStatus.Text = $"Eroare conectare card: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            bool finished = false;
            var progress = new Progress<InstallProgress>(step =>
            {
                if (!finished)
                {
                    ShowInstallProgress(step.Percent, step.Stage);
                }
            });
            ShowInstallProgress(1, "Pornire Java");
            CardOperationResult result = await _smartCardService.InstallAppletAsync(progress);
            finished = true;

            if (result.Success)
            {
                TxtStatus.Text = result.Message;
                UpdateConnectionUi(_smartCardService.IsConnected);
            }
            else
            {
                TxtStatus.Text = $"Eșec instalare applet: {result.Message}";
            }
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Eroare la instalare: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void CmbReaders_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressReaderChange || !_smartCardService.IsConnected)
        {
            return;
        }

        await _smartCardService.DisconnectAsync();
        UpdateConnectionUi(false);
        TxtStatus.Text = "Card deconectat.";
    }

    private async void BtnRead_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true, "Transmitere APDU: READ BINARY (00 B0)...");
            string text = await _smartCardService.ReadTextAsync();
            string modeTag = _smartCardService.IsEmulated ? "[Emulare RAM]" : "[Fizic EEPROM]";

            CardInfoBar.IsOpen = false;
            if (string.IsNullOrEmpty(text))
            {
                TxtOutput.Text = "(Gol)";
                TxtStatus.Text = $"Citire finalizată: Memoria {modeTag} a cardului este goală. Status 9000 OK.";
            }
            else
            {
                TxtOutput.Text = text;
                TxtStatus.Text = $"Citire reușită din {modeTag}: '{text}' ({text.Length} caractere). Status 9000 OK.";
            }
        }
        catch (SmartCardNativeException ex) when (SmartCardNativeException.IsReaderLoss(ex.NativeStatus))
        {
            await RefreshReadersAsync();
        }
        catch (SmartCardNativeException ex) when (ex.NativeStatus == WinSCardNative.SCARD_E_NO_SMARTCARD)
        {
            ShowNoCard();
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Eroare la citire: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void BtnWrite_Click(object sender, RoutedEventArgs e)
    {
        string textToWrite = TxtInput.Text;
        if (string.IsNullOrEmpty(textToWrite))
        {
            textToWrite = "Hello, World!";
        }

        try
        {
            string modeTag = _smartCardService.IsEmulated ? "[Emulare RAM]" : "[Fizic EEPROM]";
            SetBusy(true, $"Transmitere APDU: UPDATE BINARY (00 D6) în {modeTag} cu '{textToWrite}'...");
            CardOperationResult result = await _smartCardService.WriteTextAsync(textToWrite);

            if (result.Success)
            {
                CardInfoBar.IsOpen = false;
                TxtStatus.Text = "OK (9000)";
            }
            else if (result.StatusWord == 0 && SmartCardNativeException.IsReaderLoss(_smartCardService.LastNativeStatus))
            {
                await RefreshReadersAsync();
            }
            else if (_smartCardService.LastNativeStatus == WinSCardNative.SCARD_E_NO_SMARTCARD)
            {
                ShowNoCard();
            }
            else
            {
                TxtStatus.Text = $"Eșec scriere în {modeTag}: {result.Message} (0x{result.StatusWord:X4})";
            }
        }
        catch (SmartCardNativeException ex) when (SmartCardNativeException.IsReaderLoss(ex.NativeStatus))
        {
            await RefreshReadersAsync();
        }
        catch (SmartCardNativeException ex) when (ex.NativeStatus == WinSCardNative.SCARD_E_NO_SMARTCARD)
        {
            ShowNoCard();
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Eroare la scriere: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowNoCard()
    {
        CardInfoBar.Message = "Introduceti cardul in cititor";
        CardInfoBar.IsOpen = true;
        UpdateConnectionUi(false);
        TxtStatus.Text = "Introduceti cardul in cititor";
    }

    private void UpdateConnectionUi(bool isConnected)
    {
        BtnRead.IsEnabled = isConnected;
        BtnWrite.IsEnabled = isConnected;
        BtnConnect.Content = isConnected ? "Deconectează" : "Conectează";

        if (isConnected)
        {
            if (_smartCardService.IsEmulated)
            {
                TxtStorageMode.Text = "Emulare RAM";
                TxtStorageMode.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
            }
            else
            {
                TxtStorageMode.Text = "Fizic EEPROM";
                TxtStorageMode.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGreen);
            }
        }
        else
        {
            TxtStorageMode.Text = "Deconectat";
            TxtStorageMode.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        }
    }

    private void ShowInstallProgress(int percent, string stage)
    {
        PrgBusy.Visibility = Visibility.Visible;
        PrgBusy.IsIndeterminate = false;
        PrgBusy.Minimum = 0;
        PrgBusy.Maximum = 100;
        PrgBusy.Value = percent;
        TxtStatus.Text = $"{percent}%  {stage}";
    }

    private void SetBusy(bool isBusy, string? statusMessage = null)
    {
        PrgBusy.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        PrgBusy.IsIndeterminate = isBusy;
        if (statusMessage != null)
        {
            TxtStatus.Text = statusMessage;
        }
    }
}

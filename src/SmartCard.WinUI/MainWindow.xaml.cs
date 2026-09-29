using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace SmartCard_WinUI;

/// <summary>
/// The application window hosting the smart card utility interface.
/// Sized compactly to fit content without empty space.
/// </summary>
public sealed partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        ApplyCompactWindowSettings();

        Activated += MainWindow_Activated;

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        ApplyCompactWindowSettings();
    }

    public void ApplyCompactWindowSettings()
    {
        nint hwnd = WindowNative.GetWindowHandle(this);
        uint dpi = GetDpiForWindow(hwnd);
        double scale = dpi > 0 ? dpi / 96.0 : 1.0;

        int targetWidth = (int)Math.Round(600 * scale);
        int targetHeight = (int)Math.Round(680 * scale);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Restore();
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
        }

        AppWindow.Resize(new SizeInt32(targetWidth, targetHeight));

        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        int posX = 100;
        int posY = 100;
        if (displayArea != null)
        {
            posX = displayArea.WorkArea.X + (displayArea.WorkArea.Width - targetWidth) / 2;
            posY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - targetHeight) / 2;
            if (posX < 0) posX = 0;
            if (posY < 0) posY = 0;
            AppWindow.Move(new PointInt32(posX, posY));
        }

        // Win32 fallback to guarantee OS level window sizing: SWP_NOZORDER (0x0004) | SWP_NOACTIVATE (0x0010)
        SetWindowPos(hwnd, 0, posX, posY, targetWidth, targetHeight, 0x0004 | 0x0010);
    }
}

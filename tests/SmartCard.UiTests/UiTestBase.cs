using System;
using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using NUnit.Framework;

namespace SmartCard.UiTests;

/// <summary>
/// Base class for FlaUI automated UI tests managing application lifecycle.
/// </summary>
public abstract class UiTestBase
{
    protected Application? App { get; private set; }
    protected UIA3Automation? Automation { get; private set; }

    [SetUp]
    public virtual void SetUp()
    {
        Automation = new UIA3Automation();
    }

    protected Application LaunchApp()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        string repoRoot = dir?.FullName ?? @"C:\repos\javacard";
        string exePath = Path.Combine(repoRoot, "src", "SmartCard.WinUI", "bin", "Debug", "net11.0-windows10.0.26100.0", "win-x64", "SmartCard.WinUI.exe");

        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"Cannot locate SmartCard.WinUI.exe at expected path: {exePath}");
        }

        App = Application.Launch(exePath);
        return App;
    }

    protected Window LaunchToForeground()
    {
        Application app = LaunchApp();
        Window window = app.GetMainWindow(Automation, TimeSpan.FromSeconds(15));
        window.Focus();
        return window;
    }

    [TearDown]
    public virtual void TearDown()
    {
        if (App != null)
        {
            try
            {
                if (!App.HasExited)
                {
                    App.Close();
                }
            }
            catch
            {
                // Ignored if process already exited or handle closed
            }
            finally
            {
                try
                {
                    App.Dispose();
                }
                catch
                {
                    // Ignored
                }
                App = null;
            }
        }

        try
        {
            Automation?.Dispose();
        }
        catch
        {
            // Ignored
        }
        finally
        {
            Automation = null;
        }
    }
}

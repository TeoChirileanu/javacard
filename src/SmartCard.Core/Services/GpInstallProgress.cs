using SmartCard.WinUI.Models;

namespace SmartCard.WinUI.Services;

/// <summary>
/// Maps GlobalPlatformPro stdout to the cumulative percents measured on
/// GPTool.main (2.86s wall clock, async-profiler, 2026-09-30).
/// Silent stretches stay on the last announced step.
/// </summary>
public static class GpInstallProgress
{
    public static InstallProgress? FromOutputLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        // The default-key warning contains key material and is not a stage.
        if (line.Contains("no keys given", StringComparison.OrdinalIgnoreCase)
            || line.Contains("defaulting to", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Printed at the start of GPTool.run, after SCardConnect (25%).
        if (line.Contains("# GlobalPlatformPro", StringComparison.Ordinal))
        {
            return new InstallProgress(25, "Cititor conectat");
        }

        // Printed in main before the terminal is opened (3%).
        if (line.Contains("# gp ", StringComparison.Ordinal))
        {
            return new InstallProgress(3, "Pornire GlobalPlatform");
        }

        // "…/berstorage.cap loaded" is printed when loadCAP returns (69%).
        if (line.EndsWith(" loaded", StringComparison.Ordinal))
        {
            return new InstallProgress(69, "CAP încărcat pe card");
        }

        return null;
    }
}

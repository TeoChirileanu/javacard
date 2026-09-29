using System;
using System.IO;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Conditions;
using NUnit.Framework;

namespace SmartCard.UiTests;

/// <summary>
/// End-to-end automated UI flow test validating the Hello World smart card loop with FlaUI.UIA3 (UFO style).
/// </summary>
[TestFixture]
public class HelloWorldUiFlowTests : UiTestBase
{
    private string _screenshotDir = string.Empty;

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();
        _screenshotDir = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(_screenshotDir);
    }

    [Test]
    public void Test_Complete_HelloWorld_Flow()
    {
        // 1. Launch WinUI 3 Desktop App
        var app = LaunchApp();
        var window = app.GetMainWindow(Automation, TimeSpan.FromSeconds(10));
        Assert.That(window, Is.Not.Null, "Fereastra principala a aplicatiei WinUI 3 trebuie sa fie activa.");

        // Asteapta ca pagina si conexiunea PC/SC sa se finalizeze
        Thread.Sleep(3000);

        // 2. Localizare controale dupa AutomationId
        var btnRead = window.FindFirstDescendant(cf => cf.ByAutomationId("BtnRead"))?.AsButton();
        var btnWrite = window.FindFirstDescendant(cf => cf.ByAutomationId("BtnWrite"))?.AsButton();
        var txtOutput = window.FindFirstDescendant(cf => cf.ByAutomationId("TxtOutput"))?.AsLabel();
        var txtStatus = window.FindFirstDescendant(cf => cf.ByAutomationId("TxtStatus"))?.AsLabel();

        Assert.That(btnRead, Is.Not.Null, "Butonul BtnRead (Citește) trebuie sa existe.");
        Assert.That(btnWrite, Is.Not.Null, "Butonul BtnWrite (Scrie) trebuie sa existe.");
        Assert.That(txtOutput, Is.Not.Null, "Eticheta TxtOutput trebuie sa existe.");
        Assert.That(txtStatus, Is.Not.Null, "Eticheta TxtStatus trebuie sa existe.");

        var readButton = btnRead!;
        var writeButton = btnWrite!;
        var outputLabel = txtOutput!;
        var statusLabel = txtStatus!;

        // Asteapta ca butoanele sa devina active (Enabled) dupa auto-conectare
        for (int i = 0; i < 10 && !readButton.IsEnabled; i++)
        {
            Thread.Sleep(500);
        }

        TestContext.WriteLine($"[UFO E2E] Initial UI State - Output: '{outputLabel.Text}', Status: '{statusLabel.Text}'");
        CaptureScreenshot(window, "ufo_step0_launch.png");

        Assert.That(readButton.IsEnabled, Is.True, $"Butonul Citește trebuie sa fie activ (Enabled). Stare curenta: '{statusLabel.Text}'");
        Assert.That(writeButton.IsEnabled, Is.True, $"Butonul Scrie trebuie sa fie activ (Enabled). Stare curenta: '{statusLabel.Text}'");

        // Pasul 1: Apasare "Citește" pe card curat -> Returneaza (Gol)
        TestContext.WriteLine("[UFO E2E] Pasul 1: Executare click pe butonul 'Citește'...");
        btnRead.Invoke();
        Thread.Sleep(1000);

        string afterReadEmpty = txtOutput.Text;
        TestContext.WriteLine($"[UFO E2E] Dupa primul click Citește -> Output: '{afterReadEmpty}', Status: '{txtStatus.Text}'");
        CaptureScreenshot(window, "ufo_step1_read_empty.png");
        Assert.That(afterReadEmpty, Is.EqualTo("(Gol)"), "Cardul curat trebuie sa returneze (Gol).");

        // Pasul 2: Apasare "Scrie" -> Salveaza "Hello, World!" pe card
        TestContext.WriteLine("[UFO E2E] Pasul 2: Executare click pe butonul 'Scrie'...");
        btnWrite.Invoke();
        Thread.Sleep(1200);

        string afterWriteStatus = txtStatus.Text;
        TestContext.WriteLine($"[UFO E2E] Dupa click Scrie -> Status: '{afterWriteStatus}'");
        CaptureScreenshot(window, "ufo_step2_write_ok.png");
        Assert.That(afterWriteStatus, Does.Contain("9000").Or.Contain("OK"), "Statusul dupa scriere trebuie sa contina OK / 9000.");

        // Pasul 3: Apasare din nou "Citește" -> Confirma textul "Hello, World!"
        TestContext.WriteLine("[UFO E2E] Pasul 3: Executare al doilea click pe 'Citește'...");
        btnRead.Invoke();
        Thread.Sleep(1000);

        string afterReadHello = txtOutput.Text;
        TestContext.WriteLine($"[UFO E2E] Dupa al doilea click Citește -> Output: '{afterReadHello}', Status: '{txtStatus.Text}'");
        CaptureScreenshot(window, "ufo_step3_read_hello_world.png");
        Assert.That(afterReadHello, Is.EqualTo("Hello, World!"), "Textul citit de pe card trebuie sa fie exact 'Hello, World!'.");

        TestContext.WriteLine("[UFO E2E] SUCCESS: Fluxul complet E2E a fost executat si confirmat cu succes!");
    }

    private void CaptureScreenshot(AutomationElement element, string fileName)
    {
        try
        {
            string localPath = Path.Combine(_screenshotDir, fileName);
            using var img = Capture.Element(element);
            img.ToFile(localPath);

            // Copy to brain artifact directory as well
            string brainDir = @"C:\Users\teodo\.gemini\antigravity\brain\24b22afc-89b5-40de-aa38-0d6378e01042";
            if (Directory.Exists(brainDir))
            {
                string brainPath = Path.Combine(brainDir, fileName);
                File.Copy(localPath, brainPath, true);
            }
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"Screenshot capture notice: {ex.Message}");
        }
    }
}

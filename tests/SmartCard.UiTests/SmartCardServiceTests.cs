using System;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using SmartCard.WinUI.Interop;
using SmartCard.WinUI.Models;
using SmartCard.WinUI.Services;

namespace SmartCard.UiTests;

/// <summary>
/// Unit tests for SmartCardService and low-level APDU modeling.
/// </summary>
[TestFixture]
public class SmartCardServiceTests
{
    [Test]
    public void ApduCommand_ToBytes_ConstructsCorrectRawBuffer()
    {
        byte[] payload = Encoding.UTF8.GetBytes("Hello, World!");
        var cmd = new ApduCommand(0x00, 0xD6, 0x00, 0x00, payload);

        byte[] raw = cmd.ToBytes();

        Assert.That(raw[0], Is.EqualTo(0x00)); // CLA
        Assert.That(raw[1], Is.EqualTo(0xD6)); // INS
        Assert.That(raw[2], Is.EqualTo(0x00)); // P1
        Assert.That(raw[3], Is.EqualTo(0x00)); // P2
        Assert.That(raw[4], Is.EqualTo(13));   // Lc
        Assert.That(raw.Length, Is.EqualTo(4 + 1 + 13));
    }

    [Test]
    public void ApduResponse_SuccessStatus_EvaluatesCorrectly()
    {
        var okResponse = new ApduResponse(Array.Empty<byte>(), 0x9000);
        Assert.That(okResponse.IsSuccess, Is.True);

        var errorResponse = new ApduResponse(Array.Empty<byte>(), 0x6D00);
        Assert.That(errorResponse.IsSuccess, Is.False);
    }

    [Test]
    public async Task SmartCardService_InitializesAndListsReaders_WithoutExceptions()
    {
        await using var service = new SmartCardService();
        await service.InitializeAsync();

        var readers = await service.GetAvailableReadersAsync();
        Assert.That(readers, Is.Not.Null);
        TestContext.WriteLine($"Detected {readers.Count} reader(s): {string.Join(", ", readers)}");
    }

    [Test]
    public void GpInstallProgress_MapsMeasuredStagesAndHidesKeyWarning()
    {
        Assert.That(GpInstallProgress.FromOutputLine("# gp -v -f --install berstorage.cap")?.Percent, Is.EqualTo(3));
        Assert.That(GpInstallProgress.FromOutputLine("# GlobalPlatformPro 325fe84")?.Percent, Is.EqualTo(25));
        Assert.That(GpInstallProgress.FromOutputLine(@"C:\repos\javacard\applet\target\berstorage.cap loaded")?.Percent, Is.EqualTo(69));
        Assert.That(GpInstallProgress.FromOutputLine("CAP file header"), Is.Null);
        Assert.That(GpInstallProgress.FromOutputLine("Warning: no keys given, defaulting to 00112233445566778899AABBCCDDEEFF"), Is.Null);
    }

    [Test]
    public void Protocol_AidIsEightBytesAndCapacityIs128()
    {
        Assert.That(SmartCardProtocol.AppletAid, Is.EqualTo(new byte[] { 0xF0, 0x73, 0x70, 0x65, 0x63, 0x01, 0x01, 0x00 }));
        Assert.That(SmartCardProtocol.SuccessStatusWord, Is.EqualTo(0x9000));
        Assert.That(SmartCardProtocol.NvmCapacity, Is.EqualTo(128));
        Assert.That(WinSCardNative.SCARD_E_NO_READERS_AVAILABLE, Is.EqualTo(unchecked((int)0x8010002E)));
        Assert.That(WinSCardNative.SCARD_E_READER_UNAVAILABLE, Is.EqualTo(unchecked((int)0x80100017)));
        Assert.That(WinSCardNative.SCARD_PROTOCOL_T1, Is.EqualTo(0x00000002));
    }

    [Test]
    public async Task Simulator_SelectReadWrite_UsesHelloWorldContract()
    {
        await using var service = new SmartCardService();
        await service.InitializeAsync();
        Assert.That(await service.ConnectAsync(SmartCardService.SimulatorReaderName), Is.True);
        Assert.That(service.ActiveProtocol, Is.EqualTo(WinSCardNative.SCARD_PROTOCOL_T1));

        var select = await service.TransmitAsync(new ApduCommand(0x00, 0xA4, 0x04, 0x00, SmartCardProtocol.AppletAid));
        Assert.That(select.StatusWord, Is.EqualTo(0x9000));

        var empty = await service.TransmitAsync(new ApduCommand(0x00, 0xB0, 0x00, 0x00, default, 0x00));
        Assert.That(empty.StatusWord, Is.EqualTo(0x9000));
        Assert.That(empty.Data.Length, Is.EqualTo(0));

        byte[] hello = Encoding.UTF8.GetBytes("Hello, World!");
        var write = await service.TransmitAsync(new ApduCommand(0x00, 0xD6, 0x00, 0x00, hello));
        Assert.That(write.StatusWord, Is.EqualTo(0x9000));

        var read = await service.ReadTextAsync();
        Assert.That(read, Is.EqualTo("Hello, World!"));
    }

    [Test]
    public async Task PhysicalReader_ListsSpr332_ConnectsT1_AndSelectsApplet()
    {
        await using var service = new SmartCardService();
        await service.InitializeAsync();
        var readers = await service.GetAvailableReadersAsync();

        if (!readers.Contains(SmartCardProtocol.Spr332ReaderName))
        {
            Assert.That(
                service.LastNativeStatus,
                Is.EqualTo(WinSCardNative.SCARD_E_NO_READERS_AVAILABLE).Or.EqualTo(WinSCardNative.SCARD_E_READER_UNAVAILABLE));
            return;
        }

        bool connected = await service.ConnectAsync(SmartCardProtocol.Spr332ReaderName);
        if (!connected)
        {
            Assert.That(
                service.LastNativeStatus,
                Is.EqualTo(WinSCardNative.SCARD_E_NO_SMARTCARD).Or.EqualTo(WinSCardNative.SCARD_E_READER_UNAVAILABLE));
            return;
        }

        Assert.That(service.ActiveProtocol, Is.EqualTo(WinSCardNative.SCARD_PROTOCOL_T1));
        Assert.That(service.CardAtr, Is.Not.Null.And.Not.Empty);
        var select = await service.TransmitAsync(new ApduCommand(0x00, 0xA4, 0x04, 0x00, SmartCardProtocol.AppletAid));
        Assert.That(select.StatusWord, Is.EqualTo(0x9000));
        await service.DisconnectAsync();
    }
}

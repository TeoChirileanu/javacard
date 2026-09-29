using System;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
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
}

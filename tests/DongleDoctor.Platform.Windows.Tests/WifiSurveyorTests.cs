using DongleDoctor.Platform.Windows.Wifi;
using Xunit.Abstractions;

namespace DongleDoctor.Platform.Windows.Tests;

// Read-only: these never sweep, so they're safe to run while the Frame is streaming.
public sealed class WifiSurveyorTests(ITestOutputHelper output)
{
    [Fact]
    public void ReadLink_DoesNotThrow()
    {
        var link = WindowsWifiSurveyor.ReadLink();
        output.WriteLine(link is null
            ? "Dongle not connected to the Frame."
            : $"Signal {link.SignalPercent}%, rx {link.RxMbps} Mbps, tx {link.TxMbps} Mbps");
    }

    [Fact]
    public async Task IsStreaming_DoesNotThrow() =>
        output.WriteLine($"Streaming: {await new WindowsWifiSurveyor().IsStreamingAsync()}");
}

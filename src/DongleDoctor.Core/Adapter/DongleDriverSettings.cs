namespace DongleDoctor.Core.Adapter;

/// <summary>Power and USB settings in the dongle's Wi-Fi driver. A null value means the driver doesn't have that setting.</summary>
/// <param name="AllowTurnOff">Device Manager's "Allow the computer to turn off this device to save power".</param>
/// <param name="DriverPowerSaving">
/// The Realtek driver's own power saving while connected (LpsEn) or idle (IpsEn). Valve's driver ships with both off.
/// </param>
/// <param name="ForcedUsb2">The driver's "USB Mode" is set to USB 2.0, which caps the dongle whatever port it's in.</param>
/// <param name="Details">The raw registry values, for the report.</param>
public sealed record DongleDriverSettings(
    bool? AllowTurnOff,
    bool? DriverPowerSaving,
    bool? ForcedUsb2,
    string Details);

/// <summary>Reads the dongle's driver settings from the OS.</summary>
public interface IDongleDriverSettingsReader
{
    /// <summary>The settings, or null if no dongle is plugged in.</summary>
    DongleDriverSettings? Read();
}

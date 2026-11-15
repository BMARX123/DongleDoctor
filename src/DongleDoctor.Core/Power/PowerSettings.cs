namespace DongleDoctor.Core.Power;

/// <summary>Windows' built-in power plans.</summary>
public static class PowerPlans
{
    public static readonly Guid Balanced = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    public static readonly Guid HighPerformance = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    public static readonly Guid PowerSaver = new("a1841308-3541-4fab-bc81-f71556f20b4a");
    public static readonly Guid UltimatePerformance = new("e9a42b02-d5df-448d-aa00-03f14749eb61");

    /// <summary>The Windows 11 "Best performance" power mode, laid over the plan.</summary>
    public static readonly Guid BestPerformanceMode = new("ded574b5-45a0-4f42-8737-46345c09c238");
}

public enum PowerSource
{
    PluggedIn,
    Battery,
}

/// <summary>The "Wireless Adapter Settings → Power Saving Mode" values, in Windows' order.</summary>
public enum WirelessPowerSaving : uint
{
    MaximumPerformance = 0,
    Low = 1,
    Medium = 2,
    Maximum = 3,
}

/// <summary>The active power plan's settings that matter for streaming, for the current power source.</summary>
/// <param name="PowerMode">The Windows 11 power mode laid over the plan (Settings → Power), if any.</param>
/// <param name="UsbSelectiveSuspend">Whether Windows may suspend idle USB devices; null if the plan doesn't have the setting.</param>
/// <param name="WirelessPowerSaving">null if the plan doesn't have the setting.</param>
public sealed record PowerSettings(
    Guid PlanId,
    string PlanName,
    PowerSource Source,
    Guid? PowerMode,
    bool? UsbSelectiveSuspend,
    WirelessPowerSaving? WirelessPowerSaving);

/// <summary>Reads the active power plan from the OS.</summary>
public interface IPowerSettingsReader
{
    PowerSettings Read();
}

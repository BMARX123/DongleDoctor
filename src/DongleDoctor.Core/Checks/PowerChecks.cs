using DongleDoctor.Core.Power;

namespace DongleDoctor.Core.Checks;

/// <summary>
/// The instant power-plan checks. They share one read of the active plan per run;
/// call <see cref="Create"/> again for a fresh read.
/// </summary>
public static class PowerChecks
{
    public static IReadOnlyList<ICheck> Create(IPowerSettingsReader reader)
    {
        var settings = new Lazy<PowerSettings>(reader.Read, LazyThreadSafetyMode.ExecutionAndPublication);
        return
        [
            new SharedReadCheck<PowerSettings>("power.plan", "Checking your power plan", settings, EvaluatePlan),
            new SharedReadCheck<PowerSettings>("power.usb-suspend", "Checking USB power saving", settings, EvaluateUsbSuspend),
            new SharedReadCheck<PowerSettings>("power.wireless", "Checking Wi-Fi power saving", settings, EvaluateWireless),
        ];
    }

    internal static Finding EvaluatePlan(PowerSettings settings)
    {
        const string id = "power.plan";
        var details = $"{settings.PlanName} ({settings.PlanId}), power mode {settings.PowerMode?.ToString() ?? "none"}, on {Describe(settings.Source)}";
        if (settings.PlanId == PowerPlans.PowerSaver)
        {
            return new Finding(id, Severity.Warning, "Power saver plan is on",
                "Power saver slows your PC down to save energy. Switch to the High performance power plan.",
                Details: details);
        }

        // Many laptops offer only Balanced; there the "Best performance" power mode is the setting that counts.
        if (settings.PlanId == PowerPlans.Balanced && settings.PowerMode != PowerPlans.BestPerformanceMode)
        {
            return new Finding(id, Severity.Warning, "Balanced power plan is on",
                "Balanced can slow your PC down between bursts of work. Switch to the High performance power plan, or set Power mode to \"Best performance\" in Settings → System → Power.",
                Details: details);
        }

        return new Finding(id, Severity.Pass, "Power plan is fine", settings.PlanName, Details: details);
    }

    internal static Finding EvaluateUsbSuspend(PowerSettings settings)
    {
        const string id = "power.usb-suspend";
        var details = $"USB selective suspend on {Describe(settings.Source)}: {settings.UsbSelectiveSuspend?.ToString() ?? "not in this plan"}";
        return settings.UsbSelectiveSuspend switch
        {
            false => new Finding(id, Severity.Pass, "USB power saving is off", "Windows won't pause your USB ports", Details: details),
            true => new Finding(id, Severity.Warning, "USB power saving is on",
                "Windows can pause USB ports to save power. Turn off \"USB selective suspend\" in your power plan's advanced settings.",
                Details: details),
            null => new Finding(id, Severity.Pass, "USB power saving is off", "Your power plan doesn't pause USB ports", Details: details),
        };
    }

    internal static Finding EvaluateWireless(PowerSettings settings)
    {
        const string id = "power.wireless";
        var details = $"Wireless adapter power saving on {Describe(settings.Source)}: {settings.WirelessPowerSaving?.ToString() ?? "not in this plan"}";
        return settings.WirelessPowerSaving switch
        {
            WirelessPowerSaving.MaximumPerformance or null => new Finding(id, Severity.Pass, "Wi-Fi power saving is off",
                "Your dongle can run at full speed", Details: details),
            _ => new Finding(id, Severity.Warning, "Wi-Fi power saving is on",
                "Power saving can make your dongle's link pause. Set \"Wireless Adapter Settings\" to Maximum Performance in your power plan's advanced settings.",
                Details: details),
        };
    }

    private static string Describe(PowerSource source) => source == PowerSource.Battery ? "battery" : "mains power";
}

using DongleDoctor.Core.Usb;

namespace DongleDoctor.Core.Checks;

/// <summary>
/// The instant USB checks. They share one read of the USB topology per run;
/// call <see cref="Create"/> again for a fresh read (e.g. after the user moves the dongle).
/// </summary>
public static class UsbChecks
{
    public static IReadOnlyList<ICheck> Create(IUsbTopology topology)
    {
        var dongle = new Lazy<DongleConnection?>(topology.FindDongle, LazyThreadSafetyMode.ExecutionAndPublication);
        return
        [
            new UsbCheck("usb.dongle-present", "Finding your dongle", dongle, EvaluatePresence),
            new UsbCheck("usb.speed", "Checking the USB connection", dongle, EvaluateSpeed),
            new UsbCheck("usb.hub", "Checking how it's plugged in", dongle, EvaluateHub),
        ];
    }

    internal static Finding EvaluatePresence(DongleConnection? dongle)
    {
        const string id = "usb.dongle-present";
        if (dongle is null)
        {
            return new Finding(id, Severity.Blocker, "Dongle not found",
                "Plug the Steam Frame's USB dongle into your PC.");
        }

        if (dongle.ProblemCode != 0)
        {
            return new Finding(id, Severity.Blocker, "Your dongle isn't working",
                "Windows found the dongle but reports a problem with it. Try unplugging it and plugging it back in.",
                Details: $"{dongle.Name} ({dongle.InstanceId}): device problem code {dongle.ProblemCode}");
        }

        return new Finding(id, Severity.Pass, "Dongle found", "Steam Frame wireless adapter",
            Details: $"{dongle.Name} ({dongle.InstanceId})");
    }

    internal static Finding EvaluateSpeed(DongleConnection? dongle)
    {
        const string id = "usb.speed";
        if (dongle is null)
        {
            return Skipped(id);
        }

        // SuperSpeed (5 Gbps) carries the dongle's ~1.2 Gbps Wi-Fi peak, so 10 Gbps ports add nothing.
        return dongle.Speed switch
        {
            UsbSpeed.SuperSpeedPlus => new Finding(id, Severity.Pass, "Fast USB connection", "Running at 10 Gbps or faster (USB 3.2)"),
            UsbSpeed.SuperSpeed => new Finding(id, Severity.Pass, "Fast USB connection", "Running at 5 Gbps (USB 3)"),
            UsbSpeed.High or UsbSpeed.Full or UsbSpeed.Low => new Finding(id, Severity.Warning, "Slow USB connection",
                "Your dongle is running at USB 2 speed. Plug it into a USB 3 port, often marked blue or \"SS\".",
                FindingAction.Guide, $"Negotiated speed: {dongle.Speed}"),
            _ => new Finding(id, Severity.Info, "Couldn't read the USB speed",
                "This doesn't mean anything is wrong.", Details: "The hub didn't report a speed for the dongle's port."),
        };
    }

    internal static Finding EvaluateHub(DongleConnection? dongle)
    {
        const string id = "usb.hub";
        if (dongle is null)
        {
            return Skipped(id);
        }

        var hubs = dongle.Hubs.ToList();
        var chain = string.Join(" → ", dongle.ParentHubs.Select(h => h.Name + (h.IsBuiltIn ? " (built in)" : " (plugged in)")));
        if (hubs.Count == 0)
        {
            return new Finding(id, Severity.Pass, "Plugged straight into your PC", "No USB hub in between", Details: chain);
        }

        if (hubs.Any(h => !h.IsBuiltIn))
        {
            return new Finding(id, Severity.Warning, "Plugged into a USB hub",
                "Hubs can cause short lag spikes. Plug the dongle straight into your PC, or use a passive extension cable.",
                Details: chain);
        }

        return new Finding(id, Severity.Pass, "Plugged into your PC",
            "Through your PC's built-in USB hub, which is fine", Details: chain);
    }

    private static Finding Skipped(string id) =>
        new(id, Severity.Info, "Skipped", "Needs the dongle to be plugged in.");

    private sealed class UsbCheck(string id, string name, Lazy<DongleConnection?> dongle, Func<DongleConnection?, Finding> evaluate) : ICheck
    {
        public string Id => id;

        public string Name => name;

        public Task<Finding> RunAsync(CancellationToken cancellationToken = default) =>
            Task.Run(() => evaluate(dongle.Value), cancellationToken);
    }
}

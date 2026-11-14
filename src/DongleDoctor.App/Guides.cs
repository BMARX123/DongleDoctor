using System.Diagnostics;

namespace DongleDoctor.App;

/// <summary>"Show me how" guides: Markdown files in the repo's docs/guides folder, opened in the browser.</summary>
internal static class Guides
{
    private const string BaseUrl = "https://github.com/BMARX123/DongleDoctor/blob/main/docs/guides/";

    /// <summary>The guide for a check: "usb.hub" → usb-hub.md.</summary>
    public static string UrlFor(string checkId) => BaseUrl + checkId.Replace('.', '-') + ".md";

    public static void Open(string checkId) =>
        Process.Start(new ProcessStartInfo(UrlFor(checkId)) { UseShellExecute = true });
}

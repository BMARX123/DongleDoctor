using System.Text.RegularExpressions;

namespace DongleDoctor.App.ViewModels;

/// <summary>Small fixes to text before it's shown on screen.</summary>
public static partial class UiText
{
    /// <summary>Keeps a number and its unit on one line ("6 GHz", "80 MHz") by joining them with a no-break space.</summary>
    public static string KeepUnits(string text) => Unit().Replace(text, "$1 $2");

    [GeneratedRegex(@"(\d) (GHz|MHz|Mbps|Gbps|min)\b")]
    private static partial Regex Unit();
}

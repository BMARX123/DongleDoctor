using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.Audio;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DongleDoctor.Platform.Windows;

/// <summary>Gets the user's attention when a scan ends. While they stream, the PC's sound plays in the headset.</summary>
public static class Attention
{
    /// <summary>The chime plays this many times: once is easy to miss under game audio.</summary>
    private const int Chimes = 3;

    private static readonly TimeSpan ChimeGap = TimeSpan.FromSeconds(1);

    /// <summary>Plays the Windows notification sound three times and flashes the window's taskbar button until it's in front.</summary>
    /// <returns>Whether the sound started.</returns>
    public static bool Notify(nint window)
    {
        var played = Chime();
        if (played)
        {
            _ = Task.Run(async () =>
            {
                for (var i = 1; i < Chimes; i++)
                {
                    await Task.Delay(ChimeGap).ConfigureAwait(false);
                    Chime();
                }
            });
        }

        if (window == 0)
        {
            return played;
        }

        var flash = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = (HWND)window,
            dwFlags = FLASHWINFO_FLAGS.FLASHW_ALL | FLASHWINFO_FLAGS.FLASHW_TIMERNOFG,
        };
        PInvoke.FlashWindowEx(in flash);
        return played;
    }

    private static bool Chime()
    {
        // The toast chime is the clearer one. "SystemNotification" is often the quiet Windows Background thud,
        // so it's only the fallback for sound schemes without a toast sound.
        const SND_FLAGS flags = SND_FLAGS.SND_ALIAS | SND_FLAGS.SND_ASYNC;
        return PInvoke.PlaySound("Notification.Default", null, flags | SND_FLAGS.SND_NODEFAULT)
            || PInvoke.PlaySound("SystemNotification", null, flags);
    }
}

using System.Text;
using DongleDoctor.Core.Scan;
using Microsoft.Win32;

namespace DongleDoctor.Platform.Windows.Scan;

/// <summary>
/// Tails Steam's VR Link driver log for throttle events and streaming marks. The file's modified time can stay
/// stale while SteamVR holds it open, so this polls the content instead.
/// </summary>
public sealed class SteamLogProbe(string? logPath = null) : IProbe
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    // Enough of the start of the file to notice when SteamVR restarts and Steam starts a new log.
    private const int HeaderLength = 128;

    public string Name => "Steam VR Link log";

    /// <summary>Where Steam writes the log, or null if Steam isn't installed.</summary>
    public static string? DefaultPath()
    {
        var steam = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") as string
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        return Directory.Exists(steam) ? Path.GetFullPath(Path.Combine(steam, "logs", "driver_vrlink.txt")) : null;
    }

    public async Task RunAsync(ScanClock clock, Action<ScanEvent> emit, CancellationToken cancellationToken)
    {
        var path = logPath ?? DefaultPath() ?? throw new FileNotFoundException("Steam isn't installed");

        // Start at the end, so lines from before the scan don't count. A log that appears later is read from the top.
        var (position, header) = File.Exists(path) ? (new FileInfo(path).Length, ReadHeader(path)) : (0L, "");
        var decoder = Encoding.UTF8.GetDecoder();
        var pending = new StringBuilder();
        var buffer = new byte[64 * 1024];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
        var opened = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (File.Exists(path))
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (!opened)
                {
                    opened = true;
                    emit(new ScanEvent(clock.Now, ScanEventKind.LogOpened));
                }

                var currentHeader = ReadHeader(stream);
                if (stream.Length < position || !currentHeader.StartsWith(header, StringComparison.Ordinal))
                {
                    // Steam started a new log: read it from the top.
                    position = 0;
                    pending.Clear();
                    decoder.Reset();
                }

                header = currentHeader;
                stream.Seek(position, SeekOrigin.Begin);
                int read;
                while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    position += read;
                    pending.Append(chars, 0, decoder.GetChars(buffer, 0, read, chars, 0));
                }

                EmitCompleteLines(pending, clock, emit);
            }

            await Task.Delay(PollInterval, clock.Time, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Emits each complete line and keeps a trailing partial line for the next poll.</summary>
    internal static void EmitCompleteLines(StringBuilder pending, ScanClock clock, Action<ScanEvent> emit)
    {
        var text = pending.ToString();
        var lastNewline = text.LastIndexOf('\n');
        if (lastNewline < 0)
        {
            return;
        }

        foreach (var line in text[..lastNewline].Split('\n'))
        {
            if (VrLinkLog.Parse(line.TrimEnd('\r')) is { } parsed && clock.FromLocal(parsed.Local) is var at && at >= TimeSpan.Zero)
            {
                emit(new ScanEvent(at, parsed.Kind, parsed.Ms));
            }
        }

        pending.Remove(0, lastNewline + 1);
    }

    private static string ReadHeader(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return ReadHeader(stream);
    }

    private static string ReadHeader(FileStream stream)
    {
        var bytes = new byte[HeaderLength];
        stream.Seek(0, SeekOrigin.Begin);
        var read = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
        return Encoding.UTF8.GetString(bytes, 0, read);
    }
}

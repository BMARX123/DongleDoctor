using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DongleDoctor.App.ViewModels;

namespace DongleDoctor.App.Views;

/// <summary>
/// The Frame's band from low to high frequency. Each network is a bar across the channels it occupies, taller when
/// stronger; the Frame is outlined, and networks that share its channel are filled.
/// </summary>
public sealed class WifiChannelChart : Control
{
    private const double AxisHeight = 18;
    private const int WeakestRssi = -95;
    private const int StrongestRssi = -30;

    public static readonly StyledProperty<WifiChart?> ChartProperty =
        AvaloniaProperty.Register<WifiChannelChart, WifiChart?>(nameof(Chart));

    public static readonly StyledProperty<IBrush?> FrameBrushProperty =
        AvaloniaProperty.Register<WifiChannelChart, IBrush?>(nameof(FrameBrush));

    public static readonly StyledProperty<IBrush?> ConflictBrushProperty =
        AvaloniaProperty.Register<WifiChannelChart, IBrush?>(nameof(ConflictBrush));

    public static readonly StyledProperty<IBrush?> OtherBrushProperty =
        AvaloniaProperty.Register<WifiChannelChart, IBrush?>(nameof(OtherBrush));

    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<WifiChannelChart, IBrush?>(nameof(TextBrush));

    static WifiChannelChart() =>
        AffectsRender<WifiChannelChart>(ChartProperty, FrameBrushProperty, ConflictBrushProperty, OtherBrushProperty, TextBrushProperty);

    public WifiChart? Chart
    {
        get => GetValue(ChartProperty);
        set => SetValue(ChartProperty, value);
    }

    public IBrush? FrameBrush
    {
        get => GetValue(FrameBrushProperty);
        set => SetValue(FrameBrushProperty, value);
    }

    public IBrush? ConflictBrush
    {
        get => GetValue(ConflictBrushProperty);
        set => SetValue(ConflictBrushProperty, value);
    }

    public IBrush? OtherBrush
    {
        get => GetValue(OtherBrushProperty);
        set => SetValue(OtherBrushProperty, value);
    }

    public IBrush? TextBrush
    {
        get => GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Chart is not { } chart || Bounds.Width <= 0)
        {
            return;
        }

        var plot = Bounds.Height - AxisHeight;
        double X(int mhz) => (mhz - chart.LowMhz) * Bounds.Width / (chart.HighMhz - chart.LowMhz);

        foreach (var bar in chart.Bars)
        {
            var strength = Math.Clamp((bar.Rssi - WeakestRssi) / (double)(StrongestRssi - WeakestRssi), 0.08, 1);
            var rect = new Rect(X(bar.LowMhz), plot * (1 - strength), Math.Max(2, X(bar.HighMhz) - X(bar.LowMhz)), plot * strength);
            switch (bar.Kind)
            {
                case WifiChart.Kind.Frame:
                    context.DrawRectangle(null, new Pen(FrameBrush, 2), rect.Deflate(1), 3, 3);
                    break;
                case WifiChart.Kind.Conflict:
                    using (context.PushOpacity(0.55))
                    {
                        context.DrawRectangle(ConflictBrush, null, rect, 3, 3);
                    }

                    break;
                default:
                    using (context.PushOpacity(0.25))
                    {
                        context.DrawRectangle(OtherBrush, null, rect, 3, 3);
                    }

                    break;
            }
        }

        context.DrawLine(new Pen(OtherBrush, 1), new Point(0, plot), new Point(Bounds.Width, plot));
        foreach (var (mhz, label) in chart.Ticks)
        {
            var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, TextBrush);
            var x = Math.Clamp(X(mhz) - (text.Width / 2), 0, Bounds.Width - text.Width);
            context.DrawText(text, new Point(x, plot + 3));
        }
    }
}

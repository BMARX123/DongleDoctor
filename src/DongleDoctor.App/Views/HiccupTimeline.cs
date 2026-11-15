using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DongleDoctor.App.Views;

/// <summary>A strip covering the whole scan: the part so far is filled, and each hiccup is a tick.</summary>
public sealed class HiccupTimeline : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<HiccupTimeline, double>(nameof(Progress));

    public static readonly StyledProperty<IReadOnlyList<double>?> MarksProperty =
        AvaloniaProperty.Register<HiccupTimeline, IReadOnlyList<double>?>(nameof(Marks));

    public static readonly StyledProperty<IBrush?> TrackProperty =
        AvaloniaProperty.Register<HiccupTimeline, IBrush?>(nameof(Track));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<HiccupTimeline, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> MarkProperty =
        AvaloniaProperty.Register<HiccupTimeline, IBrush?>(nameof(Mark));

    static HiccupTimeline() =>
        AffectsRender<HiccupTimeline>(ProgressProperty, MarksProperty, TrackProperty, FillProperty, MarkProperty);

    /// <summary>How much of the scan has run, from 0 to 1.</summary>
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>Where each hiccup falls, from 0 to 1.</summary>
    public IReadOnlyList<double>? Marks
    {
        get => GetValue(MarksProperty);
        set => SetValue(MarksProperty, value);
    }

    public IBrush? Track
    {
        get => GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IBrush? Mark
    {
        get => GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        const double markWidth = 3;
        var bounds = new Rect(Bounds.Size);
        context.DrawRectangle(Track, null, bounds, 4, 4);
        context.DrawRectangle(Fill, null, bounds.WithWidth(bounds.Width * Math.Clamp(Progress, 0, 1)), 4, 4);
        foreach (var m in Marks ?? [])
        {
            var x = Math.Clamp(bounds.Width * m - markWidth / 2, 0, bounds.Width - markWidth);
            context.DrawRectangle(Mark, null, new Rect(x, 0, markWidth, bounds.Height));
        }
    }
}

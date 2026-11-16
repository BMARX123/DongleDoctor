using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DongleDoctor.App.ViewModels;

/// <summary>The Start scan screen: pick how long to watch, after starting a game on the Frame.</summary>
public partial class StartScanViewModel : ViewModelBase
{
    private readonly Action<TimeSpan> _start;
    private readonly Action _back;

    public StartScanViewModel(Action<TimeSpan> start, Action back)
    {
        _start = start;
        _back = back;
        foreach (var option in Options)
        {
            option.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ScanLengthOption.IsSelected))
                    OnPropertyChanged(nameof(SelectedDetail));
            };
        }
    }

    public IReadOnlyList<ScanLengthOption> Options { get; } =
    [
        new("Quick", 5, "A first look"),
        new("Standard", 10, "Recommended. Long enough to catch several hiccups.", isSelected: true),
        new("Thorough", 15, "Best for hiccups that come and go"),
    ];

    /// <summary>The line under the segmented control, describing the chosen length.</summary>
    public string SelectedDetail => Options.FirstOrDefault(o => o.IsSelected)?.Detail ?? "";

    [RelayCommand]
    private void Start() => _start(TimeSpan.FromMinutes(Options.First(o => o.IsSelected).Minutes));

    [RelayCommand]
    private void Back() => _back();
}

public partial class ScanLengthOption(string name, int minutes, string detail, bool isSelected = false) : ViewModelBase
{
    public string Name => name;

    public int Minutes => minutes;

    public string Length => $"{minutes} min";

    public string Detail => detail;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;
}

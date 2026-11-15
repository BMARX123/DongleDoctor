using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DongleDoctor.App.ViewModels;

/// <summary>The Start scan screen: pick how long to watch, after starting a game on the Frame.</summary>
public partial class StartScanViewModel(Action<TimeSpan> start, Action back) : ViewModelBase
{
    public IReadOnlyList<ScanLengthOption> Options { get; } =
    [
        new("Quick", 5, "A first look"),
        new("Standard", 10, "Recommended", isSelected: true),
        new("Thorough", 15, "Best for hiccups that come and go"),
    ];

    [RelayCommand]
    private void Start() => start(TimeSpan.FromMinutes(Options.First(o => o.IsSelected).Minutes));

    [RelayCommand]
    private void Back() => back();
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

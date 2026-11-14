using System;
using CommunityToolkit.Mvvm.Input;

namespace DongleDoctor.App.ViewModels;

public partial class WelcomeViewModel(Action start) : ViewModelBase
{
    [RelayCommand]
    private void Start() => start();
}

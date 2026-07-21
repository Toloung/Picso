using LocalPhotoManager.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LocalPhotoManager.App;

/// <summary>
/// The main content page displayed inside the application window.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; } = App.Services.GetRequiredService<MainPageViewModel>();

    public MainPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }
}

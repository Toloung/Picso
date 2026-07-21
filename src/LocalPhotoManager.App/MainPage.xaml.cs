using LocalPhotoManager.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
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

    private async void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string viewKey)
        {
            await ViewModel.SelectViewAsync(viewKey);
        }
    }

    private async void OnPhotosClick(object sender, RoutedEventArgs args) => await ViewModel.SelectViewAsync("Photos");

    private async void OnFoldersClick(object sender, RoutedEventArgs args) => await ViewModel.SelectViewAsync("Folders");

    private async void OnTimelineClick(object sender, RoutedEventArgs args) => await ViewModel.SelectViewAsync("Timeline");

    private void OnPhotoItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is PhotoListItem photo)
        {
            ViewModel.SelectPhoto(photo);
        }
    }

    private async void OnFolderItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is FolderSummaryItem folder)
        {
            await ViewModel.LoadFolderAsync(folder);
        }
    }

    private async void OnTimelineItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is TimelineGroupItem group)
        {
            await ViewModel.LoadTimelineMonthAsync(group);
        }
    }
}

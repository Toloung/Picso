using LocalPhotoManager.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

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
        Loaded += async (_, _) =>
        {
            Focus(FocusState.Programmatic);
            await ViewModel.LoadAsync();
        };
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
        if (args.ClickedItem is FolderTreeItem folder)
        {
            await ViewModel.LoadFolderAsync(folder);
        }
    }

    private void OnFolderToggleClick(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { DataContext: FolderTreeItem folder })
        {
            ViewModel.ToggleFolder(folder);
        }
    }

    private async void OnTimelineItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is TimelineGroupItem group)
        {
            await ViewModel.LoadTimelineMonthAsync(group);
        }
    }

    private async void OnPageKeyDown(object sender, KeyRoutedEventArgs args)
    {
        switch (args.Key)
        {
            case VirtualKey.Left:
                ViewModel.SelectPreviousPhotoCommand.Execute(null);
                args.Handled = true;
                break;
            case VirtualKey.Right:
                ViewModel.SelectNextPhotoCommand.Execute(null);
                args.Handled = true;
                break;
            case VirtualKey.Enter:
                await ViewModel.OpenSelectedPhotoCommand.ExecuteAsync(null);
                args.Handled = true;
                break;
            case VirtualKey.F:
                await ViewModel.RevealSelectedPhotoCommand.ExecuteAsync(null);
                args.Handled = true;
                break;
            case VirtualKey.I:
                ViewModel.ToggleInformationPaneCommand.Execute(null);
                args.Handled = true;
                break;
            case VirtualKey.Escape:
                if (ViewModel.InformationPaneVisibility == Visibility.Visible)
                {
                    ViewModel.ToggleInformationPaneCommand.Execute(null);
                    args.Handled = true;
                }

                break;
        }
    }
}

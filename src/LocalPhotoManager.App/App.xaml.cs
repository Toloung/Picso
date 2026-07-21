using LocalPhotoManager.App.ViewModels;
using LocalPhotoManager.Core.Services;
using LocalPhotoManager.Database;
using LocalPhotoManager.Imaging;
using LocalPhotoManager.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LocalPhotoManager.App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// The main application window. Use <c>App.Window</c> from any class that needs
    /// the window reference (for dialogs, pickers, interop, etc.).
    /// </summary>
    public static Window Window { get; private set; } = null!;

    public static IHost Host { get; } = CreateHost();

    public static IServiceProvider Services => Host.Services;

    /// <summary>
    /// The UI thread dispatcher. Use <c>App.DispatcherQueue</c> to marshal calls
    /// to the UI thread. Fully qualified to avoid CS0104 ambiguity with
    /// <see cref="Windows.System.DispatcherQueue"/>.
    /// </summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>
    /// The native window handle (HWND). Use for file pickers,
    /// <c>DataTransferManager</c>, and any WinRT interop that requires
    /// <c>InitializeWithWindow</c>.
    /// </summary>
    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    private static IHost CreateHost()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();
        builder.Services.AddSingleton(new AppDataPaths("LocalPhotoManager"));
        builder.Services.AddSingleton<PhotoLibraryDatabase>(serviceProvider =>
        {
            var paths = serviceProvider.GetRequiredService<AppDataPaths>();
            paths.EnsureCreated();
            return new PhotoLibraryDatabase(paths.DatabasePath);
        });
        builder.Services.AddSingleton<IPhotoScanner, FileSystemPhotoScanner>();
        builder.Services.AddSingleton<IPhotoFileWatcher, DebouncedPhotoFileWatcher>();
        builder.Services.AddSingleton<ThumbnailPathFactory>();
        builder.Services.AddSingleton<WindowsThumbnailGenerator>();
        builder.Services.AddSingleton<MainPageViewModel>();
        return builder.Build();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Window = new MainWindow();
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Window.Activate();
    }
}

using LoadManager.Services;
using LoadManager.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace LoadManager
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddSingleton<IAppSettingsService, AppSettingsService>();
            builder.Services.AddSingleton<IConsoleLogService, ConsoleLogService>();
            builder.Services.AddSingleton<HttpClient>();
            builder.Services.AddSingleton<IGasStationService, GasStationService>();
            // Rutas de impresion: dialogo del sistema y termica Bluetooth ESC/POS.
            // Se exponen tambien como keyed services (IPrinterService) y el router
            // IReceiptPrinterService decide por cual salir segun PrinterOptions.
            builder.Services.AddSingleton<WindowsPrinterService>();
            builder.Services.AddSingleton<IBluetoothEscPosPrinterService, BluetoothEscPosPrinterService>();
            builder.Services.AddKeyedSingleton<IPrinterService>(
                PrinterServiceKeys.Windows,
                (services, _) => services.GetRequiredService<WindowsPrinterService>());
            builder.Services.AddKeyedSingleton<IPrinterService>(
                PrinterServiceKeys.BluetoothCom,
                (services, _) => services.GetRequiredService<IBluetoothEscPosPrinterService>());
            builder.Services.AddSingleton<IReceiptPrinterService, ReceiptPrinterService>();
            builder.Services.AddSingleton<IConnectionValidationService, ConnectionValidationService>();
            builder.Services.AddSingleton<IDevModeService, DevModeService>();
            builder.Services.AddSingleton<IActiveDispatchTracker, ActiveDispatchTracker>();

#if WINDOWS
            // Se usa el hook de ciclo de vida OnWindowCreated (en vez de Window.HandlerChanged en
            // App.xaml.cs) porque ese ultimo dispara antes de que MAUI termine de inicializar la
            // ventana nativa: los cambios de presenter/titulo se aplicaban pero Windows los
            // pisaba despues, y la barra de titulo seguia apareciendo pese a probar varias APIs.
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddWindows(windows => windows.OnWindowCreated(nativeWindow =>
                {
                    nativeWindow.ExtendsContentIntoTitleBar = true;

                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                    var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                    if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                    {
                        presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
                        presenter.Maximize();
                    }
                }));
            });
#endif

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

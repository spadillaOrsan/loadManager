using LoadManager.Services;
using LoadManager.Services.Interfaces;
using Microsoft.Extensions.Logging;

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

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

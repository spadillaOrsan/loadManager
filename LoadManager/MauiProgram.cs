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
            builder.Services.AddSingleton<IReceiptPrinterService, ReceiptPrinterService>();
            builder.Services.AddSingleton<IConnectionValidationService, ConnectionValidationService>();
            builder.Services.AddSingleton<IDevModeService, DevModeService>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

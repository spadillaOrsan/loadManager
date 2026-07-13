using LoadManager.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace LoadManager
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new MainPage()) { Title = "ADM CARGAS" };

#if WINDOWS
            // En Windows (tablet GETAC) la ventana debe abarcar toda la pantalla:
            // se maximiza al crearse el handler nativo.
            window.HandlerChanged += (_, _) =>
            {
                if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow)
                {
                    return;
                }

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    presenter.Maximize();
                }
            };
#endif

            return window;
        }

        // Al pasar a segundo plano: si el Modo DEV tiene activado "Cancelar carga al
        // pasar a segundo plano", cancela la carga activa en la consola (best-effort).
        protected override void OnSleep()
        {
            base.OnSleep();

            var services = IPlatformApplication.Current?.Services;
            var devMode = services?.GetService<IDevModeService>();
            var tracker = services?.GetService<IActiveDispatchTracker>();

            if (devMode?.ShouldCancelOnBackground == true && tracker is not null)
            {
                _ = tracker.CancelActiveDispatchAsync();
            }
        }
    }
}

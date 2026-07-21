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

        // En Windows, la ventana se pone en pantalla completa sin barra de titulo (tablet GETAC)
        // via el hook OnWindowCreated configurado en MauiProgram.cs.
        protected override Window CreateWindow(IActivationState? activationState) =>
            new(new MainPage()) { Title = "ADM CARGAS" };

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

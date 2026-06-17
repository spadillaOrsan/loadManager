namespace LoadManager.Services.Interfaces;

/// <summary>
/// Rastrea la carga (autorizacion) activa para poder cancelarla desde el ciclo de
/// vida de la app (ej. al pasar a segundo plano). Lo alimenta la pantalla de despacho.
/// </summary>
public interface IActiveDispatchTracker
{
    /// <summary>Dispensario con autorizacion/carga activa, o null si no hay ninguna.</summary>
    int? ActiveDispenser { get; set; }

    /// <summary>Se dispara cada vez que ActiveDispenser cambia de valor.</summary>
    event Action? StateChanged;

    /// <summary>Cancela en la consola la carga activa (CAUTH) si la hay.</summary>
    Task CancelActiveDispatchAsync(CancellationToken cancellationToken = default);
}

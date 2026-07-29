using Microsoft.AspNetCore.Components;

namespace LoadManager.Services;

/// <summary>
/// Permite que una pagina (Despacho, Historial, etc.) registre contenido extra
/// (por ejemplo el icono de Refrescar) para que MainLayout lo dibuje del lado
/// derecho del titulo de modulo, a la misma altura, sin que MainLayout conozca
/// la logica de cada pagina.
/// </summary>
public sealed class ModuleHeaderState
{
    public RenderFragment? Actions { get; private set; }

    public RenderFragment? Subtitle { get; private set; }

    public event Action? Changed;

    public void SetActions(RenderFragment? actions)
    {
        Actions = actions;
        Changed?.Invoke();
    }

    public void SetSubtitle(RenderFragment? subtitle)
    {
        Subtitle = subtitle;
        Changed?.Invoke();
    }

    /// <summary>
    /// Fuerza a MainLayout a volver a dibujar las acciones actuales (por
    /// ejemplo cuando cambia una condicion interna de la pagina, sin que el
    /// fragmento en si haya cambiado de referencia).
    /// </summary>
    public void Refresh() => Changed?.Invoke();
}

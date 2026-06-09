using LoadManagerApi.Models;

namespace LoadManagerApi.Interfaces;

public interface IAppLogService
{
    Task<string> WriteAsync(
        ApiLogEntry entry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Escribe un archivo independiente (por ejemplo de intento de acceso no
    /// autorizado) en la misma estructura de carpetas Logs/anio/mes/dia, con el
    /// nombre indicado. Devuelve la ruta del archivo escrito.
    /// </summary>
    Task<string> WriteFileAsync(
        string fileName,
        string content,
        CancellationToken cancellationToken = default);
}

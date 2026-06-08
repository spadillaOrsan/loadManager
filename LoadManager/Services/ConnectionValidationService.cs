using LoadManager.Contracts.Models;
using LoadManager.Services.Interfaces;

namespace LoadManager.Services;

public sealed class ConnectionValidationService : IConnectionValidationService
{
    public bool? DatabaseIsConnected { get; private set; }

    public string DatabaseStatus { get; private set; } = "Sin validar";

    public DateTime? DatabaseValidatedAt { get; private set; }

    public void SetDatabaseResult(ConsoleCommandResult result)
    {
        DatabaseIsConnected = result.IsSuccess;
        DatabaseStatus = result.IsSuccess ? "Conectado" : "Sin conexion";
        DatabaseValidatedAt = DateTime.Now;
    }

    public void ResetDatabase()
    {
        DatabaseIsConnected = null;
        DatabaseStatus = "Sin validar";
        DatabaseValidatedAt = null;
    }
}

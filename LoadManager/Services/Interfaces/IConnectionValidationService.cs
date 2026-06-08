using LoadManager.Contracts.Models;

namespace LoadManager.Services.Interfaces;

public interface IConnectionValidationService
{
    bool? DatabaseIsConnected { get; }

    string DatabaseStatus { get; }

    DateTime? DatabaseValidatedAt { get; }

    void SetDatabaseResult(ConsoleCommandResult result);

    void ResetDatabase();
}

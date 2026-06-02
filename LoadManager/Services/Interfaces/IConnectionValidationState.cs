using LoadManager.Models;

namespace LoadManager.Services.Interfaces;

public interface IConnectionValidationState
{
    bool? DatabaseIsConnected { get; }

    string DatabaseStatus { get; }

    DateTime? DatabaseValidatedAt { get; }

    void SetDatabaseResult(ConsoleCommandResult result);

    void ResetDatabase();
}

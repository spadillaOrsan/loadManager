using Microsoft.Data.SqlClient;

namespace LoadManagerApi.Interfaces;

public interface IDatabaseScriptService
{
    Task EnsureRequiredStoredProceduresAsync(
        SqlConnection connection,
        CancellationToken cancellationToken = default);
}

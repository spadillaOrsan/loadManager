using System.Diagnostics;
using System.Text.Json;
using LoadManagerApi.Interfaces;
using LoadManagerApi.Models;

namespace LoadManagerApi.Services;

/// <summary>
/// Decorador que registra entrada (con parametros), salida (con duracion y un
/// resumen del resultado) y errores de cada metodo de <see cref="IGasStationService"/>.
/// Funciona como un "middleware" a nivel de servicio: cubre todos los metodos sin
/// ensuciar la logica de negocio. El registro es ligero (un asiento de entrada y uno
/// de salida por llamada) para no afectar el rendimiento.
/// </summary>
public sealed class GasStationServiceLoggingDecorator(
    IGasStationService inner,
    IAppLogService logService) : IGasStationService
{
    private static readonly JsonSerializerOptions CompactJson = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public Task<DeviceAuthorizationResult> CheckDeviceAuthorizationAsync(
        string ipAddress,
        string macAddress,
        string deviceName,
        CancellationToken cancellationToken = default) =>
        InvokeAsync(
            nameof(CheckDeviceAuthorizationAsync),
            $"ip={ipAddress}; mac={macAddress}; equipo={deviceName}",
            () => inner.CheckDeviceAuthorizationAsync(ipAddress, macAddress, deviceName, cancellationToken),
            result => $"Autorizado={result.IsAuthorized}; {result.DeviceName}",
            cancellationToken);

    public Task<ConsoleCommandResult> CheckConnectionAsync(CancellationToken cancellationToken = default) =>
        InvokeAsync(
            nameof(CheckConnectionAsync),
            parameters: null,
            () => inner.CheckConnectionAsync(cancellationToken),
            result => $"IsSuccess={result.IsSuccess}; {result.ResponseFrame}",
            cancellationToken);

    public Task<int> RegisterAuthorizationAsync(
        FuelAuthorizationRequest request,
        CancellationToken cancellationToken = default) =>
        InvokeAsync(
            nameof(RegisterAuthorizationAsync),
            Serialize(request),
            () => inner.RegisterAuthorizationAsync(request, cancellationToken),
            folio => $"Folio={folio}",
            cancellationToken);

    public Task UpdateDispenserStatusAsync(
        int dispenser,
        int status,
        CancellationToken cancellationToken = default) =>
        InvokeAsync(
            nameof(UpdateDispenserStatusAsync),
            $"dispensario={dispenser}; estatus={status}",
            () => inner.UpdateDispenserStatusAsync(dispenser, status, cancellationToken),
            cancellationToken);

    public Task<IReadOnlyList<DispenserProductOption>> GetDispenserProductsAsync(
        int dispenser,
        CancellationToken cancellationToken = default) =>
        InvokeAsync(
            nameof(GetDispenserProductsAsync),
            $"dispensario={dispenser}",
            () => inner.GetDispenserProductsAsync(dispenser, cancellationToken),
            result => $"{result.Count} productos",
            cancellationToken);

    public Task<IReadOnlyList<DispatchTypeOption>> GetDispatchTypesAsync(
        CancellationToken cancellationToken = default) =>
        InvokeAsync(
            nameof(GetDispatchTypesAsync),
            parameters: null,
            () => inner.GetDispatchTypesAsync(cancellationToken),
            result => $"{result.Count} tipos",
            cancellationToken);

    public Task<IReadOnlyList<DispatchHistoryRecord>> GetDispatchHistoryAsync(
        string? folio,
        CancellationToken cancellationToken = default) =>
        InvokeAsync(
            nameof(GetDispatchHistoryAsync),
            $"folio={folio ?? "(todos)"}",
            () => inner.GetDispatchHistoryAsync(folio, cancellationToken),
            result => $"{result.Count} registros",
            cancellationToken);

    // ---- Helpers ----

    private async Task<T> InvokeAsync<T>(
        string methodName,
        string? parameters,
        Func<Task<T>> action,
        Func<T, string> describeResult,
        CancellationToken cancellationToken)
    {
        var service = $"GasStationService.{methodName}";
        await LogEntryAsync(service, parameters, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await action();
            stopwatch.Stop();
            await LogExitAsync(service, describeResult(result), stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await LogErrorAsync(service, parameters, ex, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private async Task InvokeAsync(
        string methodName,
        string? parameters,
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        var service = $"GasStationService.{methodName}";
        await LogEntryAsync(service, parameters, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await action();
            stopwatch.Stop();
            await LogExitAsync(service, "OK", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await LogErrorAsync(service, parameters, ex, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private Task LogEntryAsync(string service, string? parameters, CancellationToken cancellationToken) =>
        logService.WriteAsync(new ApiLogEntry
        {
            Level = "Info",
            Service = service,
            Message = "Entrada al metodo.",
            RequestBody = parameters
        }, cancellationToken);

    private Task LogExitAsync(string service, string? result, long elapsedMs) =>
        logService.WriteAsync(new ApiLogEntry
        {
            Level = "Success",
            Service = service,
            Message = "Salida del metodo.",
            ResponseBody = result,
            ElapsedMilliseconds = elapsedMs
        }, CancellationToken.None);

    private Task LogErrorAsync(string service, string? parameters, Exception ex, long elapsedMs) =>
        logService.WriteAsync(new ApiLogEntry
        {
            Level = "Error",
            Service = service,
            Message = "Error en el metodo.",
            RequestBody = parameters,
            Exception = ex.ToString(),
            ElapsedMilliseconds = elapsedMs
        }, CancellationToken.None);

    private static string Serialize(object value)
    {
        try
        {
            return JsonSerializer.Serialize(value, CompactJson);
        }
        catch
        {
            return value.ToString() ?? string.Empty;
        }
    }
}

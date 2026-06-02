using System.Net.Sockets;
using System.Text;
using LoadManager.Helpers;
using LoadManager.Models;
using LoadManager.Services.Interfaces;

namespace LoadManager.Services;

public sealed class GasStationConsoleClient(
    IAppSettingsProvider settingsProvider,
    IConsoleLogService consoleLogService) : IGasStationConsoleClient, IAsyncDisposable
{
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private TcpClient? tcpClient;
    private NetworkStream? stream;
    private string? connectedEndpoint;

    public bool IsConnected => tcpClient?.Connected == true && stream is not null;

    public async Task<ConsoleAvailabilityResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsProvider.GetSettingsAsync(cancellationToken);
        var options = settings.GasStationConsole;
        var endpoint = $"{options.IpAddress}:{options.Port}";

        await connectionLock.WaitAsync(cancellationToken);

        try
        {
            if (IsConnected && connectedEndpoint == endpoint)
            {
                return new ConsoleAvailabilityResult
                {
                    IsAvailable = true,
                    Endpoint = endpoint,
                    UserMessage = "Se conecto exitosamente."
                };
            }

            CloseConnection();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(options.ReadTimeoutMilliseconds));

            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(options.IpAddress, options.Port, timeout.Token);
            stream = tcpClient.GetStream();
            stream.ReadTimeout = options.ReadTimeoutMilliseconds;
            stream.WriteTimeout = options.ReadTimeoutMilliseconds;
            connectedEndpoint = endpoint;

            return new ConsoleAvailabilityResult
            {
                IsAvailable = true,
                Endpoint = endpoint,
                UserMessage = "Se conecto exitosamente."
            };
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or IOException)
        {
            CloseConnection();

            return new ConsoleAvailabilityResult
            {
                IsAvailable = false,
                Endpoint = endpoint,
                UserMessage = "No se conecto exitosamente.",
                TechnicalMessage = ex.Message
            };
        }
        finally
        {
            connectionLock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await connectionLock.WaitAsync();

        try
        {
            CloseConnection();
        }
        finally
        {
            connectionLock.Release();
        }
    }

    public async Task<ConsoleAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        return await ConnectAsync(cancellationToken);
    }

    public async Task<ConsoleCommandResult> SendConfiguredCommandAsync(
        string commandName,
        CancellationToken cancellationToken = default)
    {
        return await SendConfiguredCommandAsync(commandName, replacements: null, cancellationToken);
    }

    public async Task<ConsoleCommandResult> SendRawFrameAsync(
        string requestFrame,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsProvider.GetSettingsAsync(cancellationToken);
        var options = settings.GasStationConsole;

        if (string.IsNullOrWhiteSpace(requestFrame))
        {
            return await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = false,
                CommandName = "raw",
                UserMessage = "Capture una trama para enviar.",
                RequestFrame = string.Empty,
                TechnicalMessage = "La trama cruda esta vacia."
            }, cancellationToken);
        }

        try
        {
            var responseFrame = await SendFrameAsync(options, requestFrame, cancellationToken);
            var authorizationStatus = ConsoleFrameHelper.GetAuthorizationStatus(requestFrame, responseFrame);

            return await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = authorizationStatus != AuthorizationFrameStatus.NotAuthorized,
                CommandName = "raw",
                UserMessage = ConsoleFrameHelper.GetRawFrameUserMessage(authorizationStatus),
                RequestFrame = requestFrame,
                ResponseFrame = responseFrame
            }, cancellationToken);
        }
        catch (SocketException ex)
        {
            return await LogAndReturnAsync(CreateErrorResult(
                "raw",
                requestFrame,
                "No fue posible conectarse con la consola. Verifique la IP, el puerto y la red.",
                ex), cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            return await LogAndReturnAsync(CreateErrorResult(
                "raw",
                requestFrame,
                "La consola no respondio dentro del tiempo esperado.",
                ex), cancellationToken);
        }
        catch (IOException ex)
        {
            return await LogAndReturnAsync(CreateErrorResult(
                "raw",
                requestFrame,
                "Ocurrio un problema al enviar o recibir informacion de la consola.",
                ex), cancellationToken);
        }
        catch (Exception ex)
        {
            return await LogAndReturnAsync(CreateErrorResult(
                "raw",
                requestFrame,
                "Ocurrio un error inesperado al comunicarse con la consola.",
                ex), cancellationToken);
        }
    }

    public async Task<ConsoleCommandResult> SendDispenserSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        return await SendConfiguredCommandAsync("dispenserSummary", cancellationToken);
    }

    public async Task<ConsoleCommandResult> SendDispenserDetailAsync(
        string dispenserNumber,
        CancellationToken cancellationToken = default)
    {
        return await SendConfiguredCommandAsync(
            "dispenserDetail",
            new Dictionary<string, string>
            {
                ["dispensario"] = dispenserNumber
            },
            cancellationToken);
    }

    public async Task<ConsoleCommandResult> SendDispenserStatusAsync(
        string dispenserNumber,
        CancellationToken cancellationToken = default)
    {
        return await SendConfiguredCommandAsync(
            "status",
            new Dictionary<string, string>
            {
                ["dispensario"] = dispenserNumber
            },
            cancellationToken);
    }

    private async Task<ConsoleCommandResult> SendConfiguredCommandAsync(
        string commandName,
        IReadOnlyDictionary<string, string>? replacements,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsProvider.GetSettingsAsync(cancellationToken);
        var options = settings.GasStationConsole;

        if (!options.Commands.TryGetValue(commandName, out var requestFrame) || string.IsNullOrWhiteSpace(requestFrame))
        {
            return await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = false,
                CommandName = commandName,
                UserMessage = "La configuracion de la peticion no esta completa. Revise la configuracion del sistema.",
                RequestFrame = string.Empty,
                TechnicalMessage = $"El comando '{commandName}' no existe en GasStationConsole:Commands."
            }, cancellationToken);
        }

        requestFrame = ConsoleFrameHelper.ApplyReplacements(requestFrame, replacements);

        try
        {
            var responseFrame = await SendFrameAsync(options, requestFrame, cancellationToken);

            return await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = true,
                CommandName = commandName,
                UserMessage = "La consola respondio correctamente.",
                RequestFrame = requestFrame,
                ResponseFrame = responseFrame
            }, cancellationToken);
        }
        catch (SocketException ex)
        {
            return await LogAndReturnAsync(CreateErrorResult(
                commandName,
                requestFrame,
                "No fue posible conectarse con la consola. Verifique la IP, el puerto y la red.",
                ex), cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            return await LogAndReturnAsync(CreateErrorResult(
                commandName,
                requestFrame,
                "La consola no respondio dentro del tiempo esperado.",
                ex), cancellationToken);
        }
        catch (IOException ex)
        {
            return await LogAndReturnAsync(CreateErrorResult(
                commandName,
                requestFrame,
                "Ocurrio un problema al enviar o recibir informacion de la consola.",
                ex), cancellationToken);
        }
        catch (Exception ex)
        {
            return await LogAndReturnAsync(CreateErrorResult(
                commandName,
                requestFrame,
                "Ocurrio un error inesperado al comunicarse con la consola.",
                ex), cancellationToken);
        }
    }

    private async Task<string> SendFrameAsync(
        GasStationConsoleOptions options,
        string requestFrame,
        CancellationToken cancellationToken)
    {
        await connectionLock.WaitAsync(cancellationToken);

        try
        {
            Exception? lastException = null;

            for (var attempt = 0; attempt < 2; attempt++)
            {
                var requestWasWritten = false;

                try
                {
                    await EnsureConnectedAsync(options, cancellationToken);
                    if (stream is null)
                    {
                        throw new IOException("No existe una conexion TCP activa.");
                    }

                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromMilliseconds(options.ReadTimeoutMilliseconds));

                    var encoding = Encoding.GetEncoding(options.EncodingName);
                    var requestBytes = encoding.GetBytes(requestFrame);

                    await stream.WriteAsync(requestBytes, timeout.Token);
                    await stream.FlushAsync(timeout.Token);
                    requestWasWritten = true;

                    var buffer = new byte[8192];
                    var bytesRead = await stream.ReadAsync(buffer, timeout.Token);

                    if (bytesRead == 0)
                    {
                        CloseConnection();
                        throw new IOException("La consola cerro la conexion sin enviar respuesta.");
                    }

                    return encoding.GetString(buffer, 0, bytesRead);
                }
                catch (Exception ex) when (attempt == 0 && !requestWasWritten && IsReconnectableException(ex))
                {
                    lastException = ex;
                    CloseConnection();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    CloseConnection();
                    throw;
                }
            }

            throw new IOException("No fue posible enviar la trama despues de reconectar con la consola.", lastException);
        }
        catch
        {
            CloseConnection();
            throw;
        }
        finally
        {
            connectionLock.Release();
        }
    }

    private static bool IsReconnectableException(Exception exception)
    {
        if (exception is SocketException or IOException)
        {
            return true;
        }

        if (exception.InnerException is SocketException or IOException)
        {
            return true;
        }

        return false;
    }

    private async Task EnsureConnectedAsync(
        GasStationConsoleOptions options,
        CancellationToken cancellationToken)
    {
        var endpoint = $"{options.IpAddress}:{options.Port}";
        if (IsConnected && connectedEndpoint == endpoint)
        {
            return;
        }

        CloseConnection();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(options.ReadTimeoutMilliseconds));

        tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(options.IpAddress, options.Port, timeout.Token);
        stream = tcpClient.GetStream();
        stream.ReadTimeout = options.ReadTimeoutMilliseconds;
        stream.WriteTimeout = options.ReadTimeoutMilliseconds;
        connectedEndpoint = endpoint;
    }

    private static ConsoleCommandResult CreateErrorResult(
        string commandName,
        string requestFrame,
        string userMessage,
        Exception exception)
    {
        return new ConsoleCommandResult
        {
            IsSuccess = false,
            CommandName = commandName,
            UserMessage = userMessage,
            RequestFrame = requestFrame,
            TechnicalMessage = exception.ToString()
        };
    }

    private async Task<ConsoleCommandResult> LogAndReturnAsync(
        ConsoleCommandResult result,
        CancellationToken cancellationToken)
    {
        var logPath = await consoleLogService.WriteAsync(result, cancellationToken);

        return new ConsoleCommandResult
        {
            IsSuccess = result.IsSuccess,
            UserMessage = result.UserMessage,
            CommandName = result.CommandName,
            RequestFrame = result.RequestFrame,
            ResponseFrame = result.ResponseFrame,
            TechnicalMessage = result.TechnicalMessage,
            LogPath = logPath
        };
    }

    private void CloseConnection()
    {
        stream?.Dispose();
        tcpClient?.Dispose();
        stream = null;
        tcpClient = null;
        connectedEndpoint = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        connectionLock.Dispose();
    }
}

using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LoadManager.Contracts.Models;
using LoadManager.Helpers;
using LoadManager.Models;
using LoadManager.Services.Interfaces;

namespace LoadManager.Services;

public sealed class GasStationService(
    HttpClient httpClient,
    IAppSettingsService settingsService,
    IConsoleLogService consoleLogService) : IGasStationService, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private TcpClient? tcpClient;
    private NetworkStream? stream;
    private string? connectedEndpoint;

    public bool IsConnected => tcpClient?.Connected == true && stream is not null;

    public async Task<ConsoleAvailabilityResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
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
            timeout.CancelAfter(TimeSpan.FromMilliseconds(options.ConnectionTimeoutMilliseconds));

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
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
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

    public async Task<ConsoleCommandResult> CheckDatabaseConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        const string requestPath = "api/database/health";

        try
        {
            using var response = await SendApiAsync(
                HttpMethod.Get,
                requestPath,
                content: null,
                cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<ConsoleCommandResult>(
                JsonOptions,
                cancellationToken);

            if (result is not null)
            {
                return await LogAndReturnAsync(result, cancellationToken);
            }

            return await LogAndReturnAsync(CreateApiErrorResult(
                requestPath,
                "La API no devolvio un resultado valido.",
                await response.Content.ReadAsStringAsync(cancellationToken)), cancellationToken);
        }
        catch (Exception ex)
        {
            return await LogAndReturnAsync(CreateApiErrorResult(
                requestPath,
                "No hay comunicacion con la API o la base de datos.",
                ex.ToString()), cancellationToken);
        }
    }

    public async Task<int> RegisterAuthorizationAsync(
        FuelAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        const string requestPath = "api/authorizations";

        try
        {
            using var response = await SendApiAsync(
                HttpMethod.Post,
                requestPath,
                JsonContent.Create(request, options: JsonOptions),
                cancellationToken);
            await EnsureApiSuccessAsync(response, cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<AuthorizationRegistrationResult>(
                JsonOptions,
                cancellationToken);

            if (result is null || result.Folio <= 0)
            {
                throw new InvalidOperationException("La API no devolvio el folio generado.");
            }

            await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = true,
                CommandName = "api",
                RequestFrame = $"POST {requestPath}",
                ResponseFrame = $"Folio: {result.Folio}",
                UserMessage = "Folio y bitacora registrados correctamente."
            }, cancellationToken);

            return result.Folio;
        }
        catch (Exception ex)
        {
            await LogAndReturnAsync(CreateApiErrorResult(
                $"POST {requestPath}",
                "No se pudo generar el folio ni registrar la bitacora de autorizacion.",
                ex.ToString()), cancellationToken);
            throw;
        }
    }

    public async Task UpdateDispenserStatusAsync(
        int dispenser,
        int status,
        CancellationToken cancellationToken = default)
    {
        var requestPath = $"api/dispensers/{dispenser}/status";

        try
        {
            using var response = await SendApiAsync(
                HttpMethod.Put,
                requestPath,
                JsonContent.Create(new DispenserStatusRequest { Status = status }, options: JsonOptions),
                cancellationToken);
            await EnsureApiSuccessAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            await LogAndReturnAsync(CreateApiErrorResult(
                $"PUT {requestPath}",
                "No se pudo actualizar el estatus del dispensario.",
                ex.ToString()), cancellationToken);
        }
    }

    public Task<IReadOnlyList<DispenserProductOption>> GetDispenserProductsAsync(
        int dispenser,
        CancellationToken cancellationToken = default)
    {
        return GetApiListAsync<DispenserProductOption>(
            $"api/dispensers/{dispenser}/products",
            "No se pudieron consultar los productos del dispensario.",
            cancellationToken);
    }

    public Task<IReadOnlyList<DispatchTypeOption>> GetDispatchTypesAsync(
        CancellationToken cancellationToken = default)
    {
        return GetApiListAsync<DispatchTypeOption>(
            "api/dispatch-types",
            "No se pudieron consultar los tipos de despacho.",
            cancellationToken);
    }

    public Task<IReadOnlyList<DispatchHistoryRecord>> GetDispatchHistoryAsync(
        string? folio,
        CancellationToken cancellationToken = default)
    {
        var requestPath = string.IsNullOrWhiteSpace(folio)
            ? "api/history"
            : $"api/history?folio={Uri.EscapeDataString(folio.Trim())}";

        return GetApiListAsync<DispatchHistoryRecord>(
            requestPath,
            "No se pudo consultar el historial de cargas.",
            cancellationToken);
    }

    private async Task<ConsoleCommandResult> SendConfiguredCommandAsync(
        string commandName,
        IReadOnlyDictionary<string, string>? replacements,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
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

    private async Task<IReadOnlyList<T>> GetApiListAsync<T>(
        string requestPath,
        string userMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendApiAsync(
                HttpMethod.Get,
                requestPath,
                content: null,
                cancellationToken);
            await EnsureApiSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<List<T>>(JsonOptions, cancellationToken)
                ?? [];
        }
        catch (Exception ex)
        {
            await LogAndReturnAsync(CreateApiErrorResult(
                $"GET {requestPath}",
                userMessage,
                ex.ToString()), cancellationToken);
            return [];
        }
    }

    private async Task<HttpResponseMessage> SendApiAsync(
        HttpMethod method,
        string requestPath,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        if (!Uri.TryCreate(settings.Api.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("Configure una URL valida para LoadManagerApi.");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(settings.Api.RequestTimeoutSeconds));

        using var request = new HttpRequestMessage(method, new Uri(baseUri, requestPath))
        {
            Content = content
        };
        var absoluteUrl = request.RequestUri?.ToString() ?? requestPath;
        var requestBody = content is null
            ? null
            : await content.ReadAsStringAsync(cancellationToken);

        await consoleLogService.WriteAsync(new AppLogEntry
        {
            Level = "Info",
            Service = nameof(GasStationService),
            Message = "Enviando solicitud a LoadManagerApi.",
            Method = method.Method,
            Url = absoluteUrl,
            RequestBody = requestBody
        }, cancellationToken);

        try
        {
            var response = await httpClient.SendAsync(request, timeoutSource.Token);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            await consoleLogService.WriteAsync(new AppLogEntry
            {
                Level = response.IsSuccessStatusCode ? "Success" : "Error",
                Service = nameof(GasStationService),
                Message = response.IsSuccessStatusCode
                    ? "LoadManagerApi respondio correctamente."
                    : "LoadManagerApi respondio con error.",
                Method = method.Method,
                Url = absoluteUrl,
                HttpStatus = (int)response.StatusCode,
                RequestBody = requestBody,
                ResponseBody = responseBody
            }, cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            await consoleLogService.WriteAsync(new AppLogEntry
            {
                Level = "Error",
                Service = nameof(GasStationService),
                Message = "Ocurrio una excepcion al comunicarse con LoadManagerApi.",
                Method = method.Method,
                Url = absoluteUrl,
                RequestBody = requestBody,
                Exception = ex.ToString()
            }, CancellationToken.None);
            throw;
        }
    }

    private static async Task EnsureApiSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"LoadManagerApi respondio {(int)response.StatusCode} ({response.ReasonPhrase}). {detail}");
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

            for (var attempt = 0; attempt < options.MaxSendAttempts; attempt++)
            {
                var requestWasWritten = false;

                try
                {
                    await consoleLogService.WriteAsync(new AppLogEntry
                    {
                        Level = "Info",
                        Service = nameof(GasStationService),
                        Message = "Enviando trama TCP a la consola.",
                        Method = "TCP",
                        Url = $"{options.IpAddress}:{options.Port}",
                        RequestBody = requestFrame
                    }, cancellationToken);

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

                    var buffer = new byte[options.ReceiveBufferSize];
                    var bytesRead = await stream.ReadAsync(buffer, timeout.Token);

                    if (bytesRead == 0)
                    {
                        CloseConnection();
                        throw new IOException("La consola cerro la conexion sin enviar respuesta.");
                    }

                    var responseFrame = encoding.GetString(buffer, 0, bytesRead);
                    await consoleLogService.WriteAsync(new AppLogEntry
                    {
                        Level = "Success",
                        Service = nameof(GasStationService),
                        Message = "La consola respondio la trama TCP.",
                        Method = "TCP",
                        Url = $"{options.IpAddress}:{options.Port}",
                        RequestBody = requestFrame,
                        ResponseBody = responseFrame
                    }, cancellationToken);

                    return responseFrame;
                }
                catch (Exception ex) when (
                    attempt < options.MaxSendAttempts - 1
                    && !requestWasWritten
                    && IsReconnectableException(ex))
                {
                    lastException = ex;
                    CloseConnection();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    await consoleLogService.WriteAsync(new AppLogEntry
                    {
                        Level = "Error",
                        Service = nameof(GasStationService),
                        Message = "Error al enviar o recibir la trama TCP.",
                        Method = "TCP",
                        Url = $"{options.IpAddress}:{options.Port}",
                        RequestBody = requestFrame,
                        Exception = ex.ToString()
                    }, CancellationToken.None);
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
        timeout.CancelAfter(TimeSpan.FromMilliseconds(options.ConnectionTimeoutMilliseconds));

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

    private static ConsoleCommandResult CreateApiErrorResult(
        string request,
        string userMessage,
        string technicalMessage)
    {
        return new ConsoleCommandResult
        {
            IsSuccess = false,
            CommandName = "api",
            RequestFrame = request,
            UserMessage = userMessage,
            TechnicalMessage = technicalMessage
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

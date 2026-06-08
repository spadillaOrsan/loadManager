using System.Diagnostics;
using System.Text;
using LoadManagerApi.Interfaces;
using LoadManagerApi.Models;

namespace LoadManagerApi.Middlewares;

public sealed class HttpTransactionLoggingMiddleware(
    RequestDelegate next,
    IAppLogService logService)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestBody = await ReadRequestBodyAsync(context.Request);
        var originalResponseBody = context.Response.Body;
        await using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await logService.WriteAsync(new ApiLogEntry
            {
                Level = "Info",
                Service = "LoadManagerApi.Http",
                Message = "Solicitud HTTP recibida.",
                Method = context.Request.Method,
                Url = GetRawUrl(context.Request),
                RequestBody = requestBody
            }, context.RequestAborted);

            await next(context);

            stopwatch.Stop();
            var responseBody = await ReadResponseBodyAsync(responseBuffer);
            await logService.WriteAsync(new ApiLogEntry
            {
                Level = context.Response.StatusCode >= 400 ? "Error" : "Success",
                Service = "LoadManagerApi.Http",
                Message = context.Response.StatusCode >= 400
                    ? "La solicitud HTTP termino con error."
                    : "Solicitud HTTP procesada correctamente.",
                Method = context.Request.Method,
                Url = GetRawUrl(context.Request),
                HttpStatus = context.Response.StatusCode,
                RequestBody = requestBody,
                ResponseBody = responseBody,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await logService.WriteAsync(new ApiLogEntry
            {
                Level = "Error",
                Service = "LoadManagerApi.Http",
                Message = "Excepcion no controlada durante la solicitud HTTP.",
                Method = context.Request.Method,
                Url = GetRawUrl(context.Request),
                HttpStatus = context.Response.StatusCode,
                RequestBody = requestBody,
                Exception = ex.ToString(),
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            }, CancellationToken.None);
            throw;
        }
        finally
        {
            responseBuffer.Position = 0;
            await responseBuffer.CopyToAsync(originalResponseBody);
            context.Response.Body = originalResponseBody;
        }
    }

    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is null or 0)
        {
            return null;
        }

        request.EnableBuffering();
        using var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }

    private static async Task<string?> ReadResponseBodyAsync(MemoryStream stream)
    {
        if (stream.Length == 0)
        {
            return null;
        }

        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        stream.Position = 0;
        return body;
    }

    private static string GetRawUrl(HttpRequest request)
    {
        return $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}{request.QueryString}";
    }
}

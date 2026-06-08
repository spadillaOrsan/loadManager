using LoadManagerApi.Interfaces;
using LoadManagerApi.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LoadManagerApi.Middlewares;

public sealed class ApiExceptionHandler(
    IAppLogService logService,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        await logService.WriteAsync(new ApiLogEntry
        {
            Level = "Error",
            Service = nameof(ApiExceptionHandler),
            Message = "Error no controlado al procesar la solicitud.",
            Method = httpContext.Request.Method,
            Url = $"{httpContext.Request.Path}{httpContext.Request.QueryString}",
            HttpStatus = StatusCodes.Status500InternalServerError,
            Exception = exception.ToString()
        }, CancellationToken.None);
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "No fue posible completar la operacion.",
                Detail = exception.Message
            }
        });
    }
}

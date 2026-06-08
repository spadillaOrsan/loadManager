using LoadManagerApi.Interfaces;
using LoadManagerApi.Services;
using LoadManagerApi.Middlewares;
using LoadManagerApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<IGasStationService, GasStationService>();
builder.Services.AddScoped<IDatabaseScriptService, DatabaseScriptService>();
builder.Services.Configure<FileLogOptions>(builder.Configuration.GetSection("FileLogs"));
builder.Services.AddSingleton<IAppLogService, AppLogService>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

app.UseMiddleware<HttpTransactionLoggingMiddleware>();
app.UseExceptionHandler();
app.MapControllers();

app.Run();

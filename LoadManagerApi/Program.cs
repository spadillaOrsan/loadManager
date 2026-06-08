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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "LoadManager API", Version = "v1" });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "LoadManager API v1");
    options.RoutePrefix = string.Empty;
});

app.UseMiddleware<HttpTransactionLoggingMiddleware>();
app.UseExceptionHandler();
app.MapControllers();

app.Run();

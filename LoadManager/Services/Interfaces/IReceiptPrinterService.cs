namespace LoadManager.Services.Interfaces;

public interface IReceiptPrinterService
{
    Task PrintHtmlAsync(string html, string jobName, CancellationToken cancellationToken = default);
}

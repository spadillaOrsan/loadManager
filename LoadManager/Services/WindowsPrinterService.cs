using LoadManager.Helpers;
using LoadManager.Models;
using LoadManager.Services.Interfaces;

#if ANDROID
using Android.Print;
using Microsoft.Maui.ApplicationModel;
using AWebView = Android.Webkit.WebView;
using AWebViewClient = Android.Webkit.WebViewClient;
#endif

namespace LoadManager.Services;

/// <summary>
/// Impresion por el dialogo del sistema (flujo original). En Android usa
/// PrintManager + WebView (nativo); en Windows regresa Handled=false con el HTML
/// para que la pagina abra el dialogo de impresion via window.print() y el usuario
/// elija cualquier impresora instalada (HP, PDF, etc.).
/// </summary>
public sealed class WindowsPrinterService : IPrinterService
{
    public async Task<PrintOutcome> PrintAsync(ReceiptPrintJob job, CancellationToken cancellationToken = default)
    {
        var html = TicketHtmlBuilder.Build(job.Record, job.TicketNumber);

        var handled = await PrintHtmlAsync(html, job.JobName, cancellationToken);
        return handled
            ? PrintOutcome.Ok("Impresora nativa")
            : PrintOutcome.Fallback("Dialogo de Windows", html);
    }

    /// <summary>
    /// Imprime el HTML mostrando el dialogo del sistema (selector de impresora).
    /// En Android usa PrintManager + WebView (nativo) y devuelve true (manejado).
    /// En Windows devuelve false para que el llamador use window.print() (JS).
    /// </summary>
    public Task<bool> PrintHtmlAsync(string html, string jobName, CancellationToken cancellationToken = default)
    {
#if ANDROID
        return MainThread.InvokeOnMainThreadAsync<bool>(() =>
        {
            try
            {
                PrintViaSystemDialog(html, jobName);
                return true;
            }
            catch
            {
                return false; // si algo falla, el llamador hara fallback a JS.
            }
        });
#else
        return Task.FromResult(false); // Windows: el llamador usa el dialogo del navegador (JS).
#endif
    }

#if ANDROID
    // Se retiene el WebView mientras carga; despues PrintManager mantiene la referencia.
    private static AWebView? printWebView;

    private static void PrintViaSystemDialog(string html, string jobName)
    {
        var activity = Platform.CurrentActivity;
        if (activity is null)
        {
            return;
        }

        var webView = new AWebView(activity);
        webView.Settings.JavaScriptEnabled = false;
        webView.SetWebViewClient(new PrintWebViewClient(jobName));
        printWebView = webView;

        webView.LoadDataWithBaseURL(null, html, "text/html", "UTF-8", null);
    }

    private sealed class PrintWebViewClient(string jobName) : AWebViewClient
    {
        public override void OnPageFinished(AWebView? view, string? url)
        {
            base.OnPageFinished(view, url);

            if (view is null)
            {
                return;
            }

            var activity = Platform.CurrentActivity;
            var printManager = activity?.GetSystemService(Android.Content.Context.PrintService) as PrintManager;
            if (printManager is null)
            {
                return;
            }

            var adapter = view.CreatePrintDocumentAdapter(jobName);
            var attributes = new PrintAttributes.Builder().Build();

            // Muestra el dialogo de impresion del sistema (selector de impresora).
            printManager.Print(jobName, adapter, attributes);

            // PrintManager ya tiene el adapter (que referencia al WebView); soltamos el nuestro.
            printWebView = null;
        }
    }
#endif
}

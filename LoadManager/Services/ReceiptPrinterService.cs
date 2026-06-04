using LoadManager.Services.Interfaces;

#if ANDROID
using Android.Content;
using Android.Print;
using Android.Webkit;
using Microsoft.Maui.ApplicationModel;
#endif

namespace LoadManager.Services;

public sealed class ReceiptPrinterService : IReceiptPrinterService
{
    public Task PrintHtmlAsync(string html, string jobName, CancellationToken cancellationToken = default)
    {
#if ANDROID
        return MainThread.InvokeOnMainThreadAsync(() => PrintAndroidAsync(html, jobName, cancellationToken));
#else
        return Task.CompletedTask;
#endif
    }

#if ANDROID
    private static Task PrintAndroidAsync(string html, string jobName, CancellationToken cancellationToken)
    {
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("No se encontro la actividad de Android para imprimir.");

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));

        var webView = new Android.Webkit.WebView(activity);
        webView.Settings.JavaScriptEnabled = false;
        webView.SetWebViewClient(new ReceiptPrintWebViewClient(webView, jobName, completion));
        webView.LoadDataWithBaseURL(null, html, "text/html", "UTF-8", null);

        return completion.Task;
    }

    private sealed class ReceiptPrintWebViewClient(
        Android.Webkit.WebView webView,
        string jobName,
        TaskCompletionSource completion) : WebViewClient
    {
        public override void OnPageFinished(Android.Webkit.WebView? view, string? url)
        {
            try
            {
                var printManager = (PrintManager?)webView.Context?.GetSystemService(Context.PrintService);
                if (printManager is null)
                {
                    completion.TrySetException(new InvalidOperationException("No se encontro el servicio de impresion de Android."));
                    return;
                }

                using var adapter = webView.CreatePrintDocumentAdapter(jobName);
                var attributes = new PrintAttributes.Builder().Build();
                printManager.Print(jobName, adapter, attributes);
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }
    }
#endif
}

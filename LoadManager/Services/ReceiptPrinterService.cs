using LoadManager.Services.Interfaces;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

#if ANDROID
using Android.Content;
using Android.OS;
using Microsoft.Maui.ApplicationModel;
#endif

namespace LoadManager.Services;

public sealed class ReceiptPrinterService : IReceiptPrinterService
{
    private const string PeripheralsPackage = "com.verifone.peripherals.service";
    private const string DirectPrintServiceClass = "com.verifone.peripherals.service.DirectPrintService";
    private const string DirectPrintAction = "com.verifone.intent.action.DIRECT_PRINT";
    private const string DirectPrintCategory = "com.verifone.intent.category.DIRECT_PRINT";
    private const string IDirectPrintDescriptor = "com.verifone.peripherals.IDirectPrintService";

    // AIDL transaction codes for IDirectPrintService
    private const int TransactionPrintString = 1;
    private const int TransactionPrintBitmap = 2;
    private const int TransactionPrintFileDescriptor = 3;

    public Task PrintHtmlAsync(string html, string jobName, CancellationToken cancellationToken = default)
    {
#if ANDROID
        return MainThread.InvokeOnMainThreadAsync(() => PrintAsync(html, jobName));
#else
        return Task.CompletedTask;
#endif
    }

#if ANDROID
    private static void PrintAsync(string html, string jobName)
    {
        var text = HtmlToText(html);
        var context = Android.App.Application.Context;

        var conn = new PrintServiceConnection(text);
        var intent = new Intent(DirectPrintAction);
        intent.SetClassName(PeripheralsPackage, DirectPrintServiceClass);
        intent.AddCategory(DirectPrintCategory);

        bool bound = context.BindService(intent, conn, Bind.AutoCreate);
        if (!bound)
        {
            // Fallback: try startService with extras
            intent.PutExtra("printData", Encoding.UTF8.GetBytes(text));
            intent.PutExtra("jobName", jobName);
            context.StartService(intent);
        }
    }

    private sealed class PrintServiceConnection(string text) : Java.Lang.Object, IServiceConnection
    {
        public void OnServiceConnected(ComponentName? name, IBinder? service)
        {
            if (service == null) return;
            TryPrintString(service, text);
        }

        public void OnServiceDisconnected(ComponentName? name) { }

        private static void TryPrintString(IBinder binder, string text)
        {
            var data = Parcel.Obtain();
            var reply = Parcel.Obtain();
            try
            {
                data!.WriteInterfaceToken(IDirectPrintDescriptor);
                data.WriteString(text);
                binder.Transact(TransactionPrintString, data, reply, 0);
                reply!.ReadException();
            }
            catch
            {
                TryPrintStringAlt(binder, text);
            }
            finally
            {
                data?.Recycle();
                reply?.Recycle();
            }
        }

        private static void TryPrintStringAlt(IBinder binder, string text)
        {
            // Try transaction code 3 as alternative
            var data = Parcel.Obtain();
            var reply = Parcel.Obtain();
            try
            {
                data!.WriteInterfaceToken(IDirectPrintDescriptor);
                data.WriteString(text);
                binder.Transact(3, data, reply, 0);
                reply!.ReadException();
            }
            catch { }
            finally
            {
                data?.Recycle();
                reply?.Recycle();
            }
        }
    }

    private static string HtmlToText(string html)
    {
        var block = Regex.Replace(html, @"<(div|p|br|tr|h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase);
        var plain = Regex.Replace(block, "<[^>]+>", string.Empty);
        plain = WebUtility.HtmlDecode(plain);

        var sb = new StringBuilder();
        foreach (var raw in plain.Split('\n'))
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            sb.AppendLine(line);
        }

        return sb.ToString();
    }
#endif
}

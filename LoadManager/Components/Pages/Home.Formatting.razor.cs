using Microsoft.JSInterop;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using LoadManager.Helpers;
using LoadManager.Models;
using LoadManager.Models.ViewModels;
using LoadManager.Services.Interfaces;

namespace LoadManager.Components.Pages;

public partial class Home
{
    private async Task CompleteDispatchAsync(string? finalResponseFrame = null)
    {
        if (isCompletingDispatch)
        {
            return;
        }

        isCompletingDispatch = true;

        try
        {
            var responseFrame = finalResponseFrame;
            if (string.IsNullOrWhiteSpace(responseFrame) && !string.IsNullOrWhiteSpace(selectedDispenser))
            {
                var result = await ConsoleClient.SendDispenserDetailAsync(
                    DispenserFrameHelper.ToCommandNumber(selectedDispenser),
                    pageCancellation.Token);

                responseFrame = result.ResponseFrame;
            }

            dispatchTicket = BuildDispatchTicket(responseFrame);
            isFueling = false;
            isWaitingForDispatchHoseHang = false;
            liveDispenserStatus = "2";
            DispatchTracker.ActiveDispenser = null;
            StopAuthorizationCountdown(clearAuthorization: false);
            StopFuelingMonitor();
            currentStep = 4;
            ShowToast("Despacho finalizado. Revise los datos del ticket.", true);
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ResetDispatchFlow()
    {
        StopAuthorizationCountdown();
        StopFuelingMonitor();
        ResetDispatchFormState();
    }

    private void ResetDispatchFormState()
    {
        currentStep = 2;
        dispatchFormStage = 1;
        selectedDispenser = null;
        selectedProductKey = null;
        selectedDispatchTypeId = null;
        amountValue = null;
        amountInputRenderKey++;
        productLoadMessage = null;
        hoseNumber = 1;
        lastResult = null;
        currentDispatchFolio = null;
        liveAmount = 0;
        isWaitingForDispatchHoseHang = false;
        isFueling = false;
        isCompletingDispatch = false;
        dispatchTicket = null;
        authorizedDispenserForCountdown = null;
        DispatchTracker.ActiveDispenser = null;
        countdownWarningShown = false;
        products.Clear();
    }

    private void ReturnToStart()
    {
        ResetDispatchFlow();
    }

    private async Task PrintTicketAsync()
    {
        if (dispatchTicket is null || currentDispatchFolio is not int folio)
        {
            ShowToast("No hay ticket para imprimir.", false);
            return;
        }

        isPrintingTicket = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            DispatchHistoryRecord? record = null;
            try
            {
                var records = await ConsoleClient.GetDispatchHistoryAsync(
                    folio.ToString(CultureInfo.InvariantCulture),
                    pageCancellation.Token);
                record = records.FirstOrDefault(item => item.Sequence == folio) ?? records.FirstOrDefault();
            }
            catch
            {
            }

            if (record is null || !record.IsClosed)
            {
                ShowToast("No se encontro transaccion o transaccion no cerrada.", false);
                return;
            }

            var ticketNumber = 0;
            try
            {
                ticketNumber = await ConsoleClient.RegisterImpressionAsync(folio, pageCancellation.Token);
            }
            catch (Exception impEx)
            {
                await Logger.WriteAsync(new AppLogEntry
                {
                    Level = "Error",
                    Service = "Despacho.ImprimirTicket",
                    Message = "No se pudo registrar la impresion en la API (se imprime sin numero de ticket).",
                    RequestBody = $"folio={folio}",
                    Exception = impEx.ToString()
                });
            }

            var outcome = await ReceiptPrinter.PrintReceiptAsync(record, ticketNumber, $"Ticket_{folio}");
            if (!outcome.Handled && outcome.Html is not null)
            {
                await JsRuntime.InvokeVoidAsync("uaacPrintHtml", outcome.Html);
            }

            if (!outcome.Success)
            {
                ShowToast(outcome.ErrorMessage ?? "No fue posible imprimir el ticket.", false);
                await Logger.WriteAsync(new AppLogEntry
                {
                    Level = "Error",
                    Service = "Despacho.ImprimirTicket",
                    Message = "Fallo al imprimir el ticket.",
                    RequestBody = $"folio={folio}",
                    Exception = $"{outcome.Route}: {outcome.ErrorMessage}"
                });
                return;
            }

            await Logger.WriteAsync(new AppLogEntry
            {
                Level = "Success",
                Service = "Despacho.ImprimirTicket",
                Message = $"Ticket enviado a impresion (ruta={outcome.Route}, ticket#={ticketNumber}).",
                RequestBody = $"folio={folio}"
            });
        }
        catch (Exception ex)
        {
            await Logger.WriteAsync(new AppLogEntry
            {
                Level = "Error",
                Service = "Despacho.ImprimirTicket",
                Message = "Fallo al imprimir el ticket.",
                RequestBody = $"folio={(currentDispatchFolio?.ToString() ?? "?")}",
                Exception = ex.ToString()
            });
            ShowToast($"No fue posible imprimir el ticket. {ex.Message}", false);
        }
        finally
        {
            isPrintingTicket = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private string GetAuthorizationStepTitle()
    {
        if (dispatchTicket is not null)
        {
            return "Ticket de despacho";
        }

        return isFueling ? "Despacho en curso" : string.Empty;
    }

    private string GetAuthorizationStepStatus()
    {
        if (dispatchTicket is not null)
        {
            return "Despacho finalizado";
        }

        if (isFueling)
        {
            return "Surtiendo";
        }

        return string.Empty;
    }

    private bool ShouldPromptForDispatchHoseHang()
    {
        if (SelectedDispatchType?.IsFull == true || !TryGetAmount(out var requestedAmount) || requestedAmount <= 0)
        {
            return false;
        }

        return SelectedDispatchType?.IsMoney == true
            ? liveAmount >= requestedAmount
            : liveLiters >= requestedAmount;
    }

    private string GetPrimaryLiveCounterLabel()
    {
        return SelectedDispatchType?.IsMoney == true
            ? "Importe surtido"
            : "Litros surtidos";
    }

    private string GetSecondaryLiveCounterLabel()
    {
        return SelectedDispatchType?.IsMoney == true
            ? "Litros equivalentes"
            : "Importe equivalente";
    }

    private int? FuelProgressPercent
    {
        get
        {
            if (!TryGetAmount(out var requested) || requested <= 0)
            {
                return null;
            }

            var current = SelectedDispatchType?.IsMoney == true ? liveAmount : liveLiters;
            var pct = (int)Math.Floor(current / requested * 100m);
            return Math.Clamp(pct, 0, 100);
        }
    }

    private string FuelTankColorClass => SelectedProduct?.CssClass ?? "fuel-magna";

    private string FormatPresetValue()
    {
        var requested = TryGetAmount(out var amount) ? amount : 0;
        return FormatLiveCounterValue(requested, SelectedDispatchType?.IsMoney == true);
    }

    private string FormatPrimaryLiveCounter()
    {
        var value = SelectedDispatchType?.IsMoney == true ? liveAmount : liveLiters;
        return FormatLiveCounterValue(value, SelectedDispatchType?.IsMoney == true);
    }

    private string FormatSecondaryLiveCounter()
    {
        var value = SelectedDispatchType?.IsMoney == true ? liveLiters : liveAmount;
        return FormatLiveCounterValue(value, SelectedDispatchType?.IsMoney != true);
    }

    private static string FormatLiveCounterValue(decimal value, bool isMoney)
    {
        var formatted = value.ToString("N2", CultureInfo.GetCultureInfo("en-US"));
        return isMoney
            ? $"<span class=\"live-currency-symbol\">$</span>{formatted}"
            : $"{formatted}<span class=\"live-counter-unit\">Lt</span>";
    }

    private static bool TryParseFrameDecimal(string? value, out decimal result)
    {
        result = 0;
        return !string.IsNullOrWhiteSpace(value) &&
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private static string GetProductPriceText(FuelProductViewModel product)
    {
        return product.Price.HasValue
            ? $"Precio {product.Price.Value.ToString("C2", CultureInfo.GetCultureInfo("es-MX"))}"
            : "Precio sin consultar";
    }

    private string GetDispenserClass(DispenserViewModel dispenser)
    {
        var classes = new List<string>();

        if (string.IsNullOrWhiteSpace(dispenser.LastResponse))
        {
            classes.Add("is-checking");
        }
        else if (dispenser.StatusCode == "2")
        {
            classes.Add("is-authorized");
        }
        else if (dispenser.StatusCode == "3")
        {
            classes.Add("is-hose-lifted");
        }
        else if (dispenser.StatusCode == "5")
        {
            classes.Add("is-busy");
        }
        else if (dispenser.IsAvailable)
        {
            classes.Add("is-available");
        }
        else
        {
            classes.Add("is-offline");
        }

        if (selectedDispenser == dispenser.Number)
        {
            classes.Add("is-selected");
        }

        return string.Join(" ", classes);
    }

    private static bool CanSelectDispenser(DispenserViewModel dispenser)
    {
        return dispenser.IsAvailable && !string.IsNullOrWhiteSpace(dispenser.LastResponse);
    }

    private string GetStepClass(int stepIndex)
    {
        if (stepIndex < currentStep)
        {
            return "is-complete";
        }

        if (stepIndex == currentStep)
        {
            return "is-active";
        }

        return string.Empty;
    }

    private string GetSelectedStatusClass()
    {
        if (SelectedDispenser is null)
        {
            return "is-checking";
        }

        if (SelectedDispenser.StatusCode == "3")
        {
            return "is-hose-lifted";
        }

        if (SelectedDispenser.StatusCode == "2")
        {
            return "is-checking";
        }

        return SelectedDispenser.IsAvailable ? "is-available" : "is-unavailable";
    }

 

    private string GetSelectionLoadingText()
    {
        return isLoadingDispensers
            ? $"Consultando {dispenserLabel} disponibles..."
            : "Consultando productos...";
    }

    private string GetProductSummary()
    {
        if (SelectedDispenser is null)
        {
            return $"Seleccione {dispenserLabel}";
        }

        if (!SelectedDispenser.IsAvailable)
        {
            return $"{dispenserLabel} no disponible";
        }

        if (products.Count == 0)
        {
            return "Sin productos";
        }

        return SelectedProduct?.Name ?? "Seleccione producto";
    }

    private string GetAmountSummary()
    {
        if (!CanEditSelectedDispenser)
        {
            return $"{dispenserLabel} no disponible";
        }

        return string.IsNullOrWhiteSpace(amountValue) ? "Capture importe" : amountValue;
    }

    private void ShowToast(string message, bool isSuccess)
    {
        ShowToast(message, isSuccess ? "is-success" : "is-error");
    }

    private void ShowToast(string message, string cssClass)
    {
        var version = ++toastVersion;
        toastMessage = message;
        toastCss = cssClass;
        _ = ClearToastAsync(version);
    }

    private void ShowInfoToast(string message)
    {
        ShowToast(message, "is-info");
    }

    private async Task ClearToastAsync(int version)
    {
        try
        {
            await Task.Delay(toastDurationMilliseconds, pageCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (version != toastVersion)
        {
            return;
        }

        toastMessage = null;
        await InvokeAsync(StateHasChanged);
    }

}

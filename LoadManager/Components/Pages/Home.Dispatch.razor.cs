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
    private async Task StartDispatchAsync()
    {
        if (!CanServe)
        {
            return;
        }

        dispatchCancellation = new CancellationTokenSource();
        isDispatching = true;
        StopAuthorizationCountdown();
        await InvokeAsync(StateHasChanged);

        try
        {
            if (!DevMode.BypassHoseLiftedBlock && SelectedDispenser?.StatusCode == "3")
            {
                await ShowHoseLiftedBlockAsync($"La manguera del {dispenserLabel} seleccionado esta descolgada. Cuelguela para poder cargar.", selectedDispenser);
                return;
            }

            if (DevMode.BypassSendAuthorization)
            {
                isDispatching = false;
                await RunSimulatedDispatchAsync();
                return;
            }

            if (!databaseIsReady)
            {
                ShowToast("La base de datos no ha sido validada. Presione comenzar nuevamente.", false);
                return;
            }

            if (!ConsoleClient.IsConnected)
            {
                ShowToast("No hay comunicacion activa con la consola. Presione comenzar nuevamente.", false);
                return;
            }

            if (!await ValidateSelectedDispenserForAuthorizationAsync(dispatchCancellation.Token))
            {
                return;
            }

            var settings = await SettingsProvider.GetSettingsAsync(dispatchCancellation.Token);
            var dispenser = int.Parse(DispenserFrameHelper.ToCommandNumber(selectedDispenser!), CultureInfo.InvariantCulture);
            var selectedFuel = SelectedProduct;
            if (selectedFuel is null)
            {
                ShowToast("Seleccione un producto para continuar.", false);
                return;
            }
            var amount = GetAmountOrZero();
            var loadType = SelectedDispatchType!.AuthorizationLoadType;
            var authorizationRequest = new FuelAuthorizationRequest
            {
                Tpv = settings.AppConfiguration.Tpv,
                TipoVenta = settings.AppConfiguration.TipoVenta,
                Dispensario = dispenser,
                Manguera = hoseNumber,
                Producto = selectedFuel.ProductCode,
                Usuario = settings.AppConfiguration.Usuario,
                TipoProgramado = loadType,
                Programado = amount
            };

            int folio;
            try
            {
                folio = await ConsoleClient.RegisterAuthorizationAsync(
                    authorizationRequest,
                    dispatchCancellation.Token);
            }
            catch (DispenserBusyException)
            {
                ShowToast($"El {dispenserLabel} {dispenser:00} ya fue autorizado por otro equipo.", false);
                ResetDispatchFormState();
                return;
            }

            currentDispatchFolio = folio;

            var interfaceType = settings.AppConfiguration.TipoInterfaz.Trim();
            var requestFrame = BuildAuthorizationFrame(settings.AppConfiguration.Tpv, dispenser, hoseNumber, amount, loadType, folio, interfaceType);
            ShowInfoToast("Enviando peticion...");
            await InvokeAsync(StateHasChanged);

            lastResult = await ConsoleClient.SendRawFrameAsync(requestFrame, dispatchCancellation.Token);
            var authorizationMessage = GetAuthorizationToastMessage(lastResult);
            ShowToast(authorizationMessage.Message, authorizationMessage.IsSuccess);

            if (DevMode.BypassAuthorizationResponse || IsAuthorizedResponse(lastResult))
            {
                await SendTotalizersForLogAsync(dispenser, dispatchCancellation.Token);
                StartAuthorizationCountdown(dispenser);
            }
        }
        catch (OperationCanceledException)
        {
            lastResult = new ConsoleCommandResult
            {
                IsSuccess = false,
                UserMessage = "Solicitud de servicio cancelada por el usuario."
            };
        }
        catch (HttpRequestException ex)
        {
            lastResult = new ConsoleCommandResult
            {
                IsSuccess = false,
                UserMessage = "El equipo se encuentra desconectado.",
                TechnicalMessage = ex.ToString()
            };
            ShowToast("El equipo se encuentra desconectado. Verifique la red e intente nuevamente.", false);
        }
        catch (IOException ex)
        {
            lastResult = new ConsoleCommandResult
            {
                IsSuccess = false,
                UserMessage = "Se perdio la conexion con la consola.",
                TechnicalMessage = ex.ToString()
            };
            ShowToast("Se perdio la conexion con la consola. Verifique la red e intente nuevamente.", false);
        }
        catch (Exception ex)
        {
            lastResult = new ConsoleCommandResult
            {
                IsSuccess = false,
                UserMessage = "No fue posible enviar la autorizacion.",
                TechnicalMessage = ex.ToString()
            };
            ShowToast("No fue posible enviar la autorizacion. Intente nuevamente.", false);
        }
        finally
        {
            isDispatching = false;
            dispatchCancellation?.Dispose();
            dispatchCancellation = null;
        }
    }

    private Task CancelDispatchAsync()
    {
        dispatchCancellation?.Cancel();
        return Task.CompletedTask;
    }

    private async Task CancelActiveDispatchManuallyAsync()
    {
        if (isCancelingAuthorization)
        {
            return;
        }

        cancelingAuthorizationTitle = "Cancelando carga";
        cancelingAuthorizationMessage = "Enviando cancelacion...";
        isCancelingAuthorization = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            simulatedDispatchCancellation?.Cancel();
            StopFuelingMonitor();
            StopAuthorizationCountdown(clearAuthorization: false);

            await DispatchTracker.CancelActiveDispatchAsync(pageCancellation.Token);

            ResetDispatchFormState();
            await RefreshDispenserSummaryAsync(pageCancellation.Token);
            ShowToast("Carga cancelada.", true);
        }
        finally
        {
            isCancelingAuthorization = false;
            cancelingAuthorizationTitle = "Cancelando autorizacion";
            cancelingAuthorizationMessage = "Consultando registros...";
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task RunSimulatedDispatchAsync()
    {
        var isMoney = SelectedDispatchType?.IsMoney == true;
        var isFull = SelectedDispatchType?.IsFull == true;
        var requested = GetAmountOrZero();
        var price = SelectedProduct?.Price ?? 22.50m;
        if (price <= 0)
        {
            price = 22.50m;
        }

        var targetPrimary = isFull
            ? (fullTankProgrammedAmount > 0 ? fullTankProgrammedAmount : 30m)
            : (requested > 0 ? requested : (isMoney ? 200m : 20m));

        var targetLiters = isMoney ? targetPrimary / price : targetPrimary;
        var targetAmount = isMoney ? targetPrimary : targetPrimary * price;

        currentDispatchFolio = Random.Shared.Next(1000, 9999);
        dispatchTicket = null;
        liveLiters = 0;
        liveAmount = 0;
        currentStep = 3;

        try
        {
            if (!DevMode.BypassFueling)
            {
                isFueling = false;
                remainingAuthorizationSeconds = authorizationCountdownSeconds;
                ShowInfoToast("Modo DEV: autorizacion simulada. Active 'Simular surtido' para ver el surtido.");
                await InvokeAsync(StateHasChanged);
                return;
            }

            isFueling = true;
            liveDispenserStatus = "Surtiendo";
            ShowInfoToast("Modo DEV: surtido simulado.");
            await InvokeAsync(StateHasChanged);

            simulatedDispatchCancellation = CancellationTokenSource.CreateLinkedTokenSource(pageCancellation.Token);

            const int steps = 40;
            for (var i = 1; i <= steps; i++)
            {
                var ratio = (decimal)i / steps;
                liveLiters = Math.Round(targetLiters * ratio, 3);
                liveAmount = Math.Round(targetAmount * ratio, 2);
                await InvokeAsync(StateHasChanged);
                await Task.Delay(150, simulatedDispatchCancellation.Token);
            }

            liveLiters = Math.Round(targetLiters, 3);
            liveAmount = Math.Round(targetAmount, 2);
            isFueling = false;

            var dispenser = int.Parse(DispenserFrameHelper.ToCommandNumber(selectedDispenser!), CultureInfo.InvariantCulture);
            SimulatedHistory.Add(new SimulatedDispatchRecord
            {
                Sequence = currentDispatchFolio.Value,
                Dispenser = dispenser,
                Hose = hoseNumber,
                Product = SelectedProduct?.ProductCode ?? 1,
                ProductDescription = SelectedProduct?.Name ?? "SUPREME+",
                DispatchTypeId = SelectedDispatchType?.Id ?? 0,
                ProgrammedAmount = targetPrimary,
                Amount = liveAmount,
                Liters = liveLiters,
                Price = price,
                CreatedAt = DateTime.Now
            });

            dispatchTicket = new DispatchTicketViewModel
            {
                Dispenser = selectedDispenser ?? "1",
                Hose = hoseNumber.ToString(CultureInfo.InvariantCulture),
                Product = SelectedProduct?.Name ?? "SUPREME+",
                ProductCode = SelectedProduct?.ProductCode.ToString(CultureInfo.InvariantCulture) ?? "1",
                DispatchType = SelectedDispatchType?.Description ?? "Importe",
                ProgrammedAmount = string.IsNullOrWhiteSpace(amountValue) ? FormatQuantity(targetPrimary) : amountValue,
                Price = price.ToString("N2", CultureInfo.GetCultureInfo("en-US")),
                Liters = liveLiters.ToString("N3", CultureInfo.GetCultureInfo("en-US")),
                Amount = liveAmount.ToString("N2", CultureInfo.GetCultureInfo("en-US")),
                Folio = $"DEV-{currentDispatchFolio}",
                RawResponse = "DEV simulado"
            };
            currentStep = 4;
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            simulatedDispatchCancellation?.Dispose();
            simulatedDispatchCancellation = null;
        }
    }

    private async Task<bool> ValidateSelectedDispenserForAuthorizationAsync(CancellationToken cancellationToken)
    {
        var dispenser = SelectedDispenser;
        if (dispenser is null)
        {
            ShowToast($"Seleccione un {dispenserLabel} para continuar.", false);
            return false;
        }

        ShowInfoToast($"Validando estatus del {dispenserLabel}...");

        var commandNumber = DispenserFrameHelper.ToCommandNumber(dispenser.Number);
        var result = await ConsoleClient.SendDispenserDetailAsync(commandNumber, cancellationToken);

        if (!result.IsSuccess)
        {
            ShowToast($"No fue posible validar el estatus del {dispenserLabel}.", false);
            return false;
        }

        ApplyDispenserDetailStatus(dispenser, result.ResponseFrame);

        if (dispenser.StatusCode == "3")
        {
            await ShowHoseLiftedBlockAsync($"La manguera del {dispenserLabel} seleccionado esta descolgada. Cuelguela para poder cargar.", dispenser.Number);
            return false;
        }

        if (!dispenser.IsAvailable)
        {
            ShowToast($"El {dispenserLabel} {dispenser.Number} no esta disponible para autorizar.", false);
            ResetDispatchFormState();
            return false;
        }

        return true;
    }

    private bool HasLiftedHose(string? dispenserNumber = null)
    {
        return dispensers.Any(dispenser =>
            dispenser.StatusCode == "3" &&
            (string.IsNullOrWhiteSpace(dispenserNumber) || dispenser.Number == dispenserNumber));
    }

    private string? GetFirstLiftedHoseDispenser()
    {
        return dispensers.FirstOrDefault(dispenser => dispenser.StatusCode == "3")?.Number;
    }

    private async Task<bool> HasLiftedHoseFromDetailAsync(CancellationToken cancellationToken)
    {
        foreach (var dispenser in dispensers)
        {
            if (!dispenser.IsAvailable)
            {
                continue;
            }

            var result = await ConsoleClient.SendDispenserDetailAsync(
                DispenserFrameHelper.ToCommandNumber(dispenser.Number),
                cancellationToken);

            if (!result.IsSuccess)
            {
                continue;
            }

            ApplyDispenserDetailStatus(dispenser, result.ResponseFrame);

            if (dispenser.StatusCode == "3")
            {
                blockedHoseDispenser = dispenser.Number;
                return true;
            }
        }

        return false;
    }

    private async Task ShowHoseLiftedBlockAsync(string message, string? dispenserNumber = null)
    {
        hoseLiftedBlockMessage = message;
        blockedHoseDispenser = dispenserNumber;
        isHoseLiftedBlockVisible = true;
        await InvokeAsync(StateHasChanged);

        if (!isWaitingForHoseRestored)
        {
            _ = WaitForHoseRestoredAsync(pageCancellation.Token);
        }
    }

    private async Task WaitForHoseRestoredAsync(CancellationToken cancellationToken)
    {
        isWaitingForHoseRestored = true;

        try
        {
            while (isHoseLiftedBlockVisible)
            {
                await Task.Delay(hoseRestorePollingMilliseconds, cancellationToken);
                await RefreshDispenserSummaryAsync(cancellationToken);

                if (!HasLiftedHose(blockedHoseDispenser))
                {
                    isHoseLiftedBlockVisible = false;
                    blockedHoseDispenser = null;
                    hoseLiftedBlockMessage = null;
                    ShowToast("Manguera colgada. Puede continuar.", true);
                    await InvokeAsync(StateHasChanged);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            isWaitingForHoseRestored = false;
        }
    }

    private static string BuildAuthorizationFrame(
        int tpv,
        int dispenser,
        int hose,
        decimal amount,
        int loadType,
        int folio,
        string productCode)
    {
        var formattedAmount = amount.ToString("0.##", CultureInfo.InvariantCulture);
        return $"AUTH|{tpv}-{dispenser}-{hose}-{formattedAmount}-{loadType}-{folio}-{formattedAmount}-{formattedAmount}-{productCode}";
    }

    private static (string Message, bool IsSuccess) GetAuthorizationToastMessage(ConsoleCommandResult result)
    {
        var responseFrame = result.ResponseFrame ?? string.Empty;
        if (responseFrame.Contains("NoAutorizado", StringComparison.OrdinalIgnoreCase) ||
            responseFrame.Contains("No autorizado", StringComparison.OrdinalIgnoreCase))
        {
            return ("Despacho no autorizado.", false);
        }

        if (responseFrame.Contains("Autorizado", StringComparison.OrdinalIgnoreCase))
        {
            return ("Despacho autorizado.", true);
        }

        if (!result.IsSuccess)
        {
            return (result.UserMessage, false);
        }

        return ("Peticion enviada correctamente.", result.IsSuccess);
    }

    private async Task SendTotalizersForLogAsync(int dispenser, CancellationToken cancellationToken)
    {
        var requestFrame = $"TOT|{dispenser.ToString(CultureInfo.InvariantCulture)}";
        await ConsoleClient.SendRawFrameAsync(requestFrame, cancellationToken);
    }

    private static bool IsAuthorizedResponse(ConsoleCommandResult result)
    {
        var responseFrame = result.ResponseFrame ?? string.Empty;
        if (responseFrame.Contains("NoAutorizado", StringComparison.OrdinalIgnoreCase) ||
            responseFrame.Contains("No autorizado", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return responseFrame.Contains("Autorizado", StringComparison.OrdinalIgnoreCase);
    }

}

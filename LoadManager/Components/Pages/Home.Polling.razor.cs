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
    private async Task RetryConnectionAsync()
    {
        await BeginDispatchFlowAsync();
    }

    private async Task RunDeviceNotAuthorizedCountdownAsync()
    {
        deviceAuthRemainingSeconds = 10;

        try
        {
            while (deviceAuthRemainingSeconds > 0)
            {
                await InvokeAsync(StateHasChanged);
                await Task.Delay(TimeSpan.FromSeconds(1), pageCancellation.Token);
                deviceAuthRemainingSeconds--;
            }

            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        CloseApplication();
    }

    private bool HasAnyDispenserCommunication()
    {
        return dispensers.Any(dispenser =>
            dispenser.StatusCode is "1" or "2" or "3" or "4" or "6");
    }

    private async Task RefreshDispenserSummaryAsync(CancellationToken cancellationToken)
    {
        if (DevMode.BypassDispenserCommunication)
        {
            return;
        }

        if (isRefreshingSummary)
        {
            return;
        }

        isRefreshingSummary = true;

        try
        {
            var result = await ConsoleClient.SendDispenserSummaryAsync(cancellationToken);

            if (!result.IsSuccess)
            {
                foreach (var dispenser in dispensers)
                {
                    ApplyDispenserStatusCode(dispenser, null);
                }

                return;
            }

            ApplyDispenserSummary(result.ResponseFrame, cancellationToken);
        }
        finally
        {
            isRefreshingSummary = false;
        }
    }

    private async Task RefreshConfiguredDispensersAsync()
    {
        if (isLoadingDispensers || isDispatching)
        {
            return;
        }

        isLoadingDispensers = true;

        try
        {
            var settings = await SettingsProvider.GetSettingsAsync(pageCancellation.Token);
            configuredDispenserCount = settings.AppConfiguration.DispenserCount;
            dispenserFillSequential = settings.AppConfiguration.DispenserFillSequential;
            dispenserNumbers = settings.AppConfiguration.DispenserNumbers;

            LoadConfiguredDispensers();
            await RefreshDispenserSummaryAsync(pageCancellation.Token);
            selectedDispenser = null;
            selectedProductKey = null;
            dispatchFormStage = 1;
            products.Clear();
            productLoadMessage = null;
            amountValue = null;
            lastResult = null;
            ShowToast("Se actualizo correctamente.", true);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            isLoadingDispensers = false;
        }
    }

    private void ApplyDispenserSummary(string? responseFrame, CancellationToken cancellationToken = default)
    {
        var statuses = DispenserFrameHelper.ParseDispenserSummary(responseFrame);

        foreach (var dispenser in dispensers)
        {
            var commandNumber = DispenserFrameHelper.ToCommandNumber(dispenser.Number);
            statuses.TryGetValue(commandNumber, out var statusCode);
            ApplyDispenserStatusCode(dispenser, statusCode);
            HandleSummaryStatusTransition(dispenser, statusCode);

            if (int.TryParse(commandNumber, out var dispenserNumber) &&
                int.TryParse(statusCode, out var parsedStatus))
            {
                _ = ConsoleClient.UpdateDispenserStatusAsync(dispenserNumber, parsedStatus, cancellationToken);
            }
        }
    }

    private void StartAuthorizationCountdown(int dispenser)
    {
        StopAuthorizationCountdown();
        StopFuelingMonitor();
        authorizedDispenserForCountdown = dispenser;
        DispatchTracker.ActiveDispenser = dispenser;
        isFueling = false;
        isCompletingDispatch = false;
        liveLiters = 0;
        liveAmount = 0;
        liveDispenserStatus = null;
        dispatchTicket = null;
        isWaitingForDispatchHoseHang = false;
        authorizationCountdownCancellation = CancellationTokenSource.CreateLinkedTokenSource(pageCancellation.Token);

        currentStep = 3;

        if (DevMode.ShouldShowCountdown)
        {
            remainingAuthorizationSeconds = authorizationCountdownSeconds;
            countdownWarningShown = false;
            _ = RunAuthorizationCountdownAsync(authorizationCountdownCancellation.Token);
        }
        else
        {
            _ = PollNoCountdownAsync(authorizationCountdownCancellation.Token);
        }
    }

    private void StopAuthorizationCountdown(bool clearAuthorization = true)
    {
        authorizationCountdownCancellation?.Cancel();
        authorizationCountdownCancellation?.Dispose();
        authorizationCountdownCancellation = null;
        if (clearAuthorization)
        {
            authorizedDispenserForCountdown = null;
            DispatchTracker.ActiveDispenser = null;
            isFueling = false;
            isCompletingDispatch = false;
            liveLiters = 0;
            liveAmount = 0;
            liveDispenserStatus = null;
            dispatchTicket = null;
            isWaitingForDispatchHoseHang = false;
        }

        countdownWarningShown = false;
    }

    private async Task RunAuthorizationCountdownAsync(CancellationToken cancellationToken)
    {
        using var pollCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pollTask = PollAuthorizedDispenserAsync(pollCancellation.Token);

        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(authorizationCountdownSeconds);
            remainingAuthorizationSeconds = authorizationCountdownSeconds;
            await InvokeAsync(StateHasChanged);

            while (true)
            {
                remainingAuthorizationSeconds = Math.Max(
                    0,
                    (int)Math.Ceiling((deadline - DateTime.UtcNow).TotalSeconds));
                await InvokeAsync(StateHasChanged);

                if (isFueling || dispatchTicket is not null || remainingAuthorizationSeconds <= 0)
                {
                    break;
                }

                var warningThreshold = Math.Min(
                    authorizationWarningSeconds,
                    Math.Max(1, authorizationCountdownSeconds / 4));
                if (!countdownWarningShown && remainingAuthorizationSeconds <= warningThreshold)
                {
                    countdownWarningShown = true;
                    await InvokeAsync(() => ShowInfoToast($"La carga se cancelara en {remainingAuthorizationSeconds} segundos si no inicia."));
                }

                var msToNextSecond = (int)((deadline - DateTime.UtcNow).TotalMilliseconds % 1000);
                await Task.Delay(msToNextSecond <= 0 ? 1000 : msToNextSecond, cancellationToken);
            }

            remainingAuthorizationSeconds = 0;
            await InvokeAsync(StateHasChanged);

            if (isFueling || dispatchTicket is not null)
            {
                return;
            }

            var dispenserToCancel = authorizedDispenserForCountdown;

            await InvokeAsync(() =>
            {
                ResetDispatchFormState();
                isCancelingAuthorization = true;
                StateHasChanged();
            });

            try
            {
                pollCancellation.Cancel();
                try
                {
                    await pollTask;
                }
                catch
                {
                }

                if (dispenserToCancel is int dispenser)
                {
                    try
                    {
                        await ConsoleClient.SendRawFrameAsync($"CAUTH|{dispenser.ToString(CultureInfo.InvariantCulture)}", pageCancellation.Token);
                    }
                    catch
                    {
                    }
                }

                await RefreshDispenserSummaryAsync(pageCancellation.Token);
            }
            finally
            {
                await InvokeAsync(() =>
                {
                    isCancelingAuthorization = false;
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            pollCancellation.Cancel();
            try
            {
                await pollTask;
            }
            catch
            {
            }
        }
    }

    private async Task PollAuthorizedDispenserAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && !isFueling && dispatchTicket is null)
            {
                await QueryAuthorizedDispenserDetailAsync(cancellationToken);

                if (isFueling || dispatchTicket is not null)
                {
                    await InvokeAsync(StateHasChanged);
                    break;
                }

                await Task.Delay(authorizationPollingMilliseconds, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PollNoCountdownAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && !isFueling && dispatchTicket is null)
            {
                await QueryAuthorizedDispenserDetailAsync(cancellationToken);

                if (isFueling || dispatchTicket is not null)
                {
                    await InvokeAsync(StateHasChanged);
                    break;
                }

                if (liveDispenserStatus == "2")
                {
                    await InvokeAsync(() => { ResetDispatchFlow(); StateHasChanged(); });
                    return;
                }

                await Task.Delay(authorizationPollingMilliseconds, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void StartFuelingMonitor()
    {
        StopFuelingMonitor();
        fuelingMonitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(pageCancellation.Token);
        _ = RunFuelingMonitorAsync(fuelingMonitorCancellation.Token);
    }

    private void StopFuelingMonitor()
    {
        fuelingMonitorCancellation?.Cancel();
        fuelingMonitorCancellation?.Dispose();
        fuelingMonitorCancellation = null;
    }

    private async Task RunFuelingMonitorAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (isFueling && currentStep == 3 && dispatchTicket is null)
            {
                await Task.Delay(fuelingPollingMilliseconds, cancellationToken);
                await QueryAuthorizedDispenserDetailAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task QueryAuthorizedDispenserDetailAsync(CancellationToken cancellationToken)
    {
        if (authorizedDispenserForCountdown is not int dispenser)
        {
            return;
        }

        var result = await ConsoleClient.SendDispenserDetailAsync(
            dispenser.ToString(CultureInfo.InvariantCulture),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return;
        }

        var dispenserNumber = DispenserFrameHelper.NormalizeDispenserNumber(dispenser.ToString(CultureInfo.InvariantCulture));
        var selected = dispensers.FirstOrDefault(item => item.Number == dispenserNumber);
        if (selected is not null)
        {
            ApplyDispenserDetailStatus(selected, result.ResponseFrame);
        }

        await HandleAuthorizationDetailAsync(result.ResponseFrame);
    }

    private async Task HandleAuthorizationDetailAsync(string? responseFrame)
    {
        if (currentStep != 3 || authorizedDispenserForCountdown is null)
        {
            return;
        }

        var detail = TryParseDispenserDetail(responseFrame);
        if (detail is null)
        {
            return;
        }

        liveDispenserStatus = detail.StatusCode;

        if (detail.Liters.HasValue)
        {
            liveLiters = detail.Liters.Value;
        }

        if (detail.Amount.HasValue)
        {
            liveAmount = detail.Amount.Value;
        }

        if (isFueling && ShouldPromptForDispatchHoseHang())
        {
            isWaitingForDispatchHoseHang = true;
            await InvokeAsync(StateHasChanged);
        }

        if (detail.StatusCode == "6")
        {
            if (!isFueling)
            {
                isFueling = true;
                StopAuthorizationCountdown(clearAuthorization: false);
                StartFuelingMonitor();
                ShowInfoToast("Despacho iniciado. Mostrando litros en tiempo real.");
            }

            await InvokeAsync(StateHasChanged);
            return;
        }

        if (isFueling && detail.StatusCode == "2")
        {
            if (!isWaitingForDispatchHoseHang)
            {
                isWaitingForDispatchHoseHang = true;
                await InvokeAsync(StateHasChanged);
                return;
            }

            await CompleteDispatchAsync(responseFrame);
        }
    }

    private void HandleSummaryStatusTransition(DispenserViewModel dispenser, string? statusCode)
    {
        if (currentStep != 3 ||
            !isFueling ||
            statusCode != "2" ||
            selectedDispenser != dispenser.Number)
        {
            return;
        }

        _ = CompleteDispatchAsync();
    }

    private void AddOrUpdateDispenser(string number, string? statusCode)
    {
        var dispenser = dispensers.FirstOrDefault(item => item.Number == number);

        if (dispenser is null)
        {
            dispenser = new DispenserViewModel
            {
                Number = number,
                StatusText = "Revisando",
                ImagePath = CheckingImage
            };

            dispensers.Add(dispenser);
            dispensers.Sort((left, right) => string.Compare(left.Number, right.Number, StringComparison.Ordinal));
        }

        if (statusCode is not null)
        {
            ApplyDispenserStatusCode(dispenser, statusCode);
        }
    }

    private static void ApplyDispenserStatusCode(DispenserViewModel dispenser, string? statusCode)
    {
        dispenser.LastResponse = statusCode;

        if (statusCode == "1")
        {
            dispenser.StatusCode = statusCode;
            dispenser.IsAvailable = true;
            dispenser.StatusText = "Libre";
            dispenser.ImagePath = AvailableImage;
            return;
        }

        if (statusCode == "2")
        {
            dispenser.StatusCode = statusCode;
            dispenser.IsAvailable = true;
            dispenser.StatusText = "Disponible";
            dispenser.ImagePath = CheckingImage;
            return;
        }

        if (statusCode == "3")
        {
            dispenser.StatusCode = statusCode;
            dispenser.IsAvailable = true;
            dispenser.StatusText = "Manguera levantada";
            dispenser.ImagePath = HoseLiftedImage;
            return;
        }

        if (statusCode == "4")
        {
            dispenser.StatusCode = statusCode;
            dispenser.IsAvailable = false;
            dispenser.StatusText = "Sirviendo";
            dispenser.ImagePath = "images/dispensers/sirviendo.png";
            return;
        }

        if (statusCode == "5")
        {
            dispenser.StatusCode = statusCode;
            dispenser.IsAvailable = false;
            dispenser.StatusText = "Surtiendo";
            dispenser.ImagePath = "images/dispensers/sirviendo.png";
            return;
        }

        if (statusCode == "6")
        {
            dispenser.StatusCode = statusCode;
            dispenser.IsAvailable = false;
            dispenser.StatusText = "Surtiendo";
            dispenser.ImagePath = "images/dispensers/sirviendo.png";
            return;
        }

        dispenser.StatusCode = statusCode;
        dispenser.LastResponse = string.IsNullOrWhiteSpace(statusCode) ? "sin-com" : statusCode;
        dispenser.IsAvailable = false;
        dispenser.StatusText = "Sin comunicacion";
        dispenser.ImagePath = OfflineImage;
    }

    private static void ApplyDispenserDetailStatus(DispenserViewModel dispenser, string? responseFrame)
    {
        dispenser.LastResponse = responseFrame;

        var detailStatus = TryParseDispenserDetailStatus(responseFrame);
        if (!string.IsNullOrWhiteSpace(detailStatus))
        {
            ApplyDispenserStatusCode(dispenser, detailStatus);
            return;
        }

        if (string.IsNullOrWhiteSpace(responseFrame))
        {
            ApplyDispenserStatusCode(dispenser, null);
            return;
        }

        if (responseFrame.Contains("Sirviendo", StringComparison.OrdinalIgnoreCase))
        {
            dispenser.IsAvailable = false;
            dispenser.StatusText = "Sirviendo";
            dispenser.StatusCode = "4";
            dispenser.ImagePath = "images/dispensers/sirviendo.png";
        }
        else if (responseFrame.Contains("Reservado", StringComparison.OrdinalIgnoreCase))
        {
            dispenser.IsAvailable = false;
            dispenser.StatusText = "Reservado";
            dispenser.ImagePath = "images/dispensers/reservado.png";
        }
        else if (responseFrame.Contains("Llamando", StringComparison.OrdinalIgnoreCase))
        {
            dispenser.StatusCode = "3";
            dispenser.IsAvailable = true;
            dispenser.StatusText = "Manguera levantada";
            dispenser.ImagePath = HoseLiftedImage;
        }
        else if (responseFrame.StartsWith("R-TDE|", StringComparison.OrdinalIgnoreCase) ||
            responseFrame.StartsWith("RTDE|", StringComparison.OrdinalIgnoreCase))
        {
            dispenser.IsAvailable = true;
            dispenser.StatusText = "Disponible";
            dispenser.ImagePath = AvailableImage;
        }
    }

    private static string? TryParseDispenserDetailStatus(string? responseFrame)
    {
        return TryParseDispenserDetail(responseFrame)?.StatusCode;
    }

    private static DispenserDetailFrame? TryParseDispenserDetail(string? responseFrame)
    {
        if (string.IsNullOrWhiteSpace(responseFrame))
        {
            return null;
        }

        var payload = responseFrame.StartsWith("R-TDE|", StringComparison.OrdinalIgnoreCase)
            ? responseFrame["R-TDE|".Length..]
            : responseFrame.StartsWith("RTDE|", StringComparison.OrdinalIgnoreCase)
                ? responseFrame["RTDE|".Length..]
                : responseFrame;

        var firstBlock = payload.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        var values = firstBlock?.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (values is null || values.Length < 2)
        {
            return null;
        }

        decimal? price = null;
        if (values.Length >= 4 &&
            TryParseFrameDecimal(values[3], out var parsedPrice))
        {
            price = parsedPrice;
        }

        decimal? liters = null;
        if (values.Length >= 5 &&
            TryParseFrameDecimal(values[4], out var parsedLiters))
        {
            liters = parsedLiters;
        }

        decimal? amount = null;
        if (values.Length >= 6 &&
            TryParseFrameDecimal(values[5], out var parsedAmount))
        {
            amount = parsedAmount;
        }

        var folio = values.Length >= 8 ? values[7] : string.Empty;

        return new DispenserDetailFrame(values[0], values[1], values.ElementAtOrDefault(2) ?? string.Empty, price, liters, amount, folio);
    }

    private DispatchTicketViewModel BuildDispatchTicket(string? responseFrame)
    {
        var detail = TryParseDispenserDetail(responseFrame);
        var selectedFuel = SelectedProduct;
        var selectedType = SelectedDispatchType;

        return new DispatchTicketViewModel
        {
            Dispenser = detail?.Dispenser ?? selectedDispenser ?? string.Empty,
            Hose = hoseNumber.ToString(CultureInfo.InvariantCulture),
            Product = selectedFuel?.Name ?? "Sin producto",
            ProductCode = detail?.ProductCode ?? selectedFuel?.ProductCode.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            DispatchType = selectedType?.Description ?? "Sin tipo",
            ProgrammedAmount = string.IsNullOrWhiteSpace(amountValue) ? "0.00" : amountValue,
            Price = FormatTicketDecimal(detail?.Price),
            Liters = FormatTicketDecimal(detail?.Liters),
            Amount = FormatTicketDecimal(detail?.Amount),
            Folio = !string.IsNullOrWhiteSpace(detail?.Folio)
                ? detail.Folio
                : currentDispatchFolio?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            RawResponse = responseFrame ?? string.Empty
        };
    }

    private static string FormatTicketDecimal(decimal? value)
    {
        return value.HasValue
            ? value.Value.ToString("N3", CultureInfo.GetCultureInfo("en-US"))
            : "0.000";
    }

    private sealed record DispenserDetailFrame(
        string Dispenser,
        string StatusCode,
        string ProductCode,
        decimal? Price,
        decimal? Liters,
        decimal? Amount,
        string Folio);


}

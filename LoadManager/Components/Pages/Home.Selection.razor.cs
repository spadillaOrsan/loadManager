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
    private async Task LoadDispatchTypesAsync()
    {
        dispatchTypes.Clear();

        var databaseTypes = await ConsoleClient.GetDispatchTypesAsync(pageCancellation.Token);
        foreach (var type in databaseTypes)
        {
            dispatchTypes.Add(new DispatchTypeViewModel(type.Id, type.Description));
        }

        if (dispatchTypes.Count == 0)
        {
            ShowToast("No se encontraron tipos de despacho activos.", false);
        }
    }

    private async Task BeginDispatchFlowAsync()
    {
        if (isStartingFlow)
        {
            return;
        }

        isStartingFlow = true;
        connectionBlocked = false;
        StopAuthorizationCountdown();
        databaseIsReady = false;
        startupStatusMessage = "Validando configuracion...";
        await InvokeAsync(StateHasChanged);

        try
        {
            var currentSettings = await SettingsProvider.GetSettingsAsync(pageCancellation.Token);

            if (!DevMode.BypassTcpConnection && !DevMode.BypassDatabase && !HasRequiredConfiguration(currentSettings))
            {
                startupStatusMessage = "Configuracion incompleta.";
                connectionBlockedMessage = "Capture los datos de configuracion (IP, puerto e interfaz, URL del servicio).";
                connectionBlocked = true;
                return;
            }

            startupStatusMessage = "Validando TCP/IP y base de datos...";
            await InvokeAsync(StateHasChanged);

            if (DevMode.BypassDatabase)
            {
                databaseIsReady = true;
            }
            else
            {
                var databaseResult = await ConsoleClient.CheckDatabaseConnectionAsync(pageCancellation.Token);
                ConnectionState.SetDatabaseResult(databaseResult);

                if (!databaseResult.IsSuccess)
                {
                    if (ConsoleClient.IsConnected)
                    {
                        await ConsoleClient.DisconnectAsync();
                    }

                    startupStatusMessage = "Sin comunicacion.";
                    connectionBlockedMessage = "Revise la conexion con la base de datos (BD).";
                    connectionBlocked = true;
                    return;
                }

                databaseIsReady = true;
            }

            if (!DevMode.BypassTcpConnection)
            {
                var tcpResult = ConsoleClient.IsConnected
                    ? new ConsoleAvailabilityResult { IsAvailable = true, UserMessage = "La consola ya esta conectada." }
                    : await ConsoleClient.ConnectAsync(pageCancellation.Token);

                if (!tcpResult.IsAvailable)
                {
                    databaseIsReady = false;
                    startupStatusMessage = "Sin comunicacion.";
                    connectionBlockedMessage = "Revise la conexion TCP/IP.";
                    connectionBlocked = true;
                    return;
                }
            }

            isLoadingDispensers = true;
            isLoadingDispatchTypes = true;
            startupStatusMessage = $"Validando {dispenserLabel}...";
            await InvokeAsync(StateHasChanged);

            try
            {
                configuredDispenserCount = currentSettings.AppConfiguration.DispenserCount;
                dispenserFillSequential = currentSettings.AppConfiguration.DispenserFillSequential;
                dispenserNumbers = currentSettings.AppConfiguration.DispenserNumbers;

                if (DevMode.BypassDispenserCommunication)
                {
                    LoadMockDispensers();
                }
                else
                {
                    LoadConfiguredDispensers();
                    await RefreshDispenserSummaryAsync(pageCancellation.Token);

                    if (!HasAnyDispenserCommunication())
                    {
                        startupStatusMessage = "Sin comunicacion.";
                        connectionBlockedMessage = $"Revise la conexion de los {dispenserLabel}.";
                        connectionBlocked = true;
                        return;
                    }
                }

                blockedHoseDispenser = DevMode.BypassHoseLiftedBlock ? null : GetFirstLiftedHoseDispenser();

                startupStatusMessage = "Consultando tipos de despacho...";
                await InvokeAsync(StateHasChanged);

                if (DevMode.BypassDispatchTypesAndProducts)
                {
                    LoadMockDispatchTypes();
                }
                else
                {
                    await LoadDispatchTypesAsync();
                }

                dispatchFormStage = 1;
                currentStep = 2;
            }
            finally
            {
                isLoadingDispensers = false;
                isLoadingDispatchTypes = false;
            }

            startupStatusMessage = null;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            databaseIsReady = false;
            startupStatusMessage = "No hay comunicacion.";
            connectionBlockedMessage = $"No hay comunicacion con la base de datos o la interfaz TCP/IP. {ex.Message}";
            connectionBlocked = true;
        }
        finally
        {
            isStartingFlow = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private List<int> GetConfiguredDispenserNumbers()
    {
        if (!dispenserFillSequential && !string.IsNullOrWhiteSpace(dispenserNumbers))
        {
            var list = dispenserNumbers
                .Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => int.TryParse(part, out var n) ? n : 0)
                .Where(n => n > 0)
                .Distinct()
                .ToList();

            if (list.Count > 0)
            {
                return list;
            }
        }

        var count = configuredDispenserCount > 0 ? configuredDispenserCount : 4;
        return Enumerable.Range(1, count).ToList();
    }

    private void LoadMockDispensers()
    {
        dispensers.Clear();

        foreach (var number in GetConfiguredDispenserNumbers())
        {
            var dispenser = new DispenserViewModel
            {
                Number = DispenserFrameHelper.NormalizeDispenserNumber(number.ToString(CultureInfo.InvariantCulture))
            };
            ApplyDispenserStatusCode(dispenser, "1");
            dispensers.Add(dispenser);
        }

        dispensers.Sort((left, right) => string.Compare(left.Number, right.Number, StringComparison.Ordinal));
    }

    private void LoadMockDispatchTypes()
    {
        dispatchTypes.Clear();
        dispatchTypes.Add(new DispatchTypeViewModel(1, "Importe"));
        dispatchTypes.Add(new DispatchTypeViewModel(2, "Litros"));
        dispatchTypes.Add(new DispatchTypeViewModel(3, "Lleno"));
    }

    private void LoadMockProducts()
    {
        products.Clear();
        productLoadMessage = null;
        products.Add(new FuelProductViewModel("1-1", ProductNameHelper.ResolveDisplayName("SUPREME+", productNameConfig),
            FuelProductHelper.GetCssClass("SUPREME"), FuelProductHelper.GetImagePath("SUPREME"), 1,
            FuelProductHelper.GetAuthorizationProductCode("SUPREME", 1), 1, 24.50m));
        products.Add(new FuelProductViewModel("1-2", ProductNameHelper.ResolveDisplayName("EXTRA", productNameConfig),
            FuelProductHelper.GetCssClass("EXTRA"), FuelProductHelper.GetImagePath("EXTRA"), 2,
            FuelProductHelper.GetAuthorizationProductCode("EXTRA", 2), 1, 22.30m));
        products.Add(new FuelProductViewModel("2-3", ProductNameHelper.ResolveDisplayName("DIESEL", productNameConfig),
            FuelProductHelper.GetCssClass("DIESEL"), FuelProductHelper.GetImagePath("DIESEL"), 3,
            FuelProductHelper.GetAuthorizationProductCode("DIESEL", 3), 2, 23.10m));
    }

    private void LoadConfiguredDispensers()
    {
        dispensers.Clear();

        foreach (var number in GetConfiguredDispenserNumbers())
        {
            AddOrUpdateDispenser(
                DispenserFrameHelper.NormalizeDispenserNumber(number.ToString(CultureInfo.InvariantCulture)),
                statusCode: null);
        }
    }

    private async Task RefreshSelectedDispenserDetailAsync(CancellationToken cancellationToken)
    {
        var dispenser = SelectedDispenser;
        if (dispenser is null)
        {
            return;
        }

        var result = await ConsoleClient.SendDispenserDetailAsync(DispenserFrameHelper.ToCommandNumber(dispenser.Number), cancellationToken);

        if (!result.IsSuccess)
        {
            return;
        }

        ApplyDispenserDetailStatus(dispenser, result.ResponseFrame);
        await HandleAuthorizationDetailAsync(result.ResponseFrame);
    }

    private async Task SelectDispenserAsync(DispenserViewModel dispenser)
    {
        if (!CanSelectDispenser(dispenser))
        {
            return;
        }

        selectedDispenser = dispenser.Number;
        selectedProductKey = null;
        amountValue = null;
        productLoadMessage = null;
        hoseNumber = 1;
        lastResult = null;
        isFueling = false;
        isCompletingDispatch = false;
        liveLiters = 0;
        liveAmount = 0;
        liveDispenserStatus = null;
        dispatchTicket = null;

        if (SelectedDispatchType?.IsFull == true)
        {
            amountValue = FormatQuantity(fullTankProgrammedAmount);
        }

        isSelectingDispenser = true;

        try
        {
            await LoadProductsForSelectedDispenserAsync(pageCancellation.Token);
            dispatchFormStage = 2;
        }
        finally
        {
            isSelectingDispenser = false;
        }
    }

    private async Task LoadProductsForSelectedDispenserAsync(CancellationToken cancellationToken)
    {
        products.Clear();
        productLoadMessage = null;

        if (DevMode.BypassDispatchTypesAndProducts)
        {
            LoadMockProducts();
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedDispenser) ||
            !int.TryParse(DispenserFrameHelper.ToCommandNumber(selectedDispenser), out var dispenserNumber))
        {
            return;
        }

        var dispenserProducts = await ConsoleClient.GetDispenserProductsAsync(dispenserNumber, cancellationToken);
        foreach (var product in dispenserProducts)
        {
            products.Add(new FuelProductViewModel(
                Key: $"{product.Hose}-{product.ProductId}",
                Name: ProductNameHelper.ResolveDisplayName(product.Description, productNameConfig),
                CssClass: FuelProductHelper.GetCssClass(product.Description),
                ImagePath: FuelProductHelper.GetImagePath(product.Description),
                ProductCode: product.ProductId,
                AuthorizationCode: FuelProductHelper.GetAuthorizationProductCode(product.Description, product.ProductId),
                Hose: product.Hose,
                Price: product.Price));
        }

        if (products.Count == 0)
        {
            productLoadMessage = $"No se encontraron productos activos para el {dispenserLabel} seleccionado.";
            ShowToast(productLoadMessage, false);
        }
    }

    private void SelectProduct(FuelProductViewModel product)
    {
        selectedProductKey = product.Key;
        hoseNumber = product.Hose;
        dispatchFormStage = 3;
    }

    private void SelectDispatchType(DispatchTypeViewModel dispatchType)
    {
        selectedDispatchTypeId = dispatchType.Id;
        if (dispatchType.IsFull)
        {
            amountValue = FormatQuantity(fullTankProgrammedAmount);
            return;
        }

        amountValue = null;
    }

    private void SelectDispatchTypeAndAdvance(DispatchTypeViewModel dispatchType)
    {
        SelectDispatchType(dispatchType);
        amountInputRenderKey++;
        dispatchFormStage = 4;
    }

    private void ClearAmountInput()
    {
        amountValue = null;
        amountInputRenderKey++;
    }

    private void GoBack()
    {
        if (currentStep == 3)
        {
            currentStep = 2;
            dispatchFormStage = 4;
            return;
        }

        if (currentStep == 2 && dispatchFormStage > 1)
        {
            dispatchFormStage--;
        }
    }


    private void SelectQuickAmount(int preset)
    {
        amountValue = FormatQuantity(preset);
        amountInputRenderKey++;
    }

    private string GetQuickAmountLabel(int preset) =>
        SelectedDispatchType?.IsMoney == true
            ? $"${preset.ToString(CultureInfo.InvariantCulture)}"
            : $"{preset.ToString(CultureInfo.InvariantCulture)}Lt";

    private void OnAmountChanged(ChangeEventArgs args)
    {
        var rawValue = args.Value?.ToString();
        var sanitizedValue = SanitizeAmountInput(rawValue);
        amountValue = sanitizedValue;

        if (!string.Equals(rawValue, sanitizedValue, StringComparison.Ordinal))
        {
            amountInputRenderKey++;
        }

        if (string.IsNullOrWhiteSpace(amountValue))
        {
            return;
        }

        if (DevMode.BypassAmountLimits)
        {
            return;
        }

        if ((SelectedDispatchType?.IsVolume == true || SelectedDispatchType?.IsFull == true) &&
            TryGetAmount(out var amount) &&
            amount > litersLimit)
        {
            amountValue = FormatQuantity(litersLimit);
            amountInputRenderKey++;
        }

        if (SelectedDispatchType?.IsMoney == true &&
            TryGetAmount(out var moneyAmount) &&
            moneyAmount > amountLimit)
        {
            amountValue = FormatQuantity(amountLimit);
            amountInputRenderKey++;
        }
    }

    private void OnHoseChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var parsed) && parsed > 0)
        {
            hoseNumber = parsed;
        }
    }

    private void FormatAmount()
    {
        TouchKeyboardHelper.Hide();

        if (!TryGetAmount(out var amount))
        {
            amountValue = null;
            return;
        }

        if (amount <= 0)
        {
            amountValue = null;
            ShowToast("Capture una cantidad mayor a 0.", false);
            return;
        }

        if (!DevMode.BypassAmountLimits)
        {
            if ((SelectedDispatchType?.IsVolume == true || SelectedDispatchType?.IsFull == true) &&
                amount > litersLimit)
            {
                amount = litersLimit;
                ShowToast($"El limite maximo de litros es {FormatQuantity(litersLimit)}.", false);
            }

            if (SelectedDispatchType?.IsMoney == true && amount > amountLimit)
            {
                amount = amountLimit;
                ShowToast($"El limite maximo de importe es {FormatQuantity(amountLimit)}.", false);
            }
        }

        amountValue = FormatQuantity(amount);
    }

    private bool TryGetAmount(out decimal amount)
    {
        var normalized = amountValue?.Replace(",", string.Empty);
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    private string? SanitizeAmountInput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var sanitized = new string(value
            .Where(character => char.IsDigit(character) || character is '.' or ',')
            .ToArray());

        sanitized = sanitized.Replace(",", string.Empty);
        var firstDotIndex = sanitized.IndexOf('.');
        if (firstDotIndex >= 0)
        {
            var whole = sanitized[..firstDotIndex];
            var decimals = sanitized[(firstDotIndex + 1)..].Replace(".", string.Empty);
            if (decimals.Length > quantityDecimals)
            {
                decimals = decimals[..quantityDecimals];
            }

            sanitized = $"{whole}.{decimals}";
        }

        if (sanitized.Length > 12)
        {
            sanitized = sanitized[..12];
        }

        return sanitized;
    }

    private string FormatQuantity(decimal amount)
    {
        var decimals = Math.Clamp(quantityDecimals, 0, 6);
        return amount.ToString($"N{decimals}", CultureInfo.GetCultureInfo("en-US"));
    }

    private decimal GetAmountOrZero()
    {
        return TryGetAmount(out var amount) ? amount : 0;
    }

}

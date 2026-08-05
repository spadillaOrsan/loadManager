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
    private const string CheckingImage = "images/dispensers/inactivo.png";
    private const string AvailableImage = "images/dispensers/autorizado.png";
    private const string HoseLiftedImage = "images/dispensers/llamando.png";
    private const string OfflineImage = "images/dispensers/fuera-de-linea.png";
    private readonly CancellationTokenSource pageCancellation = new();
    private CancellationTokenSource? dispatchCancellation;
    private CancellationTokenSource? authorizationCountdownCancellation;
    private CancellationTokenSource? fuelingMonitorCancellation;
    private CancellationTokenSource? simulatedDispatchCancellation;
    private bool isRefreshingSummary;
    private bool isDispatching;
    private bool isCancelingAuthorization;
    private string cancelingAuthorizationTitle = "Cancelando autorizacion";
    private string cancelingAuthorizationMessage = "Consultando registros...";
    private bool isLoadingDispensers;
    private bool isSelectingDispenser;
    private bool isStartingFlow;
    private bool isLoadingDispatchTypes;
    private bool databaseIsReady;
    private int currentStepValue = 2;

    private int currentStep
    {
        get => currentStepValue;
        set
        {
            currentStepValue = value;
            HeaderState.Refresh();
        }
    }
    private bool connectionBlocked;
    private string connectionBlockedMessage = "No hay comunicacion con la base de datos o la interfaz TCP/IP.";
    private bool deviceNotAuthorized;
    private int deviceAuthRemainingSeconds = 10;
    private string? selectedDispenser;
    private string? selectedProductKey;
    private int? selectedDispatchTypeId;
    private int dispatchFormStageValue = 1;

    private int dispatchFormStage
    {
        get => dispatchFormStageValue;
        set
        {
            dispatchFormStageValue = value;
            HeaderState.Refresh();
        }
    }
    private string? amountValue;
    private int amountInputRenderKey;
    private static readonly int[] QuickAmountPresets = [50, 100, 200, 500, 800, 1000];
    private string? productLoadMessage;
    private decimal amountLimit;
    private decimal litersLimit;
    private decimal fullTankProgrammedAmount;
    private int quantityDecimals;
    private int authorizationCountdownSeconds;
    private int authorizationWarningSeconds;
    private int authorizationPollingMilliseconds;
    private int fuelingPollingMilliseconds;
    private int hoseRestorePollingMilliseconds;
    private int toastDurationMilliseconds;
    private int configuredDispenserCount;
    private bool dispenserFillSequential = true;
    private string dispenserNumbers = string.Empty;
    private string dispenserLabel = "Dispensario";
    private AppConfigurationOptions productNameConfig = new();
    private int remainingAuthorizationSeconds;
    private int? authorizedDispenserForCountdown;
    private bool countdownWarningShown;
    private bool isFueling;
    private bool isCompletingDispatch;
    private bool isHoseLiftedBlockVisible;
    private bool isWaitingForHoseRestored;
    private bool isWaitingForDispatchHoseHang;
    private decimal liveLiters;
    private decimal liveAmount;
    private string? liveDispenserStatus;
    private string? hoseLiftedBlockMessage;
    private string? blockedHoseDispenser;
    private DispatchTicketViewModel? dispatchTicket;
    private int hoseNumber = 1;
    private string? toastMessage;
    private string? startupStatusMessage;
    private string toastCss = "is-success";
    private int toastVersion;
    private ConsoleCommandResult? lastResult;
    private int? currentDispatchFolio;
    private readonly List<DispenserViewModel> dispensers = [];
    private readonly List<DispatchStep> steps =
    [
        new(2, "Instrucciones"),
        new(3, "Despachar"),
        new(4, "Ticket")
    ];

    private readonly List<FuelProductViewModel> products = [];
    private readonly List<DispatchTypeViewModel> dispatchTypes = [];

    private int AvailableDispensersCount => dispensers.Count(dispenser => dispenser.IsAvailable);

    private DispenserViewModel? SelectedDispenser =>
        dispensers.FirstOrDefault(dispenser => dispenser.Number == selectedDispenser);

    private FuelProductViewModel? SelectedProduct =>
        products.FirstOrDefault(product => product.Key == selectedProductKey);

    private DispatchTypeViewModel? SelectedDispatchType =>
        dispatchTypes.FirstOrDefault(dispatchType => dispatchType.Id == selectedDispatchTypeId);

    private IEnumerable<int> VisibleQuickAmountPresets
    {
        get
        {
            if (DevMode.BypassAmountLimits)
            {
                return QuickAmountPresets;
            }

            var limit = SelectedDispatchType?.IsMoney == true
                ? amountLimit
                : SelectedDispatchType?.IsVolume == true
                    ? litersLimit
                    : (decimal?)null;

            return limit is decimal max
                ? QuickAmountPresets.Where(preset => preset <= max)
                : QuickAmountPresets;
        }
    }

    private bool CanEditSelectedDispenser => SelectedDispenser?.IsAvailable == true;

    private bool IsSelectionWorkspaceLoading => isLoadingDispensers || isSelectingDispenser;

    private string GetDispatchSummaryAmount()
    {
        if (SelectedDispatchType?.IsFull == true)
        {
            return "Lleno";
        }

        if (string.IsNullOrWhiteSpace(amountValue))
        {
            return "-";
        }

        return SelectedDispatchType?.IsMoney == true ? $"${amountValue}" : $"{amountValue}Lt";
    }

    private string GetDispatchSummaryPriceText() =>
        SelectedProduct?.Price is decimal price
            ? price.ToString("C2", CultureInfo.GetCultureInfo("es-MX"))
            : "-";

    private bool isPrintingTicket;

    private bool CanGoNext =>
        CanEditSelectedDispenser &&
        SelectedProduct is not null;

    private bool CanServe =>
        !isDispatching &&
        CanGoNext &&
        SelectedDispatchType is not null &&
        (SelectedDispatchType.IsFull || (TryGetAmount(out var amount) && amount > 0));

    protected override async Task OnInitializedAsync()
    {
        var settings = await SettingsProvider.GetSettingsAsync(pageCancellation.Token);
        amountLimit = settings.AppConfiguration.LimiteImporte;
        litersLimit = settings.AppConfiguration.LimiteLitros;
        fullTankProgrammedAmount = settings.AppConfiguration.CantidadTanqueLleno;
        quantityDecimals = settings.AppConfiguration.QuantityDecimals;
        authorizationCountdownSeconds = settings.AppConfiguration.AuthorizationCountdownSeconds;
        authorizationWarningSeconds = settings.AppConfiguration.AuthorizationWarningSeconds;
        authorizationPollingMilliseconds = settings.AppConfiguration.AuthorizationPollingMilliseconds;
        fuelingPollingMilliseconds = settings.AppConfiguration.FuelingPollingMilliseconds;
        hoseRestorePollingMilliseconds = settings.AppConfiguration.HoseRestorePollingMilliseconds;
        toastDurationMilliseconds = settings.AppConfiguration.ToastDurationMilliseconds;
        configuredDispenserCount = settings.AppConfiguration.DispenserCount;
        dispenserFillSequential = settings.AppConfiguration.DispenserFillSequential;
        dispenserNumbers = settings.AppConfiguration.DispenserNumbers;
        dispenserLabel = settings.AppConfiguration.DispenserLabel;
        productNameConfig = settings.AppConfiguration;

        await BeginDispatchFlowAsync();

        HeaderState.SetActions(DispatchHeaderActions);
        HeaderState.SetSubtitle(null);
    }
    private static void CloseApplication()
    {
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                Microsoft.Maui.Controls.Application.Current?.Quit();
            }
            catch
            {
            }

#if ANDROID
            Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
#endif
        });
    }

    private static bool HasRequiredConfiguration(AppSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.GasStationConsole.IpAddress)
            && settings.GasStationConsole.Port is > 0 and <= 65535
            && !string.IsNullOrWhiteSpace(settings.AppConfiguration.TipoInterfaz)
            && !string.IsNullOrWhiteSpace(settings.Api.BaseUrl)
            && Uri.TryCreate(settings.Api.BaseUrl, UriKind.Absolute, out _);
    }

    public async ValueTask DisposeAsync()
    {
        await pageCancellation.CancelAsync();
        pageCancellation.Dispose();
        dispatchCancellation?.Dispose();
        authorizationCountdownCancellation?.Dispose();
        fuelingMonitorCancellation?.Dispose();
        simulatedDispatchCancellation?.Dispose();
    }
}

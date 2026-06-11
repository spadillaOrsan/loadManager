using LoadManager.Services.Interfaces;

namespace LoadManager.Services;

/// <inheritdoc cref="IDevModeService" />
public sealed class DevModeService : IDevModeService
{
    public bool IsEnabled { get; set; }

    public bool SkipTcpConnection { get; set; }
    public bool SkipDatabase { get; set; }
    public bool SkipDeviceAuthorization { get; set; }
    public bool SkipDispenserCommunication { get; set; }
    public bool SkipDispatchTypesAndProducts { get; set; }
    public bool SkipSendAuthorization { get; set; }
    public bool SkipAuthorizationResponse { get; set; }
    public bool SkipHoseLiftedBlock { get; set; }
    public bool SkipAmountLimits { get; set; }
    public bool SimulateFueling { get; set; }

    public bool BypassTcpConnection => IsEnabled && SkipTcpConnection;
    public bool BypassDatabase => IsEnabled && SkipDatabase;
    public bool BypassDeviceAuthorization => IsEnabled && SkipDeviceAuthorization;
    public bool BypassDispenserCommunication => IsEnabled && SkipDispenserCommunication;
    public bool BypassDispatchTypesAndProducts => IsEnabled && SkipDispatchTypesAndProducts;
    public bool BypassSendAuthorization => IsEnabled && SkipSendAuthorization;
    public bool BypassAuthorizationResponse => IsEnabled && SkipAuthorizationResponse;
    public bool BypassHoseLiftedBlock => IsEnabled && SkipHoseLiftedBlock;
    public bool BypassAmountLimits => IsEnabled && SkipAmountLimits;
    public bool BypassFueling => IsEnabled && SimulateFueling;

    public void Disable()
    {
        IsEnabled = false;
        SkipTcpConnection = false;
        SkipDatabase = false;
        SkipDeviceAuthorization = false;
        SkipDispenserCommunication = false;
        SkipDispatchTypesAndProducts = false;
        SkipSendAuthorization = false;
        SkipAuthorizationResponse = false;
        SkipHoseLiftedBlock = false;
        SkipAmountLimits = false;
        SimulateFueling = false;
    }
}

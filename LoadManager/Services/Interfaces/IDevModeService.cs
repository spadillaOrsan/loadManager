namespace LoadManager.Services.Interfaces;

/// <summary>
/// Estado en memoria del "Modo DEV": permite saltar validaciones del modulo de
/// dispensario para poder desarrollar y acomodar estilos sin hardware/BD reales.
/// No se persiste: se reinicia a apagado cada vez que arranca la app.
/// Protegido por contrasena en la pantalla de Configuracion.
/// </summary>
public interface IDevModeService
{
    /// <summary>Interruptor maestro. Si esta apagado, ningun "Bypass*" aplica.</summary>
    bool IsEnabled { get; set; }

    // Interruptores individuales (solo surten efecto si IsEnabled == true).
    bool SkipTcpConnection { get; set; }
    bool SkipDatabase { get; set; }
    bool SkipDeviceAuthorization { get; set; }
    bool SkipDispenserCommunication { get; set; }
    bool SkipDispatchTypesAndProducts { get; set; }
    bool SkipSendAuthorization { get; set; }
    bool SkipAuthorizationResponse { get; set; }
    bool SkipHoseLiftedBlock { get; set; }
    bool SkipAmountLimits { get; set; }
    bool SimulateFueling { get; set; }
    bool HidePrintButton { get; set; }
    bool CancelOnBackground { get; set; }
    bool UseCountdown { get; set; }
    bool ShowLiveCounter { get; set; }

    // Efectivos: combinan el interruptor maestro con cada interruptor individual.
    bool BypassTcpConnection { get; }
    bool BypassDatabase { get; }
    bool BypassDeviceAuthorization { get; }
    bool BypassDispenserCommunication { get; }
    bool BypassDispatchTypesAndProducts { get; }
    bool BypassSendAuthorization { get; }
    bool BypassAuthorizationResponse { get; }
    bool BypassHoseLiftedBlock { get; }
    bool BypassAmountLimits { get; }
    bool BypassFueling { get; }
    bool IsPrintButtonHidden { get; }
    bool ShouldCancelOnBackground { get; }
    /// <summary>true cuando debe mostrarse el contador en autorizacion (siempre fuera de DEV, o cuando UseCountdown == true en DEV).</summary>
    bool ShouldShowCountdown { get; }
    /// <summary>true cuando debe mostrarse el contador de litros/moneda durante el surtido (siempre fuera de DEV, o cuando ShowLiveCounter == true en DEV).</summary>
    bool ShouldShowLiveCounter { get; }

    /// <summary>Apaga el modo DEV y reinicia todos los interruptores.</summary>
    void Disable();
}

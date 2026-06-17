using System.Globalization;
using LoadManager.Services.Interfaces;

namespace LoadManager.Services;

/// <inheritdoc cref="IActiveDispatchTracker" />
public sealed class ActiveDispatchTracker : IActiveDispatchTracker
{
    private readonly IGasStationService console;

    public ActiveDispatchTracker(IGasStationService console)
    {
        this.console = console;
    }

    public event Action? StateChanged;

    private int? activeDispenser;
    public int? ActiveDispenser
    {
        get => activeDispenser;
        set
        {
            activeDispenser = value;
            StateChanged?.Invoke();
        }
    }

    public async Task CancelActiveDispatchAsync(CancellationToken cancellationToken = default)
    {
        var dispenser = ActiveDispenser;
        if (dispenser is null)
        {
            return;
        }

        ActiveDispenser = null;

        try
        {
            await console.SendRawFrameAsync(
                $"CAUTH|{dispenser.Value.ToString(CultureInfo.InvariantCulture)}",
                cancellationToken);
        }
        catch
        {
            // Best-effort: si no se alcanza a cancelar en la consola, no hay mas que hacer.
        }
    }
}

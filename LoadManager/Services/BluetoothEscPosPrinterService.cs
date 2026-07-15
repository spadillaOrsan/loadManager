using LoadManager.Helpers;
using LoadManager.Models;
using LoadManager.Services.Interfaces;

#if WINDOWS
using System.IO.Ports;
using Microsoft.Win32;
#elif ANDROID
using Android.Bluetooth;
using Android.Content;
using Microsoft.Maui.ApplicationModel;
#endif

namespace LoadManager.Services;

/// <summary>
/// Impresora termica ESC/POS por Bluetooth. En Windows abre el puerto serie COM
/// (perfil SPP) con SerialPort; en Android se conecta por socket Bluetooth directo
/// al dispositivo vinculado (UUID SPP estandar). La conexion se libera siempre al
/// terminar (using/finally).
/// </summary>
public sealed class BluetoothEscPosPrinterService(IAppSettingsService settingsProvider) : IBluetoothEscPosPrinterService
{
    private const int OpenOrWriteTimeoutMilliseconds = 5000;

    public async Task<PrintOutcome> PrintAsync(ReceiptPrintJob job, CancellationToken cancellationToken = default)
    {
        var options = (await settingsProvider.GetSettingsAsync(cancellationToken)).Printer;
        var route = $"ESC/POS {EndpointLabel(options)}";

        var endpointId = GetConfiguredEndpointId(options);
        if (string.IsNullOrWhiteSpace(endpointId))
        {
            return PrintOutcome.Fail(route, "No hay impresora Bluetooth configurada. Seleccionela en Configuracion > Impresora.");
        }

        var payload = TicketEscPosBuilder.Build(job.Record, job.TicketNumber, options.PaperColumns);

        try
        {
            await SendAsync(endpointId, payload, options, cancellationToken);
            return PrintOutcome.Ok(route);
        }
        catch (Exception exception)
        {
            return PrintOutcome.Fail(route, TranslateError(EndpointLabel(options), exception));
        }
    }

    public async Task<PrintOutcome> TestAsync(string endpointId, CancellationToken cancellationToken = default)
    {
        var route = $"ESC/POS {endpointId}";

        if (string.IsNullOrWhiteSpace(endpointId))
        {
            return PrintOutcome.Fail(route, "Seleccione una impresora Bluetooth.");
        }

        var options = (await settingsProvider.GetSettingsAsync(cancellationToken)).Printer;

        try
        {
            // Conectar y cerrar sin enviar datos valida que el destino responda.
            await SendAsync(endpointId, [], options, cancellationToken);
            return PrintOutcome.Ok(route);
        }
        catch (Exception exception)
        {
            return PrintOutcome.Fail(route, TranslateError(endpointId, exception));
        }
    }

    // Windows guarda el puerto COM; Android la direccion MAC del dispositivo.
    private static string GetConfiguredEndpointId(PrinterOptions options) =>
        OperatingSystem.IsWindows() ? options.ComPort : options.BluetoothAddress;

    private static string EndpointLabel(PrinterOptions options)
    {
        if (OperatingSystem.IsWindows())
        {
            return string.IsNullOrWhiteSpace(options.ComPort) ? "(sin puerto)" : options.ComPort;
        }

        if (!string.IsNullOrWhiteSpace(options.BluetoothName))
        {
            return options.BluetoothName;
        }

        return string.IsNullOrWhiteSpace(options.BluetoothAddress) ? "(sin impresora)" : options.BluetoothAddress;
    }

#if WINDOWS

    public Task<IReadOnlyList<BluetoothPrinterEndpoint>> GetPrintersAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var availablePorts = SerialPort.GetPortNames()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var bluetoothPorts = GetBluetoothRegisteredPortNames()
                .Where(availablePorts.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(ExtractPortNumber)
                .Select(port => new BluetoothPrinterEndpoint { Id = port, DisplayName = port })
                .ToList();

            return (IReadOnlyList<BluetoothPrinterEndpoint>)bluetoothPorts;
        }, cancellationToken);
    }

    private static Task SendAsync(string portName, byte[] payload, PrinterOptions options, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            using var port = CreatePort(portName, options);
            port.Open();

            if (payload.Length > 0)
            {
                port.Write(payload, 0, payload.Length);

                // Espera a que el buffer salga antes de cerrar (algunas termicas
                // pierden el final del ticket si el puerto se cierra de inmediato).
                port.BaseStream.Flush();
            }
        }, cancellationToken);
    }

    private static SerialPort CreatePort(string portName, PrinterOptions options)
    {
        if (!Enum.TryParse<StopBits>(options.StopBits, ignoreCase: true, out var stopBits) || stopBits == StopBits.None)
        {
            stopBits = StopBits.One;
        }

        if (!Enum.TryParse<Parity>(options.Parity, ignoreCase: true, out var parity))
        {
            parity = Parity.None;
        }

        return new SerialPort(portName, options.BaudRate > 0 ? options.BaudRate : 115200, parity,
            options.DataBits is >= 5 and <= 8 ? options.DataBits : 8, stopBits)
        {
            WriteTimeout = OpenOrWriteTimeoutMilliseconds,
            ReadTimeout = OpenOrWriteTimeoutMilliseconds
        };
    }

    /// <summary>
    /// Lee los PortName registrados bajo HKLM\SYSTEM\CurrentControlSet\Enum\BTHENUM,
    /// que es donde Windows registra los puertos serie del perfil Bluetooth SPP.
    /// </summary>
    private static List<string> GetBluetoothRegisteredPortNames()
    {
        var ports = new List<string>();

        using var bluetoothRoot = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\BTHENUM");
        if (bluetoothRoot is null)
        {
            return ports;
        }

        foreach (var deviceKeyName in bluetoothRoot.GetSubKeyNames())
        {
            using var deviceKey = bluetoothRoot.OpenSubKey(deviceKeyName);
            if (deviceKey is null)
            {
                continue;
            }

            foreach (var instanceKeyName in deviceKey.GetSubKeyNames())
            {
                using var parametersKey = deviceKey.OpenSubKey($@"{instanceKeyName}\Device Parameters");
                if (parametersKey?.GetValue("PortName") is string portName && portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                {
                    ports.Add(portName);
                }
            }
        }

        return ports;
    }

    private static int ExtractPortNumber(string portName) =>
        int.TryParse(portName.AsSpan(3), out var number) ? number : int.MaxValue;

    private static string TranslateError(string portName, Exception exception) => exception switch
    {
        UnauthorizedAccessException =>
            $"El puerto {portName} esta ocupado por otra aplicacion. Cierre el programa que lo usa e intente de nuevo.",
        TimeoutException =>
            $"La impresora en {portName} no responde. Verifique que este encendida y emparejada.",
        FileNotFoundException or ArgumentException =>
            $"El puerto {portName} no existe. Actualice la lista de puertos en Configuracion > Impresora.",
        IOException =>
            $"No se pudo comunicar con {portName}. Verifique que la impresora este encendida y dentro del alcance Bluetooth.",
        OperationCanceledException =>
            "Impresion cancelada.",
        _ =>
            $"Error al usar el puerto {portName}: {exception.Message}"
    };

#elif ANDROID

    // UUID estandar del perfil serie Bluetooth (SPP), el que usan las termicas.
    private static readonly Java.Util.UUID SppUuid =
        Java.Util.UUID.FromString("00001101-0000-1000-8000-00805F9B34FB")!;

    public async Task<IReadOnlyList<BluetoothPrinterEndpoint>> GetPrintersAsync(CancellationToken cancellationToken = default)
    {
        await EnsureBluetoothPermissionAsync();

        var adapter = GetAdapter();
        if (adapter is null || !adapter.IsEnabled)
        {
            throw new InvalidOperationException("Bluetooth desactivado. Activelo en los ajustes del equipo.");
        }

        var devices = adapter.BondedDevices;
        if (devices is null)
        {
            return [];
        }

        return devices
            .Where(device => !string.IsNullOrWhiteSpace(device.Address))
            .Select(device => new BluetoothPrinterEndpoint
            {
                Id = device.Address!,
                DisplayName = string.IsNullOrWhiteSpace(device.Name) ? device.Address! : device.Name!
            })
            .OrderBy(endpoint => endpoint.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task SendAsync(string address, byte[] payload, PrinterOptions options, CancellationToken cancellationToken)
    {
        _ = options; // BaudRate/paridad no aplican al socket Bluetooth.
        await EnsureBluetoothPermissionAsync();

        await Task.Run(() =>
        {
            var adapter = GetAdapter();
            if (adapter is null || !adapter.IsEnabled)
            {
                throw new InvalidOperationException("Bluetooth desactivado. Activelo en los ajustes del equipo.");
            }

            var device = adapter.GetRemoteDevice(address)
                ?? throw new InvalidOperationException("La impresora ya no esta vinculada. Vuelva a vincularla y seleccionela en Configuracion.");

            using var socket = device.CreateRfcommSocketToServiceRecord(SppUuid)
                ?? throw new InvalidOperationException("No se pudo crear la conexion Bluetooth con la impresora.");

            try
            {
                // La busqueda activa de dispositivos hace lenta/inestable la conexion.
                // En Android 12+ CancelDiscovery pide BLUETOOTH_SCAN (no declarado);
                // si lo niega, se ignora: conectar sigue siendo posible.
                try { adapter.CancelDiscovery(); } catch (Java.Lang.SecurityException) { }
                socket.Connect();

                if (payload.Length > 0)
                {
                    var stream = socket.OutputStream
                        ?? throw new InvalidOperationException("No se pudo abrir el canal de datos con la impresora.");
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush();

                    // Da tiempo a que la impresora reciba todo antes de cerrar el
                    // socket (algunas termicas pierden el final del ticket).
                    Thread.Sleep(300);
                }
            }
            finally
            {
                socket.Close();
            }
        }, cancellationToken);
    }

    private static BluetoothAdapter? GetAdapter()
    {
        var manager = Android.App.Application.Context.GetSystemService(Context.BluetoothService) as BluetoothManager;
        return manager?.Adapter;
    }

    // Android 12+ exige el permiso BLUETOOTH_CONNECT en runtime; se pide la
    // primera vez que se usa la impresora.
    private static async Task EnsureBluetoothPermissionAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            return;
        }

        var status = await MainThread.InvokeOnMainThreadAsync(() =>
            Permissions.CheckStatusAsync<Permissions.Bluetooth>());

        if (status != PermissionStatus.Granted)
        {
            status = await MainThread.InvokeOnMainThreadAsync(() =>
                Permissions.RequestAsync<Permissions.Bluetooth>());
        }

        if (status != PermissionStatus.Granted)
        {
            throw new InvalidOperationException(
                "Permiso de Bluetooth denegado. Autorice 'Dispositivos cercanos' para la app en los ajustes de Android.");
        }
    }

    private static string TranslateError(string printerName, Exception exception) => exception switch
    {
        InvalidOperationException invalid => invalid.Message,
        Java.IO.IOException =>
            $"La impresora {printerName} no responde. Verifique que este encendida, con papel y dentro del alcance Bluetooth.",
        OperationCanceledException =>
            "Impresion cancelada.",
        _ =>
            $"Error al imprimir en {printerName}: {exception.Message}"
    };

#else

    public Task<IReadOnlyList<BluetoothPrinterEndpoint>> GetPrintersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BluetoothPrinterEndpoint>>([]);

    private static Task SendAsync(string endpointId, byte[] payload, PrinterOptions options, CancellationToken cancellationToken) =>
        throw new PlatformNotSupportedException("La impresion Bluetooth solo esta disponible en Windows y Android.");

    private static string TranslateError(string endpointId, Exception exception) => exception.Message;

#endif
}

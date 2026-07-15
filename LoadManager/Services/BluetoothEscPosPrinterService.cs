using LoadManager.Helpers;
using LoadManager.Models;
using LoadManager.Services.Interfaces;

#if WINDOWS
using System.IO.Ports;
using Microsoft.Win32;
#endif

namespace LoadManager.Services;

/// <summary>
/// Impresora termica ESC/POS por puerto serie Bluetooth (perfil SPP), solo Windows.
/// Enumera unicamente los COM registrados bajo BTHENUM, abre el puerto con la
/// configuracion de PrinterOptions y lo libera siempre al terminar (using/finally).
/// </summary>
public sealed class BluetoothEscPosPrinterService(IAppSettingsService settingsProvider) : IBluetoothEscPosPrinterService
{
    private const int OpenOrWriteTimeoutMilliseconds = 5000;

    public async Task<PrintOutcome> PrintAsync(ReceiptPrintJob job, CancellationToken cancellationToken = default)
    {
#if WINDOWS
        var options = (await settingsProvider.GetSettingsAsync(cancellationToken)).Printer;
        var route = $"ESC/POS {options.ComPort}";

        if (string.IsNullOrWhiteSpace(options.ComPort))
        {
            return PrintOutcome.Fail(route, "No hay puerto COM configurado. Seleccione uno en Configuracion > Impresora.");
        }

        var payload = TicketEscPosBuilder.Build(job.Record, job.TicketNumber, options.PaperColumns);

        try
        {
            await Task.Run(() =>
            {
                using var port = CreatePort(options.ComPort, options);
                port.Open();
                port.Write(payload, 0, payload.Length);

                // Espera a que el buffer salga antes de cerrar (algunas termicas
                // pierden el final del ticket si el puerto se cierra de inmediato).
                port.BaseStream.Flush();
            }, cancellationToken);

            return PrintOutcome.Ok(route);
        }
        catch (Exception exception)
        {
            return PrintOutcome.Fail(route, TranslatePortError(options.ComPort, exception));
        }
#else
        _ = settingsProvider;
        await Task.CompletedTask;
        return PrintOutcome.Fail("ESC/POS", "La impresion Bluetooth por COM solo esta disponible en Windows.");
#endif
    }

    public Task<IReadOnlyList<string>> GetBluetoothPortsAsync(CancellationToken cancellationToken = default)
    {
#if WINDOWS
        return Task.Run(() =>
        {
            var availablePorts = SerialPort.GetPortNames()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var bluetoothPorts = GetBluetoothRegisteredPortNames()
                .Where(availablePorts.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(ExtractPortNumber)
                .ToList();

            return (IReadOnlyList<string>)bluetoothPorts;
        }, cancellationToken);
#else
        return Task.FromResult<IReadOnlyList<string>>([]);
#endif
    }

    public async Task<PrintOutcome> TestPortAsync(string portName, CancellationToken cancellationToken = default)
    {
#if WINDOWS
        var route = $"ESC/POS {portName}";

        if (string.IsNullOrWhiteSpace(portName))
        {
            return PrintOutcome.Fail(route, "Seleccione un puerto COM.");
        }

        var options = (await settingsProvider.GetSettingsAsync(cancellationToken)).Printer;

        try
        {
            await Task.Run(() =>
            {
                using var port = CreatePort(portName, options);
                port.Open();
            }, cancellationToken);

            return PrintOutcome.Ok(route);
        }
        catch (Exception exception)
        {
            return PrintOutcome.Fail(route, TranslatePortError(portName, exception));
        }
#else
        await Task.CompletedTask;
        return PrintOutcome.Fail($"ESC/POS {portName}", "La impresion Bluetooth por COM solo esta disponible en Windows.");
#endif
    }

#if WINDOWS
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

    private static string TranslatePortError(string portName, Exception exception) => exception switch
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
#endif
}

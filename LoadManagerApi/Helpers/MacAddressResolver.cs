using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace LoadManagerApi.Helpers;

/// <summary>
/// Resuelve la MAC REAL de un equipo a partir de su IP consultando la tabla ARP de la red
/// (servidor y equipo deben estar en la misma LAN). Util para registrar la MAC verdadera de
/// un dispositivo que intenta conectarse, sin depender de lo que reporte el propio cliente.
/// </summary>
public static class MacAddressResolver
{
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(int destIp, int srcIp, byte[] macAddr, ref uint macAddrLen);

    public static string? TryResolve(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out var address))
        {
            return null;
        }

        // SendARP solo aplica a IPv4 y no tiene sentido para loopback.
        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
        {
            return null;
        }

        try
        {
            var destIp = BitConverter.ToInt32(address.GetAddressBytes(), 0);
            var macAddr = new byte[6];
            var length = (uint)macAddr.Length;

            // result == 0 => exito; cualquier otro valor significa que no se pudo resolver.
            if (SendARP(destIp, 0, macAddr, ref length) != 0 || length < 6)
            {
                return null;
            }

            var mac = string.Join(":", macAddr.Take(6).Select(b => b.ToString("X2")));

            // Una MAC en ceros indica que no hubo resolucion valida.
            return mac == "00:00:00:00:00:00" ? null : mac;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Resuelve el NOMBRE REAL del equipo a partir de su IP mediante DNS inverso (PTR).
    /// Depende de que la red tenga registros inversos para ese equipo.
    /// </summary>
    public static async Task<string?> TryResolveHostNameAsync(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out var address))
        {
            return null;
        }

        if (IPAddress.IsLoopback(address))
        {
            return null;
        }

        try
        {
            var entry = await Dns.GetHostEntryAsync(address);
            if (string.IsNullOrWhiteSpace(entry.HostName))
            {
                return null;
            }

            // Solo el nombre de host, sin el sufijo de dominio.
            var host = entry.HostName.Split('.')[0];
            return string.IsNullOrWhiteSpace(host) ? null : host;
        }
        catch
        {
            return null;
        }
    }
}

using System.Net.Sockets;
using System.Text;

namespace BuscaServidor;

internal static class ServiceProbe
{
    // Reads only the server's initial packet. No login, SQL, or application bytes are sent.
    public static async Task<Endpoint?> Check(string ip, int port, CancellationToken ct, uint timeoutSeconds = 1)
    {
        ct.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1u, 60u)));
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(ip, port, timeout.Token);
            using var stream = client.GetStream();
            byte[] header = new byte[4];
            await stream.ReadExactlyAsync(header, timeout.Token);
            int length = header[0] | header[1] << 8 | header[2] << 16;
            if (header[3] != 0 || length < 3 || length > 4096) return null;
            byte[] packet = new byte[length];
            await stream.ReadExactlyAsync(packet, timeout.Token);
            if (packet[0] == 0xff)
            {
                int code = packet[1] | packet[2] << 8;
                if (code is not (1040 or 1045 or 1129 or 1130 or 3159) || length < 4) return null;
                int message = 3;
                if (packet[3] == '#')
                {
                    if (length < 10 || packet.AsSpan(4, 5).ToArray().Any(b => b is < 48 or > 90 || b is > 57 and < 65)) return null;
                    message = 9;
                }
                if (packet.AsSpan(message).Contains((byte)0)) return null;
                return new Endpoint(ip, port, "MySQL/MariaDB", $"MySQL/MariaDB respondeu com erro {code}");
            }
            if (packet[0] != 10) return null;
            int end = Array.IndexOf(packet, (byte)0, 1);
            if (end < 4 || end > 256 || packet[1] is < 48 or > 57 || !packet.AsSpan(1, end - 1).Contains((byte)'.')) return null;
            if (packet.AsSpan(1, end - 1).ToArray().Any(b => b is < 32 or > 126)) return null;
            int offset = end + 1, remaining = length - offset;
            // thread id (4), scramble (8), zero filler (1), capabilities (2).
            if (remaining < 15 || packet[offset + 12] != 0) return null;
            if (remaining != 15)
            {
                // charset, status, upper capabilities, auth length, reserved (10).
                if (remaining < 31) return null;
                // MariaDB uses the last four reserved bytes for extended capabilities.
                if (packet.AsSpan(offset + 21, 6).ToArray().Any(b => b != 0)) return null;
                int capabilities = packet[offset + 13] | packet[offset + 14] << 8;
                if ((capabilities & 0x8000) != 0 && remaining < 43) return null;
            }
            return new Endpoint(ip, port, Encoding.ASCII.GetString(packet, 1, end - 1), "Serviço MySQL/MariaDB identificado");
        }
        catch (OperationCanceledException) { ct.ThrowIfCancellationRequested(); return null; }
        catch (Exception ex) when (ex is SocketException or IOException)
        { ct.ThrowIfCancellationRequested(); return null; }
    }
}

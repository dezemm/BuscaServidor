using System.Net;
using System.Net.Sockets;
using System.Text;

namespace BuscaServidor;

internal static class ProbeTests
{
    public static async Task Run(List<string> results)
    {
        void Assert(bool value, string description)
        {
            if (!value) throw new Exception("Falhou: " + description);
            results.Add("PASS " + description);
        }
        var nics = new[] { ("192.168.4.10", "192.168.4.0/24", "LAN"), ("10.8.2.10", "10.8.0.0/16", "VPN") };
        var auto = ScanTargets.Automatic(nics, false);
        Assert(auto.Contains("192.168.4.143") && auto.Contains("10.8.2.200") && auto.Count == 506, "Automático cobre /24 de todas as placas sem depender do ARP");
        Assert(!auto.Contains("192.168.4.10") && !auto.Contains("10.8.2.10"), "Desmarcar local exclui todos IPs locais");
        Assert(ScanTargets.Automatic(nics, true).Contains("127.0.0.1"), "Opção local inclui loopback");
        Assert(ScanTargets.Automatic(new[] { ("192.168.1.200", "192.168.1.192/26", "LAN") }, false).Count == 61, "Automático respeita sub-rede menor que /24");
        var full = ScanTargets.Full(new[] { ("192.168.0.5", "192.168.0.0/23", "LAN") }, false);
        Assert(full.Contains("192.168.1.0") && full.Contains("192.168.0.255") && full.Count == 509, "Força bruta usa máscara real sem rejeitar hosts terminados em .0/.255");
        Assert(ScanTargets.Full(new[] { ("10.0.0.0", "10.0.0.0/31", "LAN") }, false).SequenceEqual(new[] { "10.0.0.1" }), "Rede /31 considera ambos endereços utilizáveis");
        Assert(ScanTargets.Full(new[] { ("10.0.0.1", "10.0.0.1/32", "LAN") }, false).Count == 0, "Rede /32 respeita exclusão local");
        bool refused = false;
        try { ScanTargets.Full(new[] { ("25.1.2.3", "25.0.0.0/8", "VPN") }, true); } catch (ArgumentException) { refused = true; }
        Assert(refused, "Força bruta recusa rede enorme antes de enumerar hosts");
        Assert(ScanTargets.Automatic(nics.Concat(nics), false).Count == auto.Count, "Endereços duplicados de adaptadores são removidos");
        Assert(ScanTargets.Automatic(new[] { ("192.168.1.0", "192.168.0.0/16", "LAN") }, true).Contains("192.168.1.0"), "Opção local inclui IP local válido terminado em .0");
        Assert(ScanTargets.Automatic(new[] { ("120.0.0.1", "120.0.0.0/24", "Inválido"), ("169.254.2.1", "169.254.0.0/16", "Link local"), ("224.0.0.1", "224.0.0.0/24", "Multicast") }, false).Count == 0, "Endereços inadequados não originam varreduras");

        byte[] version = Encoding.ASCII.GetBytes("8.0.36-test\0");
        byte[] greeting = new byte[1 + version.Length + 44];
        greeting[0] = 10; version.CopyTo(greeting, 1);
        int offset = 1 + version.Length;
        greeting[offset] = 1;
        Encoding.ASCII.GetBytes("12345678").CopyTo(greeting, offset + 4);
        greeting[offset + 13] = 0xff; greeting[offset + 14] = 0xff;
        greeting[offset + 15] = 45; greeting[offset + 16] = 2;
        Encoding.ASCII.GetBytes("123456789012").CopyTo(greeting, offset + 31);
        byte[] Packet(byte[] payload) => new[] { (byte)payload.Length, (byte)(payload.Length >> 8), (byte)0, (byte)0 }.Concat(payload).ToArray();
        async Task<(Endpoint? Result, int Sent)> Probe(byte[] data, bool fragmented = false)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = Task.Run(async () =>
            {
                using var socket = await listener.AcceptTcpClientAsync();
                using var stream = socket.GetStream();
                if (fragmented)
                    foreach (byte b in data) { await stream.WriteAsync(new byte[] { b }); await Task.Delay(1); }
                else await stream.WriteAsync(data);
                using var timer = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { return await stream.ReadAsync(new byte[256], timer.Token); }
                catch (IOException) { return 0; }
            });
            var result = await ServiceProbe.Check("127.0.0.1", port, CancellationToken.None, 2);
            return (result, await server);
        }
        var identified = await Probe(Packet(greeting), true);
        Assert(identified.Result?.Version == "8.0.36-test", "Greeting fragmentado identifica versão real");
        Assert(identified.Sent == 0, "Probe não envia login, SQL ou qualquer byte ao servidor");
        Assert((await Probe(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\n"))).Result == null, "Resposta HTTP não identifica banco");
        byte[] bad = greeting.ToArray(); bad[offset + 12] = 1;
        Assert((await Probe(Packet(bad))).Result == null, "Greeting com estrutura inválida rejeitado");
        Assert((await Probe(new byte[] { 1, 16, 0, 0 })).Result == null, "Packet maior que limite rejeitado");
        byte[] error = new byte[] { 255, 21, 4 }.Concat(Encoding.ASCII.GetBytes("#28000Access denied")).ToArray();
        Assert((await Probe(Packet(error))).Result != null, "Erro inicial MySQL 1045 reconhecido sem autenticar");
        using var idleListener = new TcpListener(IPAddress.Loopback, 0);
        idleListener.Start(); int idlePort = ((IPEndPoint)idleListener.LocalEndpoint).Port;
        using var cancel = new CancellationTokenSource();
        var waiting = ServiceProbe.Check("127.0.0.1", idlePort, cancel.Token, 5);
        using var accepted = await idleListener.AcceptTcpClientAsync();
        cancel.Cancel(); bool cancelled = false;
        try { await waiting; } catch (OperationCanceledException) { cancelled = true; }
        Assert(cancelled, "Cancelamento interrompe leitura de greeting pendente");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var timed = ServiceProbe.Check("127.0.0.1", idlePort, CancellationToken.None, 1);
        using var accepted2 = await idleListener.AcceptTcpClientAsync();
        Assert(await timed == null && watch.Elapsed < TimeSpan.FromSeconds(3), "Timeout limita conexão silenciosa sem repetir contato");
    }
}

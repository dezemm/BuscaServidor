using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace BuscaServidor;

internal static class VpnTests
{
    public static Task RunInterface(List<string> results)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            int discoveryCalls = 0;
            bool? suppliedLocal = null;
            string[] suppliedExplicit = Array.Empty<string>();
            var snapshot = new VpnSnapshot(new Dictionary<string, string> { ["127.0.0.2"] = "Tailscale (alta) • adaptador simulado • rota individual /32" }, "CONSULTA VPN: adaptador simulado Tailscale");
            using var form = new MainForm((local, explicitHosts) =>
            {
                discoveryCalls++; suppliedLocal = local; suppliedExplicit = explicitHosts?.ToArray() ?? Array.Empty<string>();
                return snapshot;
            }) { StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ShowInTaskbar = false };
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            T Field<T>(string name) => (T)typeof(MainForm).GetField(name, flags)!.GetValue(form)!;
            Task Scan() => (Task)typeof(MainForm).GetMethod("Scan", flags)!.Invoke(form, null)!;
            void Check(bool condition, string name)
            {
                if (!condition) throw new Exception("VPN UI: " + name);
                results.Add("PASS VPN UI: " + name);
            }
            form.Shown += async (_, _) =>
            {
                try
                {
                    var consult = Field<CheckBox>("consultVpn");
                    Check(!consult.Checked, "Consultar VPN começa desmarcado");
                    Field<CheckBox>("rangeMode").Checked = true;
                    Field<CheckBox>("local").Checked = false;
                    Field<TextBox>("targets").Text = "127.0.0.1";
                    var grid = Field<DataGridView>("grid");
                    var listeners = new[] { new TcpListener(IPAddress.Loopback, 0), new TcpListener(IPAddress.Parse("127.0.0.2"), 0) };
                    using var stopping = new CancellationTokenSource();
                    Task[] servers = Array.Empty<Task>();
                    int[] contacts = new int[2];
                    try
                    {
                        listeners[0].Start();
                        int port = ((IPEndPoint)listeners[0].LocalEndpoint).Port;
                        listeners[1] = new TcpListener(IPAddress.Parse("127.0.0.2"), port);
                        listeners[1].Start();
                        Task Serve(int index) => Task.Run(async () =>
                        {
                            try
                            {
                                while (true)
                                {
                                    using var client = await listeners[index].AcceptTcpClientAsync(stopping.Token);
                                    Interlocked.Increment(ref contacts[index]);
                                    byte[] message = Encoding.ASCII.GetBytes("#28000Access denied for test discovery user");
                                    byte[] packet = new byte[7 + message.Length];
                                    int payload = packet.Length - 4;
                                    packet[0] = (byte)payload; packet[1] = (byte)(payload >> 8); packet[2] = (byte)(payload >> 16);
                                    packet[4] = 0xff; packet[5] = (byte)(1045 & 0xff); packet[6] = (byte)(1045 >> 8);
                                    message.CopyTo(packet, 7);
                                    await client.GetStream().WriteAsync(packet, stopping.Token);
                                }
                            }
                            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
                        });
                        servers = new[] { Serve(0), Serve(1) };
                        Field<TextBox>("ports").Text = port.ToString();
                        await Scan();
                        Check(discoveryCalls == 0 && grid.Rows.Count == 1 && contacts[1] == 0, "Desmarcado preserva busca manual sem consultar VPN nem testar peer");
                        consult.Checked = true;
                        await Scan();
                        Check(discoveryCalls == 1 && suppliedLocal == false && suppliedExplicit.SequenceEqual(new[] { "127.0.0.1" }), "Marcado envia IP manual e opção local ao descobridor");
                        Check(grid.Rows.Count == 2 && contacts[0] == 2 && contacts[1] == 1, "Mescla IP manual com peer VPN e testa ambos após primeiro resultado");
                        var vpnRow = grid.Rows.Cast<DataGridViewRow>().Single(r => r.Cells[0].Value?.ToString() == "127.0.0.2");
                        Check(vpnRow.Cells[2].Value?.ToString()?.Contains("VPN") == true && vpnRow.Cells[3].Value?.ToString()?.Contains("Tailscale") == true, "Resultado exibe identificação VPN do peer encontrado");
                        Check(vpnRow.Cells[4].Tag?.ToString()?.Contains("Tailscale") == true && Field<List<string>>("reports").Any(r => r == snapshot.Report), "Diagnóstico e exportação conservam evidência VPN");
                        Check(consult.Enabled && Field<Button>("scan").Enabled, "Busca concluída libera controles VPN e busca");
                        using var picture = new Bitmap(form.Width, form.Height);
                        form.DrawToBitmap(picture, new Rectangle(Point.Empty, form.Size));
                        picture.Save(Path.Combine(AppContext.BaseDirectory, "interface-vpn-simulada.png"));
                    }
                    finally
                    {
                        stopping.Cancel();
                        try { await Task.WhenAll(servers); }
                        finally { foreach (var listener in listeners) listener.Stop(); }
                    }
                    var pending = new TcpListener(IPAddress.Loopback, 0);
                    pending.Start();
                    try
                    {
                        Field<TextBox>("ports").Text = ((IPEndPoint)pending.LocalEndpoint).Port.ToString();
                        var accepting = pending.AcceptTcpClientAsync();
                        var scan = Scan();
                        using var client = await accepting.WaitAsync(TimeSpan.FromSeconds(5));
                        Check(!consult.Enabled && Field<Button>("cancel").Enabled, "Consulta VPN impede alterações durante conexão pendente");
                        Field<Button>("cancel").PerformClick();
                        await scan.WaitAsync(TimeSpan.FromSeconds(5));
                        Check(consult.Enabled && Field<Button>("scan").Enabled && Field<Label>("status").Text.Contains("cancelada"), "Cancelamento VPN interrompe conexão e libera interface");
                    }
                    finally { pending.Stop(); }
                    completion.SetResult();
                }
                catch (Exception ex) { completion.SetException(ex); }
                finally { form.Close(); }
            };
            Application.Run(form);
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        return completion.Task;
    }

    public static void Run(List<string> results)
    {
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("VPN: " + name);
            results.Add("PASS VPN: " + name);
        }

        foreach (var (ip, product) in new[] {
            ("25.10.20.30", "Hamachi"), ("26.45.80.120", "Radmin VPN"),
            ("100.64.10.25", "Tailscale"), ("100.127.250.10", "Tailscale"),
            ("10.8.0.6", "OpenVPN"), ("10.9.0.15", "OpenVPN"), ("10.10.0.8", "OpenVPN"),
            ("172.27.224.10", "OpenVPN"), ("10.13.13.5", "WireGuard"), ("10.14.0.5", "WireGuard"),
            ("10.147.20.8", "ZeroTier"), ("10.241.20.8", "ZeroTier"), ("192.168.30.25", "SoftEther") })
        {
            var identity = VpnDiscovery.FromAddress(ip);
            Check(identity != null && identity.Product.Contains(product) && identity.Evidence.Contains("não comprova"), ip + " sugere " + product + " sem confirmação indevida");
        }
        foreach (string ip in new[] { "100.63.255.255", "100.128.0.0", "10.13.12.5", "192.168.31.25", "192.168.1.143", "not-an-ip", "::1" })
            Check(VpnDiscovery.FromAddress(ip) == null, "Não classifica prefixo externo/inválido " + ip);

        foreach (var (description, product) in new[] {
            ("LogMeIn Hamachi Virtual Ethernet Adapter", "Hamachi"), ("Radmin VPN", "Radmin VPN"),
            ("Tailscale Tunnel", "Tailscale"), ("OpenVPN Data Channel Offload", "OpenVPN"),
            ("WireGuard Tunnel", "WireGuard"), ("ZeroTier One", "ZeroTier"),
            ("Fortinet SSL VPN Virtual Ethernet", "FortiClient"), ("Cisco AnyConnect", "Cisco VPN"),
            ("PANGP Virtual Ethernet Adapter", "GlobalProtect") })
        {
            var identity = VpnDiscovery.Identify("Adapter", description, "10.8.0.2");
            Check(identity?.Product == product && identity.Confidence == "alta", "Descrição prevalece sobre prefixo: " + description);
        }
        Check(VpnDiscovery.Identify("TAP-Windows Adapter V9", "", "10.8.0.2")?.Confidence == "possível", "TAP compartilhado não confirma OpenVPN");
        Check(VpnDiscovery.Identify("Wintun", "", "10.8.0.2")?.Confidence == "indeterminada", "Wintun sozinho permanece ambíguo");
        Check(VpnDiscovery.Identify("Ethernet", "Realtek", "192.168.1.20") == null, "Ethernet comum não identifica VPN");
        Check(VpnDiscovery.Identify("Tunnel", "", "192.168.77.1", NetworkInterfaceType.Tunnel)?.Confidence == "indeterminada", "Túnel desconhecido não inventa produto");

        var hamachi = new VpnAdapter(7, "Hamachi", true, new[] { "25.10.0.1" }, new[] { "25.0.0.0/8" }, VpnDiscovery.Identify("Hamachi", "", "25.10.0.1")!);
        var known = new[] {
            new KnownVpnHost(7, "25.10.0.2", "vizinho ARP"), new KnownVpnHost(7, "25.10.0.2", "rota individual /32"),
            new KnownVpnHost(7, "25.10.0.3", "rota individual /32"), new KnownVpnHost(8, "25.10.0.4", "outra interface"),
            new KnownVpnHost(7, "25.10.0.5", "rota substituída"), new KnownVpnHost(7, "25.10.0.1", "IP local") };
        int routingLookups = 0;
        int BestInterface(string ip) { routingLookups++; return ip == "25.10.0.5" ? 8 : 7; }
        var selected = VpnDiscovery.SelectHosts(new[] { hamachi }, known, false, BestInterface);
        Check(selected.Hosts.Keys.Order().SequenceEqual(new[] { "25.10.0.2", "25.10.0.3" }), "Deduplica ARP/rota e filtra interface, rota atual e IP local");
        Check(routingLookups == 3, "Rede /8 testa somente hosts conhecidos sem expansão");
        Check(selected.Hosts.Values.All(v => v.Contains("Hamachi")), "Resultado conserva identificação da VPN");
        var withLocal = VpnDiscovery.SelectHosts(new[] { hamachi }, known, true, BestInterface);
        Check(withLocal.Hosts.Count == 3 && withLocal.Hosts.ContainsKey("25.10.0.1"), "Opção local inclui IP do adaptador uma única vez");
        var down = VpnDiscovery.SelectHosts(new[] { hamachi with { Active = false } }, known, true, _ => throw new Exception("Não consultar rota de VPN desconectada"));
        Check(down.Hosts.Count == 0 && down.Report.Contains("desconectado"), "VPN desconectada não gera consultas");
        var empty = VpnDiscovery.SelectHosts(Array.Empty<VpnAdapter>(), known, true, _ => throw new Exception("Sem VPN não consultar rota"));
        Check(empty.Hosts.Count == 0 && empty.Report.Contains("Nenhum adaptador VPN"), "Ausência de VPN informada sem testar IPs");
        var noPeers = VpnDiscovery.SelectHosts(new[] { hamachi }, Array.Empty<KnownVpnHost>(), false, _ => 7);
        Check(noPeers.Hosts.Count == 0 && noPeers.Report.Contains("podem não expor todos"), "VPN sem vizinhos não expande faixa e explica limitação");
        foreach (var invalid in new[] { "25.0.0.0", "25.255.255.255", "224.0.0.1", "255.255.255.255", "127.0.0.1", "0.0.0.0", "169.254.1.2", "120.0.0.1", "25.010.0.2", "bad", "::1" })
        {
            var filtered = VpnDiscovery.SelectHosts(new[] { hamachi }, new[] { new KnownVpnHost(7, invalid, "ARP") }, false, _ => throw new Exception("Endereço inválido chegou à consulta de rota"));
            Check(filtered.Hosts.Count == 0, "Exclui destino inválido/especial " + invalid);
        }
        Check(VpnDiscovery.SelectHosts(new[] { hamachi }, known, false, _ => -1).Hosts.Count == 0, "Falha ao resolver interface impede associação indevida");
    }
}

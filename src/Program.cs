using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace BuscaServidor;

internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        if (FirewallControl.IsCommand(args)) { Environment.ExitCode = FirewallControl.RunElevated(args); return; }
#if TEST_BUILD
        if (args.Contains("--self-test")) { SelfTest.Run().GetAwaiter().GetResult(); return; }
#endif
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed record Endpoint(string Address, int Port, string Version, string Status);
internal sealed record Candidate(string Address, int[] Ports, string Evidence, int? IdentifiedPort = null);

internal static class Network
{
    public static bool InSubnet(string address, string cidr)
    {
        var parts = cidr.Split('/'); int prefix = int.Parse(parts[1]);
        uint mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        uint network = Number(IPAddress.Parse(parts[0])) & mask;
        uint value = Number(IPAddress.Parse(address));
        return (value & mask) == network && (prefix >= 31 || value != network && value != (network | ~mask));
    }
    public static string GuessOS(int ttl) => ttl is > 64 and <= 128 ? "Windows" : ttl is > 0 and <= 64 ? "Linux/Unix" : "SO indeterminado";
    public static async Task<string> PingInfo(string ip, int timeout, CancellationToken ct)
    {
        using var ping = new Ping();
        try
        {
            var reply = await ping.SendPingAsync(IPAddress.Parse(ip), TimeSpan.FromMilliseconds(timeout), new byte[16], null, ct);
            if (reply.Status != IPStatus.Success) return "Ping: sem resposta • TTL/SO indisponíveis";
            int? ttl = reply.Options?.Ttl;
            return $"Ping: {reply.RoundtripTime} ms • TTL: {(ttl.HasValue ? ttl.Value.ToString() : "indisponível")} • {(ttl.HasValue ? GuessOS(ttl.Value) : "SO indeterminado")}";
        }
        catch (PingException) { return "Ping indisponível • SO indeterminado"; }
    }
    public static uint Number(IPAddress ip) { var b = ip.GetAddressBytes(); return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3]; }
    public static string Address(uint n) => new IPAddress(new byte[] { (byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n }).ToString();
    public static IPAddress Parse(string s)
    {
        if (s.Split('.').Length != 4 || s.Split('.').Any(p => !byte.TryParse(p, out _) || p.Length > 1 && p[0] == '0') || !IPAddress.TryParse(s, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Informe IPv4 completo, por exemplo 192.168.1.10.");
        if (ip.ToString() == "120.0.0.1") throw new ArgumentException("120.0.0.1 não é localhost nem endereço de rede privada. Confira a configuração. Para testar este computador, use 127.0.0.1.");
        return ip;
    }
    public static List<string> Targets(string input, bool local)
    {
        var list = new List<string>();
        foreach (var raw in input.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = raw.Trim().Split('/'); var ip = Parse(parts[0]); var n = Number(ip);
            if (parts.Length == 1) { list.Add(ip.ToString()); continue; }
            if (parts.Length != 2 || !int.TryParse(parts[1], out var prefix) || prefix < 20 || prefix > 32)
                throw new ArgumentException("Use uma faixa CIDR de /20 a /32 (até 4096 endereços), ou um IPv4 individual.");
            uint count = 1u << (32 - prefix); uint start = n & ~(count - 1);
            uint first = prefix < 31 ? 1u : 0u, end = prefix < 31 ? count - 1 : count;
            if (list.Count + end - first > 4096) throw new ArgumentException("Limite de 4096 endereços por busca. Divida a rede em faixas menores.");
            for (uint i = first; i < end; i++) list.Add(Parse(Address(start + i)).ToString());
        }
        if (list.Count == 0 && !local) throw new ArgumentException("Informe uma faixa de rede ou um endereço.");
        if (local) list.Add("127.0.0.1");
        if (list.Count > 4097) throw new ArgumentException("Limite de 4096 endereços por busca.");
        return list.Distinct().ToList();
    }
    public static List<(string Address, string Cidr, string Name)> Interfaces()
    {
        var result = new List<(string, string, string)>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType != NetworkInterfaceType.Loopback))
        foreach (var u in nic.GetIPProperties().UnicastAddresses.Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork && !x.Address.ToString().StartsWith("169.254.")))
        {
            var prefix = u.PrefixLength;
            uint mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
            result.Add((u.Address.ToString(), Address(Number(u.Address) & mask) + "/" + prefix, nic.Name));
        }
        return result;
    }
    [DllImport("iphlpapi.dll")] static extern int GetIpNetTable(IntPtr table, ref int size, bool order);
    public static HashSet<string> CachedNeighbors(string? interfaceAddress = null)
    {
        var result = new HashSet<string>(); int size = 0;
        int? interfaceIndex = null;
        if (interfaceAddress != null)
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up &&
                n.GetIPProperties().UnicastAddresses.Any(a => a.Address.ToString() == interfaceAddress));
            interfaceIndex = nic?.GetIPProperties().GetIPv4Properties()?.Index ?? -1;
        }
        GetIpNetTable(IntPtr.Zero, ref size, false);
        if (size < 4 || size > 16 * 1024 * 1024) return result;
        var memory = Marshal.AllocHGlobal(size);
        try
        {
            if (GetIpNetTable(memory, ref size, false) != 0) return result;
            int count = Marshal.ReadInt32(memory);
            for (int i = 0; i < count && 4 + (i + 1) * 24 <= size; i++)
            {
                var row = IntPtr.Add(memory, 4 + i * 24); int type = Marshal.ReadInt32(row, 20);
                if ((type == 3 || type == 4) && Marshal.ReadInt32(row, 4) > 0 && (!interfaceIndex.HasValue || Marshal.ReadInt32(row, 0) == interfaceIndex))
                    result.Add(new IPAddress(BitConverter.GetBytes(Marshal.ReadInt32(row, 16))).ToString());
            }
        }
        finally { Marshal.FreeHGlobal(memory); }
        return result;
    }
    public static async Task<string?> DiscoverHost(string ip, int timeout, HashSet<string> cached, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (IPAddress.IsLoopback(IPAddress.Parse(ip))) return "Este computador (loopback)";
        using var ping = new Ping();
        try
        {
            var reply = await ping.SendPingAsync(IPAddress.Parse(ip), TimeSpan.FromMilliseconds(timeout), new byte[16], null, ct);
            if (reply.Status == IPStatus.Success) return "Resposta de rede (ICMP)";
        }
        catch (PingException) { }
        ct.ThrowIfCancellationRequested();
        return cached.Contains(ip) ? "Cache local ARP; presença ainda não confirmada" : null;
    }
}

internal sealed partial class MainForm : Form
{
    readonly ComboBox adapter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420 };
    readonly TextBox targets = new() { Width = 300, Enabled = false };
    readonly CheckBox rangeMode = new() { Text = "Usar faixa / IP manual", Checked = false, AutoSize = true };
    readonly TextBox ports = new() { Text = "3306,3307,3308,3310,4387", Width = 230 };
    readonly NumericUpDown timeout = new() { Minimum = 300, Maximum = 10000, Value = 1000, Increment = 100, Width = 85 };
    readonly CheckBox local = new() { Text = "Testar também 127.0.0.1 (este computador)", Checked = true, AutoSize = true };
    readonly CheckBox consultVpn = new() { Text = "Consultar VPN", Checked = false, AutoSize = true };
    readonly Button scan = new ModernButton { Text = "&Buscar servidores", Primary = true };
    readonly CheckBox force = new() { Text = "Força bruta", Checked = false, AutoSize = true };
    readonly Button disableFirewall = new ModernButton { Text = "Desativar firewall" };
    readonly Button cancel = new ModernButton { Text = "&Cancelar", Enabled = false };
    readonly Button export = new ModernButton { Text = "&Salvar relatório", Enabled = false };
    readonly Button copy = new ModernButton { Text = "Copiar &IP", Enabled = false };
    readonly ToolTip tips = new();
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowHeadersVisible = false };
    readonly TextBox details = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 10) };
    readonly Label status = new() { Text = "Pronto para buscar.", AutoSize = true };
    readonly ProgressBar progress = new() { Width = 200 };
    CancellationTokenSource? operation;
    readonly List<(string Address, string Cidr, string Name)> interfaces = Network.Interfaces();
    readonly List<string> reports = new();
    readonly Func<bool, IEnumerable<string>?, VpnSnapshot> discoverVpn;
    public MainForm() : this(VpnDiscovery.Discover) { }
    internal MainForm(Func<bool, IEnumerable<string>?, VpnSnapshot> discoverVpn)
    {
        this.discoverVpn = discoverVpn;
        InitializeVisualLayout();
        foreach (var nic in interfaces) adapter.Items.Add($"{nic.Name}: {nic.Address} ({nic.Cidr})");
        adapter.SelectedIndexChanged += (_, _) => { if (adapter.SelectedIndex >= 0) targets.Text = interfaces[adapter.SelectedIndex].Cidr; };
        if (adapter.Items.Count > 0) adapter.SelectedIndex = 0;
        rangeMode.CheckedChanged += (_, _) => targets.Enabled = rangeMode.Checked && operation == null;
        scan.Click += async (_, _) => await Scan(); cancel.Click += (_, _) => operation?.Cancel(); export.Click += (_, _) => Export();
        disableFirewall.Click += async (_, _) => await ChangeFirewall(false);
        tips.SetToolTip(force, "Percorre a sub-rede completa da placa selecionada ou a faixa manual (até 4096 IPs). Não testa usuários ou senhas.");
        copy.Click += (_, _) => { if (grid.SelectedRows.Count > 0) { try { Clipboard.SetText(grid.SelectedRows[0].Cells[0].Value?.ToString() ?? ""); status.Text = "IP copiado. Loopback serve somente neste computador."; } catch (ExternalException) { status.Text = "Área de transferência ocupada. Tente novamente."; } } };
        grid.SelectionChanged += (_, _) => { if (operation == null) ShowSelection(); RefreshActions(); };
        FormClosing += (_, _) => operation?.Cancel();
        var accessible = new (Control Control, string Name)[] { (adapter, "Placa de rede"), (targets, "Faixa de rede ou IP conhecido"), (ports, "Portas do banco"), (timeout, "Tempo de espera ICMP em milissegundos"), (grid, "Serviços de banco encontrados"), (details, "Relatório do computador selecionado"), (progress, "Progresso da descoberta") };
        foreach (var item in accessible) item.Control.AccessibleName = item.Name;
        tips.SetToolTip(targets, "Informe um IPv4 conhecido para incluí-lo mesmo se o ping estiver bloqueado. Ou informe uma faixa, por exemplo 192.168.1.0/24.");
        details.Text = "Clique em Buscar servidores para localizar os serviços da rede. Selecione um resultado para ver o diagnóstico.";
    }
    void Busy(bool active)
    {
        scan.Enabled = force.Enabled = disableFirewall.Enabled = export.Enabled = adapter.Enabled = rangeMode.Enabled = ports.Enabled = timeout.Enabled = local.Enabled = consultVpn.Enabled = !active;
        targets.Enabled = !active && rangeMode.Checked;
        cancel.Enabled = active; grid.Enabled = !active;
        if (active) operation = new CancellationTokenSource(); else { operation?.Dispose(); operation = null; }
        RefreshActions();
    }
    void RefreshActions()
    {
        bool idle = operation == null;
        copy.Enabled = idle && grid.SelectedRows.Count > 0 && grid.SelectedRows[0].Tag is Candidate;
        export.Enabled = idle && (grid.Rows.Count > 0 || reports.Count > 0);
    }
    void ShowSelection()
    {
        if (grid.SelectedRows.Count == 0) return;
        var row = grid.SelectedRows[0];
        details.Text = row.Cells[4].Tag is string report ? report : $"Computador: {row.Cells[0].Value}\r\nPortas configuradas: {row.Cells[1].Value}\r\nServiço ainda não identificado nesta linha.";
    }
    int[] ConfiguredPorts()
    {
        var items = ports.Text.Split(',');
        if (items.Length == 0 || items.Length > 20 || items.Any(p => !int.TryParse(p.Trim(), out var value) || value < 1 || value > 65535))
            throw new ArgumentException("Informe de 1 a 20 portas, entre 1 e 65535, separadas por vírgulas.");
        return items.Select(p => int.Parse(p.Trim())).Distinct().ToArray();
    }
    void RefreshInterfaces()
    {
        var selected = adapter.SelectedIndex >= 0 ? interfaces[adapter.SelectedIndex] : default;
        var current = Network.Interfaces();
        if (current.SequenceEqual(interfaces)) return;
        string manualText = targets.Text;
        adapter.Items.Clear(); interfaces.Clear(); interfaces.AddRange(current);
        foreach (var nic in interfaces) adapter.Items.Add($"{nic.Name}: {nic.Address} ({nic.Cidr})");
        int index = interfaces.FindIndex(n => n.Address == selected.Address && n.Name == selected.Name);
        if (index < 0) index = interfaces.FindIndex(n => n.Name == selected.Name);
        adapter.SelectedIndex = index >= 0 ? index : interfaces.Count > 0 ? 0 : -1;
        if (rangeMode.Checked) targets.Text = manualText;
    }
    Task Scan() => RunScan(force.Checked);
    async Task ChangeFirewall(bool restore)
    {
        Busy(true); cancel.Enabled = false;
        try
        {
            string result = restore ? await FirewallControl.RestoreAsync(this) : await FirewallControl.DisableAsync(this);
            if (!IsDisposed) { details.Text = result; reports.Add(result); status.Text = "Operação de firewall concluída; veja os detalhes."; }
        }
        finally { if (!IsDisposed) Busy(false); }
    }
    async Task RunScan(bool exhaustive)
    {
        List<string> addresses; int[] portList; VpnSnapshot? vpn = null;
        try
        {
            portList = ConfiguredPorts(); RefreshInterfaces();
            if (rangeMode.Checked) addresses = Network.Targets(targets.Text, local.Checked);
            else if (exhaustive)
            {
                if (adapter.SelectedIndex < 0) throw new ArgumentException("Selecione uma placa de rede ou informe uma faixa manual.");
                addresses = ScanTargets.Full(new[] { interfaces[adapter.SelectedIndex] }, local.Checked);
            }
            else addresses = ScanTargets.Automatic(interfaces, local.Checked);
            if (!rangeMode.Checked && !local.Checked) addresses.RemoveAll(ip => interfaces.Any(n => n.Address == ip));
            if (consultVpn.Checked) vpn = discoverVpn(local.Checked, addresses);
            if (vpn != null) addresses = addresses.Concat(vpn.Hosts.Keys).Distinct().ToList();
            if (addresses.Count > 4097) throw new ArgumentException("Limite de 4096 IPs. Informe uma faixa menor em Usar faixa / IP manual.");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Confira a busca"); return; }
        Busy(true); grid.Rows.Clear(); reports.Clear(); details.Clear(); progress.Value = 0;
        string scope = rangeMode.Checked ? "IP / faixa manual" : exhaustive ? "Sub-rede completa da placa selecionada" : "Automático como BAT: /24 de todas as placas IPv4 ativas";
        reports.Add($"{scope}\r\n{addresses.Count} IP(s) selecionado(s). Portas: {string.Join(", ", portList)}.\r\nSem login, consultas SQL ou leitura de tabelas.");
        if (vpn != null) { reports.Add(vpn.Report); details.Text = vpn.Report; }
        if (addresses.Count == 0) { status.Text = "Nenhum IPv4 disponível para testar."; details.Text = (vpn?.Report ?? "") + "\r\nConfira as placas conectadas ou informe IP / faixa manual."; Busy(false); return; }
        var ct = operation!.Token; var elapsed = Stopwatch.StartNew();
        int done = 0, total = addresses.Count * portList.Length;
        uint seconds = (uint)Math.Max(1, Math.Ceiling((double)timeout.Value / 1000));
        int pingTimeout = (int)timeout.Value;
        var foundHosts = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();
        var neighbors = Network.CachedNeighbors();
        addresses = addresses.OrderByDescending(ip => neighbors.Contains(ip)).ToList();
        var update = new Progress<(Endpoint Service, string Ping)>(item =>
        {
            if (IsDisposed) return;
            var service = item.Service;
            string kind = IPAddress.IsLoopback(IPAddress.Parse(service.Address)) ? "Local / loopback" : interfaces.Any(i => i.Address == service.Address) ? "Este PC / rede" : "Rede";
            string? vpnInfo = null;
            if (vpn != null && vpn.Hosts.TryGetValue(service.Address, out vpnInfo)) kind += " / VPN";
            int index = grid.Rows.Add(service.Address, service.Port, kind, "MySQL/MariaDB\r\n" + item.Ping + (vpnInfo == null ? "" : "\r\n" + vpnInfo.Split('•')[0].Trim()), service.Status);
            var row = grid.Rows[index]; row.Tag = new Candidate(service.Address, new[] { service.Port }, "Serviço identificado", service.Port);
            string report = $"SERVIÇO IDENTIFICADO\r\nIP: {service.Address}\r\nPorta: {service.Port}\r\nVersão: {service.Version}\r\n{service.Status}\r\n{item.Ping}\r\n" +
                (vpnInfo == null ? "" : "Indicação VPN: " + vpnInfo + "\r\n") +
                "Sistema operacional estimado pelo TTL; não confirmado pelo servidor.\r\nIdentificação pelo protocolo enviado pelo servidor. Nenhum login, comando SQL ou leitura de tabelas.\r\n" +
                (IPAddress.IsLoopback(IPAddress.Parse(service.Address)) ? "Loopback: não use este IP em outros terminais." : $"Configuração dos terminais: IP {service.Address}, porta {service.Port}.");
            row.Cells[4].Tag = report; reports.Add(report);
            if (grid.Rows.Count == 1) { row.Selected = true; details.Text = report; }
            status.Text = $"{grid.Rows.Count} serviço(s) encontrado(s) • {elapsed.Elapsed.TotalSeconds:F1} s";
        });
        var progressUpdate = new Progress<int>(value =>
        {
            if (IsDisposed) return;
            progress.Value = Math.Min(100, value * 100 / total);
            if (grid.Rows.Count == 0) status.Text = $"Buscando serviços • {elapsed.Elapsed.TotalSeconds:F1} s • {scope}";
        });
        try
        {
            var probes = addresses.SelectMany(ip => portList.Select(port => (ip, port)));
                await Parallel.ForEachAsync(probes, new ParallelOptions { MaxDegreeOfParallelism = 512, CancellationToken = ct }, async (probe, token) =>
                {
                    var (ip, port) = probe;
                    if (!foundHosts.ContainsKey(ip))
                    {
                        var service = await ServiceProbe.Check(ip, port, token, seconds);
                        if (service != null && foundHosts.TryAdd(ip, true))
                        {
                            string ping;
                            try { ping = await Network.PingInfo(ip, pingTimeout, token); }
                            catch (OperationCanceledException) when (token.IsCancellationRequested) { ping = "Ping não concluído: operação cancelada. Serviço já identificado."; }
                            ((IProgress<(Endpoint, string)>)update).Report((service, ping));
                        }
                    }
                    int current = Interlocked.Increment(ref done);
                    if (current % 16 == 0 || current == total) ((IProgress<int>)progressUpdate).Report(current);
                });
            progress.Value = 100;
            status.Text = $"{grid.Rows.Count} serviço(s) encontrado(s) em {elapsed.Elapsed.TotalSeconds:F1} s • todos os IPs selecionados testados.";
            if (vpn != null) status.Text += $" VPN: {vpn.Hosts.Count} IP(s).";
            if (grid.Rows.Count > 0) { grid.Rows[0].Selected = true; ShowSelection(); }
            else details.Text = "Nenhum serviço MySQL/MariaDB respondeu nas portas informadas. Confira as portas, a rede e a espera. Força bruta amplia a busca para a sub-rede completa da placa selecionada; também é possível informar um IP ou faixa manual.";
            if (vpn != null) details.AppendText("\r\n\r\n" + vpn.Report);
        }
        catch (OperationCanceledException) { status.Text = "Busca cancelada; resultados parciais."; }
        catch (Exception ex) { status.Text = "Falha na busca: " + ex.GetType().Name; }
        finally { if (!IsDisposed) Busy(false); }
    }
    void Export()
    {
        using var dialog = new SaveFileDialog { Filter = "Relatório de texto (*.txt)|*.txt", FileName = $"Busca-Servidor-Suporte-{DateTime.Now:yyyyMMdd-HHmmss}.txt" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var report = new StringBuilder($"BUSCA SERVIDOR — {DateTime.Now:dd/MM/yyyy HH:mm:ss}\r\n{status.Text}\r\n");
        foreach (DataGridViewRow row in grid.Rows) report.AppendLine(string.Join(" | ", row.Cells.Cast<DataGridViewCell>().Select(c => c.Value?.ToString())));
        foreach (var item in reports) report.AppendLine("\r\n" + item);
        try { File.WriteAllText(dialog.FileName, report.ToString(), new UTF8Encoding(true)); status.Text = "Relatório salvo."; }
        catch (Exception ex) { MessageBox.Show(this, "Não foi possível salvar: " + ex.Message); }
    }
}

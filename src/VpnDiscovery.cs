using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace BuscaServidor;

internal sealed record VpnIdentity(string Product, string Confidence, string Evidence)
{
    public string Label => $"{Product} ({Confidence})";
}
internal sealed record VpnAdapter(int Index, string Name, bool Active, string[] Addresses, string[] Networks, VpnIdentity Identity);
internal sealed record KnownVpnHost(int Index, string Address, string Source);
internal sealed record VpnSnapshot(Dictionary<string, string> Hosts, string Report);

internal static class VpnDiscovery
{
    public static VpnIdentity? Identify(string name, string description, string address, NetworkInterfaceType type = NetworkInterfaceType.Ethernet)
    {
        string text = (name + " " + description).ToLowerInvariant();
        string? product = text.Contains("hamachi") ? "Hamachi" : text.Contains("radmin") ? "Radmin VPN" :
            text.Contains("tailscale") ? "Tailscale" : text.Contains("zerotier") ? "ZeroTier" :
            text.Contains("softether") ? "SoftEther VPN" : text.Contains("fortinet") || text.Contains("forticlient") ? "FortiClient" :
            text.Contains("anyconnect") || text.Contains("cisco secure client") ? "Cisco VPN" :
            text.Contains("pangp") || text.Contains("globalprotect") ? "GlobalProtect" :
            text.Contains("openvpn") ? "OpenVPN" : text.Contains("wireguard") ? "WireGuard" : null;
        if (product != null) return new(product, "alta", "nome/descrição do adaptador");
        if (text.Contains("tap-windows")) return new("Possível OpenVPN", "possível", "driver TAP compartilhado");
        if (text.Contains("wintun")) return new("VPN Wintun", "indeterminada", "driver compartilhado; pode ser WireGuard, OpenVPN ou outro produto");
        var prefix = FromAddress(address);
        if (prefix != null) return prefix;
        return type is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp || text.Contains("vpn")
            ? new("VPN / túnel", "indeterminada", "tipo/nome do adaptador") : null;
    }

    public static VpnIdentity? FromAddress(string address)
    {
        if (!IPAddress.TryParse(address, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork) return null;
        var b = ip.GetAddressBytes();
        string? name = b[0] == 25 ? "Hamachi provável" : b[0] == 26 ? "Radmin VPN provável" :
            b[0] == 100 && b[1] is >= 64 and <= 127 ? "Tailscale possível" :
            b[0] == 10 && b[1] is 8 or 9 or 10 || b[0] == 172 && b[1] == 27 ? "Possível OpenVPN" :
            b[0] == 10 && (b[1] == 13 && b[2] == 13 || b[1] == 14) ? "Possível WireGuard" :
            b[0] == 192 && b[1] == 168 && b[2] == 30 ? "Possível SoftEther VPN" :
            b[0] == 10 && b[1] is 147 or 241 ? "Possível ZeroTier" : null;
        return name == null ? null : new(name, b[0] is 25 or 26 ? "alta pelo prefixo" : b[0] == 100 ? "média pelo prefixo" : "possível", "prefixo de IP; não comprova produto nem conexão VPN");
    }

    public static List<VpnAdapter> Adapters()
    {
        var result = new List<VpnAdapter>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            try
            {
                var properties = nic.GetIPProperties();
                var unicast = properties.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork).ToArray();
                var identity = Identify(nic.Name, nic.Description, "");
                identity ??= unicast.Select(a => Identify(nic.Name, nic.Description, a.Address.ToString(), nic.NetworkInterfaceType)).FirstOrDefault(i => i != null);
                identity ??= Identify(nic.Name, nic.Description, "", nic.NetworkInterfaceType);
                if (identity == null) continue;
                int index = nic.Supports(NetworkInterfaceComponent.IPv4) ? properties.GetIPv4Properties()?.Index ?? -1 : -1;
                result.Add(new(index, nic.Name, nic.OperationalStatus == OperationalStatus.Up && index > 0 && unicast.Length > 0,
                    unicast.Select(a => a.Address.ToString()).ToArray(),
                    unicast.Select(a => a.Address + "/" + a.PrefixLength).ToArray(), identity));
            }
            catch (NetworkInformationException)
            {
                var identity = Identify(nic.Name, nic.Description, "", nic.NetworkInterfaceType);
                if (identity != null) result.Add(new(-1, nic.Name + " (IPv4 indisponível)", false, Array.Empty<string>(), Array.Empty<string>(), identity));
            }
        }
        return result;
    }

    public static bool Usable(string address, IEnumerable<string> networks)
    {
        IPAddress ip;
        try { ip = Network.Parse(address); } catch (ArgumentException) { return false; }
        var b = ip.GetAddressBytes();
        if (b[0] is 0 or 127 or >= 224 || b[0] == 169 && b[1] == 254) return false;
        uint value = Network.Number(ip);
        foreach (var cidr in networks)
        {
            var parts = cidr.Split('/'); int prefix = int.Parse(parts[1]);
            if (prefix >= 31) continue;
            uint mask = prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);
            uint net = Network.Number(IPAddress.Parse(parts[0])) & mask;
            if (value == net || value == (net | ~mask)) return false;
        }
        return true;
    }

    public static VpnSnapshot SelectHosts(IEnumerable<VpnAdapter> adapters, IEnumerable<KnownVpnHost> known,
        bool includeLocal, Func<string, int> bestInterface)
    {
        var all = adapters.ToArray(); var hosts = new Dictionary<string, string>(); var lines = new List<string> { "CONSULTA VPN" };
        var localAddresses = all.SelectMany(a => a.Addresses).ToHashSet();
        foreach (var adapter in all)
        {
            lines.Add($"{adapter.Name}: {adapter.Identity.Label} • {(adapter.Active ? "ativo" : "desconectado")} • {adapter.Identity.Evidence}.");
            if (!adapter.Active) continue;
            var candidates = known.Where(h => h.Index == adapter.Index);
            if (includeLocal) candidates = candidates.Concat(adapter.Addresses.Select(ip => new KnownVpnHost(adapter.Index, ip, "IP deste computador")));
            int count = 0;
            foreach (var host in candidates.DistinctBy(h => h.Address))
            {
                if (!Usable(host.Address, adapter.Networks) || !includeLocal && localAddresses.Contains(host.Address)) continue;
                if (!localAddresses.Contains(host.Address) && bestInterface(host.Address) != adapter.Index) continue;
                hosts[host.Address] = $"{adapter.Identity.Label} • {adapter.Name} • {host.Source} • {adapter.Identity.Evidence}";
                count++;
            }
            lines.Add($"  {count} IP(s) conhecido(s) selecionado(s); nenhuma faixa foi expandida.");
        }
        if (all.Length == 0) lines.Add("Nenhum adaptador VPN reconhecido neste computador.");
        lines.Add("VPNs podem não expor todos os participantes ao Windows. Se faltar um servidor, informe o IP ou uma faixa em Usar faixa / IP manual. A indicação descreve o adaptador/rota observado, não confirma uma sessão VPN autenticada.");
        return new(hosts, string.Join("\r\n", lines));
    }

    [DllImport("iphlpapi.dll")] static extern int GetIpNetTable(IntPtr table, ref int size, bool order);
    [DllImport("iphlpapi.dll")] static extern int GetIpForwardTable(IntPtr table, ref int size, bool order);
    [DllImport("iphlpapi.dll")] static extern int GetBestInterface(uint address, out int index);
    delegate int TableReader(IntPtr table, ref int size, bool order);
    static string ReadAddress(IntPtr row, int offset) => new IPAddress(BitConverter.GetBytes(Marshal.ReadInt32(row, offset))).ToString();
    static List<KnownVpnHost> ReadTable(TableReader read, bool routes)
    {
        var result = new List<KnownVpnHost>(); int size = 0;
        int code = read(IntPtr.Zero, ref size, false);
        if (code != 122 && code != 0) throw new NetworkInformationException(code);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (size < 4 || size > 16 * 1024 * 1024) throw new InvalidOperationException("Tabela de rede inválida.");
            int allocated = size; var memory = Marshal.AllocHGlobal(allocated);
            try
            {
                code = read(memory, ref size, false);
                if (code == 122) continue;
                if (code != 0) throw new NetworkInformationException(code);
                int count = Marshal.ReadInt32(memory), stride = routes ? 56 : 24;
                for (int i = 0; i < count && 4L + (i + 1L) * stride <= allocated; i++)
                {
                    var row = IntPtr.Add(memory, 4 + i * stride); int type = Marshal.ReadInt32(row, 20);
                    if (routes)
                    {
                        if (Marshal.ReadInt32(row, 4) == -1 && type is 3 or 4)
                            result.Add(new(Marshal.ReadInt32(row, 16), ReadAddress(row, 0), "rota individual /32"));
                    }
                    else if (type is 3 or 4 && Marshal.ReadInt32(row, 4) > 0)
                        result.Add(new(Marshal.ReadInt32(row, 0), ReadAddress(row, 16), "vizinho ARP"));
                }
                return result;
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
        throw new InvalidOperationException("A rede mudou durante a consulta; tente novamente.");
    }

    public static VpnSnapshot Discover(bool includeLocal, IEnumerable<string>? explicitHosts = null)
    {
        var adapters = Adapters(); var known = new List<KnownVpnHost>(); var warnings = new List<string>();
        foreach (bool routes in new[] { false, true })
        {
            try { known.AddRange(ReadTable(routes ? GetIpForwardTable : GetIpNetTable, routes)); }
            catch (Exception ex) when (ex is NetworkInformationException or InvalidOperationException)
            { warnings.Add($"Não foi possível ler {(routes ? "rotas" : "vizinhos ARP")}: {ex.Message}"); }
        }
        if (explicitHosts != null)
            foreach (string ip in explicitHosts)
                if (Usable(ip, Array.Empty<string>()) && GetBestInterface(BitConverter.ToUInt32(IPAddress.Parse(ip).GetAddressBytes()), out int index) == 0)
                    known.Add(new(index, ip, "endereço selecionado para busca"));
        var snapshot = SelectHosts(adapters, known, includeLocal, ip =>
            GetBestInterface(BitConverter.ToUInt32(IPAddress.Parse(ip).GetAddressBytes()), out int index) == 0 ? index : -1);
        return snapshot with { Report = snapshot.Report + (warnings.Count == 0 ? "" : "\r\n" + string.Join("\r\n", warnings)) };
    }
}

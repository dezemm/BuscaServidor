namespace BuscaServidor;

internal static class ScanTargets
{
    public static List<string> Automatic(IEnumerable<(string Address, string Cidr, string Name)> interfaces, bool includeLocal)
        => Build(interfaces, includeLocal, false);
    public static List<string> Full(IEnumerable<(string Address, string Cidr, string Name)> interfaces, bool includeLocal)
        => Build(interfaces, includeLocal, true);

    static List<string> Build(IEnumerable<(string Address, string Cidr, string Name)> interfaces, bool includeLocal, bool full)
    {
        var adapters = interfaces.ToArray();
        var locals = adapters.Select(i => i.Address).ToHashSet();
        var result = new HashSet<string>();
        foreach (var nic in adapters)
        {
            if (!Usable(nic.Address)) continue;
            var address = Network.Parse(nic.Address);
            var parts = nic.Cidr.Split('/');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int prefix) || prefix < 0 || prefix > 32)
                throw new ArgumentException($"Máscara inválida no adaptador {nic.Name}.");
            int effective = full ? prefix : Math.Max(24, prefix);
            if (effective < 20) throw new ArgumentException($"A rede {nic.Cidr} de {nic.Name} excede 4096 endereços. Use uma faixa manual menor.");
            uint count = 1u << (32 - effective);
            uint start = Network.Number(address) & ~(count - 1);
            uint first = effective < 31 ? 1u : 0u, last = effective < 31 ? count - 1 : count;
            for (uint i = first; i < last; i++)
            {
                string candidate = Network.Address(start + i);
                if (Usable(candidate) && (includeLocal || !locals.Contains(candidate))) result.Add(candidate);
                if (result.Count > 4096) throw new ArgumentException("Limite de 4096 endereços por busca. Selecione uma faixa manual menor.");
            }
        }
        if (includeLocal)
        {
            foreach (string local in locals.Where(Usable)) result.Add(local);
            if (result.Count > 4096) throw new ArgumentException("Limite de 4096 endereços por busca. Selecione uma faixa manual menor.");
            result.Add("127.0.0.1");
        }
        return result.ToList();
    }

    static bool Usable(string value)
    {
        if (value == "120.0.0.1") return false;
        var bytes = Network.Parse(value).GetAddressBytes();
        return bytes[0] is > 0 and < 224 && bytes[0] != 127 && !(bytes[0] == 169 && bytes[1] == 254);
    }
}

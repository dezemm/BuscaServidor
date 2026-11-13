#if TEST_BUILD
namespace BuscaServidor;

internal static class FirewallTests
{
    public static void Run(Action<bool, string> check)
    {
        check(FirewallControl.IsCommand(["--firewall-disable"]) && FirewallControl.IsCommand(["--firewall-restore"]), "Firewall: comandos explícitos reconhecidos");
        check(!FirewallControl.IsCommand([]) && !FirewallControl.IsCommand(["--firewall-disable", "extra"]) && !FirewallControl.IsCommand(["cmd.exe"]), "Firewall: argumentos extras e comandos arbitrários rejeitados");
        var state = new Dictionary<int, bool> { [1] = true, [2] = false, [4] = true };
        var original = new[] { true, false, true };
        int result = FirewallControl.ApplyProfiles(p => state[p], (p, v) => state[p] = v, original, [false, false, false]);
        check(result == 0 && state.Values.All(v => !v), "Firewall simulado: todos os perfis desativados");
        result = FirewallControl.ApplyProfiles(p => state[p], (p, v) => state[p] = v, [false, false, false], original);
        check(result == 0 && state[1] && !state[2] && state[4], "Firewall simulado: restauração preserva perfil originalmente desligado");
        result = FirewallControl.ApplyProfiles(p => state[p], (p, v) => { if (p == 4 && !v) throw new InvalidOperationException(); state[p] = v; }, original, [false, false, false]);
        check(result == 5 && state[1] && !state[2] && state[4], "Firewall simulado: falha parcial reverte alterações");
        result = FirewallControl.ApplyProfiles(p => state[p], (p, v) => { }, original, [false, false, false]);
        check(result == 5 && state[1] && state[4], "Firewall simulado: configuração ignorada é detectada");
        result = FirewallControl.ApplyProfiles(p => state[p], (p, v) => { if (p == 4 || v) throw new InvalidOperationException(); state[p] = v; }, original, [false, false, false]);
        check(result == 6, "Firewall simulado: falha na reversão informada");
        check(FirewallControl.DescribeResult(4, true).Contains("Nenhuma alteração"), "Firewall: restauração sem backup é explicada");
        using var dialog = FirewallControl.CreateConfirmation(false);
        var buttons = dialog.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().ToArray();
        check(buttons.Any(b => b.Text == "Confirmar") && buttons.Any(b => b.Text == "Cancelar"), "Firewall: confirmação tem Confirmar e Cancelar");
        check(dialog.AcceptButton == dialog.CancelButton, "Firewall: Enter não confirma desativação acidentalmente");
    }
}
#endif

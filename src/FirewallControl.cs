using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace BuscaServidor;

internal static class FirewallControl
{
    const string BackupPath = @"SOFTWARE\BuscaServidorSuporte\FirewallBackup";
    static readonly int[] Profiles = [1, 2, 4]; // Domain, Private, Public; never combined for COM.

    public static Task<string> DisableAsync(IWin32Window owner) => ShowChangeAsync(owner, false);
    public static Task<string> RestoreAsync(IWin32Window owner) => ShowChangeAsync(owner, true);

    public static async Task<string> ShowChangeAsync(IWin32Window owner, bool restore)
    {
        using var dialog = CreateConfirmation(restore);
        if (dialog.ShowDialog(owner) != DialogResult.OK) return "Operação de firewall cancelada. Nenhuma alteração realizada.";
        try
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Executável não localizado.");
            using var process = Process.Start(new ProcessStartInfo(executable)
            {
                Arguments = restore ? "--firewall-restore" : "--firewall-disable",
                UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden
            }) ?? throw new InvalidOperationException("Não foi possível iniciar a operação.");
            await process.WaitForExitAsync();
            return DescribeResult(process.ExitCode, restore);
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 1223) { return "Elevação de administrador cancelada. Nenhuma alteração realizada."; }
        catch (Exception e) { return "Não foi possível executar a operação de firewall: " + e.Message; }
    }

    internal static Form CreateConfirmation(bool restore)
    {
        var form = new Form
        {
            Text = restore ? "Restaurar firewall" : "Desativar firewall para teste",
            ClientSize = new Size(560, 320), StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
            ShowInTaskbar = false, Font = new Font("Segoe UI", 10), AutoScaleMode = AutoScaleMode.Dpi
        };
        var message = new Label
        {
            Dock = DockStyle.Fill, Padding = new Padding(20),
            Text = restore
                ? "Restaurar os perfis Domínio, Privado e Público deste computador ao estado salvo antes da desativação?\n\nAs regras existentes serão preservadas. O Windows poderá solicitar autorização de administrador."
                : "Desativar o Firewall do Windows nos perfis Domínio, Privado e Público deste computador?\n\nO computador ficará sem essa proteção durante o teste. As regras serão preservadas. Isso não altera o firewall do servidor remoto.\n\nApós testar, reative a proteção pela Segurança do Windows. O Windows poderá solicitar autorização de administrador."
        };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(10), FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Size = new Size(110, 34) };
        var confirm = new Button { Text = "Confirmar", DialogResult = DialogResult.OK, Size = new Size(110, 34) };
        buttons.Controls.Add(cancel); buttons.Controls.Add(confirm);
        form.Controls.Add(message); form.Controls.Add(buttons);
        form.AcceptButton = cancel; form.CancelButton = cancel; // Enter cannot accidentally disable protection.
        return form;
    }

    internal static bool IsCommand(string[] args) => args.Length == 1 && args[0] is "--firewall-disable" or "--firewall-restore";

    // Called only by the explicit elevated helper process. No shells, paths or commands from the caller.
    public static int RunElevated(string[] args)
    {
        if (!IsCommand(args)) return 2;
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) return 3;
        using var mutex = new Mutex(false, @"Global\BuscaServidorSuporte.FirewallChange");
        var acquired = false;
        object? com = null;
        try
        {
            try { acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) return 7;
            bool restore = args[0] == "--firewall-restore";
            using var registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var existing = registry.OpenSubKey(BackupPath, true);
            bool[]? saved = null;
            if (existing != null)
            {
                if (existing.GetValue("Ready") is not int ready || ready != 1) return 8;
                saved = Profiles.Select(p => existing.GetValue("Profile" + p) is int value && value is 0 or 1
                    ? value == 1 : throw new InvalidOperationException("Estado salvo inválido.")).ToArray();
            }
            if (restore && saved == null) return 4;
            com = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2", true)!)!;
            dynamic policy = com;
            var before = Profiles.Select(p => (bool)policy.FirewallEnabled[p]).ToArray();
            if (!restore && saved == null)
            {
                // HKLM default ACL: normal users cannot overwrite the original state, even under alternate UAC credentials.
                using var backup = registry.CreateSubKey(BackupPath, true);
                for (int i = 0; i < Profiles.Length; i++) backup.SetValue("Profile" + Profiles[i], before[i] ? 1 : 0, RegistryValueKind.DWord);
                backup.SetValue("Ready", 1, RegistryValueKind.DWord);
                backup.Flush();
            }
            var desired = restore ? saved! : new bool[3];
            int result = ApplyProfiles(p => (bool)policy.FirewallEnabled[p], (p, enabled) => policy.FirewallEnabled[p] = enabled, before, desired);
            if (result != 0) return result;
            if (restore) registry.DeleteSubKeyTree(BackupPath, false);
            return 0;
        }
        catch { return 5; }
        finally
        {
            if (com != null && Marshal.IsComObject(com)) Marshal.FinalReleaseComObject(com);
            if (acquired) mutex.ReleaseMutex();
        }
    }

    // Pure injectable operation allows rollback tests without touching the actual firewall.
    internal static int ApplyProfiles(Func<int, bool> read, Action<int, bool> write, bool[] before, bool[] desired)
    {
        if (before.Length != 3 || desired.Length != 3) return 2;
        try
        {
            for (int i = 0; i < Profiles.Length; i++)
            {
                if (read(Profiles[i]) != desired[i]) write(Profiles[i], desired[i]);
                if (read(Profiles[i]) != desired[i]) throw new InvalidOperationException("Configuração não aplicada.");
            }
            return 0;
        }
        catch
        {
            bool rollback = true;
            for (int i = 0; i < Profiles.Length; i++)
                try
                {
                    if (read(Profiles[i]) != before[i]) write(Profiles[i], before[i]);
                    rollback &= read(Profiles[i]) == before[i];
                }
                catch { rollback = false; }
            return rollback ? 5 : 6;
        }
    }

    internal static string DescribeResult(int code, bool restore) => code switch
    {
        0 => (restore ? "Estado original dos perfis do Firewall do Windows restaurado." : "Perfis do Firewall do Windows desativados localmente. Após o teste, reative a proteção pela Segurança do Windows.")
            + " As regras foram preservadas. Políticas da organização e outros firewalls podem continuar em vigor.",
        3 => "A operação exige autorização de administrador.",
        4 => "Não existe estado original salvo por este aplicativo. Nenhuma alteração realizada.",
        6 => "A operação falhou e a reversão não foi completa. Confira e reative a proteção pela Segurança do Windows. O estado original permanece salvo.",
        7 => "Outra operação de firewall está em andamento. Aguarde e tente novamente.",
        8 => "O estado salvo do firewall está incompleto. Nenhuma alteração realizada. Confira a Segurança do Windows.",
        _ => "Não foi possível concluir a operação. Confira permissões, políticas e o serviço do Firewall do Windows. Se já houve desativação, confira a proteção na Segurança do Windows."
    };
}

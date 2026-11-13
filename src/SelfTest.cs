using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace BuscaServidor;
internal static class SelfTest
{
    public static async Task Run()
    {
        var results = new List<string>();
        void Check(bool value, string name)
        {
            if (!value) throw new Exception(name);
            results.Add("PASS " + name);
        }
        try
        {
            ApplicationConfiguration.Initialize();
            VisualTests.Run(results);
            using (var form = new MainForm { StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ShowInTaskbar = false })
            {
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                foreach (string name in new[] { "user", "password", "validate", "automatic", "readTables", "tls" })
                    Check(typeof(MainForm).GetField(name, flags) == null, "Interface não possui campo SQL/credencial: " + name);
                foreach (string name in new[] { "disableFirewall" })
                    Check(typeof(MainForm).GetField(name, flags)?.GetValue(form) is Button, "Botão disponível: " + name);
                Check(typeof(MainForm).GetField("force", flags)?.GetValue(form) is CheckBox { Checked: false }, "Força bruta é checkbox desmarcado por padrão");
                Check(typeof(MainForm).GetField("restoreFirewall", flags) == null, "Botão Restaurar firewall removido");
                form.Show(); Application.DoEvents(); form.PerformLayout();
                using var image = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                image.Save(Path.Combine(AppContext.BaseDirectory, "interface.png"));
                Check(form.Controls.Count > 0, "Janela criada e renderizada sem conexão de rede");
                FirewallTests.Run(Check);
                using var dialog = FirewallControl.CreateConfirmation(false);
                dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new Point(-3000, -3000); dialog.ShowInTaskbar = false;
                dialog.Show(); Application.DoEvents();
                using var confirmation = new Bitmap(dialog.Width, dialog.Height);
                dialog.DrawToBitmap(confirmation, new Rectangle(Point.Empty, dialog.Size));
                confirmation.Save(Path.Combine(AppContext.BaseDirectory, "confirmacao-firewall.png"));
                Check(dialog.CancelButton != null, "Confirmação de firewall pode ser cancelada sem executar alteração");
                dialog.Close(); form.Close();
            }
            SynchronizationContext.SetSynchronizationContext(null);
            Check(Network.GuessOS(128).Contains("Windows") && Network.GuessOS(64).Contains("Linux") && Network.GuessOS(255).Contains("indeterminado"), "TTL indica estimativa do sistema operacional");
            foreach (string input in new[] { "120.0.0.1", "120.0.0.01", "192.168.001.1", "127.1", "192.168.1.999", "192.168.0.0/16", "a.b.c.d", "192.168.1.1/33" })
            {
                bool rejected = false;
                try { Network.Targets(input, false); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Entrada inválida rejeitada: " + input);
            }
            await ProbeTests.Run(results);
            VpnTests.Run(results);
            await VpnTests.RunInterface(results);
            await ForceInterface(results);
            results.Add("SUCCESS");
        }
        catch (Exception ex) { results.Add("FAIL " + ex); Environment.ExitCode = 1; }
        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "self-test-results.txt"), results, new UTF8Encoding(true));
    }

    static Task ForceInterface(List<string> results)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var form = new MainForm { StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ShowInTaskbar = false };
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            T Field<T>(string name) => (T)typeof(MainForm).GetField(name, flags)!.GetValue(form)!;
            void Check(bool value, string name) { if (!value) throw new Exception(name); results.Add("PASS " + name); }
            form.Shown += async (_, _) =>
            {
                try
                {
                    using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
                    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    Field<CheckBox>("rangeMode").Checked = true;
                    Field<CheckBox>("local").Checked = false;
                    Field<TextBox>("targets").Text = "127.0.0.1";
                    Field<TextBox>("ports").Text = port.ToString();
                    var serving = Task.Run(async () =>
                    {
                        using var client = await listener.AcceptTcpClientAsync();
                        byte[] version = Encoding.ASCII.GetBytes("8.0.36-simulado\0");
                        byte[] payload = new byte[1 + version.Length + 44];
                        payload[0] = 10; version.CopyTo(payload, 1);
                        int offset = 1 + version.Length;
                        payload[offset] = 1; payload[offset + 13] = 0xff; payload[offset + 14] = 0xff;
                        byte[] packet = new byte[] { (byte)payload.Length, 0, 0, 0 }.Concat(payload).ToArray();
                        await client.GetStream().WriteAsync(packet);
                        using var timeout = new CancellationTokenSource(5000);
                        return await client.GetStream().ReadAsync(new byte[256], timeout.Token);
                    });
                    Field<CheckBox>("force").Checked = true;
                    Field<Button>("scan").PerformClick();
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    while (Field<CancellationTokenSource?>("operation") != null) await Task.Delay(10, deadline.Token);
                    await Task.Delay(100); // Drain posted progress callbacks before inspecting the grid.
                    Check(await serving == 0, "Força bruta não envia login ou SQL ao serviço simulado");
                    var grid = Field<DataGridView>("grid");
                    Check(grid.Rows.Count == 1 && grid.Rows[0].Cells[0].Value?.ToString() == "127.0.0.1", "Força bruta identifica serviço na faixa manual");
                    Check(grid.Rows[0].Cells[3].Value?.ToString() is string identification && identification.StartsWith("MySQL/MariaDB\r\n") && !identification.Contains("8.0.36") && !identification.Contains("provável"), "Identificação limpa mantém serviço, ping e SO sem versão numérica");
                    Check(Field<CheckBox>("force").Enabled && Field<Button>("scan").Enabled && !Field<Button>("cancel").Enabled, "Força bruta concluída restaura estado dos botões");
                    using var image = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                    image.Save(Path.Combine(AppContext.BaseDirectory, "interface-forca-bruta.png"));
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
}

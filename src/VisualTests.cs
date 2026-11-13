#if TEST_BUILD
using System.Reflection;

namespace BuscaServidor;

internal static class VisualTests
{
    public static void Run(List<string> results)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("Visual: " + name);
            results.Add("PASS Visual: " + name);
        }
        static IEnumerable<Control> Children(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (var descendant in Children(child)) yield return descendant;
            }
        }
        foreach (var scenario in new (float Scale, int Width, int Height, string Name)[] { (1f, 1180, 730, "notebook-100"),
            (1f, 1000, 680, "compacto-100"), (1.25f, 1320, 720, "notebook-125-simulado"),
            (1.25f, 1600, 960, "fullhd-125-simulado"), (1.5f, 1800, 1000, "fullhd-150-simulado") })
        {
            using var form = new MainForm { StartPosition = FormStartPosition.Manual, Location = new Point(-6000, -6000), ShowInTaskbar = false };
            T Field<T>(string name) => (T)typeof(MainForm).GetField(name, flags)!.GetValue(form)!;
            var fonts = new List<Font>();
            try
            {
                form.Show(); Application.DoEvents();
                if (scenario.Scale != 1)
                {
                    // Layout stress simulation only; does not change the user's Windows DPI setting.
                    var original = Children(form).Append(form).Select(c => (Control: c, Font: c.Font)).ToArray();
                    form.AutoScaleMode = AutoScaleMode.None;
                    form.Scale(new SizeF(scenario.Scale, scenario.Scale));
                    foreach (var item in original)
                    {
                        var font = new Font(item.Font.FontFamily, item.Font.Size * scenario.Scale, item.Font.Style);
                        fonts.Add(font); item.Control.Font = font;
                    }
                }
                form.Size = new Size(scenario.Width, scenario.Height);
                form.PerformLayout(); Application.DoEvents();
                Check(form.Width <= scenario.Width && form.Height <= scenario.Height, scenario.Name + ": janela respeita a área solicitada");
                using (var preview = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(preview, new Rectangle(Point.Empty, form.Size));
                    preview.Save(Path.Combine(AppContext.BaseDirectory, "visual-" + scenario.Name + "-layout.png"));
                }
                var names = new[] { "adapter", "targets", "ports", "timeout", "rangeMode", "local", "consultVpn", "force", "scan", "cancel", "copy", "export", "disableFirewall", "status", "progress" };
                var inputs = names.Select(n => (Name: n, Control: Field<Control>(n))).Where(x => x.Control.Visible).ToArray();
                var viewport = form.RectangleToScreen(form.ClientRectangle);
                foreach (var item in inputs)
                {
                    Rectangle rect = item.Control.RectangleToScreen(item.Control.ClientRectangle);
                    Check(rect.Width > 0 && rect.Height > 0 && viewport.Contains(rect), scenario.Name + ": controle dentro da janela: " + item.Name + " " + rect + " janela " + viewport);
                    for (Control? parent = item.Control.Parent; parent != null && parent != form; parent = parent.Parent)
                        Check(parent.RectangleToScreen(parent.ClientRectangle).Contains(rect), scenario.Name + ": controle sem recorte em " + parent.GetType().Name + ": " + item.Name);
                }
                foreach (var a in inputs)
                foreach (var b in inputs.Where(b => string.CompareOrdinal(a.Name, b.Name) < 0))
                    Check(!a.Control.RectangleToScreen(a.Control.ClientRectangle).IntersectsWith(b.Control.RectangleToScreen(b.Control.ClientRectangle)), scenario.Name + ": sem sobreposição " + a.Name + "/" + b.Name);
                void Capture(string state)
                {
                    form.PerformLayout(); Application.DoEvents();
                    using var bitmap = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(Path.Combine(AppContext.BaseDirectory, "visual-" + scenario.Name + "-" + state + ".png"));
                }
                Capture("inicial");
                Check(!Field<ProgressBar>("progress").Visible, scenario.Name + ": progresso oculto quando ocioso");
                Check(form.AcceptButton == Field<Button>("scan"), scenario.Name + ": Enter associado à busca");
                Check(!Field<CheckBox>("force").Checked && !Field<CheckBox>("consultVpn").Checked && Field<CheckBox>("local").Checked, scenario.Name + ": opções padrão preservadas");
                if (scenario.Scale == 1 && scenario.Name == "notebook-100")
                {
                    foreach (var n in new[] { "adapter", "targets", "ports", "timeout", "grid", "details" })
                        Check(!string.IsNullOrWhiteSpace(Field<Control>(n).AccessibleName), "Nome acessível: " + n);
                    typeof(MainForm).GetMethod("Busy", flags)!.Invoke(form, new object[] { true });
                    Field<Label>("status").Text = "Buscando serviços • 1,2 s (simulação visual)";
                    Field<ProgressBar>("progress").Value = 40;
                    Capture("buscando");
                    Check(Field<ProgressBar>("progress").Visible, "Progresso visível durante operação");
                    var busyProgress = Field<ProgressBar>("progress");
                    Check(viewport.Contains(busyProgress.RectangleToScreen(busyProgress.ClientRectangle)), "Progresso ativo dentro da janela");
                    Check(!busyProgress.RectangleToScreen(busyProgress.ClientRectangle).IntersectsWith(Field<Label>("status").RectangleToScreen(Field<Label>("status").ClientRectangle)), "Progresso ativo sem sobrepor status");
                    Check(!Field<Button>("scan").Enabled && Field<Button>("cancel").Enabled, "Busca em andamento preserva bloqueio e cancelamento");
                    typeof(MainForm).GetMethod("Busy", flags)!.Invoke(form, new object[] { false });
                    Check(!Field<ProgressBar>("progress").Visible, "Progresso oculto após operação");
                    Field<Label>("status").Text = "0 serviço(s) encontrado(s) • simulação visual";
                    Field<TextBox>("details").Text = "Nenhum serviço encontrado. Confira portas e rede ou informe um IP manual. Simulação visual sem consulta de rede.";
                    Capture("sem-resultados");
                    Field<Label>("status").Text = "Busca cancelada; resultados parciais.";
                    Capture("cancelado");
                    Field<Label>("status").Text = "Falha na busca: exemplo visual";
                    Field<TextBox>("details").Text = "Estado de erro simulado exclusivamente para revisar a apresentação.";
                    Capture("erro-simulado");
                    Field<TextBox>("ports").Text = "inválida";
                    bool invalid = false;
                    try { typeof(MainForm).GetMethod("ConfiguredPorts", flags)!.Invoke(form, null); }
                    catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { invalid = true; }
                    Check(invalid, "Validação de porta inválida preservada");
                    Field<TextBox>("ports").Text = "3306,3307,3308,3310,4387";
                }
                var grid = Field<DataGridView>("grid");
                for (int i = 0; i < 3; i++)
                {
                    string ip = "192.0.2." + (10 + i);
                    int index = grid.Rows.Add(ip, 3306 + i, "Rede (simulação)", "MySQL/MariaDB\r\nPing: 2 ms • TTL: 128 • Windows", "Serviço MySQL/MariaDB identificado");
                    grid.Rows[index].Tag = new Candidate(ip, new[] { 3306 + i }, "Simulação visual", 3306 + i);
                    grid.Rows[index].Cells[4].Tag = "SIMULAÇÃO VISUAL — nenhum servidor consultado.\r\nIP: " + ip + "\r\nServiço: MySQL/MariaDB\r\nSistema operacional estimado pelo TTL.";
                }
                grid.ClearSelection(); grid.Rows[0].Selected = true;
                Field<Label>("status").Text = "3 serviço(s) encontrado(s) em 3,1 s • simulação visual.";
                typeof(MainForm).GetMethod("ShowSelection", flags)!.Invoke(form, null);
                Capture("resultados-simulados");
                Check(Field<TextBox>("details").ClientSize.Height >= Field<TextBox>("details").Font.Height, scenario.Name + ": diagnóstico mostra ao menos uma linha completa");
                Check(grid.ClientSize.Height >= grid.ColumnHeadersHeight + grid.Rows[0].Height, scenario.Name + ": tabela mostra primeiro resultado completo");
                Check(Field<TextBox>("details").Text.Contains("192.0.2.10"), scenario.Name + ": detalhes acompanham seleção");
            }
            finally { form.Close(); form.Dispose(); foreach (var font in fonts) font.Dispose(); }
        }
        results.Add("LIMITAÇÃO VISUAL: 125%/150% são simulações de escala de layout/fonte; não houve mudança do DPI físico do Windows nem teste com leitor de tela.");
    }
}
#endif

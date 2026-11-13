namespace BuscaServidor;

internal sealed partial class MainForm
{
    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        base.ScaleControl(factor, specified);
        // Keep the window usable on notebook work areas; content can scroll at higher DPI.
        MinimumSize = new Size(960, 640);
    }

    void InitializeVisualLayout()
    {
        SuspendLayout();
        Text = "Busca Servidor - Suporte"; Font = UiTheme.Font();
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = UiTheme.Background; ForeColor = UiTheme.Text;
        StartPosition = FormStartPosition.CenterScreen; MinimumSize = new Size(960, 640);
        var available = Screen.FromPoint(Cursor.Position).WorkingArea;
        Size = new Size(Math.Min(1180, available.Width), Math.Min(780, available.Height));
        AutoScroll = true;
        using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream("BuscaServidor.AppIcon"))
            if (stream != null) { using var appIcon = new Icon(stream); Icon = (Icon)appIcon.Clone(); }

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, MinimumSize = new Size(900, 0), ColumnCount = 1, RowCount = 5, Padding = new Padding(16, 4, 16, 4), BackColor = UiTheme.Background, TabStop = false };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 188));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.Controls.Add(BuildHeader(), 0, 0);

        var settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, Margin = Padding.Empty, BackColor = UiTheme.Surface };
        foreach (float width in new[] { 40f, 26f, 23f, 11f }) settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 63));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        settings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        adapter.DrawMode = DrawMode.Normal; adapter.FlatStyle = FlatStyle.Standard; adapter.DropDownWidth = 680;
        settings.Controls.Add(Field("Placa de rede", adapter, 0), 0, 0);
        var targetField = Field("", targets, 1);
        targetField.Controls.RemoveAt(0); rangeMode.Margin = Padding.Empty; rangeMode.TabIndex = 0; targetField.Controls.Add(rangeMode, 0, 0); targets.TabIndex = 1;
        settings.Controls.Add(targetField, 1, 0);
        settings.Controls.Add(Field("Portas", ports, 2), 2, 0);
        settings.Controls.Add(Field("Espera (ms)", timeout, 3), 3, 0);
        var options = Row(local, consultVpn, force); options.TabIndex = 4;
        settings.Controls.Add(options, 0, 1); settings.SetColumnSpan(options, 4);
        foreach (var button in new[] { scan, cancel, copy, export, disableFirewall })
        {
            button.AutoSize = true; button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(84, 38); button.Padding = new Padding(14, 7, 14, 7);
            button.Margin = new Padding(0, 0, 8, 0);
            button.AccessibleName = button.Text.Replace("&", "");
        }
        ((ModernButton)disableFirewall).Caution = true;
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.Controls.Add(Row(scan, cancel), 0, 0);
        var firewallActions = Row(disableFirewall); firewallActions.AutoSize = true; actions.Controls.Add(firewallActions, 1, 0);
        actions.TabIndex = 5; settings.Controls.Add(actions, 0, 2); settings.SetColumnSpan(actions, 4);
        layout.Controls.Add(Section("Configuração da busca", settings), 0, 1);

        ConfigureGrid();
        var results = Section("Servidores encontrados", grid, Row(copy, export)); results.TabIndex = 1;
        layout.Controls.Add(results, 0, 2);
        details.BorderStyle = BorderStyle.None; details.BackColor = UiTheme.Secondary; details.ForeColor = UiTheme.Text;
        details.Font = new Font("Consolas", 9.5f); details.WordWrap = true; details.ScrollBars = ScrollBars.Vertical;
        var diagnosticFrame = new Panel { Padding = new Padding(10, 4, 8, 4), BackColor = UiTheme.Secondary };
        details.Dock = DockStyle.Fill; diagnosticFrame.Controls.Add(details);
        var diagnostic = Section("Detalhes e diagnóstico", diagnosticFrame); diagnostic.TabIndex = 2;
        layout.Controls.Add(diagnostic, 0, 3);
        var statusBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, TabStop = false };
        statusBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0)); statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        progress.Dock = DockStyle.Fill; progress.Margin = new Padding(0, 13, 12, 13); progress.Visible = false;
        status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleLeft; status.AutoSize = false; status.AutoEllipsis = true; status.Font = UiTheme.Font(9); status.ForeColor = UiTheme.Muted; status.Margin = Padding.Empty;
        status.AccessibleName = "Estado da operação"; statusBar.Controls.Add(progress, 0, 0); statusBar.Controls.Add(status, 1, 0);
        layout.Controls.Add(statusBar, 0, 4); Controls.Add(layout);
        foreach (var check in new[] { local, consultVpn, force, rangeMode }) check.AccessibleName = check.Text;
        tips.SetToolTip(adapter, "Na busca automática, todas as placas IPv4 ativas são consideradas. A seleção define a sub-rede usada em Força bruta.");
        tips.SetToolTip(consultVpn, "Acrescenta IPs conhecidos e identifica a VPN por adaptador/prefixo. O automático já testa o /24 de todas as placas ativas, inclusive VPN.");
        tips.SetToolTip(disableFirewall, "Altera o firewall deste computador. A operação mantém a confirmação e a solicitação de permissão do Windows.");
        status.TextChanged += (_, _) => { grid.Invalidate(); tips.SetToolTip(status, status.Text); };
        scan.EnabledChanged += (_, _) => { statusBar.ColumnStyles[0].Width = scan.Enabled ? 0 : 120 * DeviceDpi / 96f; progress.Visible = !scan.Enabled; grid.Invalidate(); };
        AcceptButton = scan;
        Shown += (_, _) => scan.Focus();
        Disposed += (_, _) => tips.Dispose();
        ResumeLayout(true);
    }

    Control BuildHeader()
    {
        var surface = new ModernCard { Header = true, Dock = DockStyle.Fill, Padding = new Padding(20, 10, 20, 10), Margin = new Padding(0, 0, 0, 8), BackColor = Color.Transparent, TabStop = false };
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty, BackColor = Color.Transparent, TabStop = false };
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 152));
        var brand = new PictureBox { Dock = DockStyle.Fill, Margin = new Padding(0, 7, 14, 7), BackColor = Color.Transparent, SizeMode = PictureBoxSizeMode.Zoom, TabStop = false };
        if (Icon != null) brand.Image = Icon.ToBitmap(); brand.Disposed += (_, _) => brand.Image?.Dispose();
        var titles = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = Color.Transparent, TabStop = false };
        titles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        titles.RowStyles.Add(new RowStyle(SizeType.Percent, 58)); titles.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        titles.Controls.Add(new Label { Text = Text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, ForeColor = Color.White, BackColor = Color.Transparent, Font = UiTheme.Font(19, FontStyle.Bold), AutoEllipsis = true, Margin = Padding.Empty }, 0, 0);
        titles.Controls.Add(new Label { Text = "Localize os serviços de banco e prepare os terminais.", Dock = DockStyle.Fill, ForeColor = UiTheme.HeaderText, BackColor = Color.Transparent, AutoEllipsis = true, Margin = new Padding(0, 3, 0, 0) }, 0, 1);
        var mode = new Label { Text = "AUTOMÁTICO", Dock = DockStyle.Fill, Margin = new Padding(0, 17, 0, 17), TextAlign = ContentAlignment.MiddleCenter, ForeColor = UiTheme.HeaderText, BackColor = Color.FromArgb(38, 75, 112), BorderStyle = BorderStyle.FixedSingle, Font = UiTheme.Font(8.5f, FontStyle.Bold) };
        rangeMode.CheckedChanged += (_, _) => mode.Text = rangeMode.Checked ? "FAIXA / IP MANUAL" : "AUTOMÁTICO";
        header.Controls.Add(brand, 0, 0); header.Controls.Add(titles, 1, 0); header.Controls.Add(mode, 2, 0); surface.Controls.Add(header); return surface;
    }

    static TableLayoutPanel Field(string label, Control input, int tab)
    {
        var field = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 12, 0), TabIndex = tab };
        field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        field.RowStyles.Add(new RowStyle(SizeType.Absolute, 23)); field.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        field.Controls.Add(new Label { Text = label, AutoSize = true, Margin = Padding.Empty, ForeColor = UiTheme.Muted, TabIndex = 0 }, 0, 0);
        input.Margin = Padding.Empty; input.TabIndex = 0;
        var frame = new ModernField(input) { Dock = DockStyle.Top, Height = 36, Margin = Padding.Empty, TabIndex = 1 };
        field.Controls.Add(frame, 0, 1); return field;
    }

    static ModernCard Section(string title, Control content, Control? tools = null)
    {
        var card = new ModernCard { Dock = DockStyle.Fill, Padding = new Padding(16, 6, 16, 8), Margin = new Padding(0, 0, 0, 8), BackColor = Color.Transparent };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = UiTheme.Surface };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, tools == null ? 24 : 42)); table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); heading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        heading.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, ForeColor = UiTheme.Text, Font = UiTheme.Font(10, FontStyle.Bold), Margin = Padding.Empty, TextAlign = tools == null ? ContentAlignment.TopLeft : ContentAlignment.MiddleLeft }, 0, 0);
        if (tools != null) { tools.AutoSize = true; heading.Controls.Add(tools, 1, 0); }
        table.Controls.Add(heading, 0, 0); content.Dock = DockStyle.Fill; content.Margin = Padding.Empty; content.TabIndex = 0; heading.TabIndex = 1; table.Controls.Add(content, 0, 1); card.Controls.Add(table); return card;
    }

    static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, BackColor = Color.Transparent };
        for (int i = 0; i < controls.Length; i++) { var control = controls[i]; control.TabIndex = i; if (control is CheckBox) control.Margin = new Padding(0, 8, 20, 0); row.Controls.Add(control); }
        return row;
    }

    void ConfigureGrid()
    {
        grid.Columns.Add("ip", "IP do servidor"); grid.Columns.Add("port", "Porta"); grid.Columns.Add("kind", "Localização"); grid.Columns.Add("version", "Ping / identificação"); grid.Columns.Add("state", "Status");
        int[] minimum = { 130, 60, 105, 205, 180 }; float[] weights = { 110, 50, 90, 200, 190 };
        for (int i = 0; i < grid.Columns.Count; i++) { grid.Columns[i].MinimumWidth = minimum[i]; grid.Columns[i].FillWeight = weights[i]; }
        grid.BackgroundColor = UiTheme.Surface; grid.BorderStyle = BorderStyle.None; grid.GridColor = UiTheme.Border; grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.Font = UiTheme.Font(); grid.DefaultCellStyle.Font = grid.Font;
        grid.Columns[0].DefaultCellStyle.Font = new Font("Consolas", 9.5f);
        grid.Columns[1].DefaultCellStyle.Font = grid.Columns[0].DefaultCellStyle.Font;
        grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(237, 244, 253); grid.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.Muted; grid.ColumnHeadersDefaultCellStyle.Font = UiTheme.Font(9, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.Background; grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiTheme.Text;
        grid.ColumnHeadersHeight = 34; grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.DefaultCellStyle.Padding = new Padding(8, 8, 8, 8); grid.DefaultCellStyle.ForeColor = UiTheme.Text; grid.RowTemplate.MinimumHeight = 36;
        grid.DefaultCellStyle.SelectionBackColor = UiTheme.Selected; grid.DefaultCellStyle.SelectionForeColor = UiTheme.Text;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 253); grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True; grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        int hovered = -1;
        grid.CellMouseEnter += (_, e) => { if (hovered >= 0 && hovered < grid.Rows.Count) grid.Rows[hovered].DefaultCellStyle.BackColor = Color.Empty; hovered = e.RowIndex; if (hovered >= 0) grid.Rows[hovered].DefaultCellStyle.BackColor = UiTheme.Hover; };
        grid.MouseLeave += (_, _) => { if (hovered >= 0 && hovered < grid.Rows.Count) grid.Rows[hovered].DefaultCellStyle.BackColor = Color.Empty; hovered = -1; };
        grid.Paint += (_, e) =>
        {
            if (grid.Rows.Count != 0) return;
            string text = operation != null ? (cancel.Enabled ? "Buscando serviços na rede…" : "Operação em andamento. Consulte os detalhes.") : status.Text.StartsWith("Busca cancelada") ? "Busca cancelada. Você pode iniciar uma nova busca." : status.Text.StartsWith("0 serviço") || status.Text.StartsWith("Nenhum IPv4") ? "Nenhum servidor encontrado. Confira os detalhes abaixo." : status.Text.StartsWith("Falha na busca") ? "A busca não foi concluída. Confira o estado da operação." : "Clique em Buscar servidores para começar.";
            int center = grid.ColumnHeadersHeight + (grid.Height - grid.ColumnHeadersHeight) / 2;
            int x = grid.Width / 2;
            if (grid.Height > grid.ColumnHeadersHeight + 115)
            {
                using var pen = new Pen(Color.FromArgb(132, 165, 204), 1.5f);
                e.Graphics.DrawRectangle(pen, x - 10, center - 45, 20, 12);
                e.Graphics.DrawLine(pen, x, center - 33, x, center - 25);
                e.Graphics.DrawLine(pen, x - 21, center - 25, x + 21, center - 25);
                foreach (int offset in new[] { -21, 0, 21 }) e.Graphics.DrawRectangle(pen, x + offset - 5, center - 25, 10, 8);
                TextRenderer.DrawText(e.Graphics, "Descoberta de servidores", grid.ColumnHeadersDefaultCellStyle.Font, new Rectangle(12, center - 4, Math.Max(0, grid.Width - 24), 24), UiTheme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                center += 28;
            }
            TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(12, center - 12, Math.Max(0, grid.Width - 24), 40), UiTheme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
        };
        grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex == 4 && e.CellStyle != null && e.Value is string value && value.StartsWith("Serviço MySQL/MariaDB identificado", StringComparison.Ordinal))
            { e.CellStyle.ForeColor = UiTheme.Success; e.CellStyle.SelectionForeColor = UiTheme.Success; }
        };
        var menu = new ContextMenuStrip { Font = UiTheme.Font() };
        var copyItem = menu.Items.Add("Copiar IP", null, (_, _) => copy.PerformClick());
        var exportItem = menu.Items.Add("Salvar relatório", null, (_, _) => export.PerformClick());
        menu.Opening += (_, _) => { copyItem.Enabled = copy.Enabled; exportItem.Enabled = export.Enabled; };
        grid.ContextMenuStrip = menu;
        var ownedGridFont = grid.Font;
        var ownedAddressFont = grid.Columns[0].DefaultCellStyle.Font;
        var ownedHeadingFont = grid.ColumnHeadersDefaultCellStyle.Font;
        var ownedMenuFont = menu.Font;
        grid.Disposed += (_, _) => { menu.Dispose(); ownedMenuFont.Dispose(); ownedAddressFont.Dispose(); ownedHeadingFont.Dispose(); ownedGridFont.Dispose(); };
        grid.CellMouseDown += (_, e) => { if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && grid.Enabled) { grid.CurrentCell = grid.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)]; grid.Rows[e.RowIndex].Selected = true; } };
        grid.KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.C) { copy.PerformClick(); e.Handled = true; e.SuppressKeyPress = true; } };
    }
}

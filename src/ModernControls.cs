using System.Drawing.Drawing2D;

namespace BuscaServidor;

// The native editors remain responsible for keyboard, selection and accessibility.
// This frame adds padding and a shared focus treatment without replacing their input logic.
internal sealed class ModernField : Panel
{
    readonly Control editor;
    public ModernField(Control editor)
    {
        this.editor = editor;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = UiTheme.Surface; TabStop = false;
        editor.Dock = DockStyle.None;
        if (editor is TextBox text) text.BorderStyle = BorderStyle.None;
        if (editor is NumericUpDown number) number.BorderStyle = BorderStyle.None;
        if (editor is ComboBox combo) combo.FlatStyle = FlatStyle.Flat;
        editor.BackColor = UiTheme.Secondary; editor.ForeColor = UiTheme.Text;
        Controls.Add(editor);
        editor.Enter += RefreshBorder; editor.Leave += RefreshBorder; editor.EnabledChanged += RefreshBorder;
    }
    void RefreshBorder(object? sender, EventArgs e) => Invalidate();
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset = Math.Max(6, 9 * DeviceDpi / 96);
        editor.SetBounds(inset, Math.Max(2, (ClientSize.Height - editor.PreferredSize.Height) / 2), Math.Max(1, ClientSize.Width - inset * 2), editor.PreferredSize.Height);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 16 || Height < 16) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = ModernCard.Shape(new Rectangle(1, 1, Width - 3, Height - 3), Math.Max(4, 6 * DeviceDpi / 96));
        using var fill = new SolidBrush(editor.Enabled ? UiTheme.Secondary : Color.FromArgb(239, 243, 248));
        using var border = new Pen(ContainsFocus ? UiTheme.Accent : UiTheme.Border, ContainsFocus ? 2 : 1);
        e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(border, shape);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { editor.Enter -= RefreshBorder; editor.Leave -= RefreshBorder; editor.EnabledChanged -= RefreshBorder; }
        base.Dispose(disposing);
    }
}

internal sealed class ModernCard : Panel
{
    public bool Header { get; set; }
    public ModernCard()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
    }
    internal static GraphicsPath Shape(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath(); int d = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        if (Width < 32 || Height < 32) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Shape(new Rectangle(0, 0, Width - 1, Height - 1), Math.Max(4, 11 * DeviceDpi / 96));
        using Brush fill = Header ? new LinearGradientBrush(ClientRectangle, UiTheme.HeaderStart, UiTheme.HeaderEnd, 0f) : new SolidBrush(UiTheme.Surface);
        using var border = new Pen(Header ? UiTheme.HeaderEnd : UiTheme.Border);
        e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(border, shape);
    }
}

internal sealed class ModernButton : Button
{
    public bool Primary { get; set; }
    public bool Caution { get; set; }
    bool hover;
    bool pressed;
    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnMouseCaptureChanged(EventArgs e) { pressed = false; Invalidate(); base.OnMouseCaptureChanged(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) pressed = true; Invalidate(); base.OnKeyDown(e); }
    protected override void OnKeyUp(KeyEventArgs e) { pressed = false; Invalidate(); base.OnKeyUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { pressed = false; Invalidate(); base.OnLostFocus(e); }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // ButtonBase can leave the rounded corners unpainted in the screen buffer.
        // The action row is transparent, so use its first opaque ancestor.
        var background = Parent;
        while (background != null && background.BackColor.A < 255) background = background.Parent;
        using var brush = new SolidBrush(background?.BackColor ?? SystemColors.Control);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        // ButtonBase may skip WM_ERASEBKGND; initialize the paint buffer here too.
        OnPaintBackground(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = ModernCard.Shape(new Rectangle(0, 0, Width - 1, Height - 1), Math.Max(4, 7 * DeviceDpi / 96));
        var fillColor = !Enabled ? Color.FromArgb(240, 243, 247) : Primary ? (pressed ? Color.FromArgb(30, 64, 175) : hover ? UiTheme.AccentHover : UiTheme.Accent) : pressed ? UiTheme.Selected : hover ? UiTheme.Hover : UiTheme.Surface;
        using var fill = new SolidBrush(fillColor); e.Graphics.FillPath(fill, shape);
        if (!Primary) { using var border = new Pen(UiTheme.Border); e.Graphics.DrawPath(border, shape); }
        var textColor = !Enabled ? Color.FromArgb(105, 117, 133) : Primary ? Color.White : Caution ? UiTheme.Caution : UiTheme.Text;
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | (ShowKeyboardCues ? 0 : TextFormatFlags.HidePrefix));
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5), textColor, fillColor);
    }
}

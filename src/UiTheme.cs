namespace BuscaServidor;

internal static class UiTheme
{
    internal static readonly Color Background = Color.FromArgb(237, 242, 248);
    internal static readonly Color Secondary = Color.FromArgb(245, 248, 252);
    internal static readonly Color HeaderStart = Color.FromArgb(11, 23, 48);
    internal static readonly Color HeaderEnd = Color.FromArgb(23, 61, 101);
    internal static readonly Color HeaderText = Color.FromArgb(203, 225, 249);
    internal static readonly Color Surface = Color.White;
    internal static readonly Color Text = Color.FromArgb(23, 36, 59);
    internal static readonly Color Muted = Color.FromArgb(89, 107, 130);
    internal static readonly Color Border = Color.FromArgb(215, 225, 237);
    internal static readonly Color Accent = Color.FromArgb(37, 99, 235);
    internal static readonly Color AccentHover = Color.FromArgb(29, 78, 216);
    internal static readonly Color Tech = Color.FromArgb(56, 189, 248);
    internal static readonly Color Success = Color.FromArgb(21, 128, 61);
    internal static readonly Color Selected = Color.FromArgb(223, 235, 249);
    internal static readonly Color Hover = Color.FromArgb(241, 246, 252);
    internal static readonly Color Caution = Color.FromArgb(180, 83, 9);
    internal static Font Font(float size = 9.5f, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);
}

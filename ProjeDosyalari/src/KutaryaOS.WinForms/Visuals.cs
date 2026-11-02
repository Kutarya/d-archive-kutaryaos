using System.Drawing.Drawing2D;

namespace KutaryaOS.WinForms;

public record Palette(string Name, Color Top, Color Bottom, Color Accent, Color Surface)
{
    public static readonly Palette[] All = [
        new("Okyanus", Color.FromArgb(12, 40, 68), Color.FromArgb(37, 112, 134), Color.FromArgb(28, 129, 153), Color.FromArgb(247, 250, 252)),
        new("Gün Doğumu", Color.FromArgb(80, 50, 85), Color.FromArgb(223, 151, 112), Color.FromArgb(170, 81, 67), Color.FromArgb(255, 248, 241)),
        new("Orman", Color.FromArgb(16, 49, 44), Color.FromArgb(88, 134, 106), Color.FromArgb(37, 121, 87), Color.FromArgb(246, 250, 245))];
    public static Palette FromName(string name) => All.FirstOrDefault(p => p.Name == name) ?? All[0];
}

public static class Ui
{
    public static readonly Color Ink = Color.FromArgb(31, 44, 62);
    public static readonly Color Muted = Color.FromArgb(104, 119, 139);
    public static Font Font(float size = 10, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);
    public static Label Label(string text, float size = 10, bool bold = false) => new() { Text = text, AutoSize = true, Font = Font(size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = Ink, BackColor = Color.Transparent };
    public static TextBox TextBox(string text = "") => new() { Text = text, Font = Font(11), BorderStyle = BorderStyle.FixedSingle, Height = 32 };
    public static void Style(Form form)
    {
        form.Font = Font(); form.BackColor = Color.FromArgb(247, 250, 252); form.ForeColor = Ink;
        form.AutoScaleMode = AutoScaleMode.Dpi; form.StartPosition = FormStartPosition.CenterParent;
        using var iconStream = typeof(Ui).Assembly.GetManifestResourceStream("KutaryaOS.Icon.ico");
        if (iconStream != null) { using var source = new Icon(iconStream); form.Icon = (Icon)source.Clone(); form.Disposed += (_, _) => form.Icon?.Dispose(); }
    }
    public static Button Button(string text, EventHandler action, bool primary = false)
    {
        var button = new Button { Text = text, AutoSize = false, Height = 38, Width = 140, FlatStyle = FlatStyle.Flat, Font = Font(10, FontStyle.Bold), BackColor = primary ? Palette.All[0].Accent : Color.White, ForeColor = primary ? Color.White : Ink, Cursor = Cursors.Hand, Margin = new Padding(5) };
        button.FlatAppearance.BorderColor = primary ? button.BackColor : Color.FromArgb(218, 226, 233); button.Click += action; return button;
    }
    public static string Bytes(long value) => value >= 1024L * 1024 * 1024 ? $"{value / (1024d * 1024 * 1024):0.##} GB" : value >= 1024 * 1024 ? $"{value / (1024d * 1024):0.#} MB" : value >= 1024 ? $"{value / 1024d:0.#} KB" : $"{value} B";
    public static void Error(IWin32Window owner, Exception error) => MessageBox.Show(owner, error.Message, "Kutarya OS", MessageBoxButtons.OK, MessageBoxIcon.Information);
    public static async Task GuardAsync(IWin32Window owner, Func<Task> action)
    {
        try { await action(); } catch (Exception error) { Error(owner, error); }
    }
    public static string? Prompt(IWin32Window owner, string title, string initial = "")
    {
        using var form = new Form { Text = title, ClientSize = new Size(420, 160), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        Style(form); var label = Label(title); label.Location = new(20, 16); var input = TextBox(initial); input.SetBounds(20, 45, 375, 30);
        var save = Button("Tamam", (_, _) => form.DialogResult = DialogResult.OK, true); save.Location = new(255, 102);
        form.Controls.AddRange([label, input, save]); form.AcceptButton = save; return form.ShowDialog(owner) == DialogResult.OK ? input.Text : null;
    }

    public static Bitmap Icon(string kind, int size = 64)
    {
        var image = new Bitmap(size, size); using var g = Graphics.FromImage(image); g.SmoothingMode = SmoothingMode.AntiAlias;
        using var bg = new SolidBrush(kind switch { "files" or "folder" => Color.FromArgb(232, 172, 60), "notes" => Color.FromArgb(238, 247, 254), "calculator" => Color.FromArgb(82, 153, 160), "monitor" => Color.FromArgb(79, 126, 189), "settings" => Color.FromArgb(130, 146, 167), "trash" => Color.FromArgb(129, 176, 164), _ => Color.FromArgb(149, 179, 212) });
        using var shadow = new SolidBrush(Color.FromArgb(65, 5, 20, 35));
        using var shadowPath = Rounded(new RectangleF(size * .15f, size * .18f, size * .72f, size * .72f), size * .13f); g.FillPath(shadow, shadowPath);
        var r = new RectangleF(size * .14f, size * .12f, size * .72f, size * .72f);
        using var shape = Rounded(r, size * .13f);
        using var shade = new LinearGradientBrush(r, ControlPaint.Light(bg.Color), ControlPaint.Dark(bg.Color, .08f), 70);
        g.FillPath(shade, shape);
        using var rim = new Pen(Color.FromArgb(115, Color.White), size / 70f); g.DrawPath(rim, shape);
        using var light = new Pen(Color.FromArgb(65, Color.White), size / 12f); g.DrawLine(light, size * .3f, size * .22f, size * .7f, size * .22f);
        using var pen = new Pen(kind == "notes" ? Color.FromArgb(75, 128, 170) : Color.White, size / 24f);
        if (kind is "files" or "folder") { g.DrawLine(pen, size * .27f, size * .38f, size * .44f, size * .38f); g.DrawLine(pen, size * .44f, size * .38f, size * .49f, size * .45f); g.DrawLine(pen, size * .49f, size * .45f, size * .74f, size * .45f); }
        else if (kind == "monitor") g.DrawLines(pen, new PointF[] { new(size * .25f, size * .65f), new(size * .39f, size * .47f), new(size * .52f, size * .57f), new(size * .7f, size * .31f) });
        else if (kind == "calculator") { for (var x = 0; x < 3; x++) for (var y = 0; y < 2; y++) g.DrawRectangle(pen, size * (.28f + .17f * x), size * (.47f + .16f * y), size * .065f, size * .065f); g.DrawLine(pen, size * .29f, size * .33f, size * .71f, size * .33f); }
        else if (kind == "settings") { g.DrawEllipse(pen, size * .3f, size * .3f, size * .4f, size * .4f); g.DrawEllipse(pen, size * .43f, size * .43f, size * .14f, size * .14f); }
        else if (kind == "trash") { g.DrawRectangle(pen, size * .35f, size * .35f, size * .3f, size * .35f); g.DrawLine(pen, size * .28f, size * .3f, size * .72f, size * .3f); }
        else { for (var y = 0; y < 3; y++) g.DrawLine(pen, size * .3f, size * (.35f + .14f * y), size * .7f, size * (.35f + .14f * y)); }
        return image;
    }
    public static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var path = new GraphicsPath(); var d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90); path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }
    public static Bitmap Wallpaper(Size size, Palette palette, bool showBrand = true)
    {
        var bitmap = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height)); using var g = Graphics.FromImage(bitmap);
        using var stream = typeof(Ui).Assembly.GetManifestResourceStream("KutaryaOS.Wallpaper.png");
        if (stream != null)
        {
            using var photo = Image.FromStream(stream);
            var scale = Math.Max(bitmap.Width / (float)photo.Width, bitmap.Height / (float)photo.Height);
            var rectangle = new Rectangle((int)((bitmap.Width - photo.Width * scale) / 2), (int)((bitmap.Height - photo.Height * scale) / 2), (int)Math.Ceiling(photo.Width * scale), (int)Math.Ceiling(photo.Height * scale));
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            using var attributes = new System.Drawing.Imaging.ImageAttributes();
            var matrix = new System.Drawing.Imaging.ColorMatrix();
            if (palette.Name == "Okyanus") { matrix.Matrix00 = .86f; matrix.Matrix11 = .95f; matrix.Matrix22 = 1.05f; }
            if (palette.Name == "Orman") { matrix.Matrix00 = .88f; matrix.Matrix11 = 1.03f; matrix.Matrix22 = .88f; }
            attributes.SetColorMatrix(matrix);
            g.DrawImage(photo, rectangle, 0, 0, photo.Width, photo.Height, GraphicsUnit.Pixel, attributes);
            return bitmap;
        }
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var gradient = new LinearGradientBrush(new Rectangle(Point.Empty, bitmap.Size), palette.Top, palette.Bottom, 65f); g.FillRectangle(gradient, 0, 0, bitmap.Width, bitmap.Height);
        var w = bitmap.Width; var h = bitmap.Height;
        using var glow = new SolidBrush(Color.FromArgb(24, 240, 241, 209)); g.FillEllipse(glow, w * .54f, -h * .4f, w * .75f, w * .75f);
        for (var i = 0; i < 4; i++)
        {
            using var brush = new SolidBrush(Color.FromArgb(28 + i * 12, 3, 24, 43));
            using var path = new GraphicsPath(); path.AddBezier(-w * .15f, h * (.9f + .03f * i), w * .15f, h * (.2f + .12f * i), w * .7f, h * (1.28f - .08f * i), w * 1.18f, h * (.38f + .1f * i));
            path.AddLine(w * 1.18f, h * (.38f + .1f * i), w * 1.18f, h * 1.2f); path.AddLine(w * 1.18f, h * 1.2f, -w * .15f, h * 1.2f); path.CloseFigure(); g.FillPath(brush, path);
        }
        return bitmap;
    }
}

public sealed class WallpaperPanel : Panel
{
    public WallpaperPanel() { DoubleBuffered = true; ResizeRedraw = true; }
}

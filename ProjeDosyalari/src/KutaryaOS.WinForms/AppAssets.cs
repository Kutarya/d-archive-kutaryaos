using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace KutaryaOS.WinForms;

internal static class AppAssets
{
    public static void WriteIcon(string root)
    {
        Directory.CreateDirectory(root);
        using var image = new Bitmap(256, 256);
        using var graphics = Graphics.FromImage(image);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Ui.Rounded(new RectangleF(12, 12, 232, 232), 54);
        using var fill = new LinearGradientBrush(new Rectangle(0, 0, 256, 256), Color.FromArgb(44, 156, 164), Color.FromArgb(16, 60, 92), 65);
        graphics.FillPath(fill, shape);
        using var font = new Font("Segoe UI", 145, FontStyle.Bold, GraphicsUnit.Pixel);
        graphics.DrawString("K", font, Brushes.White, new RectangleF(0, 26, 256, 230), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        using var png = new MemoryStream(); image.Save(png, ImageFormat.Png);
        using var output = new BinaryWriter(File.Create(Path.Combine(root, "kutarya.ico")));
        output.Write((ushort)0); output.Write((ushort)1); output.Write((ushort)1);
        output.Write((byte)0); output.Write((byte)0); output.Write((byte)0); output.Write((byte)0);
        output.Write((ushort)1); output.Write((ushort)32); output.Write((uint)png.Length); output.Write((uint)22); output.Write(png.ToArray());
    }
}

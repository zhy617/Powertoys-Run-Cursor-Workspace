using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

if (args.Length < 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("用法: RenderPluginIcons <输出目录>");
    Environment.Exit(1);
    return;
}

string outDir = args[0];

RenderFolder(Path.Combine(outDir, "folder.png"));
RenderMonitor(Path.Combine(outDir, "monitor.png"));
RenderCodeGlyph(Path.Combine(outDir, "cursor.dark.png"), darkUi: true);
RenderCodeGlyph(Path.Combine(outDir, "cursor.light.png"), darkUi: false);

Console.WriteLine("已写入: " + outDir);

static void RenderFolder(string path)
{
    const int s = 256;
    using var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.Clear(Color.Transparent);
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;

    using var pathFolder = new GraphicsPath();
    float tabW = 100, bodyR = 18;
    float bodyX = 28, bodyY = 52, bodyW = 200, bodyH = 168;
    pathFolder.AddArc(bodyX, bodyY + bodyH - bodyR * 2, bodyR * 2, bodyR * 2, 90, 90);
    pathFolder.AddLine(bodyX + bodyW - bodyR, bodyY + bodyH, bodyX + bodyR, bodyY + bodyH);
    pathFolder.AddArc(bodyX, bodyY + bodyH - bodyR * 2, bodyR * 2, bodyR * 2, 180, 90);
    pathFolder.AddLine(bodyX, bodyY + 28, bodyX + 32, bodyY);
    pathFolder.AddLine(bodyX + 32 + tabW, bodyY, bodyX + bodyW - bodyR, bodyY);
    pathFolder.AddArc(bodyX + bodyW - bodyR * 2, bodyY, bodyR * 2, bodyR * 2, 270, 90);
    pathFolder.AddLine(bodyX + bodyW, bodyY + bodyR, bodyX + bodyW, bodyY + bodyH - bodyR);
    pathFolder.AddArc(bodyX + bodyW - bodyR * 2, bodyY + bodyH - bodyR * 2, bodyR * 2, bodyR * 2, 0, 90);
    pathFolder.CloseFigure();

    using var br = new LinearGradientBrush(
        new RectangleF(0, 40, s, s),
        Color.FromArgb(255, 255, 214, 102),
        Color.FromArgb(255, 245, 180, 60),
        LinearGradientMode.Vertical);
    using var pen = new Pen(Color.FromArgb(220, 200, 140, 40), 2.5f);
    g.FillPath(br, pathFolder);
    g.DrawPath(pen, pathFolder);

    bmp.Save(path, ImageFormat.Png);
}

static void RenderMonitor(string path)
{
    const int s = 256;
    using var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.Clear(Color.Transparent);

    float mx = 40, my = 36, mw = 176, mh = 120, r = 12;
    using var screen = new GraphicsPath();
    screen.AddArc(mx, my, r * 2, r * 2, 180, 90);
    screen.AddLine(mx + mw - r, my, mx + mw - r, my);
    screen.AddArc(mx + mw - r * 2, my, r * 2, r * 2, 270, 90);
    screen.AddLine(mx + mw, my + r, mx + mw, my + mh - r);
    screen.AddArc(mx + mw - r * 2, my + mh - r * 2, r * 2, r * 2, 0, 90);
    screen.AddLine(mx + r, my + mh, mx + mw - r, my + mh);
    screen.AddArc(mx, my + mh - r * 2, r * 2, r * 2, 90, 90);
    screen.AddLine(mx, my + mh - r, mx, my + r);
    screen.CloseFigure();

    using var fill = new LinearGradientBrush(
        new RectangleF(mx, my, mw, mh),
        Color.FromArgb(255, 120, 132, 212),
        Color.FromArgb(255, 76, 94, 178),
        LinearGradientMode.Vertical);
    using var edge = new Pen(Color.FromArgb(255, 52, 62, 120), 2.2f);
    g.FillPath(fill, screen);
    g.DrawPath(edge, screen);

    using var hi = new SolidBrush(Color.FromArgb(55, 255, 255, 255));
    g.FillRectangle(hi, mx + 16, my + 16, mw - 32, 28);

    using var stand = new SolidBrush(Color.FromArgb(255, 88, 96, 118));
    float bx = s / 2f - 48, by = my + mh + 6, bw = 96, bh = 14;
    g.FillRoundedRect(stand, bx, by, bw, bh, 4);
    g.FillRoundedRect(stand, bx + 18, by + bh - 2, bw - 36, 10, 3);

    bmp.Save(path, ImageFormat.Png);
}

static void RenderCodeGlyph(string path, bool darkUi)
{
    const int s = 256;
    using var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    g.CompositingMode = CompositingMode.SourceOver;
    g.Clear(Color.Transparent);

    // darkUi：供 PowerToys 深色主题用 → 浅色字形；浅色主题用 → 深色字形
    Color fg = darkUi ? Color.FromArgb(255, 248, 250, 252) : Color.FromArgb(255, 28, 30, 35);
    using var font = new Font(FontFamily.GenericMonospace, 102, FontStyle.Bold, GraphicsUnit.Pixel);
    using var br = new SolidBrush(fg);
    const string text = "</>";
    var size = g.MeasureString(text, font);
    g.DrawString(text, font, br, (s - size.Width) / 2f, (s - size.Height) / 2f - 8);

    bmp.Save(path, ImageFormat.Png);
}

file static class GraphicsEx
{
    public static void FillRoundedRect(this Graphics g, Brush b, float x, float y, float w, float h, float r)
    {
        using var p = new GraphicsPath();
        p.AddArc(x, y, r * 2, r * 2, 180, 90);
        p.AddLine(x + r, y, x + w - r, y);
        p.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
        p.AddLine(x + w, y + r, x + w, y + h - r);
        p.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
        p.AddLine(x + w - r, y + h, x + r, y + h);
        p.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
        p.AddLine(x, y + h - r, x, y + r);
        p.CloseFigure();
        g.FillPath(b, p);
    }
}

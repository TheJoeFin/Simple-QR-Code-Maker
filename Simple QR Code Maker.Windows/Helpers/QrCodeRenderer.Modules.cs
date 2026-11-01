using Simple_QR_Code_Maker.Models;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using ZXing.QrCode.Internal;
using SvgImage = ZXing.Rendering.SvgRenderer.SvgImage;

namespace Simple_QR_Code_Maker.Helpers;

public static partial class QrCodeRenderer
{
    private static bool HasStyledModules(QrCodeAppearance? appearance)
    {
        if (appearance is null)
            return false;
        if (!Enum.IsDefined(appearance.ModuleStyle) || !Enum.IsDefined(appearance.EyeStyle) || !Enum.IsDefined(appearance.CaptionIcon))
            throw new ArgumentOutOfRangeException(nameof(appearance), "Unknown QR appearance style.");
        return appearance.ModuleStyle != QrModuleStyle.Square || appearance.EyeStyle != QrEyeStyle.Square;
    }

    // Function modules retain their exact square footprints; only the three 7x7 eyes
    // are replaced deliberately. This includes every alignment pattern, not just the last.
    internal static bool[,] GetProtectedQrModules(QRCode qrCode)
    {
        int size = qrCode.Matrix.Width;
        bool[,] protectedModules = new bool[size, size];

        void Protect(int left, int top, int width, int height)
        {
            for (int y = top; y < top + height; y++)
                for (int x = left; x < left + width; x++)
                    protectedModules[x, y] = true;
        }

        Protect(0, 0, 9, 9);
        Protect(size - 8, 0, 8, 9);
        Protect(0, size - 8, 9, 8);
        Protect(6, 9, 1, size - 17);
        Protect(9, 6, size - 17, 1);

        int[] centers = qrCode.Version.AlignmentPatternCenters;
        for (int y = 0; y < centers.Length; y++)
        {
            for (int x = 0; x < centers.Length; x++)
            {
                if ((x == 0 && y == 0) || (x == 0 && y == centers.Length - 1) || (y == 0 && x == centers.Length - 1))
                    continue;
                Protect(centers[x] - 2, centers[y] - 2, 5, 5);
            }
        }

        if (qrCode.Version.VersionNumber >= 7)
        {
            Protect(size - 11, 0, 3, 6);
            Protect(0, size - 11, 6, 3);
        }

        return protectedModules;
    }

    private static bool IsFinderModule(int x, int y, int size)
        => (x < 7 && y < 7) || (x >= size - 7 && y < 7) || (x < 7 && y >= size - 7);

    private static GraphicsPath CreateQrModulePath(QRCode qrCode, int renderSize, int margin, QrCodeAppearance appearance)
    {
        GraphicsPath path = new(FillMode.Winding);
        try
        {
            int size = qrCode.Matrix.Width;
            int scale = renderSize / (size + 2 * margin);
            bool[,] protectedModules = GetProtectedQrModules(qrCode);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (qrCode.Matrix[x, y] != 1 || (appearance.EyeStyle != QrEyeStyle.Square && IsFinderModule(x, y, size)))
                        continue;

                    RectangleF rect = new((x + margin) * scale, (y + margin) * scale, scale, scale);
                    if (protectedModules[x, y] || appearance.ModuleStyle == QrModuleStyle.Square)
                    {
                        path.AddRectangle(rect);
                    }
                    else if (appearance.ModuleStyle == QrModuleStyle.Dots)
                    {
                        // Substantial dots keep almost the full module diameter, even in dense symbols.
                        rect.Inflate(-scale * 0.025f, -scale * 0.025f);
                        path.AddEllipse(rect);
                    }
                    else
                    {
                        using GraphicsPath rounded = CreateRoundedRectanglePath(rect, scale * 0.18f);
                        path.AddPath(rounded, false);
                    }
                }
            }

            if (appearance.EyeStyle != QrEyeStyle.Square)
            {
                AddFinderEye(path, margin * scale, margin * scale, scale, appearance.EyeStyle);
                AddFinderEye(path, (margin + size - 7) * scale, margin * scale, scale, appearance.EyeStyle);
                AddFinderEye(path, margin * scale, (margin + size - 7) * scale, scale, appearance.EyeStyle);
            }

            return path;
        }
        catch
        {
            path.Dispose();
            throw;
        }
    }

    private static void AddFinderEye(GraphicsPath path, float x, float y, float scale, QrEyeStyle style)
    {
        // Winding holes are reversed so the light ring remains genuinely transparent.
        using GraphicsPath outer = CreateEyeContour(new RectangleF(x, y, 7 * scale, 7 * scale), scale, style);
        using GraphicsPath hole = CreateEyeContour(new RectangleF(x + scale, y + scale, 5 * scale, 5 * scale), scale * 0.65f, style);
        using GraphicsPath center = CreateEyeContour(new RectangleF(x + 2 * scale, y + 2 * scale, 3 * scale, 3 * scale), scale * 0.6f, style);
        hole.Reverse();
        path.AddPath(outer, false);
        path.AddPath(hole, false);
        path.AddPath(center, false);
    }

    private static GraphicsPath CreateEyeContour(RectangleF rect, float radius, QrEyeStyle style)
    {
        if (style != QrEyeStyle.Circle)
            return CreateRoundedRectanglePath(rect, radius);

        GraphicsPath path = new();
        path.AddEllipse(rect);
        return path;
    }

    private static Bitmap CreateStyledQrBitmap(QRCode qrCode, int renderSize, int margin, Color foreground, Color background, QrCodeAppearance appearance)
    {
        using Bitmap coverage = new(renderSize, renderSize, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(coverage))
        {
            graphics.Clear(Color.Black);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            using GraphicsPath path = CreateQrModulePath(qrCode, renderSize, margin, appearance);
            graphics.FillPath(Brushes.White, path);
        }

        Bitmap bitmap = new(renderSize, renderSize, PixelFormat.Format32bppArgb);
        try
        {
            ApplyModuleCoverage(coverage, bitmap, foreground, background);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static unsafe void ApplyModuleCoverage(Bitmap coverage, Bitmap target, Color foreground, Color background)
    {
        Rectangle bounds = new(0, 0, target.Width, target.Height);
        BitmapData mask = coverage.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            BitmapData pixels = target.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < bounds.Height; y++)
                {
                    byte* source = (byte*)mask.Scan0 + y * mask.Stride;
                    byte* destination = (byte*)pixels.Scan0 + y * pixels.Stride;
                    for (int x = 0; x < bounds.Width; x++, source += 4, destination += 4)
                    {
                        int weight = source[0];
                        int foregroundAlpha = foreground.A * weight;
                        int backgroundAlpha = background.A * (255 - weight);
                        int alpha = foregroundAlpha + backgroundAlpha;
                        // Straight-alpha bytes avoid GDI+'s lossy premultiply/unpremultiply round trip.
                        destination[0] = BlendModuleChannel(foreground.B, background.B, foregroundAlpha, backgroundAlpha, alpha);
                        destination[1] = BlendModuleChannel(foreground.G, background.G, foregroundAlpha, backgroundAlpha, alpha);
                        destination[2] = BlendModuleChannel(foreground.R, background.R, foregroundAlpha, backgroundAlpha, alpha);
                        destination[3] = (byte)((alpha + 127) / 255);
                    }
                }
            }
            finally
            {
                target.UnlockBits(pixels);
            }
        }
        finally
        {
            coverage.UnlockBits(mask);
        }
    }

    private static byte BlendModuleChannel(byte foreground, byte background, int foregroundAlpha, int backgroundAlpha, int alpha)
        => alpha == 0 ? background : (byte)((foreground * foregroundAlpha + background * backgroundAlpha + alpha / 2) / alpha);

    private static SvgImage CreateStyledQrSvg(QRCode qrCode, int renderSize, int margin, Color foreground, Color background, QrCodeAppearance appearance)
    {
        using GraphicsPath path = CreateQrModulePath(qrCode, renderSize, margin, appearance);
        string modulePath = GetSvgPathData(path);
        // Translucent modules replace their background pixels, matching the bitmap coverage mask.
        string backgroundShape = foreground.A == 255
            ? $"<rect width=\"{renderSize}\" height=\"{renderSize}\" fill=\"{ToSvgColor(background)}\"/>"
            : $"<path d=\"M 0 0 L {renderSize} 0 L {renderSize} {renderSize} L 0 {renderSize} Z {modulePath}\" fill=\"{ToSvgColor(background)}\" fill-rule=\"evenodd\"/>";
        return new SvgImage($"""
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {renderSize} {renderSize}" width="{renderSize}" height="{renderSize}">
{backgroundShape}
<path d="{modulePath}" fill="{ToSvgColor(foreground)}" fill-rule="nonzero"/>
</svg>
""");
    }

    // Both outputs consume the same native paths (including cubic arcs and winding holes).
    private static string GetSvgPathData(GraphicsPath path)
    {
        StringBuilder result = new();
        PointF[] points = path.PathPoints;
        byte[] types = path.PathTypes;
        for (int i = 0; i < points.Length; i++)
        {
            int type = types[i] & (int)PathPointType.PathTypeMask;
            if (type == (int)PathPointType.Start)
                result.Append("M ").Append(SvgValue(points[i].X)).Append(' ').Append(SvgValue(points[i].Y)).Append(' ');
            else if (type == (int)PathPointType.Line)
                result.Append("L ").Append(SvgValue(points[i].X)).Append(' ').Append(SvgValue(points[i].Y)).Append(' ');
            else if (type == (int)PathPointType.Bezier3)
            {
                result.Append("C ");
                for (int control = 0; control < 3; control++, i++)
                    result.Append(SvgValue(points[i].X)).Append(' ').Append(SvgValue(points[i].Y)).Append(' ');
                i--;
            }
            else
                throw new InvalidOperationException("Unsupported rendering path point.");

            if ((types[i] & (int)PathPointType.CloseSubpath) != 0)
                result.Append("Z ");
        }
        return result.ToString();
    }
}

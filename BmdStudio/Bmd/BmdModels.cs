using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;

namespace BMDEditor.Bmd;

internal enum BmdGameFormat
{
    Cultures1 = 1,
    Cultures2 = 2
}

internal enum BmdFrameType
{
    Empty = 0,
    Normal = 1,
    Shadow = 2,
    Build = 3,
    Extended = 4
}

internal sealed class IndexedPixel
{
    public byte PaletteIndex { get; set; }
    public byte Alpha { get; set; }

    public IndexedPixel Clone()
    {
        return new IndexedPixel
        {
            PaletteIndex = PaletteIndex,
            Alpha = Alpha
        };
    }
}

internal sealed class BmdFrame
{
    public int Number { get; set; }
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public BmdFrameType Type { get; set; }
    public IndexedPixel[][]? Pixels { get; set; }

    public int Width => Pixels is { Length: > 0 } ? Pixels[0].Length : 0;

    public int Height => Pixels?.Length ?? 0;

    public bool IsEmpty => Type == BmdFrameType.Empty || Width == 0 || Height == 0;

    public Bitmap ToBitmap(Color[] palette, bool useAlpha, bool showAnchor = false)
    {
        if (IsEmpty)
        {
            return new Bitmap(1, 1);
        }

        var bitmap = new Bitmap(Width, Height);
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var pixel = Pixels![y][x];
                var alpha = ResolvePreviewAlpha(pixel, useAlpha);
                var color = Type == BmdFrameType.Shadow ? Color.Black : palette[pixel.PaletteIndex];
                bitmap.SetPixel(x, y, Color.FromArgb(alpha, color));
            }
        }

        if (showAnchor)
        {
            DrawAnchor(bitmap);
        }

        return bitmap;
    }

    public void LoadFromBitmap(Bitmap bitmap, Color[] palette)
    {
        Pixels = new IndexedPixel[bitmap.Height][];
        var indexedPalette = TryReadIndexedPalette(bitmap);

        for (var y = 0; y < bitmap.Height; y++)
        {
            Pixels[y] = new IndexedPixel[bitmap.Width];
            for (var x = 0; x < bitmap.Width; x++)
            {
                var source = bitmap.GetPixel(x, y);
                if (source.A == 0)
                {
                    Pixels[y][x] = new IndexedPixel();
                    continue;
                }

                byte paletteIndex;
                if (Type == BmdFrameType.Shadow)
                {
                    paletteIndex = 0;
                }
                else if (indexedPalette is not null)
                {
                    paletteIndex = FindClosestPaletteIndex(indexedPalette, source);
                }
                else
                {
                    paletteIndex = FindClosestPaletteIndex(palette, source);
                }

                var alpha = Type switch
                {
                    BmdFrameType.Normal => (byte)255,
                    BmdFrameType.Shadow => (byte)128,
                    BmdFrameType.Build => (byte)255,
                    BmdFrameType.Extended => source.A,
                    _ => (byte)0
                };

                Pixels[y][x] = new IndexedPixel
                {
                    PaletteIndex = paletteIndex,
                    Alpha = alpha
                };
            }
        }
    }

    public string ToMetadataLine()
    {
        return string.Join(",",
            Number.ToString(CultureInfo.InvariantCulture),
            (int)Type,
            OffsetX.ToString(CultureInfo.InvariantCulture),
            OffsetY.ToString(CultureInfo.InvariantCulture));
    }

    public override string ToString()
    {
        return $"{Number:0000}  {Type}  [{Width}x{Height}]  ({OffsetX},{OffsetY})";
    }

    private static byte ResolvePreviewAlpha(IndexedPixel pixel, bool useAlpha)
    {
        if (pixel.Alpha == 0)
        {
            return 0;
        }

        return useAlpha ? pixel.Alpha : (byte)255;
    }

    private static byte FindClosestPaletteIndex(IReadOnlyList<Color> palette, Color source)
    {
        var bestIndex = 0;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < palette.Count; i++)
        {
            var candidate = palette[i];
            var distance =
                (candidate.R - source.R) * (candidate.R - source.R) +
                (candidate.G - source.G) * (candidate.G - source.G) +
                (candidate.B - source.B) * (candidate.B - source.B);

            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestIndex = i;
        }

        return (byte)bestIndex;
    }

    private static Color[]? TryReadIndexedPalette(Bitmap bitmap)
    {
        var format = bitmap.PixelFormat;
        var isIndexed =
            format == PixelFormat.Format1bppIndexed ||
            format == PixelFormat.Format4bppIndexed ||
            format == PixelFormat.Format8bppIndexed;

        if (!isIndexed || bitmap.Palette.Entries.Length == 0)
        {
            return null;
        }

        return bitmap.Palette.Entries
            .Take(PaletteCodec.PaletteSize)
            .Concat(Enumerable.Repeat(Color.Black, Math.Max(0, PaletteCodec.PaletteSize - bitmap.Palette.Entries.Length)))
            .Take(PaletteCodec.PaletteSize)
            .ToArray();
    }

    private void DrawAnchor(Bitmap bitmap)
    {
        var rawAnchorX = -OffsetX;
        var rawAnchorY = -OffsetY;
        var outOfBounds = rawAnchorX < 0 || rawAnchorX >= bitmap.Width || rawAnchorY < 0 || rawAnchorY >= bitmap.Height;

        // Keep anchor visible even when real point is outside sprite bounds.
        var anchorX = Math.Clamp(rawAnchorX, 0, bitmap.Width - 1);
        var anchorY = Math.Clamp(rawAnchorY, 0, bitmap.Height - 1);

        var outlineColor = Color.Black;
        var primaryColor = outOfBounds ? Color.OrangeRed : Color.DeepPink;
        var secondaryColor = outOfBounds ? Color.White : Color.Lime;

        DrawRing(bitmap, anchorX, anchorY, 9, outlineColor);
        DrawRing(bitmap, anchorX, anchorY, 8, primaryColor);
        DrawDiamond(bitmap, anchorX, anchorY, 6, outlineColor);
        DrawDiamond(bitmap, anchorX, anchorY, 5, secondaryColor);
        DrawDot(bitmap, anchorX, anchorY, 2, primaryColor);
        DrawDot(bitmap, anchorX, anchorY, 1, secondaryColor);

        // Extra reference points to improve visibility without a plus/cross.
        DrawDot(bitmap, anchorX - 11, anchorY, 1, outlineColor);
        DrawDot(bitmap, anchorX + 11, anchorY, 1, outlineColor);
        DrawDot(bitmap, anchorX, anchorY - 11, 1, outlineColor);
        DrawDot(bitmap, anchorX, anchorY + 11, 1, outlineColor);
    }

    private static void DrawDiagonalCross(Bitmap bitmap, int centerX, int centerY, int armLength, int thickness, Color color)
    {
        for (var d = -armLength; d <= armLength; d++)
        {
            for (var t = -thickness; t <= thickness; t++)
            {
                SetPixelSafe(bitmap, centerX + d + t, centerY + d, color);
                SetPixelSafe(bitmap, centerX + d + t, centerY - d, color);
            }
        }
    }

    private static void DrawCornerBrackets(Bitmap bitmap, int centerX, int centerY, int radius, int arm, Color color)
    {
        DrawLine(bitmap, centerX - radius, centerY - radius, centerX - radius + arm, centerY - radius, color);
        DrawLine(bitmap, centerX - radius, centerY - radius, centerX - radius, centerY - radius + arm, color);

        DrawLine(bitmap, centerX + radius, centerY - radius, centerX + radius - arm, centerY - radius, color);
        DrawLine(bitmap, centerX + radius, centerY - radius, centerX + radius, centerY - radius + arm, color);

        DrawLine(bitmap, centerX - radius, centerY + radius, centerX - radius + arm, centerY + radius, color);
        DrawLine(bitmap, centerX - radius, centerY + radius, centerX - radius, centerY + radius - arm, color);

        DrawLine(bitmap, centerX + radius, centerY + radius, centerX + radius - arm, centerY + radius, color);
        DrawLine(bitmap, centerX + radius, centerY + radius, centerX + radius, centerY + radius - arm, color);
    }

    private static void DrawDot(Bitmap bitmap, int centerX, int centerY, int radius, Color color)
    {
        for (var y = -radius; y <= radius; y++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                if ((x * x) + (y * y) <= radius * radius)
                {
                    SetPixelSafe(bitmap, centerX + x, centerY + y, color);
                }
            }
        }
    }

    private static void DrawRing(Bitmap bitmap, int centerX, int centerY, int radius, Color color)
    {
        for (var y = -radius; y <= radius; y++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                var distance2 = (x * x) + (y * y);
                if (distance2 < (radius - 1) * (radius - 1) || distance2 > radius * radius)
                {
                    continue;
                }

                SetPixelSafe(bitmap, centerX + x, centerY + y, color);
            }
        }
    }

    private static void DrawDiamond(Bitmap bitmap, int centerX, int centerY, int radius, Color color)
    {
        DrawLine(bitmap, centerX, centerY - radius, centerX + radius, centerY, color);
        DrawLine(bitmap, centerX + radius, centerY, centerX, centerY + radius, color);
        DrawLine(bitmap, centerX, centerY + radius, centerX - radius, centerY, color);
        DrawLine(bitmap, centerX - radius, centerY, centerX, centerY - radius, color);
    }

    private static void DrawLine(Bitmap bitmap, int x0, int y0, int x1, int y1, Color color)
    {
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;

        var x = x0;
        var y = y0;
        while (true)
        {
            SetPixelSafe(bitmap, x, y, color);
            if (x == x1 && y == y1)
            {
                break;
            }

            var e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

    private static void SetPixelSafe(Bitmap bitmap, int x, int y, Color color)
    {
        if (x < 0 || x >= bitmap.Width || y < 0 || y >= bitmap.Height)
        {
            return;
        }

        bitmap.SetPixel(x, y, color);
    }
}

internal sealed class BmdDocument
{
    public List<BmdFrame> Frames { get; } = new();

    public string? SourcePath { get; set; }

    public BmdGameFormat GameFormat { get; set; } = BmdGameFormat.Cultures2;

    public void RenumberFrames()
    {
        for (var i = 0; i < Frames.Count; i++)
        {
            Frames[i].Number = i;
        }
    }
}

param(
    [Parameter(Mandatory = $true)][string]$SourcePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Deterministic extraction of the approved dark-background PNG. No artwork is
# generated, traced, recolored, or retyped. The original file is never modified.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class RebazBrandAssets
{
    private static bool IsArtwork(Color pixel)
    {
        // The supplied navy background is dark in every channel; the gold and
        // white artwork is bright. Preserve the RGB of every retained pixel.
        return pixel.R > 60 || pixel.G > 60 || pixel.B > 65;
    }

    private static Bitmap Resize(Bitmap source, int size)
    {
        var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(result))
        using (var attributes = new ImageAttributes())
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            graphics.DrawImage(source, new Rectangle(0, 0, size, size),
                0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        }
        return result;
    }

    public static string Prepare(string sourcePath, string staticRoot)
    {
        var destination = Path.Combine(staticRoot, "images", "branding");
        Directory.CreateDirectory(destination);
        using (var source = new Bitmap(sourcePath))
        using (var transparent = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            int left = source.Width, top = source.Height, right = -1, bottom = -1;
            var occupiedColumns = new bool[source.Width];
            for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
            {
                var pixel = source.GetPixel(x, y);
                if (!IsArtwork(pixel)) continue;
                transparent.SetPixel(x, y, Color.FromArgb(255, pixel.R, pixel.G, pixel.B));
                left = Math.Min(left, x); right = Math.Max(right, x);
                top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                occupiedColumns[x] = true;
            }
            if (right < left) throw new InvalidOperationException("No logo artwork found.");
            const int margin = 10;
            if (left < margin || top < margin || right + margin >= source.Width || bottom + margin >= source.Height)
                throw new InvalidOperationException("Insufficient safe crop margin; inspect the source.");
            var bounds = Rectangle.FromLTRB(left - margin, top - margin, right + margin + 1, bottom + margin + 1);
            using (var logo = transparent.Clone(bounds, PixelFormat.Format32bppArgb))
            {
                logo.Save(Path.Combine(destination, "rebaz-logo.png"), ImageFormat.Png);
                // Verify retained artwork is a pixel-for-pixel copy, not a recreation.
                for (int y = 0; y < logo.Height; y++)
                for (int x = 0; x < logo.Width; x++)
                {
                    var pixel = logo.GetPixel(x, y);
                    if (pixel.A > 0 && pixel.ToArgb() != source.GetPixel(x + bounds.X, y + bounds.Y).ToArgb())
                        throw new InvalidOperationException("Artwork pixel verification failed.");
                }
            }

            // The first occupied horizontal band is the approved R and its
            // connected road-arrow. The gap before the wordmark separates it.
            int symbolRight = left;
            while (symbolRight + 1 < source.Width && occupiedColumns[symbolRight + 1]) symbolRight++;
            if (symbolRight - left < 100 || symbolRight >= right)
                throw new InvalidOperationException("Cannot safely isolate the R; inspect the source.");
            int symbolWidth = symbolRight - left + 1, symbolHeight = bottom - top + 1;
            int squareSize = Math.Max(symbolWidth, symbolHeight) + 2 * margin;
            using (var square = new Bitmap(squareSize, squareSize, PixelFormat.Format32bppArgb))
            {
                int offsetX = (squareSize - symbolWidth) / 2;
                int offsetY = (squareSize - symbolHeight) / 2;
                for (int y = 0; y < symbolHeight; y++)
                for (int x = 0; x < symbolWidth; x++)
                    square.SetPixel(offsetX + x, offsetY + y, transparent.GetPixel(left + x, top + y));

                using (var icon = Resize(square, 256))
                    icon.Save(Path.Combine(destination, "rebaz-symbol.png"), ImageFormat.Png);
                var sizes = new[] { 16, 32, 48, 64, 256 };
                var frames = new byte[sizes.Length][];
                for (int i = 0; i < sizes.Length; i++)
                using (var icon = Resize(square, sizes[i]))
                using (var stream = new MemoryStream())
                {
                    icon.Save(stream, ImageFormat.Png);
                    frames[i] = stream.ToArray();
                }
                using (var writer = new BinaryWriter(File.Create(Path.Combine(staticRoot, "favicon.ico"))))
                {
                    writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
                    int offset = 6 + sizes.Length * 16;
                    for (int i = 0; i < sizes.Length; i++)
                    {
                        writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                        writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                        writer.Write((byte)0); writer.Write((byte)0);
                        writer.Write((ushort)1); writer.Write((ushort)32);
                        writer.Write(frames[i].Length); writer.Write(offset);
                        offset += frames[i].Length;
                    }
                    foreach (var frame in frames) writer.Write(frame);
                }
            }
            return String.Format("Logo: {0}x{1}; source crop: {2},{3}; R bounds: {4}..{5}; original artwork RGB verified; transparent background; ICO sizes: 16,32,48,64,256.",
                bounds.Width, bounds.Height, bounds.X, bounds.Y, left, symbolRight);
        }
    }
}
'@

$source = (Resolve-Path -LiteralPath $SourcePath).Path
$staticRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../src/FinSight.Api/wwwroot'))
[RebazBrandAssets]::Prepare($source, $staticRoot)

using System.Runtime.InteropServices;
using Duxel.Core;

internal static unsafe partial class InlineTextRenderingTests
{
    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativeRange(uint Start, uint Length);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeLine
    {
        internal uint Length, TrailingWhitespaceLength, NewlineLength;
        internal float Height, Baseline;
        internal int IsTrimmed;
    }
    private static void* NativeMethod(nint instance, int slot) => (*(void***)instance)[slot];

    private static void PaintNativeParagraphReference(UiContext context, UiImmediateContext ui,
        string heading, string body, UiInlineTextWeight weight, float width, bool rightToLeft, UiColor? foreground = null,
        Action<nint>? configureLayout = null, UiVector2? origin = null, bool combineRasters = false, UiColor? bakedBackground = null)
    {
        // The oracle owns the native layout and analyzes its original callback runs directly.
        // It does not use product paragraph requests, extracted glyphs, rasterizers or line geometry.
        var rasters = new List<DirectWriteLayoutOracle.RunRaster>();
        var lines = Array.Empty<NativeLine>();
        DirectWriteLayoutOracle.Draw("Segoe UI", heading + body, rightToLeft, layout =>
        {
            uint count = 0;
            var status = ((delegate* unmanaged[Stdcall]<nint, NativeLine*, uint, uint*, int>)NativeMethod(layout, 59))
                (layout, null, 0, &count);
            if (status != unchecked((int)0x8007007A))
                Marshal.ThrowExceptionForHR(status);
            lines = new NativeLine[count];
            fixed (NativeLine* output = lines)
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, NativeLine*, uint, uint*, int>)NativeMethod(layout, 59))
                    (layout, output, count, &count));
        }, 14, layout =>
        {
            var title = new NativeRange(0, (uint)heading.Length);
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, NativeRange, int>)NativeMethod(layout, 32))
                (layout, weight == UiInlineTextWeight.Bold ? 700 : 600, title));
            var source = heading + body;
            for (var index = 0; index < source.Length; index++)
            {
                if (source[index] < '\uAC00' || source[index] > '\uD7A3')
                    continue;
                var end = index + 1;
                while (end < source.Length && end != heading.Length && (char.IsWhiteSpace(source[end])
                    || char.GetUnicodeCategory(source[end]) == System.Globalization.UnicodeCategory.Format))
                    end++;
                // Explicit fixture intervals include trailing spaces/controls in the preceding selected face.
                var korean = new NativeRange((uint)index, (uint)(end - index));
                fixed (char* family = "Malgun Gothic")
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, char*, NativeRange, int>)NativeMethod(layout, 31))
                        (layout, family, korean));
                if (index < heading.Length)
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, NativeRange, int>)NativeMethod(layout, 32))
                        (layout, 700, korean));
            }
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, float, int>)NativeMethod(layout, 28))(layout, width));
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, int>)NativeMethod(layout, 5))(layout, 0));
            configureLayout?.Invoke(layout);
        }, absoluteParagraphPositions: true, rasters: rasters);

        if (combineRasters && rasters.Count > 0)
        {
            // A literal input uploads one line bitmap. Pack independent system rasters on the same integer grid;
            // tight per-run quads otherwise introduce different fractional MSAA edges around identical pixels.
            var left = Math.Min(0, rasters.Min(raster => raster.Left));
            var top = rasters.Min(raster => raster.Top);
            var bitmapWidth = rasters.Max(raster => raster.Left + raster.Width) - left;
            var bitmapHeight = rasters.Max(raster => raster.Top + raster.Height) - top;
            var pixels = new byte[bitmapWidth * bitmapHeight * 4];
            foreach (var raster in rasters)
                for (var y = 0; y < raster.Height; y++)
                    for (var x = 0; x < raster.Width; x++)
                    {
                        var from = (y * raster.Width + x) * 4;
                        if (raster.Pixels[from + 3] == 0)
                            continue;
                        var to = ((y + raster.Top - top) * bitmapWidth + x + raster.Left - left) * 4;
                        raster.Pixels.AsSpan(from, 4).CopyTo(pixels.AsSpan(to, 4));
                    }
            var baseline = rasters[0].Baseline;
            rasters.Clear();
            rasters.Add(new(left, top, bitmapWidth, bitmapHeight, baseline, pixels));
        }
        var paint = new UiControlContext(ui);
        foreach (var raster in rasters)
        {
            var nativeTop = 0f;
            var designedTop = 0f;
            var found = false;
            var offset = 0f;
            foreach (var line in lines)
            {
                var height = MathF.Max(20, line.Height);
                if (MathF.Abs(raster.Baseline - nativeTop - line.Baseline) < .002f)
                {
                    offset = designedTop - nativeTop + (height - line.Height) / 2;
                    found = true;
                    break;
                }
                nativeTop += line.Height;
                designedTop += height;
            }
            if (!found)
                throw new InvalidOperationException("Native paragraph run baseline does not identify a line.");
            var texture = ui.AllocateOwnedTextureId();
            var rasterPixels = raster.Pixels;
            var ink = foreground ?? paint.Theme.Text;
            if (bakedBackground is { } background)
            {
                // Independent CPU reference: native RGB coverage is composed over an opaque background.
                // Upload the completed color image, bypassing the GPU font/dual-source blend path.
                rasterPixels = new byte[raster.Pixels.Length];
                var opacity = (ink.Rgba >> 24) / 255d;
                for (var index = 0; index < rasterPixels.Length; index += 4)
                {
                    for (var channel = 0; channel < 3; channel++)
                    {
                        var coverage = raster.Pixels[index + channel] / 255d * opacity;
                        var front = (ink.Rgba >> (channel * 8)) & 255;
                        var back = (background.Rgba >> (channel * 8)) & 255;
                        rasterPixels[index + channel] = (byte)Math.Round(front * coverage + back * (1 - coverage));
                    }
                    rasterPixels[index + 3] = 255;
                }
            }
            context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, texture, UiTextureFormat.Rgba8Unorm,
                raster.Width, raster.Height, rasterPixels)
            {
                IsColorImage = bakedBackground is not null
            });
            // PixelSnap translates the finished native coverage bitmap onto the physical grid.
            // The independent system layout and every glyph inside that bitmap remain unchanged.
            var position = new UiVector2(MathF.Round((origin?.X ?? 20) + raster.Left), MathF.Round((origin?.Y ?? 20) + raster.Top + offset));
            ui.GetWindowDrawList().AddImage(texture, position, new(position.X + raster.Width, position.Y + raster.Height),
                default, new(1, 1), bakedBackground is null ? ink : new(255, 255, 255));
        }
    }
}

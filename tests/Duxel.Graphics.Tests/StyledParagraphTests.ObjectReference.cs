using System.Runtime.InteropServices;
using Duxel.Core;

internal static unsafe partial class StyledParagraphTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectReferenceLine
    {
        internal uint Length, TrailingWhitespaceLength, NewlineLength;
        internal float Height, Baseline;
        internal int IsTrimmed;
    }
    private static ObjectReferenceLine[] ObjectReferenceLines(nint layout)
    {
        uint count = 0;
        var status = ((delegate* unmanaged[Stdcall]<nint, ObjectReferenceLine*, uint, uint*, int>)Method(layout, 59))
            (layout, null, 0, &count);
        if (status != unchecked((int)0x8007007A))
            Marshal.ThrowExceptionForHR(status);
        var lines = new ObjectReferenceLine[count];
        fixed (ObjectReferenceLine* output = lines)
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, ObjectReferenceLine*, uint, uint*, int>)Method(layout, 59))
                (layout, output, count, &count));
        return lines;
    }

    internal static void PaintOwnedObjectReference(UiContext context, UiImmediateContext ui, UiWidget child,
        UiVector2 extent, float width, bool rtl)
    {
        // This system TextLayout and object callback do not consume product paragraph metrics or allocations.
        ObjectReferenceLine fontLine = default;
        DirectWriteLayoutOracle.Draw("Segoe UI", " ", false, layout => fontLine = ObjectReferenceLines(layout)[0], 14);
        var callback = (OracleObject*)NativeMemory.AllocZeroed((nuint)sizeof(OracleObject));
        *callback = new()
        {
            Table = OracleObjectTable,
            References = 1,
            Width = extent.X,
            Height = extent.Y,
            Baseline = fontLine.Baseline + (extent.Y - fontLine.Height) / 2
        };
        var rasters = new List<DirectWriteLayoutOracle.RunRaster>();
        var placements = new List<(float X, float Y, bool RightToLeft)>();
        ObjectReferenceLine[] lines = [];
        try
        {
            var pointer = (nint)callback;
            DirectWriteLayoutOracle.Draw("Segoe UI", "\uFFFCregular body", rtl,
                layout => lines = ObjectReferenceLines(layout), 14, layout =>
                {
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, float, int>)Method(layout, 28))(layout, width));
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, int>)Method(layout, 5))(layout, 0));
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint, TextRange, int>)Method(layout, 39))
                        (layout, pointer, new(0, 1)));
                }, absoluteParagraphPositions: true, rasters: rasters, objects: placements);
        }
        finally
        {
            var references = callback->References;
            NativeMemory.Free(callback);
            if (references != 1)
                throw new InvalidOperationException("The independent object layout retained its owner.");
        }
        var paint = new UiControlContext(ui);
        using var clip = paint.Clip(new(20, 20, width, 160));
        foreach (var raster in rasters)
        {
            var nativeTop = 0f;
            var logicalTop = 0f;
            float? offset = null;
            foreach (var line in lines)
            {
                var height = MathF.Max(20, line.Height);
                if (MathF.Abs(raster.Baseline - nativeTop - line.Baseline) < .002f)
                {
                    offset = logicalTop - nativeTop + (height - line.Height) / 2;
                    break;
                }
                nativeTop += line.Height;
                logicalTop += height;
            }
            if (offset is null)
                throw new InvalidOperationException("The native object reference run has no matching line.");
            var texture = ui.AllocateOwnedTextureId();
            context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, texture, UiTextureFormat.Rgba8Unorm,
                raster.Width, raster.Height, raster.Pixels)
            {
                IsColorImage = false
            });
            var origin = new UiVector2(MathF.Round(20 + raster.Left), MathF.Round(20 + raster.Top + offset.Value));
            ui.GetWindowDrawList().AddImage(texture, origin, new(origin.X + raster.Width, origin.Y + raster.Height),
                default, new(1, 1), paint.Theme.Text);
        }
        var objectOrigin = placements.Single();
        var firstLineOffset = (MathF.Max(20, lines[0].Height) - lines[0].Height) / 2;
        ui.RenderLayoutChild(child, new(20 + objectOrigin.X, 20 + objectOrigin.Y + firstLineOffset, extent.X, extent.Y),
            new(20, 20, width, 160));
    }
}

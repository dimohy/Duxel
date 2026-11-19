using System.Runtime.InteropServices;
using Duxel.Core;

internal static unsafe partial class StyledParagraphTests
{
    internal static void PaintMarkdownObjectReference(UiContext context, UiImmediateContext ui, string text,
        int source, UiVector2 extent, float width, UiColor foreground, Action<UiControlContext, UiRect> paintObject)
    {
        // Independent native text and object layout; no product source mapping or paragraph snapshot is consumed.
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
            DirectWriteLayoutOracle.Draw("Segoe UI", text, false,
                layout => lines = ObjectReferenceLines(layout), 14, layout =>
                {
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, float, int>)Method(layout, 28))(layout, width));
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, int>)Method(layout, 5))(layout, 0));
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint, TextRange, int>)Method(layout, 39))
                        (layout, pointer, new((uint)source, 1)));
                }, absoluteParagraphPositions: true, rasters: rasters, objects: placements);
        }
        finally
        {
            var references = callback->References;
            NativeMemory.Free(callback);
            if (references != 1)
                throw new InvalidOperationException("The independent Markdown image layout retained its owner.");
        }
        var paint = new UiControlContext(ui);
        using var clip = paint.Clip(new(20, 20, width, 180));
        var offsets = new float[lines.Length];
        var nativeTop = 0f;
        var logicalTop = 0f;
        var character = 0u;
        var imageLine = -1;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (source >= character && source < character + line.Length)
                imageLine = index;
            var height = MathF.Max(20, line.Height);
            offsets[index] = logicalTop - nativeTop + (height - line.Height) / 2;
            nativeTop += line.Height;
            logicalTop += height;
            character += line.Length;
        }
        foreach (var raster in rasters)
        {
            nativeTop = 0;
            var row = -1;
            for (var index = 0; index < lines.Length; index++)
            {
                if (MathF.Abs(raster.Baseline - nativeTop - lines[index].Baseline) < .002f)
                {
                    row = index;
                    break;
                }
                nativeTop += lines[index].Height;
            }
            if (row < 0)
                throw new InvalidOperationException("The independent Markdown image glyph has no native line.");
            var texture = ui.AllocateOwnedTextureId();
            context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, texture, UiTextureFormat.Rgba8Unorm,
                raster.Width, raster.Height, raster.Pixels)
            {
                IsColorImage = false
            });
            var origin = new UiVector2(MathF.Round(20 + raster.Left), MathF.Round(20 + raster.Top + offsets[row]));
            ui.GetWindowDrawList().AddImage(texture, origin, new(origin.X + raster.Width, origin.Y + raster.Height),
                default, new(1, 1), foreground);
        }
        if (imageLine < 0)
            throw new InvalidOperationException("The independent Markdown image has no native line.");
        var placement = placements.Single();
        paintObject(paint, new(20 + placement.X, 20 + placement.Y + offsets[imageLine], extent.X, extent.Y));
    }
}

using System.Diagnostics;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private UiRect _repaintContentBounds, _repaintOutline;
    private UiRect[] _repaintImageOutlines = [];
    private UiRect _repaintMsaaOutline;
    private long _repaintUntil;
    private bool _repaintHighlightActive, _repaintDiagnosticRedraw;
    private readonly List<UiRect> _repaintBorderRepairs = [];

    private void PrepareRepaintHighlight(UiDrawData data, uint imageIndex)
    {
        _repaintBorderRepairs.Clear();
        _repaintContentBounds = _damageEmpty ? default : new(_damageArea.Offset.X, _damageArea.Offset.Y,
            _damageArea.Extent.Width, _damageArea.Extent.Height);
        _repaintDiagnosticRedraw = false;
        if (!data.RepaintHighlights && _repaintImageOutlines.Length == 0)
        {
            _repaintHighlightActive = false;
            return;
        }
        if (_repaintImageOutlines.Length != _damageImages.Length)
        {
            _repaintImageOutlines = new UiRect[_damageImages.Length];
            _repaintMsaaOutline = default;
        }
        var now = Stopwatch.GetTimestamp();
        if (data.RepaintHighlights && !data.RepaintDiagnosticCleanup && _repaintContentBounds.Width > 0)
        {
            _repaintOutline = _repaintContentBounds;
            _repaintUntil = now + Stopwatch.Frequency / 5;
        }
        _repaintHighlightActive = data.RepaintHighlights && now < _repaintUntil;
        var current = _repaintHighlightActive ? _repaintOutline : default;
        var previous = _repaintImageOutlines[imageIndex];
        var previousMsaa = _msaaSampleCount != SampleCountFlags.Count1Bit ? _repaintMsaaOutline : previous;
        if (previous != current || previousMsaa != current)
        {
            if (previous != current)
                AddBorderRepair(previous);
            if (previousMsaa != previous && previousMsaa != current)
                AddBorderRepair(previousMsaa);
            AddBorderRepair(current);
        }
        _repaintImageOutlines[imageIndex] = current;
        _repaintMsaaOutline = current;
        _repaintDiagnosticRedraw = _repaintBorderRepairs.Count != 0;
    }

    private void AddBorderRepair(UiRect outline)
    {
        // The interior of a diagnostic outline never changes its underlying pixels.
        // Split any strip crossing the content repair so we do not redraw that work.
        Span<UiRect> strips = stackalloc UiRect[4];
        foreach (var strip in strips[..GetRepaintBorder(outline, strips)])
        {
            var overlap = IntersectRepaintRects(strip, _repaintContentBounds);
            if (overlap.Width <= 0 || overlap.Height <= 0)
            {
                Add(strip);
                continue;
            }
            var right = overlap.X + overlap.Width;
            var bottom = overlap.Y + overlap.Height;
            Add(new(strip.X, strip.Y, strip.Width, overlap.Y - strip.Y));
            Add(new(strip.X, bottom, strip.Width, strip.Y + strip.Height - bottom));
            Add(new(strip.X, overlap.Y, overlap.X - strip.X, overlap.Height));
            Add(new(right, overlap.Y, strip.X + strip.Width - right, overlap.Height));
        }

        void Add(UiRect strip)
        {
            if (strip.Width > 0 && strip.Height > 0 && !_repaintBorderRepairs.Contains(strip))
                _repaintBorderRepairs.Add(strip);
        }
    }

    private static UiRect IntersectRepaintRects(UiRect a, UiRect b)
    {
        var x = MathF.Max(a.X, b.X);
        var y = MathF.Max(a.Y, b.Y);
        return new(x, y, MathF.Min(a.X + a.Width, b.X + b.Width) - x,
            MathF.Min(a.Y + a.Height, b.Y + b.Height) - y);
    }

    private static int GetRepaintBorder(UiRect bounds, Span<UiRect> strips)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return 0;
        const int thickness = 2;
        if (bounds.Width <= thickness * 2 || bounds.Height <= thickness * 2)
        {
            strips[0] = bounds;
            return 1;
        }
        strips[0] = new(bounds.X, bounds.Y, bounds.Width, thickness);
        strips[1] = new(bounds.X, bounds.Y + bounds.Height - thickness, bounds.Width, thickness);
        strips[2] = new(bounds.X, bounds.Y + thickness, thickness, bounds.Height - thickness * 2);
        strips[3] = new(bounds.X + bounds.Width - thickness, bounds.Y + thickness, thickness, bounds.Height - thickness * 2);
        return 4;
    }

    private static Rect2D RepaintRectToRenderArea(UiRect rect) =>
        new(new((int)rect.X, (int)rect.Y), new((uint)rect.Width, (uint)rect.Height));

    private long CountRepaintBorderPixels()
    {
        var pixels = 0L;
        foreach (var strip in _repaintBorderRepairs)
            pixels += (long)strip.Width * (long)strip.Height;
        return pixels;
    }

    private void RecordRepaintHighlight(CommandBuffer commandBuffer)
    {
        if (!_repaintHighlightActive)
            return;
        Span<UiRect> strips = stackalloc UiRect[4];
        var area = new UiRect(_damageArea.Offset.X, _damageArea.Offset.Y, _damageArea.Extent.Width, _damageArea.Extent.Height);
        var rectangles = stackalloc ClearRect[4];
        var count = 0u;
        foreach (var strip in strips[..GetRepaintBorder(_repaintOutline, strips)])
        {
            var clipped = IntersectRepaintRects(strip, area);
            if (clipped.Width > 0 && clipped.Height > 0)
                rectangles[count++] = new() { Rect = RepaintRectToRenderArea(clipped), LayerCount = 1 };
        }
        if (count == 0)
            return;
        var attachment = new ClearAttachment
        {
            AspectMask = ImageAspectFlags.ColorBit,
            ColorAttachment = 0,
            ClearValue = new() { Color = new ClearColorValue(0.1f, 1f, 0.3f, 1f) }
        };
        _vk.CmdClearAttachments(commandBuffer, 1, &attachment, count, rectangles);
    }
}

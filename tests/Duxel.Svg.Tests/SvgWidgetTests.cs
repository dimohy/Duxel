using System.Numerics;
using Duxel.Core;
using Duxel.Svg;

internal static class SvgWidgetTests
{
    internal static UiSvgTextureStatistics Run(string output, Action<bool, string> check)
    {
        var source = Path.Combine(output, "widget.svg");
        const string first = "<svg xmlns='http://www.w3.org/2000/svg' width='64' height='48'><rect width='32' height='24' fill='#e03020'/></svg>";
        File.WriteAllText(source, first);
        using var store = new SvgStore();
        var document = store.Load(source);
        using var widget = new UiSvg(document);
        using var peer = new UiSvg(document);
        using var context = Context();
        UiImmediateContext? immediate = null;
        var measureOnly = true;
        Snapshot Frame(params UiSvg[] widgets)
        {
            var measuredSize = default(UiVector2);
            var nextCursor = default(UiVector2);
            var inlineCursor = default(UiVector2);
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new View(ui =>
            {
                immediate = ui;
                if (measureOnly)
                {
                    var measured = widget.MeasureConstrained(ui, UiBoxConstraints.Tight(new(30, 20)));
                    check(measured == new UiVector2(30, 20) && store.Statistics.Rasterizations == 0 &&
                        ui.SvgTextureStatistics.Uploads == 0,
                        "constrained SVG measurement respects parent allocation without raster or texture side effects");
                    measureOnly = false;
                }
                measuredSize = widget.MeasureConstrained(ui, new UiBoxConstraints());
                foreach (var item in widgets)
                    item.Render(ui);
                nextCursor = ui.GetCursorScreenPos();
                ui.SameLine();
                inlineCursor = ui.GetCursorScreenPos();
            }));
            using var draw = context.GetDrawData();
            UiVector2 Project(UiVector2 point, UiLayer3D? scene)
            {
                if (scene is not { } plane)
                    return point;
                var viewport = plane.Viewport ?? new UiRect(0, 0, 640, 480);
                var clipPoint = new Vector4((point.X - viewport.X) * 2 / viewport.Width - 1,
                    (point.Y - viewport.Y) * 2 / viewport.Height - 1, 0, 1);
                var projected = Vector4.Transform(clipPoint, plane.GetEffectiveTransform(new(0, 0, 640, 480)));
                return new(viewport.X + (projected.X / projected.W + 1) * viewport.Width / 2,
                    viewport.Y + (projected.Y / projected.W + 1) * viewport.Height / 2);
            }
            UiRect ProjectBounds(UiRect bounds, UiLayer3D? scene)
            {
                var points = new[] { Project(new(bounds.X, bounds.Y), scene), Project(new(bounds.X + bounds.Width, bounds.Y), scene),
                    Project(new(bounds.X, bounds.Y + bounds.Height), scene), Project(new(bounds.X + bounds.Width, bounds.Y + bounds.Height), scene) };
                var x = points.Min(point => point.X);
                var y = points.Min(point => point.Y);
                return new(x, y, points.Max(point => point.X) - x, points.Max(point => point.Y) - y);
            }
            // Assertions target effective client geometry, whether vertices or a retained scene carry the transform.
            var commands = draw.DrawLists.SelectMany(list => list.Commands.Where(command => command.TextureId.Value >= 2_100_000_000)
                .Select(command => command with { ClipRect = ProjectBounds(command.ClipRect, list.Scene3D) })).ToArray();
            var vertices = draw.DrawLists.SelectMany(list => list.Vertices.Select(vertex => vertex with { Position = Project(vertex.Position, list.Scene3D) })).ToArray();
            return new(draw.TextureUpdates.ToArray(), commands, vertices, measuredSize, nextCursor, inlineCursor);
        }
        var initial = Frame(widget);
        var id = initial.Updates.Single(update => update.Kind == UiTextureUpdateKind.RenderVectorScene).TextureId;
        check(initial.Commands.Length == 1 && initial.Commands[0].TextureId == id && initial.Vertices.Length == 4,
            "SVG widget uploads and draws the retained resource as one textured quad");
        check(initial.Updates[0] is { Width: 64, Height: 48 } && initial.Updates[0].RgbaPixels.IsEmpty &&
            store.Statistics is { XmlParses: 1, SceneCompiles: 1, Rasterizations: 0 },
            "SVG widget requests intrinsic GPU output dimensions without CPU pixels");
        var unchanged = true;
        for (var i = 0; i < 100; i++)
            unchanged &= Frame(widget).Updates.Length == 0;
        check(unchanged && immediate!.SvgTextureStatistics is { Uploads: 0, GpuRenderRequests: 1, ResidentTextures: 1 },
            "one hundred unchanged widget frames enqueue no raster or texture upload");
        widget.RotationDegrees = 90;
        widget.Offset = new(10, 20);
        widget.Tint = new(0x80FFFFFF);
        var transformed = Frame(widget);
        var a = initial.Vertices[0].Position;
        var b = transformed.Vertices[0].Position;
        check(transformed.Updates.Length == 0 && transformed.Commands[0].TextureId == id &&
            MathF.Abs(b.X - (a.X + 58)) < .01f && MathF.Abs(b.Y - (a.Y + 20)) < .01f &&
            transformed.Vertices.All(vertex => vertex.Color == widget.Tint),
            "clockwise rotation, translation and tint change only geometry while preserving the SVG texture");
        check(transformed.MeasuredSize == new UiVector2(58, 84) && initial.MeasuredSize == new UiVector2(64, 48) &&
            transformed.NextCursor.Y - initial.NextCursor.Y == 36 &&
            transformed.InlineCursor.X - initial.InlineCursor.X == -6 &&
            transformed.Vertices.All(vertex => vertex.Position.X >= transformed.Commands[0].ClipRect.X &&
                vertex.Position.Y >= transformed.Commands[0].ClipRect.Y &&
                vertex.Position.X <= transformed.Commands[0].ClipRect.X + transformed.Commands[0].ClipRect.Width &&
                vertex.Position.Y <= transformed.Commands[0].ClipRect.Y + transformed.Commands[0].ClipRect.Height),
            "rotated SVG bounds and leading margins expand measurement and following flow without clipping its corners");
        var shared = Frame(widget, peer);
        check(shared.Updates.Length == 0 && shared.Commands.All(command => command.TextureId == id) &&
            immediate!.SvgTextureStatistics is { Uploads: 0, GpuRenderRequests: 1, ResidentTextures: 1 },
            "two SVG widgets share one context texture for the same document revision and resolution");
        widget.Zoom = 2;
        var zoomed = Frame(widget);
        var zoomId = zoomed.Updates.Single().TextureId;
        check(zoomed.Updates[0] is { Width: 128, Height: 96 } && zoomId != id &&
            zoomed.MeasuredSize == new UiVector2(106, 148) &&
            store.Statistics is { XmlParses: 1, SceneCompiles: 1, Rasterizations: 0 } &&
            ReferenceEquals(zoomed.Updates[0].VectorScene, initial.Updates[0].VectorScene),
            "zoom requests higher-resolution GPU output with the same paths and no CPU rasterization");
        widget.Zoom = 1;
        var returned = Frame(widget);
        check(returned.Updates.Length == 0 && returned.Commands[0].TextureId == id,
            "returning to a cached zoom level reuses the original GPU texture identity");
        widget.RasterScale = 2;
        var resolution = Frame(widget);
        check(resolution.Updates.Length == 0 && resolution.Commands[0].TextureId == zoomId,
            "explicit raster scale shares a matching resolution independently from layout zoom");
        widget.RasterScale = 1;
        File.WriteAllText(source, first.Replace("#e03020", "#2080e0", StringComparison.Ordinal));
        store.Reload(source);
        var changed = Frame(widget, peer);
        check(changed.Updates.Length == 1 && changed.Commands.All(command => command.TextureId == changed.Updates[0].TextureId) &&
            changed.Updates[0].TextureId != id && changed.Updates[0].VectorScene!.Data.Span[2].Z == 224 / 255f,
            "source reload replaces one shared GPU scene without mutating previous frame commands");
        check(initial.Updates[0].VectorScene!.Data.Span[2].X == 224 / 255f && initial.Updates[0].VectorScene!.Data.Span[2].Z == 32 / 255f,
            "queued old SVG scene colors remain immutable after source reload");
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "static SVG rendering creates no animation dependency or periodic frame schedule");
        widget.RotationDegrees = 45;
        widget.Margin = new(10, 20, 30, 40);
        var diagonal = Frame(widget);
        var diagonalExtent = 112 / MathF.Sqrt(2);
        check(MathF.Abs(diagonal.MeasuredSize.X - diagonalExtent - 40) < .001f &&
            MathF.Abs(diagonal.MeasuredSize.Y - diagonalExtent - 60) < .001f && diagonal.Updates.Length == 0,
            "arbitrary SVG rotation measures its tight transformed bounds plus four margins without raster or texture work");
        widget.RotationDegrees = -90;
        var negativeTurn = Frame(widget);
        check(negativeTurn.MeasuredSize == new UiVector2(88, 124) && negativeTurn.Updates.Length == 0,
            "negative SVG quarter turns use exact swapped extents without floating-point raster growth");
        var invalidMargin = false;
        try
        {
            widget.Margin = new(-1, 0, 0, 0);
        }
        catch (ArgumentOutOfRangeException) { invalidMargin = true; }
        check(invalidMargin, "SVG margins reject negative layout space explicitly");
        using var second = Context();
        second.NewFrame(new(1f / 60, new(640, 480), new(1, 1)),
            new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
            new(0, 0, 640, 480), UiTextSettings.Default);
        second.Render(new View(widget.Render));
        using (var draw = second.GetDrawData())
            check(draw.TextureUpdates.Count == 1, "a second context creates its own texture while sharing the retained CPU SVG");
        var widgetStatistics = immediate!.SvgTextureStatistics;
        using (var cameraIcon = new UiSvgIcon(document))
        {
            CameraIcon.Icon = cameraIcon.MenuIcon;
            using var previewIcon = Fluent.Icon(UiMenuIcon.Create<CameraIcon, CameraIcon>(), new(640, 480), new(255, 255, 255));
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new View(previewIcon.Render));
            using var cameraDraw = context.GetDrawData();
            var request = cameraDraw.TextureUpdates.Single(update => update.Kind == UiTextureUpdateKind.RenderVectorScene);
            check(request.VectorCamera is { FullPixelSize.X: 4096, FullPixelSize.Y: 3072 }
                && request.Width <= 642 && request.Height <= 482 && request.RgbaPixels.IsEmpty && store.Statistics.Rasterizations == 0,
                "SVG painting uses a visible camera window instead of allocating the full virtual zoom viewport");
            CameraIcon.Icon = default;
        }
        context.Dispose();
        check(immediate!.SvgTextureStatistics is { ResidentTextures: 0, ResidentBytes: 0 },
            "context teardown clears SVG residency and detaches outstanding widget leases");
        widget.Dispose();
        peer.Dispose();
        check(true, "widget disposal safely releases leases after context teardown");
        var invalid = false;
        using var bad = new UiSvg(document);
        try
        {
            bad.Zoom = float.NaN;
        }
        catch (ArgumentOutOfRangeException) { invalid = true; }
        check(invalid, "SVG transforms reject non-finite geometry before rendering");
        Budget(document, check);
        ReplacementBudget((IUiGpuSvgDocument)document, check);
        return widgetStatistics;
    }

    private static void ReplacementBudget(IUiGpuSvgDocument document, Action<bool, string> check)
    {
        using var context = Context();
        var source = new ReplacementDocument(document);
        var small = new UiVector2(3000, 3000);
        var large = new UiVector2(3200, 3200);
        using var widget = new UiSvg(source) { Size = small, UseFixedResolutionPreview = true };
        using var peer = new UiSvg(source) { Size = small, UseFixedResolutionPreview = true };
        UiImmediateContext immediate = null!;
        UiDrawData Frame(Action<UiImmediateContext> render)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new View(ui => { immediate = ui; render(ui); }));
            return context.GetDrawData();
        }
        void Paint(UiImmediateContext ui, UiSvg target)
        {
            ui.SetCursorScreenPos(new(10, 10));
            target.Render(ui);
        }
        void AssertRejected(string message, Action<UiImmediateContext> render, string reason)
        {
            try { using var drawing = Frame(render); }
            catch (InvalidOperationException ex) when (ex.Message.Contains(message, StringComparison.Ordinal))
            {
                check(immediate.SvgTextureStatistics.ResidentTextures == 1, reason);
                return;
            }
            check(false, reason);
        }

        using var original = Frame(ui => { Paint(ui, widget); Paint(ui, peer); });
        var first = original.TextureUpdates.Single();
        widget.Size = large;
        AssertRejected("cache budget", ui => Paint(ui, widget), "large replacement preserves the other widget's shared texture");
        peer.Dispose();

        source.ThrowOnPrepare = true;
        AssertRejected("Synthetic preparation failure", ui => Paint(ui, widget), "failed preparation preserves the previous texture lease");
        source.ThrowOnPrepare = false;
        widget.Size = small;
        using (var restored = Frame(ui => Paint(ui, widget)))
            check(restored.TextureUpdates.Count == 0, "failed replacement leaves the old texture reusable without another upload");

        widget.Size = large;
        using var replacement = Frame(ui => Paint(ui, widget));
        var updates = replacement.TextureUpdates;
        check(updates.Count == 2 && updates[0].Kind == UiTextureUpdateKind.Destroy && updates[0].TextureId == first.TextureId
            && updates[1].Kind == UiTextureUpdateKind.RenderVectorScene && updates[1].TextureId != first.TextureId
            && updates[1].Width == 3200 && updates[1].Height == 3200
            && immediate.SvgTextureStatistics is { ResidentTextures: 1, ResidentBytes: 40_960_000 }
            && original.TextureUpdates.Single() == first,
            "an exclusive SVG can replace its previous size without counting both sizes against the residency budget");

        var layer = new UiLayerOptions(false, 1, default) { ContentVersion = 1 };
        using (Frame(ui =>
        {
            if (ui.BeginLayer("replacement-retained", layer)) Paint(ui, widget);
            ui.EndLayer();
        })) { }
        widget.Size = small;
        AssertRejected("cache budget", ui => Paint(ui, widget), "replacement preserves a texture used by a retained layer");
        immediate.RemoveLayer("replacement-retained");

        widget.Size = large;
        AssertRejected("cache budget", ui =>
        {
            Paint(ui, widget);
            widget.Size = small;
            Paint(ui, widget);
        }, "replacement preserves a texture already used by the current frame");
    }

    private sealed class ReplacementDocument(IUiGpuSvgDocument document) : IUiGpuSvgDocument
    {
        internal bool ThrowOnPrepare;
        public UiVector2 IntrinsicSize => document.IntrinsicSize;
        public long Revision => document.Revision;
        public event Action? Changed { add { } remove { } }
        public UiGpuVectorScene GpuScene => ThrowOnPrepare
            ? throw new InvalidOperationException("Synthetic preparation failure.") : document.GpuScene;
        public UiSvgRaster Rasterize(int width, int height) => document.Rasterize(width, height);
    }

    private static void Budget(IUiSvgDocument document, Action<bool, string> check)
    {
        using var context = Context();
        var widgets = Enumerable.Range(1, 129).Select(width => new UiSvg(document) { Size = new(width, 1) }).ToArray();
        UiImmediateContext? immediate = null;
        void Begin() => context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)),
            new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
            new(0, 0, 640, 480), UiTextSettings.Default);
        View Screen(IEnumerable<UiSvg> items) => new(ui =>
        {
            immediate = ui;
            foreach (var item in items)
            {
                ui.SetCursorScreenPos(new(10, 10));
                if (ReferenceEquals(item, widgets[0]))
                {
                    if (ui.BeginLayer("svg-budget-retained", new(false, 1, default)
                    {
                        ContentVersion = 1
                    }))
                        item.Render(ui);
                    ui.EndLayer();
                }
                else
                    item.Render(ui);
            }
        });
        try
        {
            Begin();
            context.Render(Screen(widgets.Take(128)));
            using (var draw = context.GetDrawData())
                check(draw.TextureUpdates.Count == 128 && immediate!.SvgTextureStatistics.ResidentTextures == 128,
                    "bounded SVG texture cache retains the complete live widget working set");
            Begin();
            var rejected = false;
            try
            {
                context.Render(Screen(widgets));
            }
            catch (InvalidOperationException error) when (error.Message.Contains("cache budget", StringComparison.Ordinal)) { rejected = true; }
            check(rejected && immediate!.SvgTextureStatistics.ResidentTextures == 128,
                "cache pressure rejects a live working-set overflow instead of deleting pinned textures");
            widgets[0].Dispose();
            Begin();
            var retained = false;
            try
            {
                context.Render(Screen(widgets.Skip(1)));
            }
            catch (InvalidOperationException error) when (error.Message.Contains("cache budget", StringComparison.Ordinal)) { retained = true; }
            check(retained && immediate!.SvgTextureStatistics.ResidentTextures == 128,
                "retained-layer texture references stay protected after the source widget releases its lease");
            immediate!.RemoveLayer("svg-budget-retained");
            Begin();
            context.Render(Screen(widgets.Skip(1)));
            using var next = context.GetDrawData();
            check(next.TextureUpdates.Count(update => update.Kind == UiTextureUpdateKind.Destroy) == 1 &&
                next.TextureUpdates.Count(update => update.Kind == UiTextureUpdateKind.RenderVectorScene) == 1 &&
                immediate!.SvgTextureStatistics.ResidentTextures == 128,
                "disposing a widget and removing its retained layer permits texture eviction while preserving live dependencies");
        }
        finally { foreach (var item in widgets) item.Dispose(); }
    }
    private static UiContext Context() => new(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
    private readonly struct CameraIcon : IUiButtonIcon
    {
        internal static UiMenuIcon Icon;
        public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction)
            => Icon.Paint(context, new(16, 16, 4096, 3072), color, interaction);
    }
    private sealed class View(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private sealed record Snapshot(UiTextureUpdate[] Updates, UiDrawCommand[] Commands, UiDrawVertex[] Vertices,
        UiVector2 MeasuredSize, UiVector2 NextCursor, UiVector2 InlineCursor);
}

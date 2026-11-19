using System.Security.Cryptography;
using System.Text;
using Duxel.Core;

internal static class RasterLayerLifetimeTests
{
    internal static void RunCpu(Action<bool, string> check, bool rounded = false)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        UiDrawData BuildFrame(Action<UiImmediateContext> action)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(action));
            return context.GetDrawData();
        }
        foreach (var svg in new[] { false, true })
        {
            using var frames = Build(BuildFrame, check, svg, rounded);
        }
    }

    internal sealed class Frames : IDisposable
    {
        internal readonly List<(string Name, UiDrawData Data)> Items = [];
        public void Dispose()
        {
            foreach (var item in Items)
                item.Data.Dispose();
        }
    }

    internal static Frames Build(Func<Action<UiImmediateContext>, UiDrawData> build,
        Action<bool, string> check, bool svg, bool rounded = false)
    {
        var kind = svg ? "SVG" : "Image";
        var layer = "raster-lifetime-" + kind;
        var options = new UiLayerOptions(true, 1, default) { ContentVersion = 1 };
        UiWidget Create(byte red, byte green, byte blue, UiVector2 size)
        {
            if (svg)
                return new UiSvg(new SolidDocument(red, green, blue)) { Size = size, CornerRadius = rounded ? MathF.Min(size.X, size.Y) * .5f : 0 };
            var image = Fluent.Image(new(new(1, 1, [red, green, blue, 255])));
            image.Size = size;
            image.CornerRadius = rounded ? MathF.Min(size.X, size.Y) * .5f : 0;
            return image;
        }
        var frames = new Frames();
        using var target = Create(220, 30, 40, new(32, 32));
        try
        {
            var builds = 0;
            void Retained(UiImmediateContext ui)
            {
                if (ui.BeginLayer(layer, options))
                {
                    builds++;
                    ui.SetCursorScreenPos(new(24, 24));
                    target.Render(ui);
                }
                ui.EndLayer();
            }
            var first = build(Retained);
            frames.Items.Add(("queued-original", first));
            var commands = first.DrawLists.SelectMany(list => list.Commands);
            var texture = (rounded ? commands.Single(command => command.IsImagePrimitive) : commands.Single()).TextureId;
            var snapshot = Fingerprint(first);
            target.Dispose();
            var fill = build(ui =>
            {
                for (var index = 0; index < 127; index++)
                {
                    using var transient = Create(80, 90, 100, new(1, 1));
                    ui.SetCursorScreenPos(new(200, 150));
                    transient.Render(ui);
                }
            });
            frames.Items.Add(("queued-cache-fill", fill));
            var pressure = build(ui =>
            {
                using var transient = Create(40, 80, 120, new(1, 1));
                ui.SetCursorScreenPos(new(200, 150));
                transient.Render(ui);
                Retained(ui);
            });
            frames.Items.Add(("queued-protected-replay", pressure));
            check(builds == 1, kind + " cached layer replays after its widget has been disposed without invoking that widget");
            check(pressure.TextureUpdates.Count(update => update.Kind == UiTextureUpdateKind.Destroy) == 1 &&
                pressure.TextureUpdates.All(update => update.Kind != UiTextureUpdateKind.Destroy || update.TextureId != texture),
                kind + " texture pressure retires an unused entry and protects the retained layer dependency");
            check(pressure.DrawLists.SelectMany(list => list.Commands).Any(command => command.TextureId == texture),
                kind + " retained replay keeps the original texture identity under cache pressure");
            var pressureSnapshot = Fingerprint(pressure);
            var retired = build(ui =>
            {
                ui.RemoveLayer(layer);
                using var transient = Create(20, 50, 90, new(1, 1));
                ui.SetCursorScreenPos(new(200, 150));
                transient.Render(ui);
            });
            frames.Items.Add(("queued-after-removal", retired));
            check(retired.TextureUpdates.Any(update => update.Kind == UiTextureUpdateKind.Destroy && update.TextureId == texture),
                kind + " layer removal makes its unleased texture eligible for later ordered retirement");
            check(snapshot == Fingerprint(first) && pressureSnapshot == Fingerprint(pressure),
                kind + " queued frame geometry commands and upload pixels stay immutable after layer removal and retirement");
            return frames;
        }
        catch { frames.Dispose(); throw; }
    }

    internal static string Fingerprint(UiDrawData data)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string text) => hash.AppendData(Encoding.UTF8.GetBytes(text));
        foreach (var list in data.DrawLists)
        {
            foreach (var vertex in list.Vertices)
                Add(vertex.ToString());
            foreach (var index in list.Indices)
                Add(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (list.RectFilledPrimitives is { } primitives)
                foreach (var primitive in primitives)
                    Add(primitive.ToString());
            foreach (var command in list.Commands)
                Add(command.ToString());
        }
        foreach (var update in data.TextureUpdates)
        {
            Add(update.Kind + "/" + update.TextureId + "/" + update.Width + "/" + update.Height);
            hash.AppendData(update.RgbaPixels.Span);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    internal static Frames BuildExclusiveReplacement(Func<Action<UiImmediateContext>, UiDrawData> build,
        Action<bool, string> check, bool rounded)
    {
        using var target = new UiSvg(new SolidDocument(220, 30, 40))
        {
            Size = new(32, 32),
            CornerRadius = rounded ? 16 : 0
        };
        var source = new SolidDocument(60, 80, 100);
        var fillers = Enumerable.Range(1, 127).Select(width => new UiSvg(source) { Size = new(width, 1) }).ToArray();
        var frames = new Frames();
        try
        {
            var original = build(ui =>
            {
                foreach (var filler in fillers)
                {
                    ui.SetCursorScreenPos(new(180, 160));
                    filler.Render(ui);
                }
                DrawTarget(ui);
            });
            frames.Items.Add(("exclusive-original", original));
            var texture = original.TextureUpdates.Last(update => update.Kind == UiTextureUpdateKind.RenderVectorScene).TextureId;
            var snapshot = Fingerprint(original);
            target.Size = new(48, 32);
            var replacement = build(DrawTarget);
            frames.Items.Add(("exclusive-replacement", replacement));
            var updates = replacement.TextureUpdates;
            check(updates.Count == 2 && updates[0].Kind == UiTextureUpdateKind.Destroy && updates[0].TextureId == texture
                && updates[1].Kind == UiTextureUpdateKind.RenderVectorScene && updates[1].TextureId != texture,
                "full SVG entry cache retires only the exclusive prior texture and queues its replacement");
            check(snapshot == Fingerprint(original), "SVG size replacement preserves the already claimed frame");
            return frames;
        }
        catch { frames.Dispose(); throw; }
        finally { foreach (var filler in fillers) filler.Dispose(); }

        void DrawTarget(UiImmediateContext ui)
        {
            ui.SetCursorScreenPos(new(24, 24));
            target.Render(ui);
        }
    }

    private sealed class SolidDocument(byte red, byte green, byte blue) : IUiGpuSvgDocument
    {
        public UiGpuVectorScene GpuScene { get; } = GpuVectorTestScenes.Solid(new(1, 1), red / 255f, green / 255f, blue / 255f);
        public UiVector2 IntrinsicSize => new(1, 1);
        public long Revision => 0;
        public event Action? Changed { add { } remove { } }
        public UiSvgRaster Rasterize(int width, int height)
        {
            var pixels = new byte[width * height * 4];
            for (var offset = 0; offset < pixels.Length; offset += 4)
            {
                pixels[offset] = red;
                pixels[offset + 1] = green;
                pixels[offset + 2] = blue;
                pixels[offset + 3] = 255;
            }
            return new(width, height, pixels);
        }
    }

    private sealed class Screen(Action<UiImmediateContext> action) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => action(ui);
    }
}

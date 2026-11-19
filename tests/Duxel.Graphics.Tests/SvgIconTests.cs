using Duxel.Core;
using Duxel.Svg;

internal static class SvgIconTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        var regularPath = Path.Combine(output, "icon-regular.svg");
        var filledPath = Path.Combine(output, "icon-filled.svg");
        const string regularSource = "<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24'><rect width='24' height='24' fill='#e03020'/></svg>";
        File.WriteAllText(regularPath, regularSource);
        File.WriteAllText(filledPath, regularSource.Replace("#e03020", "#2080e0", StringComparison.Ordinal));
        using var store = new SvgStore();
        var regular = store.Load(regularPath);
        var filled = store.Load(filledPath);
        using var authored = new UiSvgIcon(regular, filled);
        using var foreground = new UiSvgIcon(regular, filled, UiSvgIconColorMode.MultiplyForeground);
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        var color = new UiColor(60, 180, 210);
        var bounds = new UiRect(40, 30, 24, 24);
        (UiTextureUpdate[] Updates, UiDrawCommand Command, UiDrawVertex[] Vertices) Frame(UiSvgIcon icon,
            bool hovered = false, bool disabled = false, bool tab = false, bool selected = false)
        {
            context.NewFrame(new(1f / 60, new(320, 200), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 320, 200), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                var cursor = ui.GetCursorScreenPos();
                if (disabled) ui.BeginDisabled();
                var control = new UiControlContext(ui);
                if (tab) icon.TabIcon.Paint(control, bounds, color, selected, new(false, hovered, false));
                else icon.MenuIcon.Paint(control, bounds, color, new(false, hovered, false));
                if (disabled) ui.EndDisabled();
                if (ui.GetCursorScreenPos() != cursor) throw new InvalidOperationException("An icon painter advanced layout.");
            }));
            using var draw = context.GetDrawData();
            return (draw.TextureUpdates.ToArray(), draw.DrawLists.SelectMany(list => list.Commands).Single(),
                draw.DrawLists.SelectMany(list => list.Vertices).ToArray());
        }
        var first = Frame(authored);
        check(first.Updates.Single().Kind == UiTextureUpdateKind.RenderVectorScene && first.Updates.Single().RgbaPixels.IsEmpty &&
            first.Updates.Single().VectorScene!.Data.Span[2].X == 224 / 255f && first.Vertices.All(vertex => vertex.Color == new UiColor(0xFFFFFFFF)),
            "SVG menu icon preserves authored colors without applying the control foreground by default");
        check(first.Command.ClipRect == bounds && first.Vertices[0].Position == new UiVector2(40, 30),
            "SVG icon uses the supplied bounds and clip without advancing control layout");
        var hover = Frame(authored, hovered : true);
        check(hover.Updates.Single().VectorScene!.Data.Span[2].Z == 224 / 255f && hover.Command.TextureId != first.Command.TextureId,
            "enabled menu hover selects the retained filled SVG document");
        var disabled = Frame(authored, hovered : true, disabled : true);
        check(disabled.Updates.Length == 0 && disabled.Command.TextureId == first.Command.TextureId,
            "disabled menu hover keeps the regular SVG and reuses its texture");
        var selected = Frame(authored, tab : true, selected : true);
        var unselected = Frame(authored, hovered : true, tab : true);
        check(selected.Updates.Length == 0 && selected.Command.TextureId == hover.Command.TextureId &&
            unselected.Command.TextureId == first.Command.TextureId,
            "SVG tab icon follows selection independently of pointer hover");
        var tinted = Frame(foreground);
        check(tinted.Updates.Length == 0 && tinted.Command.TextureId == first.Command.TextureId &&
            tinted.Vertices.All(vertex => vertex.Color == color),
            "explicit foreground modulation shares authored raster/texture and changes vertex color only");
        check(authored.MenuIcon == authored.MenuIcon && authored.TabIcon == authored.TabIcon &&
            store.Statistics is { XmlParses : 2, SceneCompiles : 2, Rasterizations : 0, GpuSceneCompiles : 2 },
            "SVG icon adapters retain stable painter identities and parse/compile each source once");
        var before = requests;
        File.WriteAllText(regularPath, regularSource.Replace("#e03020", "#20c060", StringComparison.Ordinal));
        store.Reload(regularPath);
        check(requests > before, "SVG icon source reload wakes event-wait rendering even when painted inside another control");
        var reloaded = Frame(authored);
        check(reloaded.Updates.Length == 1 && reloaded.Updates[0].VectorScene!.Data.Span[2].Y == 192 / 255f,
            "SVG icon reload updates its shared source revision without stale pixels");
        authored.Dispose();
        foreground.Dispose();
        before = requests;
        File.WriteAllText(regularPath, regularSource);
        store.Reload(regularPath);
        check(requests == before, "disposed SVG icon adapters unsubscribe resource notifications and release leases");
        var rejected = false;
        try { Frame(authored); } catch (ObjectDisposedException) { rejected = true; }
        check(rejected, "a retained adapter cannot paint a disposed SVG icon");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}

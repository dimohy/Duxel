using Duxel.Core;

internal static partial class PopoverPaintBoundsTests
{
    internal static void Run(Action<bool, string> check)
    {
        using (var fixture = new Fixture<DefaultSurface>())
        {
            fixture.Popover.Show();
            using var first = fixture.Frame();
            var content = first.TextureUpdates.Single(x => x.Kind == UiTextureUpdateKind.RenderPaintGroup).PaintGroup!.Content;
            var body = Find(fixture.Tree, "Bounds owner", UiAccessibilityRole.Group).Node.Bounds;
            var marker = Marker(body);
            check(content.Bounds == new UiRect(0, 0, 640, 480), "Custom Popover inherits viewport paint extent instead of a Fluent-only margin");
            check(content.DrawLists.Any(list => list.RectFilledPrimitives?.Any(p => p.Rect == marker && p.Color == new UiColor(200, 0, 0)) == true),
                "Custom paint beyond88px remains present in the captured scene");
            using var middle = fixture.Frame(.05f);
            using var replay = fixture.Frame();
            check(replay.TextureUpdates.All(x => x.Kind != UiTextureUpdateKind.RenderPaintGroup), "Default custom extent preserves same-time group reuse");
        }
        using (var fixture = new Fixture<BoundedSurface<WideExtent>>())
        {
            fixture.Popover.Show();
            using var first = fixture.Frame();
            var body = Find(fixture.Tree, "Bounds owner", UiAccessibilityRole.Group).Node.Bounds;
            var content = first.TextureUpdates.Single(x => x.Kind == UiTextureUpdateKind.RenderPaintGroup).PaintGroup!.Content;
            var declared = WideExtent.Resolve(body);
            check(content.Bounds == declared && content.PixelWidth * content.PixelHeight < 640 * 480,
                "An explicit custom paint extent bounds the target without cropping decoration");
            using var settled = fixture.Frame(.41f);
            check(fixture.Context.State.NextScheduledFrameTimeSeconds is null, "Custom paint extent returns to event wait after finite entry");
        }
        using (var fixture = new Fixture<BoundedSurface<WideExtent>>(new(4, 4)))
        {
            fixture.Popover.Show();
            using var first = fixture.Frame();
            var content = first.TextureUpdates.Single(x => x.Kind == UiTextureUpdateKind.RenderPaintGroup).PaintGroup!.Content;
            check(content.Bounds.X == 0 && content.Bounds.Y == 0, "Declared paint extent is clipped only to the display viewport");
        }
        Invalid<NonFiniteExtent>(check, false);
        Invalid<EmptyExtent>(check, false);
        Invalid<SmallExtent>(check, true);
        using (var fixture = new Fixture<DefaultSurface>(viewport: new(10000, 10000)))
        {
            DefaultSurface.Paints = 0;
            fixture.Popover.Show();
            var rejected = false;
            try
            {
                using var frame = fixture.Frame();
            }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected && DefaultSurface.Paints == 0, "Viewport-default paint extent checks the paired target budget before surface painting");
        }
        using (var fixture = new Fixture<BoundedSurface<WideExtent>>(viewport: new(10000, 10000)))
        {
            fixture.Popover.Show();
            using var frame = fixture.Frame();
            var content = frame.TextureUpdates.Single(x => x.Kind == UiTextureUpdateKind.RenderPaintGroup).PaintGroup!.Content;
            check(content.PixelWidth * (long)content.PixelHeight * 8 < 64 * 1024 * 1024,
                "An explicit compact extent remains within budget on a large viewport");
        }
        using (var fixture = new Fixture<BoundedSurface<NonFiniteExtent>>())
        {
            fixture.Popover.Options = fixture.Popover.Options with
            {
                MotionEnabled = false
            };
            fixture.Popover.Show();
            using var frame = fixture.Frame();
            check(frame.TextureUpdates.All(x => x.Kind != UiTextureUpdateKind.RenderPaintGroup)
                && fixture.Context.State.NextScheduledFrameTimeSeconds is null, "Static painting does not evaluate the motion paint-extent contract");
        }
    }

    private static void Invalid<T>(Action<bool, string> check, bool containment) where T : IExtent
    {
        using var fixture = new Fixture<BoundedSurface<T>>();
        DefaultSurface.Paints = 0;
        fixture.Popover.Show();
        var rejected = false;
        try
        {
            using var frame = fixture.Frame();
        }
        catch (ArgumentOutOfRangeException) when (!containment) { rejected = true; }
        catch (InvalidOperationException e) when (containment && e.Message == "Popover paint bounds must contain the visible surface.") { rejected = true; }
        check(rejected && DefaultSurface.Paints == 0, "Invalid paint extent is rejected before surface painting : " + typeof(T).Name);
    }

    private static UiRect Marker(UiRect body) => new(body.X - 112, body.Y + 8, 16, 16);

    private static UiAccessibilityElement Find(UiAccessibilityTree tree, string name, UiAccessibilityRole role)
    {
        UiAccessibilityElement? found = null;
        void Visit(string? id)
        {
            while (id is not null && tree.TryGet(id, out var element))
            {
                if (element.Node.Name == name && element.Node.Role == role)
                    found = element;
                Visit(element.FirstChild);
                id = element.Next;
            }
        }
        Visit(tree.Root.First);
        return found ?? throw new InvalidOperationException("Paint extent fixture missing : " + name);
    }

    private sealed class Fixture<T> : IDisposable where T : IUiPopoverPresentation
    {
        internal readonly UiContext Context = new(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        internal readonly UiAccessibilityTree Tree = new();
        internal readonly UiPopover<T> Popover;
        private readonly UiVector2 _origin;
        private readonly UiVector2 _viewport;
        internal Fixture(UiVector2? origin = null, UiVector2? viewport = null)
        {
            _origin = origin ?? new(280, 180);
            _viewport = viewport ?? new(640, 480);
            ControlFontFixture.Configure(Context);
            Context.SetAccessibilityTree(Tree);
            Popover = new("Bounds owner", Fluent.Button("Bounds child"), options: new(Width: 200, DisableAutoFocus: true,
                Positioning: new()
                {
                    Position = UiPopupPosition.BelowStart,
                    Pinned = true
                }));
        }
        internal UiDrawData Frame(float elapsed = 0)
        {
            Context.NewFrame(new(elapsed, _viewport, new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, _viewport.X, _viewport.Y), UiTextSettings.Default);
            Context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(_origin);
                Popover.Render(ui);
            }));
            return Context.GetDrawData();
        }
        public void Dispose()
        {
            Popover.Dispose();
            Context.Dispose();
            Tree.Dispose();
        }
    }

    private interface IExtent
    {
        static abstract UiRect Resolve(UiRect body);
    }
    private readonly struct WideExtent : IExtent
    {
        public static UiRect Resolve(UiRect body) => new(body.X - 128, body.Y - 88, body.Width + 256, body.Height + 176);
    }
    private readonly struct NonFiniteExtent : IExtent
    {
        public static UiRect Resolve(UiRect body) => new(float.NaN, 0, 100, 100);
    }
    private readonly struct EmptyExtent : IExtent
    {
        public static UiRect Resolve(UiRect body) => new(0, 0, 0, 100);
    }
    private readonly struct SmallExtent : IExtent
    {
        public static UiRect Resolve(UiRect body) => new(body.X, body.Y, 1, 1);
    }

    private readonly struct DefaultSurface : IUiPopoverPresentation
    {
        internal static int Paints;
        public static UiControlTypographyScope BeginTriggerText(UiControlContext context) => UiFluentPopoverPresentation.BeginTriggerText(context);
        public static UiControlTypographyScope BeginBodyText(UiControlContext context) => UiFluentPopoverPresentation.BeginBodyText(context);
        public static UiVector2 MeasureTrigger(UiControlContext context, string label) => UiFluentPopoverPresentation.MeasureTrigger(context, label);
        public static void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction)
            => UiFluentPopoverPresentation.PaintTrigger(context, label, bounds, interaction);
        public static float Padding(UiPopoverOptions options) => UiFluentPopoverPresentation.Padding(options);
        public static UiColor Foreground(UiControlContext context, UiPopoverOptions options) => UiFluentPopoverPresentation.Foreground(context, options);
        public static void PaintSurface(UiControlContext context, UiPopupSurface surface, UiPopupLayout layout, UiPopoverOptions options)
        {
            Paints++;
            context.Frame(Marker(layout.Bounds), new(200, 0, 0), default, 0, 0);
            UiFluentPopoverPresentation.PaintSurface(context, surface, layout, options);
        }
    }

    private readonly struct BoundedSurface<T> : IUiPopoverPresentation where T : IExtent
    {
        public static UiControlTypographyScope BeginTriggerText(UiControlContext context) => DefaultSurface.BeginTriggerText(context);
        public static UiControlTypographyScope BeginBodyText(UiControlContext context) => DefaultSurface.BeginBodyText(context);
        public static UiVector2 MeasureTrigger(UiControlContext context, string label) => DefaultSurface.MeasureTrigger(context, label);
        public static void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction) => DefaultSurface.PaintTrigger(context, label, bounds, interaction);
        public static float Padding(UiPopoverOptions options) => DefaultSurface.Padding(options);
        public static UiColor Foreground(UiControlContext context, UiPopoverOptions options) => DefaultSurface.Foreground(context, options);
        public static UiRect PaintBounds(UiControlContext context, UiPopupLayout layout, UiPopoverOptions options) => T.Resolve(layout.Bounds);
        public static void PaintSurface(UiControlContext context, UiPopupSurface surface, UiPopupLayout layout, UiPopoverOptions options) => DefaultSurface.PaintSurface(context, surface, layout, options);
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}

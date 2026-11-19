using Duxel.Core;

internal static class AllocationRoutingTests
{
    internal static readonly (string Name, Func<UiWidget> Create)[] Leaves =
    [
        ("button", () => Fluent.Button("Button")),
        ("checkbox", () => new UiToggle<bool?, UiNullableToggleValue, UiReleaseButtonBehavior, UiFluentCheckboxPresentation>("Check", new UiState<bool?>(false).Binding)),
        ("switch", () => Fluent.Switch("Switch", new UiState<bool>(false).Binding)),
        ("slider", () => Fluent.Slider("Slider", new UiState<float>(50).Binding)),
        ("input", () => Fluent.Input("Input", new UiState<string>("Value").Binding, 32, width : 220)),
        ("radio", () => new UiFluentRadioGroup("Radio", new UiState<string?>("a").Binding, new UiRadioOption[] { new("a", "A") }, width : 220)),
        ("spinner", () => Fluent.Spinner("Spin")),
        ("progress", () => Fluent.ProgressBar(.5f)),
        ("text", () => Fluent.Text("Text")),
        ("divider", () => Fluent.Divider("Divider", new(Length : 220))),
        ("link", () => Fluent.Link("Link", new("/details", UriKind.Relative), static _ => { })),
        ("badge", () => Fluent.Badge("Badge")),
        ("counter", () => Fluent.CounterBadge(108)),
        ("hidden-counter", () => Fluent.CounterBadge()),
        ("field", () => Fluent.Field(Fluent.Input("Field", new UiState<string>("Value").Binding, 32)))
    ];

    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> draw)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(default);
                draw(ui);
            }));
            using var data = context.GetDrawData();
        }
        foreach (var (name, create) in Leaves)
        foreach (var table in new[] { true, false })
        {
            var probe = new Probe(create());
            using var box = Dux.SizedBox(Dux.Padding(Dux.Padding(probe, new(3)), new(7)), 80, 64);
            Frame(ui =>
            {
                if (table)
                {
                    if (!ui.BeginTable("allocation-routing-" + name, 1))
                        throw new InvalidOperationException("Table fixture did not open.");
                    ui.TableSetupColumn("Column", 200, 0);
                    ui.TableNextRow();
                }
                else
                {
                    ui.Columns(2);
                    ui.NextColumn();
                }
                box.Render(ui);
                var origin = ui.GetItemRectMin();
                var expected = new UiRect(origin.X + 10, origin.Y + 10, 60, 44);
                check(probe.Bounds == expected,
                    $"nested padding routes {name} at the assigned child root; table = {table}, expected = {expected}, actual = {probe.Bounds}");
                if (table)
                    ui.EndTable();
                else
                    ui.Columns(1);
            });
        }
        using var bad = Dux.SizedBox(new ThrowingChild(), 80, 64);
        Frame(ui =>
        {
            if (!ui.BeginTable("routing-after-failure", 1))
                throw new InvalidOperationException("Table fixture did not open.");
            ui.TableSetupColumn("Column", 200, 0);
            ui.TableNextRow();
            var origin = ui.GetCursorScreenPos();
            var rejected = false;
            try
            {
                bad.Render(ui);
            }
            catch (ProbeException)
            {
                rejected = true;
            }
            ui.SetCursorScreenPos(new(250, 180));
            ui.Button("After failure", new(80, 32));
            check(rejected && ui.GetItemRectMin() == origin,
                "failed allocated rendering restores the caller's table routing and clip scope");
            ui.EndTable();
        });
    }

    internal static (UiVector2 Measured, UiRect Actual) InspectField()
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var probe = new Probe(Fluent.Field(Fluent.Input("Input", new UiState<string>("Value").Binding, 32)));
        using var box = Dux.SizedBox(probe, 80, 64);
        var measured = default(UiVector2);
        context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), idle, new(0, 0, 320, 240), UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(default);
            measured = box.MeasureConstrained(ui, new());
            box.Render(ui);
        }));
        using var data = context.GetDrawData();
        return (measured, probe.Bounds);
    }

    private sealed class ProbeException : Exception;
    private sealed class ThrowingChild : UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context) => context.ConstrainToLayout(new(20, 20));
        protected override void RenderControl(UiImmediateContext ui) => throw new ProbeException();
    }

    internal sealed class Probe : UiControlWidget
    {
        private readonly UiWidget _child;
        internal UiRect Bounds;
        internal Probe(UiWidget child)
        {
            _child = child;
            OwnChild("observed", child);
        }
        protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureConstrained(_child, context.LayoutConstraints);
        protected override void RenderControl(UiImmediateContext ui)
        {
            _child.Render(ui);
            var p = ui.GetItemRectMin();
            var s = ui.GetItemRectSize();
            Bounds = new(p.X, p.Y, s.X, s.Y);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}

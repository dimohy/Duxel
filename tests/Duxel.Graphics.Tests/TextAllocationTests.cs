using Duxel.Core;

internal static class TextAllocationTests
{
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

        var text = new ObservedText("Hi");
        using var box = Dux.SizedBox(text, 80, 64);
        Frame(ui =>
        {
            check(box.MeasureConstrained(ui, new()) == new UiVector2(80, 64),
                "SizedBox measures the assigned 80x64 text root");
            box.Render(ui);
        });
        check(text.Bounds == new UiRect(0, 0, 80, 64),
            $"text actual root honors its parent's allocation; actual = {text.Bounds}");

        text.Text = "Several words must wrap at the real parent width without changing their font.";
        box.Options = new(80, 120);
        Frame(ui =>
        {
            ui.PushTextWrapPos(240);
            box.Render(ui);
            ui.PopTextWrapPos();
        });
        check(text.Bounds == new UiRect(0, 0, 80, 120),
            "a long wrapped label keeps the actual root instead of reserving its natural multiline height");
        text.Text = string.Empty;
        Frame(box.Render);
        check(text.Bounds == new UiRect(0, 0, 80, 120), "empty text preserves its assigned root without fabricating glyphs");
        box.Options = new(0, 0);
        Frame(box.Render);
        check(text.Bounds == new UiRect(0, 0, 0, 0), "zero-sized text allocation remains a valid zero root");

        var reads = 0;
        var content = "Dynamic label";
        using var dynamic = Fluent.Text(() =>
        {
            reads++;
            return content;
        });
        Frame(ui =>
        {
            var cursor = ui.GetCursorScreenPos();
            dynamic.Measure(ui, new(160, 120));
            check(reads == 1 && ui.GetCursorScreenPos() == cursor,
                "text measurement preserves its existing one content-source read and caller cursor");
            dynamic.Render(ui);
        });
        check(reads == 2, "rendering reads the current text once rather than adding a new source-read phase");
        content = "Changed dynamic label";
        Frame(dynamic.Render);
        check(reads == 3, "a retained dynamic text still reads once each rendered frame after a source change");

        using var counted = Dux.SizedBox(new CountedTextControl(), 80, 64);
        CountedTextPresentation.Measurements = 0;
        Frame(counted.Render);
        check(CountedTextPresentation.Measurements == 0 && CountedTextControl.Last == new UiVector2(80, 64),
            "the allocated render path reserves the real root without invoking an extra presentation measurement");
        using var legacy = Dux.SizedBox(new CountedTextControl(false), 80, 64);
        Frame(legacy.Render);
        check(CountedTextControl.Last.Y == 20 && CountedTextControl.Last.X < 80,
            "the existing typed immediate Text overload retains its natural line box inside a parent");

        using var unsupported = Dux.SizedBox(new UiText<FixedTextPresentation>("Custom"), 80, 64);
        var rejected = false;
        Frame(ui =>
        {
            try
            {
                unsupported.MeasureConstrained(ui, new());
            }
            catch (NotSupportedException)
            {
                rejected = true;
            }
        });
        check(rejected, "a custom presentation must explicitly opt into changed text allocations during measurement");
        using var invalid = Dux.SizedBox(new UiText<DoubleTextPresentation>("Custom"), 80, 64);
        rejected = false;
        Frame(ui =>
        {
            try
            {
                invalid.Render(ui);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
        });
        check(rejected, "an opted-in presentation that places two common roots fails explicitly");
        text.Text = "Hi";
        box.Options = new(80, 64);
        Frame(ui =>
        {
            box.Render(ui);
            check(text.Bounds == new UiRect(0, 0, 80, 64),
                "the next text render recovers after an invalid presentation without leaking its allocation scope");
        });

        Frame(ui =>
        {
            if (!ui.BeginTable("allocated-text-table", 1))
                throw new InvalidOperationException("Table fixture did not open.");
            ui.TableSetupColumn("Column", 200, .5f);
            ui.TableNextRow();
            box.Render(ui);
            var origin = ui.GetItemRectMin();
            var size = ui.GetItemRectSize();
            check(text.Bounds == new UiRect(origin.X, origin.Y, size.X, size.Y),
                "an allocated text root uses the parent's table bounds without applying legacy table alignment twice");
            ui.EndTable();
        });
        Frame(ui =>
        {
            ui.Columns(2);
            ui.NextColumn();
            box.Render(ui);
            var origin = ui.GetItemRectMin();
            var size = ui.GetItemRectSize();
            check(text.Bounds == new UiRect(origin.X, origin.Y, size.X, size.Y),
                "an allocated text root preserves its parent column coordinates");
            ui.Columns(1);
        });

        using var generated = Dux.SizedBox(new GeneratedAllocatedText("User"), 80, 64);
        CustomAllocatedTextPresentation.Last = default;
        Frame(generated.Render);
        check(CustomAllocatedTextPresentation.Last == new UiRect(0, 0, 80, 64),
            "a generated user text presentation shares the single-root allocation and clipping contract");
        TextWidgetTests.Run(check);
    }

    private sealed class ObservedText(string text) : UiFluentText(text)
    {
        internal UiRect Bounds;
        protected override void RenderControl(UiImmediateContext ui)
        {
            base.RenderControl(ui);
            var position = ui.GetItemRectMin();
            var size = ui.GetItemRectSize();
            Bounds = new(position.X, position.Y, size.X, size.Y);
        }
    }

    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }

    private sealed class CountedTextControl(bool constrain = true) : UiControlWidget
    {
        internal static UiVector2 Last;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.ConstrainToLayout(new(80, 64));
        protected override void RenderControl(UiImmediateContext ui)
        {
            ui.Text<CountedTextPresentation>("Hi", null, constrain);
            Last = ui.GetItemRectSize();
        }
    }
}

internal readonly struct CountedTextPresentation : IUiTextPresentation
{
    internal static int Measurements;
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, string text)
    {
        Measurements++;
        return UiFluentTextPresentation.Measure(context, text);
    }
    public static void Render(UiControlContext context, string text, UiColor? color) => UiFluentTextPresentation.Render(context, text, color);
}
internal readonly struct FixedTextPresentation : IUiTextPresentation
{
    public static UiVector2 Measure(UiControlContext context, string text) => new(24, 12);
    public static void Render(UiControlContext context, string text, UiColor? color) => context.PlaceText(new(24, 12));
}
internal readonly struct DoubleTextPresentation : IUiTextPresentation
{
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, string text) => new(24, 12);
    public static void Render(UiControlContext context, string text, UiColor? color)
    {
        context.PlaceText(new(24, 12));
        context.PlaceText(new(24, 12));
    }
}
internal readonly struct CustomAllocatedTextPresentation : IUiTextPresentation
{
    internal static UiRect Last;
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, string text) => new(24, 12);
    public static void Render(UiControlContext context, string text, UiColor? color)
    {
        var layout = context.PlaceText(new(24, 12));
        Last = new(layout.Position.X, layout.Position.Y, layout.Size.X, layout.Size.Y);
        context.Frame(Last, context.Theme.Button, context.Theme.Border, 0, 1);
    }
}
[UiWidgetStyle(typeof(UiText<>), typeof(CustomAllocatedTextPresentation))]
internal partial class GeneratedAllocatedText;

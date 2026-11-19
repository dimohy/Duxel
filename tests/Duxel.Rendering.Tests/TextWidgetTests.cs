using Duxel.Core;
using Duxel.Platform.Windows;

internal static class TextWidgetTests
{
    internal static void Run(Action<bool, string> check)
    {
        var glyphs = Enumerable.Range(32, 95).ToDictionary(c => c, c => new UiGlyphInfo(7, 0, -10, c == 32 ? 0 : 6, 12, new(0, 0, 1, 1)));
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255], glyphs, new Dictionary<uint, float>(), 12, -3, 1, -1), new(1), new(2));
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        context.SetPlatformTextBackend(WindowsPlatformTextBackend.Instance);
        context.SetDirectTextEnabled(true);
        context.SetDirectTextPrimaryFontPath(Path.Combine(fonts, "segoeui.ttf"));
        context.SetDirectTextBaseFontSize(16);
        context.SetTypographyFonts(new(Path.Combine(fonts, "segoeui.ttf"), Path.Combine(fonts, "seguisb.ttf"), Path.Combine(fonts, "segoeuib.ttf")));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        UiDrawData Frame(Action<UiImmediateContext> action)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(action));
            return context.GetDrawData();
        }
        static string Geometry(UiDrawData data) => string.Join(";", data.DrawLists.Select(list =>
            $"{list.GetStaticGeometryStamp()}:{list.Commands.Count}"));
        static bool Equivalent(UiDrawData expected, UiDrawData actual)
        {
            if (expected.DrawLists.Count != actual.DrawLists.Count)
                return false;
            for (var i = 0; i < expected.DrawLists.Count; i++)
            {
                var a = expected.DrawLists[i];
                var b = actual.DrawLists[i];
                if (!a.Vertices.AsSpan().SequenceEqual(b.Vertices.AsSpan())
                    || !a.Indices.AsSpan().SequenceEqual(b.Indices.AsSpan())
                    || !a.Commands.AsSpan().SequenceEqual(b.Commands.AsSpan()))
                    return false;
                var ar = a.RectFilledPrimitives is { } aRects ? aRects.AsSpan() : ReadOnlySpan<UiRectFilledPrimitive>.Empty;
                var br = b.RectFilledPrimitives is { } bRects ? bRects.AsSpan() : ReadOnlySpan<UiRectFilledPrimitive>.Empty;
                var ac = a.CircleFilledPrimitives is { } aCircles ? aCircles.AsSpan() : ReadOnlySpan<UiCircleFilledPrimitive>.Empty;
                var bc = b.CircleFilledPrimitives is { } bCircles ? bCircles.AsSpan() : ReadOnlySpan<UiCircleFilledPrimitive>.Empty;
                if (!ar.SequenceEqual(br) || !ac.SequenceEqual(bc))
                    return false;
            }
            return true;
        }
        void Equal(Action<UiImmediateContext> before, IUiView after, string name)
        {
            using var expected = Frame(before);
            using var actual = Frame(after.Render);
            check(Equivalent(expected, actual), name);
        }
        check(Dux.Text("Text") is UiFluentText && Dux.TextWrapped("Wrapped") is UiFluentWrappedText
            && Dux.TextColored(new(0xFF123456), "Color") is UiFluentText,
            "existing text factories use generated inheritable widgets instead of render closures");
        using var text = Fluent.Text("Content ## stays literal");
        Equal(ui => ui.Text<UiFluentTypographyPresentation<UiRegularTypeface, UiFluentType300>>("Content ## stays literal"), text,
            "generated Text uses the same real14px font and20px line box as Body1");
        using var color = Fluent.Text("Colored", new(0xFF123456));
        Equal(ui => ui.Text<UiFluentTextPresentation>("Colored", new(0xFF123456)), color, "compiled text preserves an explicit color");
        using var wrapped = new UiFluentWrappedText("Several words on multiple lines");
        var wrappedSize = default(UiVector2);
        using (var data = Frame(ui => { ui.PushTextWrapPos(80); wrapped.Render(ui); wrappedSize = ui.GetItemRectSize(); ui.PopTextWrapPos(); }))
        {
        }
        check(wrappedSize.X <= 80 && wrappedSize.Y > 20 && wrappedSize.Y % 20 == 0,
            "wrapped Fluent Text measures at14px and advances in20px lines inside the requested width");
        var sameText = false;
        using (var data = Frame(ui => { var value = "Unchanged short text"; sameText = ReferenceEquals(value, new UiControlContext(ui).WrapText(value)); }))
        {
        }
        check(sameText, "fitting single-line wrapping preserves the input string without constructing a replacement");
        using var multiline = Fluent.Text("First\nSeveral words on another line");
        var multilineSize = default(UiVector2);
        using (var data = Frame(ui => { ui.PushTextWrapPos(80); multiline.Render(ui); multilineSize = ui.GetItemRectSize(); ui.PopTextWrapPos(); }))
        {
        }
        check(multilineSize.X <= 80 && multilineSize.Y > 40 && multilineSize.Y % 20 == 0,
            "explicit newlines reset wrap width before subsequent text is measured");
        using var defaultWrap = Fluent.Text(wrapped.Text);
        using (var expected = Frame(ui => { ui.PushTextWrapPos(80); wrapped.Render(ui); ui.PopTextWrapPos(); }))
        using (var actual = Frame(ui => { ui.PushTextWrapPos(80); defaultWrap.Render(ui); ui.PopTextWrapPos(); }))
            check(Equivalent(expected, actual), "default Fluent Text enables wrapping as the official component does");
        using (var expected = Frame(ui => { ui.PushTextWrapPos(80); ui.Text<UiFluentWrappedTextPresentation>(wrapped.Text); ui.PopTextWrapPos(); }))
        using (var actual = Frame(ui => { ui.PushTextWrapPos(80); wrapped.Render(ui); ui.PopTextWrapPos(); }))
            check(Equivalent(expected, actual), "compiled wrapping preserves the current wrap-position scope");
        using (var expected = Frame(ui => { ui.PushClipRect(new(0, 0, 28, 30), true); ui.Text<UiFluentTextPresentation>(text.Text); ui.PopClipRect(); }))
        using (var actual = Frame(ui => { ui.PushClipRect(new(0, 0, 28, 30), true); text.Render(ui); ui.PopClipRect(); }))
            check(Equivalent(expected, actual), "compiled text preserves clip and texture command state as well as geometry");
        void Table(UiImmediateContext ui, bool compiled)
        {
            ui.BeginTable("text-table", 1);
            ui.TableSetupColumn("Column", 160, .5f);
            ui.TableNextRow();
            if (compiled)
                text.Render(ui);
            else
                ui.Text<UiFluentTextPresentation>(text.Text);
            ui.EndTable();
        }
        using (var expected = Frame(ui => Table(ui, false)))
        using (var actual = Frame(ui => Table(ui, true)))
            check(Equivalent(expected, actual), "compiled text preserves table alignment and column clipping");
        var reads = 0;
        var current = "First";
        using var dynamic = Fluent.Text(() => { reads++; return current; });
        check(dynamic is UiText<Func<string>, UiGetterTextSource, UiFluentTextPresentation>,
            "dynamic text source selection is fixed by a closed generic type rather than a per-frame source-kind branch");
        using var first = Frame(dynamic.Render);
        var firstGeometry = Geometry(first);
        current = "Second longer";
        using var second = Frame(dynamic.Render);
        check(reads == 2 && firstGeometry != Geometry(second), "a retained text source executes once every rendered frame without requiring Build invalidation");
        using var inherited = new SuffixText("Extended");
        Equal(ui => ui.Text<UiFluentTextPresentation>("Extended!"), inherited, "user inheritance replaces content sourcing while preserving the compiled presentation");
        using var decorated = new UiText<BoxPresentation>("Decorated");
        using (var data = Frame(decorated.Render))
            check(data.DrawLists.Any(list => list.RectFilledPrimitives?.Count > 0),
                "a different compiled text presentation controls measurement, placement and background composition");
        var expectedCursor = default(UiVector2);
        var actualCursor = default(UiVector2);
        using (var a = Frame(ui => { ui.Text<UiFluentTextPresentation>(text.Text); expectedCursor = ui.GetCursorScreenPos(); }))
        {
        }
        using (var b = Frame(ui => { text.Render(ui); actualCursor = ui.GetCursorScreenPos(); }))
        {
        }
        check(expectedCursor == actualCursor, "compiled plain text preserves cursor advancement");
        text.Text = "Changed";
        Equal(ui => ui.Text<UiFluentTextPresentation>("Changed"), text, "mutable retained content invalidates the real text request");
        var surrounding = default(UiVector2);
        using (var data = Frame(ui => { ui.PushFontSize(28); text.Render(ui); surrounding = new(ui.GetFontSize(), ui.GetItemRectSize().Y); ui.PopFontSize(); }))
        {
        }
        check(surrounding == new UiVector2(28, 20), "default Fluent Text uses its compiled line box and restores the surrounding font scope");
        using var unsupported = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        unsupported.NewFrame(new(1f / 60, new(100, 100), new(1, 1)), idle, new(0, 0, 100, 100), UiTextSettings.Default);
        var rejected = false;
        try
        {
            unsupported.Render(new Screen(text.Render));
        }
        catch (NotSupportedException) { rejected = true; }
        check(rejected, "default Fluent Text rejects a backend without font metrics instead of substituting atlas glyphs");
        var oldAllocation = Allocation(static () => LegacyText("Static"));
        var newAllocation = Allocation(static () => Dux.Text("Static"));
        check(newAllocation <= oldAllocation, $"compiled text factory does not increase construction allocation: old={oldAllocation},new={newAllocation},count=1024");
        using var mutable = Fluent.Text("Before");
        var requests = 0;
        var published = new List<(string, UiColor?)>();
        context.SetRequestFrameCallback(() => { requests++; published.Add((mutable.Source, mutable.Color)); });
        using (var data = Frame(mutable.Render))
        {
        }
        var requestStart = requests;
        mutable.Text = "After";
        mutable.Text = "After";
        var changedColor = new UiColor(0xff123456);
        mutable.Color = changedColor;
        mutable.Color = changedColor;
        check(requests - requestStart == 2 && published.TakeLast(2).SequenceEqual(new[] { ("After", (UiColor?)null), ("After", (UiColor?)changedColor) }),
            "text and color publish before waking an idle host exactly once per real change");
        Equal(ui => ui.Text<UiFluentTextPresentation>("After", changedColor), mutable,
            "a woken retained Text renders current content and color through the original presentation");
        requestStart = requests;
        mutable.Source = null!;
        mutable.Source = null!;
        mutable.Text = "";
        check(mutable.Source == "" && requests - requestStart == 1,
            "null and empty static text normalize before equality without redundant frame requests");
        mutable.Text = "Stable";
        mutable.Color = changedColor;
        AssignUnchanged(mutable, changedColor);
        requestStart = requests;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        AssignUnchanged(mutable, changedColor);
        var unchangedAllocation = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        check(unchangedAllocation == 0 && requests == requestStart,
            $"ten thousand unchanged text/color assignments allocate zero bytes and leave the idle host asleep : allocated {unchangedAllocation}, requests {requests - requestStart}");
        using var preset = Fluent.Body1("Preset");
        using (var data = Frame(preset.Render))
        {
        }
        requestStart = requests;
        preset.Source = "Updated preset";
        check(requests - requestStart == 1, "generated typography presets inherit the common text invalidation path");
        using (var data = Frame(inherited.Render))
        {
        }
        requestStart = requests;
        inherited.Text = "Updated subclass";
        check(requests - requestStart == 1, "user-derived text inherits the same idle-host notification path");
        Equal(ui => ui.Text<UiFluentTextPresentation>("Updated subclass!"), inherited,
            "source notification preserves the user content override and compiled painting");
        using var getter = Fluent.Text(static () => "Old getter");
        using (var data = Frame(getter.Render))
        {
        }
        var getterReads = 0;
        Func<string> replacement = () => { getterReads++; return "New getter"; };
        requestStart = requests;
        getter.Source = replacement;
        getter.Source = replacement;
        check(requests - requestStart == 1 && getterReads == 0,
            "replacing a text getter wakes once without evaluating runtime content in a setter");
        using (var data = Frame(getter.Render))
        {
        }
        check(getterReads == 1, "a replacement getter executes exactly once on the next rendered frame");
        var nullRejected = false;
        requestStart = requests;
        try
        {
            getter.Source = null!;
        }
        catch (ArgumentNullException) { nullRejected = true; }
        check(nullRejected && ReferenceEquals(getter.Source, replacement) && requests == requestStart,
            "an invalid getter leaves existing ownership and frame scheduling unchanged");
        getter.Dispose();
        mutable.Dispose();
        check(getter.Source is null && mutable.Source is null, "disposing retained text releases getter closures and owned content");
        static bool Disposed(Action action)
        {
            try
            {
                action();
                return false;
            }
            catch (ObjectDisposedException) { return true; }
        }
        requestStart = requests;
        check(Disposed(() => getter.Source = replacement) && Disposed(() => getter.Source = null!)
            && Disposed(() => mutable.Text = "After disposal") && Disposed(() => mutable.Color = changedColor)
            && requests == requestStart,
            "disposed text rejects new content/color ownership before normalization and emits no frame request");
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static IUiView LegacyText(string text) => new UiView(ui => ui.Text(text));
    private static long Allocation(Func<IUiView> factory)
    {
        for (var i = 0; i < 64; i++)
            GC.KeepAlive(factory());
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1024; i++)
            GC.KeepAlive(factory());
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private sealed class SuffixText(string text) : UiFluentText(text)
    {
        protected override string ReadText() => base.ReadText() + "!";
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void AssignUnchanged(UiText<UiFluentTextPresentation> widget, UiColor color)
    {
        for (var index = 0; index < 10000; index++)
        {
            widget.Text = "Stable";
            widget.Color = color;
        }
    }

    private readonly struct BoxPresentation : IUiTextPresentation
    {
        public static void Render(UiControlContext context, string text, UiColor? color)
        {
            var size = context.MeasureText(text);
            var layout = context.PlaceText(new(size.X + 12, size.Y + 8));
            if (!layout.Visible)
                return;
            context.Frame(new(layout.Position.X, layout.Position.Y, layout.Size.X, layout.Size.Y), new(0xFF225588), new(0xFFFFFFFF), 6, 1);
            context.PaintText(text, layout with
            {
                Position = new(layout.Position.X + 6, layout.Position.Y + 4)
            }, color ?? context.Theme.Text);
        }
    }
}

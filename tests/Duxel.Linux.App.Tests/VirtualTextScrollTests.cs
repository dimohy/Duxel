using System.Globalization;
using Duxel.Core;
using Duxel.Platform.Linux;

internal static class VirtualTextScrollTests
{
    internal const int BaseIndex = 123_456_789;
    internal const float Extent = 256.25f;
    internal const double InitialOffset = BaseIndex * (double)Extent + 17.375;

    internal static void Run(string primary, string secondary, Action<bool, string> check, bool variableExtents = false)
    {
        foreach (var mode in new[] { "single", "multiline", "markdown" })
        {
            using var tree = new UiAccessibilityTree();
            using var wake = new ManualResetEventSlim();
            using var backend = new LinuxPlatformTextBackend();
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            context.SetPlatformTextBackend(backend);
            context.SetDirectTextEnabled(true);
            context.SetDirectTextPrimaryFontPath(primary);
            context.SetDirectTextSecondaryFontPath(secondary);
            context.SetTypographyFonts(new(primary, primary, primary, secondary, secondary, secondary) { Monospace = primary });
            context.SetAccessibilityTree(tree);
            tree.SetRequestFrameCallback(wake.Set);
            var source = new Source(mode, variableExtents);
            var initialOffset = variableExtents ? source.GetBoundary(BaseIndex) + 17.375 : InitialOffset;
            using var list = new UiVirtualList<Source>(source, 1_000_000_000,
                new(Extent, 240, 100) { ExtentIndex = variableExtents ? source : null }) { Offset = initialOffset };
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            var screen = new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(10, 10));
                list.Render(ui);
            });
            const int rowIndex = BaseIndex + 1;
            var name = Name(mode, rowIndex);
            UiAccessibilityElement Frame()
            {
                context.NewFrame(new(.1f, new(400, 300), new(1, 1)), idle, new(0, 0, 400, 300), UiTextSettings.Default);
                context.Render(screen);
                context.GetDrawData().ReleasePooled();
                return tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == name);
            }
            UiAccessibilityResult Apply(UiAccessibilityElement node, UiAccessibilityTextScroll scroll, Action? beforeFrame = null)
            {
                wake.Reset();
                var task = Task.Run(() => tree.RequestTextScroll(node.Node.Id, node.RuntimeId, scroll));
                if (!wake.Wait(1000))
                    throw new InvalidOperationException(task.IsCompleted ? "Virtual admission: " + task.Result : "Virtual request did not wake its frame.");
                beforeFrame?.Invoke();
                for (var frame = 0; frame < 4 && !task.IsCompleted; frame++)
                {
                    Frame();
                    task.Wait(10);
                }
                if (!task.Wait(1000))
                    throw new TimeoutException("Virtual source-range publication did not complete within its frame budget.");
                return task.Result;
            }
            var node = Frame();
            var original = node.Node.TextInfo!;
            var runtime = node.RuntimeId;
            var target = mode == "single" ? 30 : 8 * 63 + 20;
            if (variableExtents)
                check(source.ExtentAt(BaseIndex) == 256.25f && source.ExtentAt(rowIndex) == 384.75f
                    && source.ExtentAt(rowIndex + 1) == 192.5f && ReferenceEquals(list.Options.ExtentIndex, source)
                    && source.GetBoundary(1_000_000_000) > int.MaxValue,
                    "The indexed model supplies three distinct fractional row heights without fixed-extent substitution, " + mode);
            check(node.Node.Offscreen && node.Node.CanScrollIntoView && list.Offset == initialOffset && list.Offset > int.MaxValue,
                "A realized overscan source starts offscreen at an exact double-precision billion-row viewport, " + mode);
            var viewport = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Virtual list").Node.Scroll!.Value.Viewport;
            check(node.Node.Bounds.Width <= viewport.X && node.Node.Bounds.Height <= source.ExtentAt(rowIndex),
                "The realized text producer obeys its real scrollbar-reduced virtual row allocation, " + mode);
            foreach (var placement in Enum.GetValues<UiAccessibilityTextScrollPlacement>())
            {
                list.Offset = initialOffset;
                node = Frame();
                var content = node.Node.TextInfo!.Content;
                check(Apply(node, new(content, content.GetUtf16Offset(target), content.GetUtf16Offset(target + 1), placement)) == UiAccessibilityResult.Success,
                    "Virtualized ancestor accepts the committed source placement, " + mode + "/" + placement);
                node = Frame();
                var cell = node.Node.TextLayout!.GetCharacterExtents(target);
                check(node.RuntimeId == runtime && !node.Node.Offscreen && cell.Y >= 9.95f && cell.Y + cell.Height <= 110.05f
                    && node.Node.TextLayout.GetOffsetAtPoint(cell.X + cell.Width / 2, cell.Y + cell.Height / 2) == target
                    && list.Offset > int.MaxValue,
                    "Virtual source publication preserves the realized identity and visible scalar geometry beyond float offset capacity, " + mode + "/" + placement);
            }
            node = Frame();
            var current = node.Node.TextInfo!.Content;
            var point = new UiVector2(60, 60);
            check(Apply(node, new(current, current.GetUtf16Offset(target), current.GetUtf16Offset(target + 1), point)) == UiAccessibilityResult.Success,
                "A vertical-only virtual ancestor accepts a source point with a locally scrollable horizontal axis, " + mode);
            node = Frame();
            var positioned = node.Node.TextLayout!.GetCharacterExtents(target);
            check(MathF.Abs(positioned.X - point.X) < .05f && MathF.Abs(positioned.Y - point.Y) < .05f,
                $"Virtual point composes local horizontal and ancestor vertical scrolling, {mode}; actual={positioned}; offset={list.Offset:R}");
            var saved = list.Offset;
            check(Apply(node, new(current, current.GetUtf16Offset(target), current.GetUtf16Offset(target + 1))) == UiAccessibilityResult.Success
                && list.Offset == saved, "An already visible virtual source range preserves its exact double offset, " + mode);
            node = Frame();
            check(node.Node.TextInfo!.Content.Text == original.Content.Text && node.Node.TextInfo.CaretUtf16 == original.CaretUtf16
                && node.Node.TextInfo.AnchorUtf16 == original.AnchorUtf16 && !node.Node.Focused && source.Keys < 1000 && source.Created < 100
                && list.RealizedCount <= 4,
                $"Virtual source scrolling preserves text/focus/selection and bounded realization rather than enumerating the model, {mode}; keys={source.Keys}; created={source.Created}; live={list.RealizedCount}; bounds={source.Boundaries}");
            current = node.Node.TextInfo.Content;
            var endResult = Apply(node, new(current, current.Text.Length, current.Text.Length));
            node = Frame();
            check(endResult == UiAccessibilityResult.Success,
                $"Virtual EOF range completes, {mode}; result={endResult}; box={node.Node.TextLayout!.GetRangeExtents(current.CharacterCount, current.CharacterCount)}; clip={node.Node.TextLayout.ClipBounds}; scroll={node.Node.Scroll}; offset={list.Offset:R}");
            if (variableExtents)
            {
                var previousBounds = node.Node.Bounds;
                var previousOffset = list.Offset;
                source.PrecedingGrowth = 64.5;
                list.Refresh();
                node = Frame();
                check(node.RuntimeId == runtime && node.Node.Bounds == previousBounds && list.Offset == previousOffset + 64.5
                    && ReferenceEquals(node.Node.TextInfo!.Content, current),
                    "A preceding row height change preserves the keyed source anchor and its exact fractional viewport position, " + mode);
                check(Apply(node, new(current, current.GetUtf16Offset(target), current.GetUtf16Offset(target + 1), point), () =>
                {
                    source.PrecedingGrowth += 32.25;
                    list.Refresh();
                }) == UiAccessibilityResult.Success,
                    "An admitted source-range request resolves against updated variable row boundaries, " + mode);
                node = Frame();
                var moved = node.Node.TextLayout!.GetCharacterExtents(target);
                check(node.RuntimeId == runtime && MathF.Abs(moved.X - point.X) < .05f && MathF.Abs(moved.Y - point.Y) < .05f
                    && node.Node.TextLayout.GetOffsetAtPoint(moved.X + moved.Width / 2, moved.Y + moved.Height / 2) == target
                    && source.Boundaries < 100_000 && source.Keys < 1000 && source.Created < 100 && list.RealizedCount <= 4,
                    "Variable-height source publication keeps exact point geometry and logarithmic bounded realization after reflow, " + mode);
            }
            list.Dispose();
            check(source.Live.Count == 0, "Disposing the virtual owner releases all realized text input owners, " + mode);
        }
    }

    internal static string Name(string mode, int index) => "Virtual " + mode + " " + index.ToString(CultureInfo.InvariantCulture);

    internal sealed class Source(string mode, bool variableExtents = false) : IUiVirtualListSource, IUiVirtualListExtentIndex
    {
        internal readonly Dictionary<int, Row> Live = [];
        internal int Keys, Created, Boundaries;
        internal double PrecedingGrowth;
        internal float ExtentAt(int index) => variableExtents ? (float)(GetBoundary(index + 1) - GetBoundary(index)) : Extent;
        public double GetBoundary(int index)
        {
            Boundaries++;
            return (index / 3) * 833.5 + ((index % 3) switch { 0 => 0, 1 => 256.25, _ => 641.0 })
                + (index > BaseIndex ? PrecedingGrowth : 0);
        }
        public int FindIndexByKey(string key, int itemCount) =>
            int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < itemCount ? index : -1;
        public string GetKey(int index) { Keys++; return index.ToString(CultureInfo.InvariantCulture); }
        public UiWidget Create(int index)
        {
            var row = new Row(this, mode, index);
            Live.Add(index, row);
            Created++;
            return row;
        }
        public void Bind(UiWidget widget, int index)
        {
            if (!ReferenceEquals(Live[index], widget))
                throw new InvalidOperationException("A virtual source was rebound across model identities.");
        }
    }

    internal sealed class Row : UiControlWidget
    {
        private readonly Source _source;
        private readonly string _mode, _name;
        private readonly int _index;
        private readonly UiTextInputOwner _singleOwner = new();
        private readonly UiMultilineInputOwner _multilineOwner = new();
        private readonly UiMarkdownEditor? _markdown;
        private string _value;
        internal Row(Source source, string mode, int index)
        {
            (_source, _mode, _index, _name) = (source, mode, index, Name(mode, index));
            _value = mode == "single" ? "정😀" + new string('W', 80)
                : string.Join('\n', Enumerable.Repeat("정😀" + new string('W', 60), 15));
            if (mode == "markdown")
            {
                _markdown = new(_name, new(() => _value, value => _value = value), new(Width: 240, Height: source.ExtentAt(index), MaxLength: 4096, Wrap: false));
                OwnChild("editor", _markdown);
            }
        }
        protected override UiVector2 MeasureContent(UiControlContext context) => throw new InvalidOperationException("Fixed virtual rows do not measure source items.");
        protected override void RenderControl(UiImmediateContext ui)
        {
            if (_mode == "single")
                ui.InputText<UiStandardTextInputBehavior, UiFluentInputPresentation<UiFluentSmallInputSize, UiFluentOutlineInput>>(
                    _name, ref _value, 4096, new UiInputOptions(), 240, constrainLayout: true, owner: _singleOwner);
            else if (_mode == "multiline")
                ui.InputTextMultiline<UiStandardMultilineInputBehavior, UiFluentTextareaPresentation<UiFluentSmallTextareaSize, UiFluentOutlineInput>>(
                    _name, ref _value, 4096, new() { Wrap = false }, 240, _source.ExtentAt(_index), _multilineOwner, constrainLayout: true);
            else
                _markdown!.Render(ui);
        }
        protected override void OnDispose()
        {
            _singleOwner.Dispose();
            _multilineOwner.Dispose();
            _source.Live.Remove(_index);
        }
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}

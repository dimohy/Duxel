using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static unsafe class SliderRangeTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var native = new WindowsAccessibility(0);
        var tree = native.Tree;
        context.SetAccessibilityTree(tree);
        ControlFontFixture.Configure(context);
        var value = 35f;
        var writes = 0;
        using var slider = Fluent.Slider("Numeric slider", new UiBinding<float>(() => value, next => { value = next; writes++; }), 0, 100, 5);
        slider.Size = new(200, 32);
        var disabled = false;
        var hidden = false;
        var offscreen = false;
        var changes = new List<UiAccessibilityRangeChange>();
        var committed = true;
        tree.RangeChanged += change =>
        {
            committed &= tree.TryGet(change.Element.Node.Id, out var published) && published == change.Element;
            changes.Add(change);
        };
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame()
        {
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)), idle, new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                if (hidden) return;
                ui.SetCursorScreenPos(new(8, offscreen ? 800 : 8));
                ui.BeginDisabled(disabled);
                slider.Render(ui);
                ui.EndDisabled();
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find()
        {
            if (tree.Root.First is { } id && tree.TryGet(id, out var entry)) return entry;
            throw new InvalidOperationException("Numeric Slider was not published.");
        }
        UiAccessibilityResult Request(double amount) { var entry = Find(); return tree.RequestRangeValue(entry.Node.Id, entry.RuntimeId, amount, wait : false); }
        Frame();
        var initial = Find();
        check(initial.Node.Name == "Numeric slider" && initial.Node.Role == UiAccessibilityRole.Slider && initial.Node.Range == new UiAccessibilityRangeInfo(35, 0, 100, 5, 10) &&
            !initial.Node.SupportsInvoke && !initial.Node.ValuePattern,
            "Slider publishes numeric range semantics without pretending to be an Invoke button or text editor");
        var provider = native.Find(initial.Node.Id)!;
        provider.GetPatternProvider(10003, out var pattern);
        try
        {
            provider.GetPropertyValue(30003, out var controlType);
            provider.GetPropertyValue(30033, out var available);
            check(pattern != 0 && controlType.Data.Integer == 50015 && available.Data.Boolean != 0 &&
                provider.GetRangeValue(out var current) == 0 && current == 35 && provider.GetRangeMaximum(out var maximum) == 0 && maximum == 100 &&
                provider.GetRangeMinimum(out var minimum) == 0 && minimum == 0 && provider.GetRangeSmallChange(out var small) == 0 && small == 5 &&
                provider.GetRangeLargeChange(out var large) == 0 && large == 10 && provider.GetRangeReadOnly(out var readOnly) == 0 && readOnly == 0,
                "native Slider provider exposes the actual RangeValue pattern, role and all six getters");
        }
        finally { if (pattern != 0) Marshal.Release(pattern); }
        var revision = tree.Revision;
        Frame();
        check(tree.Revision == revision && changes.Count == 0, "unchanged Slider frames publish no range changes or tree revision");
        check(Request(60) == UiAccessibilityResult.Success, "typed numeric accessibility request is queued on the existing UI wake path");
        Frame();
        check(value == 60 && writes == 1 && changes.Count == 1 && changes[0].Previous?.Value == 35 && changes[0].Element.Node.Range?.Value == 60 && committed,
            "numeric accessibility action commits the real binding once and notifies only after final tree publication");
        Request(60); Frame();
        check(writes == 1 && changes.Count == 1, "setting the current numeric value is idempotent without callback or property event");
        Request(63); Frame();
        check(value == 65 && writes == 2, "numeric accessibility mutation shares the actual Slider step snapping and callback path");
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1, 101 })
            check(Request(invalid) == UiAccessibilityResult.InvalidArgument, "numeric accessibility rejects invalid value before queueing : " + invalid);
        check(tree.RequestAction(initial.Node.Id, initial.RuntimeId, UiAccessibilityAction.SetValue, "50", wait : false) == UiAccessibilityResult.InvalidArgument,
            "numeric Slider rejects the unrelated string Value pattern");
        Request(80); disabled = true; Frame();
        check(value == 65 && writes == 2 && Request(80) == UiAccessibilityResult.Disabled,
            "numeric action revalidates live disabled state and disabled sliders reject new mutations");
        disabled = false; Frame(); Request(80); offscreen = true; Frame();
        check(value == 65 && writes == 2 && Request(80) == UiAccessibilityResult.Unavailable,
            "numeric action revalidates clipping and rejects offscreen mutations");
        offscreen = false; Frame(); Request(90); slider.Options = new(0, 70, 5); Frame();
        check(value == 65 && writes == 2 && Find().Node.Range?.Maximum == 70,
            "queued numeric mutation cannot bypass a range narrowed before the render frame");
        Request(30); Request(40); Frame();
        check(value == 30 && writes == 3, "ordered numeric requests apply at most one mutation per control per frame");
        Frame(); check(value == 40 && writes == 4, "the next numeric request remains pending until the following frame");
        slider.Options = new(0, 100, 5, Vertical : true, Reversed : true); Frame(); Request(70); Frame();
        provider.GetPropertyValue(30023, out var orientation);
        check(value == 70 && Find().Node.Range is { Minimum : 0, Maximum : 100, Vertical : true } && orientation.Data.Integer == 2,
            "vertical/reversed Slider exposes logical numeric bounds and explicit vertical orientation");
        var retired = Find();
        provider.GetPatternProvider(10003, out var retainedNativePattern);
        var simplePointer = UiaNative.Pointer<IUiaSimple>(provider);
        var unknownId = new Guid("00000000-0000-0000-c000-000000000046");
        nint rangeIdentity = 0, simpleIdentity = 0;
        try
        {
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(retainedNativePattern, in unknownId, out rangeIdentity));
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(simplePointer, in unknownId, out simpleIdentity));
            check(rangeIdentity == simpleIdentity, "native RangeValue and Simple providers share the same canonical COM identity for disconnect");
            hidden = true; Frame();
            double nativeValue = -1;
            var nativeResult = ((delegate* unmanaged[Stdcall]<nint, double*, int>)(*(nint**)retainedNativePattern)[4])(
                retainedNativePattern, &nativeValue);
            Console.WriteLine($"ServerRangeGetter : HRESULT = 0x{nativeResult:X8}, value = {nativeValue}");
            check(nativeResult == UiaNative.Unavailable,
                "the actual generated native RangeValue vtable propagates removal errors rather than returning a successful zero");
            var identityResult = provider.GetRuntimeId(out var runtimeArray);
            try
            {
                check(provider.GetProviderOptions(out var retiredOptions) == 0 && retiredOptions == 34 &&
                    identityResult == 0 && runtimeArray != 0,
                    "retired providers retain immutable COM configuration and runtime identity needed for native disconnection");
            }
            finally { if (runtimeArray != 0) Marshal.ThrowExceptionForHR(UiaNative.SafeArrayDestroy(runtimeArray)); }
            var fragmentResult = provider.GetFragmentRoot(out var fragmentRoot);
            var expectedRoot = UiaNative.Pointer<IUiaFragmentRoot>(native.Root);
            try
            {
                check(fragmentResult == 0 && fragmentRoot == expectedRoot &&
                    provider.GetHostProvider(out var childHost) == 0 && childHost == 0,
                    "retired controls retain the original fragment root and null child HWND host association for native disconnection");
            }
            finally
            {
                if (fragmentRoot != 0) Marshal.Release(fragmentRoot);
                Marshal.Release(expectedRoot);
            }
        }
        finally
        {
            if (rangeIdentity != 0) Marshal.Release(rangeIdentity);
            if (simpleIdentity != 0) Marshal.Release(simpleIdentity);
            Marshal.Release(simplePointer);
            Marshal.Release(retainedNativePattern);
        }
        check(provider.GetRangeValue(out _) == UiaNative.Unavailable &&
            tree.RequestRangeValue(retired.Node.Id, retired.RuntimeId, 20, wait : false) == UiAccessibilityResult.Unavailable,
            "removed Slider rejects retained native getters and stale numeric mutations");
        hidden = false; Frame();
        check(Find().RuntimeId != retired.RuntimeId && provider.GetRangeValue(out _) == UiaNative.Unavailable,
            "reintroduced Slider receives a fresh runtime identity without retargeting stale native providers");
        using var readonlyHost = new WindowsAccessibility(0);
        var readonlyTree = readonlyHost.Tree;
        void Publish(UiAccessibilityRangeInfo? range, bool readOnly)
        {
            readonlyTree.BeginFrame();
            readonlyTree.Add(new("range", string.Empty, UiAccessibilityRole.Slider, "Read-only range", new(0, 0, 100, 30), true, false, false,
                ReadOnly : readOnly, ValuePattern : false) { Range = range });
            readonlyTree.EndFrame();
        }
        Publish(new(25, 0, 100, 1, 10), true);
        readonlyTree.TryGet("range", out var readonlyEntry);
        var readonlyProvider = readonlyHost.Find("range")!;
        check(readonlyProvider.GetRangeReadOnly(out var isReadonly) == 0 && isReadonly == 1 &&
            readonlyTree.RequestRangeValue("range", readonlyEntry.RuntimeId, 50, wait : false) == UiAccessibilityResult.ReadOnly,
            "read-only numeric semantics remain distinct from disabled state");
        Publish(null, false);
        readonlyProvider.GetPatternProvider(10003, out var removedPattern);
        check(removedPattern == 0 && readonlyProvider.GetRangeValue(out _) == UiaNative.InvalidOperation,
            "range capability withdrawal invalidates retained pattern getters while preserving the live element identity");
        readonlyHost.Dispose();
        check(readonlyHost.Root.GetProviderOptions(out _) == UiaNative.Unavailable &&
            readonlyHost.Root.GetRuntimeId(out var rootRuntime) == UiaNative.Unavailable && rootRuntime == 0 &&
            readonlyHost.Root.GetHostProvider(out var rootHost) == UiaNative.Unavailable && rootHost == 0 &&
            readonlyHost.Root.GetFragmentRoot(out var rootFragment) == UiaNative.Unavailable && rootFragment == 0,
            "HWND root metadata expires with the disposed host while retired child identity remains independent of tree membership");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    { public override void Render(UiImmediateContext ui) => render(ui); }
}

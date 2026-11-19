using Duxel.Core;
using Duxel.Platform.Windows;
using System.Runtime.InteropServices;

internal static unsafe class ToggleAccessibilityTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var native = new WindowsAccessibility(0);
        var tree = native.Tree;
        context.SetAccessibilityTree(tree);
        ControlFontFixture.Configure(context);
        bool? value = null;
        var writes = 0;
        using var checkbox = Fluent.Checkbox("Mixed checkbox", new UiBinding<bool?>(() => value, next => { value = next; writes++; }));
        using var toggle = Fluent.ToggleButton("Toggle button", new UiState<bool>(false));
        var switchState = new UiState<bool>(true);
        using var control = Fluent.Switch("Switch", switchState);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var disabled = false;
        var clipped = false;
        var changes = new List<UiAccessibilityToggleChange>();
        var committed = true;
        tree.ToggleChanged += change =>
        {
            committed &= tree.TryGet(change.Element.Node.Id, out var current) && current == change.Element;
            changes.Add(change);
        };
        var screen = new Screen(ui =>
        {
            ui.SetCursorScreenPos(new(8, clipped ? 800 : 8));
            ui.BeginDisabled(disabled); checkbox.Render(ui); ui.EndDisabled();
            ui.SetCursorScreenPos(new(8, 60)); toggle.Render(ui);
            ui.SetCursorScreenPos(new(8, 110)); control.Render(ui);
        });
        void Frame()
        {
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)), idle, new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name)
        {
            for (var id = tree.Root.First; id is not null;)
            {
                if (!tree.TryGet(id, out var item)) throw new InvalidOperationException("Missing toggle node.");
                if (item.Node.Name == name) return item;
                id = item.Next;
            }
            throw new InvalidOperationException("Toggle not found : " + name);
        }
        UiAccessibilityResult Request()
        {
            var item = Find("Mixed checkbox");
            return tree.RequestAction(item.Node.Id, item.RuntimeId, UiAccessibilityAction.Toggle, wait : false);
        }
        Frame();
        check(tree.Count == 3, "actual generated Checkbox, Switch and ToggleButton publish three semantic controls");
        var mixed = Find("Mixed checkbox");
        check(mixed.Node.Role == UiAccessibilityRole.Checkbox && mixed.Node.ToggleState == UiAccessibilityToggleState.Indeterminate &&
            mixed.Node.Checked is null && !mixed.Node.ValuePattern && !mixed.Node.SupportsInvoke && mixed.Node.Focusable,
            "mixed Checkbox is a supported third toggle state rather than an unsupported capability or Invoke button");
        check(Find("Switch").Node is { Role : UiAccessibilityRole.Switch, ToggleState : UiAccessibilityToggleState.On } &&
            Find("Toggle button").Node is { Role : UiAccessibilityRole.Button, ToggleState : UiAccessibilityToggleState.Off, SupportsInvoke : false },
            "Switch and ToggleButton preserve distinct logical roles while sharing stateful Toggle semantics");
        var provider = native.Find(mixed.Node.Id)!;
        provider.GetPatternProvider(10015, out var pattern);
        if (pattern == 0) throw new InvalidOperationException("Mixed Checkbox must expose the actual native Toggle pattern.");
        try
        {
            int nativeState = -1;
            var hr = ((delegate* unmanaged[Stdcall]<nint, int*, int>)(*(nint**)pattern)[4])(pattern, &nativeState);
            provider.GetPropertyValue(30003, out var role);
            provider.GetPropertyValue(30041, out var available);
            check(hr == 0 && nativeState == 2 && role.Data.Integer == 50002 && available.Data.Boolean != 0,
                "actual generated COM Toggle getter exposes Indeterminate2 and Checkbox control type50002");
        }
        finally { if (pattern != 0) Marshal.Release(pattern); }
        var revision = tree.Revision;
        Frame();
        check(tree.Revision == revision && changes.Count == 0 && writes == 0, "unchanged toggle frames emit no state event, callback or tree revision");
        switchState.Value = false;
        for (var frame = 0; frame < 3; frame++)
            Frame();
        check(context.State.NextScheduledFrameTimeSeconds.HasValue
            && Find("Switch").Node.ToggleState == UiAccessibilityToggleState.Off,
            "Switch paints the finite travel while publishing the new semantic state immediately");
        switchState.Value = true;
        Frame();
        check(context.State.NextScheduledFrameTimeSeconds.HasValue, "Switch retargets while already in motion");
        for (var frame = 0; frame < 12; frame++)
            Frame();
        check(context.State.NextScheduledFrameTimeSeconds is null, "Switch releases event-wait scheduling after settling");
        changes.Clear();
        check(Request() == UiAccessibilityResult.Success, "mixed Checkbox accepts the existing typed Toggle queue action");
        Frame();
        check(value == true && writes == 1 && changes.Count == 1 && committed &&
            changes[0].PreviousState == UiAccessibilityToggleState.Indeterminate && changes[0].Element.Node.ToggleState == UiAccessibilityToggleState.On,
            "mixed-to-on action follows the real value policy once and reports final immutable state");
        Request(); Frame();
        check(value == false && writes == 2 && changes[^1].PreviousState == UiAccessibilityToggleState.On &&
            changes[^1].Element.Node.ToggleState == UiAccessibilityToggleState.Off,
            "on-to-off action uses the same caller binding and existing activation cycle");
        value = null; Frame();
        check(provider.GetToggleState(out var restored) == 0 && restored == 2 && changes[^1].PreviousState == UiAccessibilityToggleState.Off,
            "caller-supplied mixed state remains queryable without a fabricated activation callback");
        var beforeWrites = writes;
        Request(); disabled = true; Frame();
        check(value is null && writes == beforeWrites && Request() == UiAccessibilityResult.Disabled,
            "toggle action revalidates live disabled state and rejects new disabled requests");
        disabled = false; Frame(); Request(); clipped = true; Frame();
        check(value is null && writes == beforeWrites && Find("Mixed checkbox").Node.Offscreen,
            "toggle action rejects a control clipped after queue acceptance");
        clipped = false; Frame(); Request(); Request(); Frame();
        check(value == true && writes == beforeWrites + 1, "only one ordered toggle request is consumed per control per frame");
        Frame(); check(value == false && writes == beforeWrites + 2, "the next ordered toggle request remains pending for the following frame");
        mixed = Find("Mixed checkbox");
        check(tree.RequestAction(mixed.Node.Id, mixed.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.InvalidArgument &&
            tree.RequestAction(mixed.Node.Id, mixed.RuntimeId, UiAccessibilityAction.SetValue, "true", wait : false) == UiAccessibilityResult.InvalidArgument,
            "stateful controls reject unrelated Invoke and string Value actions");
        checkbox.AccessibleName = "Explicit checkbox name"; Frame();
        check(Find("Explicit checkbox name").Node.ToggleState == UiAccessibilityToggleState.Off,
            "toggle publication preserves caller-provided accessibility naming without changing state");

        using var lifecycle = new WindowsAccessibility(0);
        var lifecycleTree = lifecycle.Tree;
        var lifecycleChanges = new List<UiAccessibilityToggleChange>();
        lifecycleTree.ToggleChanged += lifecycleChanges.Add;
        var node = new UiAccessibilityNode("retained", string.Empty, UiAccessibilityRole.Checkbox, "Retained", new(0, 0, 100, 24), true, false, false,
            ValuePattern : false) { Checked = true };
        check(node.ToggleState == UiAccessibilityToggleState.On && (node with { ToggleState = UiAccessibilityToggleState.Indeterminate }).Checked is null &&
            (node with { ToggleState = UiAccessibilityToggleState.Indeterminate, Checked = false }).ToggleState == UiAccessibilityToggleState.Off,
            "legacy Checked and three-state ToggleState are projections of one canonical value");
        void Publish(UiAccessibilityNode current)
        {
            lifecycleTree.BeginFrame(); lifecycleTree.Add(current); lifecycleTree.EndFrame();
        }
        Publish(node);
        var retained = lifecycle.Find("retained")!;
        retained.GetPatternProvider(10015, out var retainedPattern);
        if (retainedPattern == 0) throw new InvalidOperationException("Retained control must expose Toggle before withdrawal.");
        try
        {
            Publish(node with { ToggleState = UiAccessibilityToggleState.Indeterminate });
            Publish(node with { ToggleState = null });
            retained.GetPatternProvider(10015, out var withdrawn);
            check(withdrawn == 0 && retained.GetToggleState(out _) == UiaNative.InvalidOperation &&
                lifecycleChanges[^1].PreviousState == UiAccessibilityToggleState.Indeterminate && lifecycleChanges[^1].Element.Node.ToggleState is null,
                "withdrawn Toggle capability invalidates a held state getter without inventing Off");
            lifecycleTree.BeginFrame(); lifecycleTree.EndFrame();
            int stale = -1;
            var hr = ((delegate* unmanaged[Stdcall]<nint, int*, int>)(*(nint**)retainedPattern)[4])(retainedPattern, &stale);
            check(hr == UiaNative.Unavailable, "actual retained native Toggle getter rejects a removed control with0x80040201");
        }
        finally { if (retainedPattern != 0) Marshal.Release(retainedPattern); }
        var rejected = false;
        lifecycleTree.BeginFrame();
        try { lifecycleTree.Add(node with { ToggleState = (UiAccessibilityToggleState)3 }); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        finally { lifecycleTree.AbortFrame(); }
        check(rejected && lifecycleTree.Count == 0, "invalid toggle enum values fail before publication and preserve the previous tree snapshot");
        var legacyChange = new UiAccessibilityToggleChange(mixed, false);
        var (element, previous) = legacyChange;
        check(element == mixed && previous == false && legacyChange.PreviousState == UiAccessibilityToggleState.Off &&
            (legacyChange with { PreviousState = UiAccessibilityToggleState.Indeterminate }).Previous is null,
            "existing two-state change construction/deconstruction remains compatible with typed previous state");
        CountingPolicy.Calls = 0;
        var countedValue = false;
        context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)), idle, new(0, 0, 640, 400), UiTextSettings.Default);
        context.Render(new Screen(ui => ui.Toggle<bool, CountingPolicy, UiSpaceButtonBehavior, UiFluentCheckboxPresentation>("Count once", ref countedValue)));
        using var countedDraw = context.GetDrawData();
        check(CountingPolicy.Calls == 1, "semantic publication and painting share one custom value-policy evaluation per frame");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }

    private readonly struct CountingPolicy : IUiToggleValue<bool>
    {
        internal static int Calls;
        public static bool? Checked(bool value) { Calls++; return value; }
        public static bool Activate(bool value) => !value;
    }
}

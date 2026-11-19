using Duxel.Core;
using Duxel.Platform.Windows;
using System.Runtime.InteropServices;

internal static class SpinButtonTests
{
    internal static void Run(Action<bool, string> check)
    {
        var focus = new UiState();
        var transitions = new List<(string? Previous, string? Current)>();
        focus.ControlFocusChanged += (previous, current) => transitions.Add((previous, current));
        focus.FocusedId = "editor";
        transitions.Clear();
        focus.ClearControlFocusForPointer();
        check(transitions.Count == 0 && !focus.IsControlFocusLeaving("editor"),
            "Pointer dispatch does not publish its provisional empty focus as an editor blur");
        focus.FocusedId = "editor";
        check(transitions.Count == 0, "Pointer reentry into the same logical editor emits no focus transition");
        focus.ClearControlFocusForPointer();
        focus.FocusedId = "command";
        check(transitions.SequenceEqual(new[] { ((string?)"editor", (string?)"command") }),
            "Pointer focus reports the actual destination synchronously once");
        focus.FocusedId = "editor";
        transitions.Clear();
        focus.ClearControlFocusForPointer();
        focus.CompleteControlPointerFocus();
        check(transitions.SequenceEqual(new[] { ((string?)"editor", (string?)null) }),
            "Pointer focus without a destination publishes one completed blur");
        focus.FocusedId = "editor";
        transitions.Clear();
        focus.ClearControlFocusForPointer();
        focus.CancelControlPointerFocus();
        check(focus.FocusedId == "editor" && transitions.Count == 0,
            "Aborted pointer dispatch restores its provisional origin without a false blur");
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var native = new WindowsAccessibility(0);
        var tree = native.Tree;
        context.SetAccessibilityTree(tree);
        var number = new UiState<double?>(1);
        using var spin = Fluent.SpinButton("Amount", number.Binding, new(Minimum: 0, Maximum: 10, PageStep: 5));
        using var label = Fluent.Label("Amount label", spin);
        var changes = new List<UiSpinButtonChange>();
        spin.Changed = changes.Add;
        double? valueSeenByBefore = null;
        using var before = Fluent.Button("Before number", () => valueSeenByBefore = number.Value);
        using var after = Fluent.Button("After number");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        UiImmediateContext? live = null;
        var abortPointer = false;
        void Frame(UiInputState? input = null, float delta = 1f / 60)
        {
            context.NewFrame(new(delta, new(480, 320), new(1, 1)), input ?? idle, new(0, 0, 480, 320), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                live = ui;
                if (abortPointer)
                    throw new PointerFrameAborted();
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(20, 10));
                before.Render(ui);
                ui.SetCursorScreenPos(new(20, 40));
                label.Render(ui);
                ui.SetCursorScreenPos(new(20, 60));
                spin.Render(ui);
                ui.SetCursorScreenPos(new(20, 140));
                after.Render(ui);
            }));
            using var draw = context.GetDrawData();
        }
        List<UiAccessibilityElement> Nodes()
        {
            var nodes = new List<UiAccessibilityElement>();
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var element))
                {
                    nodes.Add(element);
                    Visit(element.FirstChild);
                    id = element.Next;
                }
            }
            Visit(tree.Root.First);
            return nodes;
        }
        UiAccessibilityElement Root() => Nodes().Single(e => e.Node.Role == UiAccessibilityRole.SpinButton);
        UiAccessibilityElement Find(string name) => Nodes().Single(e => e.Node.Name == name);
        void Request(UiAccessibilityElement element, UiAccessibilityAction action, string? value = null)
        {
            check(tree.RequestAction(element.Node.Id, element.RuntimeId, action, value, wait: false) == UiAccessibilityResult.Success,
                "SpinButton queues its actual native action : " + action);
            Frame();
        }
        void Key(UiKey key)
        {
            Frame(idle with
            {
                KeyEvents = [new(key, true, default)]
            });
            Frame(idle with
            {
                KeyEvents = [new(key, false, default)]
            });
        }
        Frame();
        check(Root().Node is { Value: "1", Range.Value: 1, MaximumLength: 4096, SupportsValue: true },
            "SpinButton publishes nullable numeric Value and RangeValue on its one logical editor");
        check(Root().Node.LabeledBy == Nodes().Single(e => e.Node.Role == UiAccessibilityRole.Label).Node.Id,
            "SpinButton generated factory participates in the common standalone label association");
        check(Nodes().Count(e => e.Node.Focusable) == 3 && !Find("Increment value").Node.Focusable && !Find("Decrement value").Node.Focusable,
            "SpinButton steppers are independently invokable without adding keyboard Tab stops");
        var provider = native.Find(Root().Node.Id)!;
        provider.GetPropertyValue(30003, out var type);
        check(type.Data.Integer == 50016, "SpinButton uses native Spinner control type50016");
        check(provider.GetPatternProvider(10002, out var valuePattern) == 0 && valuePattern != 0,
            "SpinButton exposes the native Value pattern");
        if (valuePattern != 0)
            Marshal.Release(valuePattern);
        Request(Root(), UiAccessibilityAction.SetValue, "5");
        check(number.Value == 5 && Root().Node.Value == "5" && Root().Node.Range?.Value == 5,
            "SpinButton native SetValue commits text and numeric snapshots together");
        Request(Root(), UiAccessibilityAction.Focus);
        Key(UiKey.UpArrow);
        check(number.Value == 6, "SpinButton Up changes its real bound model");
        Key(UiKey.PageDown);
        check(number.Value == 1, "SpinButton PageDown uses its separately configured page step");
        Key(UiKey.Home);
        check(number.Value == 0 && !Find("Decrement value").Node.Enabled, "SpinButton Home reaches the minimum and disables decrement");
        Key(UiKey.End);
        check(number.Value == 10 && !Find("Increment value").Node.Enabled, "SpinButton End reaches the maximum and disables increment");
        Key(UiKey.DownArrow);
        check(number.Value == 9, "SpinButton Down steps back inside its range");
        Key(UiKey.Tab);
        check(Find("After number").Node.Focused, "SpinButton Tab skips both stepper buttons");
        number.Value = 2;
        Frame();
        var up = Find("Increment value").Node.Bounds;
        var point = new UiVector2(up.X + up.Width / 2, up.Y + up.Height / 2);
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        check(number.Value == 3 && spin.Input.RepeatAt is not null, "SpinButton pointer press performs one step and schedules its owned initial repeat");
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseDown = true
        }, .31f);
        check(number.Value == 4, "SpinButton actual held pointer repeats after the bounded delay");
        Frame(idle with
        {
            MousePosition = new(400, 280),
            LeftMouseDown = true
        });
        check(spin.Input.RepeatAt is null, "SpinButton pointer leave cancels its held-repeat deadline");
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseDown = true
        }, .5f);
        check(number.Value == 4 && spin.Input.RepeatAt is null, "SpinButton reentry during the same hold does not restart cancelled repetition");
        Frame(idle with
        {
            LeftMouseReleased = true
        });
        Request(Find("Increment value"), UiAccessibilityAction.Invoke);
        check(number.Value == 5, "SpinButton actual stepper Invoke uses the same numeric commit path");
        spin.Options = spin.Options with
        {
            ReadOnly = true
        };
        Frame();
        check(Root().Node.ReadOnly && !Find("Increment value").Node.Enabled && !Find("Decrement value").Node.Enabled,
            "SpinButton read-only state keeps the editor readable and disables both stepper actions");
        spin.Options = spin.Options with
        {
            ReadOnly = false,
            RightToLeft = true
        };
        Frame();
        check(Find("Increment value").Node.Bounds.X == Root().Node.Bounds.X, "SpinButton RTL mirrors the real stepper hit regions");
        Request(Root(), UiAccessibilityAction.SetValue, "");
        check(number.Value is null && Root().Node.Value == "" && Root().Node.Range is null,
            "SpinButton empty Value withdraws the numeric range instead of inventing zero");
        Request(Find("Increment value"), UiAccessibilityAction.Invoke);
        check(number.Value == 1, "SpinButton empty increment starts from the configured origin");
        void Type(char digit)
        {
            Request(Root(), UiAccessibilityAction.Focus);
            Frame(idle with
            {
                KeyEvents = [new(UiKey.A, true, KeyModifiers.Ctrl)],
                Modifiers = KeyModifiers.Ctrl
            });
            check(context.State.GetSelection(Root().Node.Id).HasSelection, $"SpinButton Ctrl+A selects the draft: {context.State.GetSelection(Root().Node.Id)}");
            Frame(idle with
            {
                KeyEvents = [new(UiKey.A, false, default)],
                CharEvents = [new(digit)]
            });
        }
        Type('7');
        check(number.Value == 1 && spin.DisplayText == "7", $"SpinButton typed draft is separate from its committed numeric value : {number.Value} / {spin.DisplayText} / focus={context.State.FocusedId} / active={context.State.ActiveId} / root={Root().Node.Id}");
        Key(UiKey.Enter);
        check(number.Value == 7 && Root().Node.Range?.Value == 7, $"SpinButton Enter commits the actual typed draft : {number.Value} / {spin.DisplayText}");
        Type('3');
        Key(UiKey.Escape);
        check(number.Value == 7 && spin.DisplayText == "7", "SpinButton Escape cancels a draft without replacing its bound number");
        Type('4');
        var editingBounds = Root().Node.Bounds;
        var editingPoint = new UiVector2(editingBounds.X + editingBounds.Width / 2, editingBounds.Y + editingBounds.Height / 2);
        Frame(idle with
        {
            MousePosition = editingPoint,
            LeftMousePressed = true,
            LeftMouseDown = true
        });
        Frame(idle with
        {
            MousePosition = editingPoint,
            LeftMouseReleased = true
        });
        check(number.Value == 7 && spin.DisplayText == "4",
            "SpinButton pointer caret reposition inside the same editor keeps its draft uncommitted");
        abortPointer = true;
        var aborted = false;
        try
        {
            Frame(idle with
            {
                MousePosition = editingPoint,
                LeftMousePressed = true,
                LeftMouseDown = true
            });
        }
        catch (PointerFrameAborted) { aborted = true; }
        abortPointer = false;
        Frame();
        check(aborted && number.Value == 7 && spin.DisplayText == "4" && Root().Node.Focused,
            "SpinButton aborted pointer frame preserves the draft and logical editor focus");
        var destination = Find("Before number");
        tree.RequestAction(destination.Node.Id, destination.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        tree.RequestAction(destination.Node.Id, destination.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        Frame();
        check(number.Value == 4 && valueSeenByBefore == 4 && Root().Node.Value == "4" && Root().Node.Range?.Value == 4,
            "SpinButton blur commits before an earlier-rendered destination command and publishes one coherent numeric snapshot");
        Type('6');
        var commandBounds = Find("Before number").Node.Bounds;
        var commandPoint = new UiVector2(commandBounds.X + commandBounds.Width / 2, commandBounds.Y + commandBounds.Height / 2);
        Frame(idle with
        {
            MousePosition = commandPoint,
            LeftMousePressed = true,
            LeftMouseDown = true
        });
        Frame(idle with
        {
            MousePosition = commandPoint,
            LeftMouseReleased = true
        });
        check(number.Value == 6 && valueSeenByBefore == 6,
            "SpinButton pointer focus commits before an earlier-rendered command runs on release");
        Type('5');
        var previousChanges = changes.Count;
        Frame(idle with
        {
            MousePosition = new(400, 280),
            LeftMousePressed = true,
            LeftMouseDown = true
        });
        Frame(idle with
        {
            MousePosition = new(400, 280),
            LeftMouseReleased = true
        });
        check(number.Value == 5 && changes.Count == previousChanges + 1 && !Root().Node.Focused,
            "SpinButton clicking empty space commits its draft once and clears logical focus");
        Type('8');
        abortPointer = true;
        try
        {
            Frame(idle with
            {
                MousePosition = editingPoint,
                LeftMousePressed = true,
                LeftMouseDown = true
            });
        }
        catch (PointerFrameAborted) { }
        abortPointer = false;
        Frame(idle with
        {
            MousePosition = new(400, 280),
            LeftMousePressed = true,
            LeftMouseReleased = true
        });
        check(number.Value == 8 && !Root().Node.Focused,
            "SpinButton the next pointer click after an aborted frame still releases restored owned focus");
        Type('2');
        previousChanges = changes.Count;
        var stepBounds = Find("Increment value").Node.Bounds;
        var stepPoint = new UiVector2(stepBounds.X + stepBounds.Width / 2, stepBounds.Y + stepBounds.Height / 2);
        Frame(idle with
        {
            MousePosition = stepPoint,
            LeftMousePressed = true,
            LeftMouseDown = true
        });
        Frame(idle with
        {
            MousePosition = stepPoint,
            LeftMouseReleased = true
        });
        check(number.Value == 3 && changes.Count == previousChanges + 1,
            "SpinButton stepping an edited draft emits one numeric commit without an intermediate blur commit");
        Type('-');
        previousChanges = changes.Count;
        Frame(idle with
        {
            MousePosition = editingPoint,
            LeftMousePressed = true,
            LeftMouseDown = true
        });
        Frame(idle with
        {
            MousePosition = editingPoint,
            LeftMouseReleased = true
        });
        check(number.Value == 3 && spin.DisplayText == "-" && changes.Count == previousChanges && Root().Node.Invalid,
            "SpinButton incomplete numeric text survives caret clicks until explicit commit or cancel");
        Key(UiKey.Escape);
        number.Value = 4.25;
        Frame();
        check(spin.DisplayText == "4.25" && Root().Node.Range?.Value == 4.25,
            "SpinButton external fractional updates keep display and numeric accessibility coherent");
        spin.Formatter = value => value is { } n ? $"{n.ToString(System.Globalization.CultureInfo.InvariantCulture)} kg" : "";
        spin.Parser = text => text.EndsWith(" kg", StringComparison.Ordinal)
            && double.TryParse(text.AsSpan(0, text.Length - 3), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? new(true, parsed) : new(false, null);
        Frame();
        check(Root().Node.Value == "4.25 kg", "SpinButton formatter changes only the display representation");
        Request(Root(), UiAccessibilityAction.SetValue, "6 kg");
        check(number.Value == 6 && Root().Node.Value == "6 kg" && Root().Node.Range?.Value == 6,
            "SpinButton custom parser is shared by native text commits");
        Request(Root(), UiAccessibilityAction.SetValue, "99 kg");
        check(number.Value == 10 && Root().Node.Value == "10 kg",
            "SpinButton custom parser commits obey the declared numeric bounds");
        var rejected = changes.Count;
        Request(Root(), UiAccessibilityAction.SetValue, "broken");
        check(number.Value == 10 && changes.Count == rejected + 1 && !changes[^1].Accepted,
            "SpinButton rejected custom text preserves its model and emits an explicit rejected change");
        spin.Parser = null;
        spin.Formatter = null;
        spin.Changed = change => { changes.Add(change); if (change.Value == 3) number.Value = 8; };
        Request(Root(), UiAccessibilityAction.SetValue, "3");
        check(number.Value == 8 && spin.DisplayText == "8" && Root().Node.Range?.Value == 8,
            "SpinButton reads back model changes made by its change callback before publishing");
        check(live!.TextFocusOwnerCount == 1, "SpinButton owns one weak editor focus observer");
        spin.Dispose();
        check(live.TextFocusOwnerCount == 0, "SpinButton disposal releases its editor focus observer");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private sealed class PointerFrameAborted : Exception;
}

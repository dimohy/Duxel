using Duxel.Core;

internal static class DialogTests
{
    internal static void Run(Action<bool, string> check)
    {
        SurfaceClosePolicyTests.Run(check);
        DialogAccessibilityContractTests.Run(check);
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var name = new UiState<string>("");
        var content = new UiState<string>("Short content");
        var opened = new UiState<bool>(false);
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        var changes = new List<bool>();
        var saves = 0;
        var outside = 0;
        var input = Fluent.Input("Name", name.Binding, 100, 220);
        var save = Fluent.Button("Save", () => saves++);
        var cancel = Fluent.Button("Cancel");
        using var dialog = Fluent.Dialog("Profile", Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
            input, Fluent.Body1(() => content.Value)), [save, cancel], new(Description: "Profile description", MotionEnabled: false));
        dialog.Open = opened.Binding;
        dialog.OpenChanged = changes.Add;
        using var trigger = Fluent.Button("Open", dialog.Show);
        using var other = Fluent.Button("Outside", () => outside++);
        UiWidget current = dialog;
        var visible = true;
        var triggerVisible = true;
        var triggerEnabled = true;
        var triggerAfterDialog = false;
        var measureClosed = true;
        var display = new UiVector2(640, 480);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? state = null)
        {
            context.NewFrame(new(1f / 60, display, new(1, 1)), state ?? idle, new(0, 0, display.X, display.Y), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                if (measureClosed)
                {
                    check(dialog.Measure(ui, new(600, 400)) == default,
                        "Dialog portal has zero natural flow size and pure closed measurement");
                    measureClosed = false;
                }
                if (triggerVisible && !triggerAfterDialog)
                {
                    ui.BeginDisabled(!triggerEnabled);
                    trigger.Render(ui);
                    ui.EndDisabled();
                }
                if (visible)
                    current.Render(ui);
                other.Render(ui);
                if (triggerVisible && triggerAfterDialog)
                {
                    ui.BeginDisabled(!triggerEnabled);
                    trigger.Render(ui);
                    ui.EndDisabled();
                }
            }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement Find(string text, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(x => x.Node.Name == text && x.Node.Role == role);
        bool HasDialog() => tree.CaptureSnapshot().Elements.Any(x => x.Node.Role is UiAccessibilityRole.Dialog or UiAccessibilityRole.AlertDialog);
        UiVector2 Center(UiAccessibilityElement item) => new(item.Node.Bounds.X + item.Node.Bounds.Width / 2, item.Node.Bounds.Y + item.Node.Bounds.Height / 2);
        void Click(UiVector2 point)
        {
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseReleased = true
            });
            Frame();
        }
        void Key(UiKey key, KeyModifiers modifiers = default)
        {
            Frame(idle with
            {
                KeyEvents = [new(key, true, modifiers)]
            });
            Frame(idle with
            {
                KeyEvents = [new(key, false, modifiers)]
            });
        }
        void Open()
        {
            Click(Center(Find("Open", UiAccessibilityRole.Button)));
            Frame();
        }
        Frame();
        var closedAfterY = Find("Outside", UiAccessibilityRole.Button).Node.Bounds.Y;
        check(!HasDialog() && !opened.Value && !dialog.IsOpen, "Dialog is absent until explicitly opened");
        Open();
        var surface = Find("Profile", UiAccessibilityRole.Dialog);
        check(dialog.IsOpen && opened.Value && changes.SequenceEqual([true]), "Dialog factory opens through a real trigger and controlled binding once");
        check(surface.Node.Bounds.Width == 600 && surface.Node.Bounds.X == 20
            && MathF.Abs(surface.Node.Bounds.Y * 2 + surface.Node.Bounds.Height - 480) < .01f,
            "Dialog600px natural surface is centered in the display");
        check(surface.Node.Modal && surface.Node.GetHelpText().Contains("Profile description", StringComparison.Ordinal),
            "Modal Dialog publishes its name, description and modal state");
        check(Find("Outside", UiAccessibilityRole.Button).Node.Bounds.Y == closedAfterY,
            "Open Dialog does not consume normal flow height");
        check(!Find("Outside", UiAccessibilityRole.Button).Node.Enabled && Find("Name", UiAccessibilityRole.Edit).Node.Focused,
            "Modal Dialog disables background access and focuses its first editable child");
        Frame(idle with
        {
            CharEvents = [new('X')]
        });
        check(name.Value == "X", "Dialog routes editing to retained content");
        Key(UiKey.Tab);
        check(Find("Save", UiAccessibilityRole.Button).Node.Focused, "Tab enters Dialog actions after content");
        Key(UiKey.Tab);
        check(Find("Cancel", UiAccessibilityRole.Button).Node.Focused, "Tab follows Dialog action order");
        Key(UiKey.Tab);
        check(Find("Name", UiAccessibilityRole.Edit).Node.Focused, "Modal Tab wraps inside the Dialog");
        Key(UiKey.Tab, KeyModifiers.Shift);
        check(Find("Cancel", UiAccessibilityRole.Button).Node.Focused, "Modal Shift+Tab wraps to the last Dialog action");
        var oldRequests = requests;
        Frame();
        Frame();
        check(requests == oldRequests, "Settled Dialog does not continuously request frames");
        Click(new(5, 5));
        check(!dialog.IsOpen && !opened.Value && !HasDialog() && outside == 0,
            "Modal backdrop press/release dismisses without activating background content");
        check(Find("Open", UiAccessibilityRole.Button).Node.Focused, "Dismissed Dialog restores its opener");
        Open();
        check(name.Value == "X", "Reopened Dialog retains its content model");
        Key(UiKey.Escape);
        check(!dialog.IsOpen && changes.Count(x => !x) == 2, "Focused Escape closes Dialog once");

        dialog.Options = dialog.Options with
        {
            Type = UiDialogType.NonModal
        };
        Open();
        check(!Find("Profile", UiAccessibilityRole.Dialog).Node.Modal && Find("Outside", UiAccessibilityRole.Button).Node.Enabled,
            "Non-modal Dialog permits background interaction");
        check(Find("Close", UiAccessibilityRole.Button).Node.Focused, "Non-modal title exposes and initially focuses its default close action");
        Click(Center(Find("Outside", UiAccessibilityRole.Button)));
        check(outside == 1 && dialog.IsOpen, "Non-modal outside click does not dismiss the surface");
        Key(UiKey.Escape);
        check(dialog.IsOpen, "Escape outside a non-modal Dialog does not close it");
        dialog.Options = dialog.Options with
        {
            Type = UiDialogType.Modal
        };
        Frame();
        Frame();
        check(Find("Name", UiAccessibilityRole.Edit).Node.Focused && !Find("Outside", UiAccessibilityRole.Button).Node.Enabled,
            "Changing an open non-modal Dialog to modal acquires focus and background isolation");
        dialog.Close();
        Frame();
        Frame();
        dialog.Options = dialog.Options with
        {
            Type = UiDialogType.Alert
        };
        Open();
        check(Find("Profile", UiAccessibilityRole.AlertDialog).Node.Modal, "Alert Dialog publishes the distinct alertdialog role");
        Click(new(5, 5));
        check(dialog.IsOpen && outside == 1, "Alert backdrop cannot dismiss or activate background content");
        Key(UiKey.Escape);
        check(!dialog.IsOpen, "Alert Dialog defaults to the React implementation's focused Escape dismissal");
        dialog.Options = dialog.Options with { SuppressEscapeDismissal = true };
        Open();
        var beforeEscape = changes.Count;
        Key(UiKey.Escape);
        check(dialog.IsOpen && changes.Count == beforeEscape, "Suppressed Escape does not request a close or alter the bound state");
        check(dialog.Close(), "Suppressed Escape still allows an explicit dismissal");
        Frame();

        var accept = false;
        using var rejected = Fluent.Dialog("Rejected", Fluent.Body1("content"), options: new(MotionEnabled: false));
        rejected.Open = new UiBinding<bool>(() => accept, value => { });
        current = rejected;
        rejected.Show();
        Frame();
        Frame();
        check(!rejected.IsOpen && !HasDialog(), "Dialog does not publish a rejected controlled opening");
        rejected.Open = new UiBinding<bool>(() => accept, value => { if (value) accept = true; });
        rejected.Show();
        Frame();
        Frame();
        check(rejected.IsOpen && !rejected.Close(), "Rejected controlled closing leaves the Dialog open");
        accept = false;
        Frame();
        Frame();
        check(!rejected.IsOpen && !HasDialog(), "External controlled false closes Dialog without another binding write");
        current = dialog;

        content.Value = string.Join('\n', Enumerable.Range(0, 80).Select(x => "Long content line " + x));
        dialog.Options = dialog.Options with
        {
            Type = UiDialogType.Modal
        };
        Open();
        var titleY = Find("Profile", UiAccessibilityRole.Label).Node.Bounds.Y;
        var actionY = Find("Save", UiAccessibilityRole.Button).Node.Bounds.Y;
        var body = dialog.Body.Content;
        var contentBounds = tree.CaptureSnapshot().Elements.First(x => x.Node.Scroll is { } && x.Node.Bounds.Y > titleY).Node.Bounds;
        Frame(idle with
        {
            MousePosition = new(contentBounds.X + 10, contentBounds.Y + 10),
            MouseWheel = -5
        });
        Frame();
        check(body.Offset.Y > 0 && dialog.Viewport.Offset.Y == 0, "Normal-height Dialog scrolls content independently");
        check(Find("Profile", UiAccessibilityRole.Label).Node.Bounds.Y == titleY && Find("Save", UiAccessibilityRole.Button).Node.Bounds.Y == actionY,
            "Dialog title and actions stay fixed while content scrolls");
        dialog.Close();
        Frame();
        Frame();
        display = new(400, 480);
        Open();
        var firstAction = Find("Save", UiAccessibilityRole.Button).Node.Bounds;
        var secondAction = Find("Cancel", UiAccessibilityRole.Button).Node.Bounds;
        check(firstAction.Width == secondAction.Width && firstAction.Width == 350 && secondAction.Y >= firstAction.Y + firstAction.Height + 8,
            "Narrow viewport stacks full-width Dialog actions with Fluent spacing");
        dialog.Close();
        Frame();
        Frame();
        display = new(640, 300);
        Open();
        var area = Find("Profile", UiAccessibilityRole.Dialog).Node.Bounds;
        Frame(idle with
        {
            MousePosition = new(area.X + 30, area.Y + 80),
            MouseWheel = -5
        });
        Frame();
        check(dialog.Viewport.Offset.Y > 0, "Short viewport scrolls the whole Dialog surface body");
        visible = false;
        Frame();
        check(!dialog.IsOpen && !HasDialog() && dialog.Input.Shadow is null, "Hidden Dialog cancels ownership and releases surface resources");
        visible = true;
        opened.Value = false;
        Frame();
        check(Fluent.ComponentNames.Contains("Dialog") && !Fluent.MissingComponentNames.Contains("Dialog"),
            "Actual Fluent factory is connected to the required component inventory");

        dialog.Open = null;
        display = new(640, 480);
        dialog.Options = new(LightDismiss: false, MotionEnabled: false);
        dialog.Show();
        Frame();
        Frame();
        check(dialog.IsOpen && !opened.Value, "Uncontrolled Dialog opens without changing an unrelated model");
        Click(new(5, 5));
        check(dialog.IsOpen, "Modal light-dismiss can be explicitly disabled while focus remains trapped");
        dialog.Options = dialog.Options with
        {
            LightDismiss = true
        };
        Frame();
        area = Find("Profile", UiAccessibilityRole.Dialog).Node.Bounds;
        Frame(idle with
        {
            MousePosition = new(area.X + 30, area.Y + 20),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(idle with
        {
            MousePosition = new(5, 5),
            LeftMouseReleased = true
        });
        Frame();
        check(dialog.IsOpen, "A drag starting inside Dialog does not become a backdrop click on release");
        var runtime = Find("Profile", UiAccessibilityRole.Dialog).RuntimeId;
        dialog.Title = "Updated profile";
        Frame();
        check(Find("Updated profile", UiAccessibilityRole.Dialog).RuntimeId == runtime
            && Find("Updated profile", UiAccessibilityRole.Label).Node.Bounds.Height >= 28,
            "Dialog title updates accessible names while retaining the live surface identity and typography height");
        dialog.AccessibleName = "Explicit dialog name";
        Frame();
        check(Find("Explicit dialog name", UiAccessibilityRole.Dialog).RuntimeId == runtime
            && Find("Updated profile", UiAccessibilityRole.Label).Node.Bounds.Height >= 28,
            "Explicit Dialog accessible name overrides its label while preserving the displayed title");
        dialog.AccessibleName = null;
        check(dialog.Close(), "Uncontrolled Dialog accepts explicit Close");
        Frame();
        Frame();

        using var empty = Fluent.Dialog("Empty focus", Fluent.Body1("No interactive children"), options: new(MotionEnabled: false));
        current = empty;
        empty.Show();
        Frame();
        Frame();
        check(Find("Empty focus", UiAccessibilityRole.Dialog).Node is { Focusable: true, Focused: true },
            "Dialog with no focusable children receives surface focus instead of losing keyboard access");
        Key(UiKey.Escape);
        check(!empty.IsOpen, "Empty-focus Dialog can still be dismissed with Escape");

        var child = Fluent.Popover("Owned details", Fluent.Body1("Details"), options: new(MotionEnabled: false));
        using var parent = Fluent.Dialog("Parent dialog", child, options: new(MotionEnabled: false));
        current = parent;
        parent.Show();
        Frame();
        Frame();
        child.Show();
        Frame();
        Frame();
        check(parent.IsOpen && child.IsOpen, "Dialog owns a child Popover in the same overlay registry");
        Key(UiKey.Escape);
        check(parent.IsOpen && !child.IsOpen, "First Escape dismisses the owned child Popover without closing Dialog");
        Key(UiKey.Escape);
        check(!parent.IsOpen, "Next Escape dismisses Dialog after child focus restoration");
        parent.Show();
        Frame();
        Frame();
        child.Show();
        Frame();
        Frame();
        visible = false;
        Frame();
        check(!parent.IsOpen && !child.IsOpen && parent.Input.Shadow is null,
            "Hiding a parent Dialog cancels its open child portal and releases the parent surface");
        visible = true;
        Frame();

        var doomed = Fluent.Dialog("Dispose dialog", Fluent.Button("Dispose child"), options: new(MotionEnabled: false));
        current = doomed;
        doomed.Show();
        Frame();
        Frame();
        doomed.Dispose();
        check(!doomed.IsOpen && doomed.Input.Shadow is null, "Disposing an open Dialog cancels ownership and releases its shadow lease");
        current = dialog;
        Frame();
        check(!HasDialog(), "Disposed Dialog leaves no stale visible accessibility surface");

        current = dialog;
        dialog.Options = new(Type: UiDialogType.NonModal, MotionEnabled: false);
        content.Value = "Short content";
        Open();
        Key(UiKey.Tab);
        Key(UiKey.Tab);
        Key(UiKey.Tab);
        check(Find("Cancel", UiAccessibilityRole.Button).Node.Focused,
            "Non-modal Tab follows close, editor and action order");
        Key(UiKey.Tab);
        check(Find("Outside", UiAccessibilityRole.Button).Node.Focused && dialog.IsOpen,
            "Non-modal Tab exits to the next background control without closing");
        Key(UiKey.Tab, KeyModifiers.Shift);
        check(Find("Cancel", UiAccessibilityRole.Button).Node.Focused,
            "Non-modal Shift+Tab reenters from the following background control");
        dialog.Close();
        Frame();
        Frame();

        dialog.Options = new(MotionEnabled: false);
        Open();
        var ime = new Composition();
        context.SetImeHandler(ime);
        Frame();
        var beforeComposition = name.Value;
        ime.Preview = "정";
        Key(UiKey.Escape);
        check(dialog.IsOpen && name.Value == beforeComposition && ime.Owner is not null,
            "Escape during Korean composition preserves Dialog and the uncommitted model");
        ime.Preview = null;
        ime.Pending = "정";
        Frame();
        var committed = name.Value;
        Frame();
        check(committed.Length == beforeComposition.Length + 1 && name.Value == committed,
            "Dialog consumes committed Korean composition once before subsequent dismissal");
        Key(UiKey.Escape);
        check(!dialog.IsOpen && ime.Owner is null,
            "Escape after composition ends dismisses Dialog and releases IME ownership");

        Open();
        triggerVisible = false;
        Frame();
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Escape, true, default)]
        });
        check(!dialog.IsOpen && context.State.FocusedId is null,
            "Dialog dismissal never restores focus to an opener removed from the current frame");
        triggerVisible = true;
        Frame();
        Frame();

        Open();
        triggerEnabled = false;
        Frame();
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Escape, true, default)]
        });
        check(!dialog.IsOpen && context.State.FocusedId is null,
            "Dialog dismissal excludes an explicitly disabled opener even though its visual remains present");
        triggerEnabled = true;
        Frame();
        Frame();
        Open();
        triggerAfterDialog = true;
        Frame();
        Key(UiKey.Escape);
        check(Find("Open", UiAccessibilityRole.Button).Node.Focused,
            "Dialog restores an enabled opener rendered later in the same frame");
        triggerAfterDialog = false;
        Frame();

        Open();
        using (var nextContext = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2)))
        using (var nextTree = new UiAccessibilityTree())
        {
            ControlFontFixture.Configure(nextContext);
            nextContext.SetAccessibilityTree(nextTree);
            var oldShadow = dialog.Input.Shadow;
            nextContext.NewFrame(new(1f / 60, display, new(1, 1)), idle, new(0, 0, display.X, display.Y), UiTextSettings.Default);
            nextContext.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); dialog.Render(ui); }));
            using (var draw = nextContext.GetDrawData())
            {
            }
            check(!dialog.IsOpen && dialog.Input.Shadow is null && nextTree.CaptureSnapshot().Elements.All(x => x.Node.Role != UiAccessibilityRole.Dialog),
                "Moving an uncontrolled Dialog to another host cancels the old overlay and its surface resources");
            current = empty;
            Frame();
            check(!HasDialog() && context.State.FocusedId is null,
                "The old host retires moved Dialog accessibility and keyboard focus");
            dialog.Show();
            nextContext.NewFrame(new(1f / 60, display, new(1, 1)), idle, new(0, 0, display.X, display.Y), UiTextSettings.Default);
            nextContext.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); dialog.Render(ui); }));
            using (var draw = nextContext.GetDrawData())
            {
            }
            check(dialog.IsOpen && !ReferenceEquals(oldShadow, dialog.Input.Shadow)
                && nextTree.CaptureSnapshot().Elements.Any(x => x.Node.Role == UiAccessibilityRole.Edit && x.Node.Name == "Name"),
                "A moved Dialog reopens with new host ownership and retained editing content");
        }
        current = dialog;
        Frame();
        check(!dialog.IsOpen && !HasDialog(), "Disposing a Dialog host releases overlay state before returning to another host");

        using var shared = Fluent.Body1("Shared");
        var rejectedComposition = false;
        try
        {
            using var invalid = Fluent.Dialog("Invalid", shared, [shared]);
        }
        catch (ArgumentException) { rejectedComposition = true; }
        using var valid = Dux.Column(new(), shared);
        check(rejectedComposition, "Duplicate Dialog slots fail before adopting caller content");
        var rejectedReparent = false;
        try
        {
            using var invalidParent = Fluent.Dialog("Invalid parent", dialog.Content);
        }
        catch (InvalidOperationException) { rejectedReparent = true; }
        current = dialog;
        dialog.Show();
        Frame();
        Frame();
        check(rejectedReparent && dialog.IsOpen && Find("Name", UiAccessibilityRole.Edit).Node.Focused,
            "Reparenting an owned Dialog child is rejected without changing its original owner or focus");
        dialog.Close();
        Frame();
        Frame();
        var originalAction = Fluent.Button("Original action");
        var array = new UiWidget[] { originalAction };
        using var copied = Fluent.Dialog("Copied slots", Fluent.Body1("Content"), array);
        using var replacement = Fluent.Button("Replacement");
        array[0] = replacement;
        check(ReferenceEquals(copied.Actions[0], originalAction), "Mutating the caller action array cannot change Dialog ownership");
    }

    private sealed class Composition : IUiImeHandler
    {
        internal string? Owner, Preview, Pending;
        public string? GetCompositionText() => Owner is null ? null : Preview;
        public void SetCompositionOwner(string? inputId) => Owner = inputId;
        public string? ConsumeCommittedText(string inputId)
        {
            if (inputId != Owner)
                return null;
            var value = Pending;
            Pending = null;
            return value;
        }
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
        {
        }
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}

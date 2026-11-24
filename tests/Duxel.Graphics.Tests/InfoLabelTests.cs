using Duxel.Core;
using Duxel.Platform.Windows;
using System.Runtime.InteropServices;

internal static class InfoLabelTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var factory = Fluent.InfoLabel("Factory label", Fluent.Body1("Supplemental information"));
        check(factory is UiFluentInfoLabel && Fluent.ComponentNames.Contains("InfoLabel") && !Fluent.MissingComponentNames.Contains("InfoLabel"),
            "InfoLabel exposes its generated Fluent factory and connects the official component requirement");
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var native = new WindowsAccessibility(0);
        var tree = native.Tree;
        context.SetAccessibilityTree(tree);
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        using var target = Fluent.Input("Original", new UiState<string>("정세일").Binding, 100, 200);
        var content = Fluent.Button("Supplemental command");
        using var label = new UiInfoLabel("User name", content, target, new(Required: true), "추가 정보");
        label.PopoverOptions = label.PopoverOptions with { MotionEnabled = false };
        using var after = Fluent.Button("After information");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var width = 300f;
        var direction = UiFlowDirection.LeftToRight;
        UiVector2 measured = default;
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(20, 80));
                using (ui.BeginFluentProvider(null, direction : direction))
                {
                    measured = label.MeasureConstrained(ui, new(0, width, 0, 400));
                    ui.RenderLayoutChild(label, new(20, 80, measured.X, measured.Y), new(0, 0, 640, 480));
                }
                ui.SetCursorScreenPos(new(20, 240));
                target.Render(ui);
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
        UiAccessibilityElement Trigger() => Nodes().Single(e => e.Node.Role == UiAccessibilityRole.Button && e.Node.Name.EndsWith("추가 정보", StringComparison.Ordinal));
        void Request(UiAccessibilityElement element, UiAccessibilityAction action)
        {
            check(tree.RequestAction(element.Node.Id, element.RuntimeId, action, wait: false) == UiAccessibilityResult.Success,
                "InfoLabel accepts its actual accessibility action : " + action);
            Frame();
        }
        Frame();
        check(Nodes().Single(e => e.Node.Role == UiAccessibilityRole.Edit).Node is { Name: "User name", Required: true },
            "InfoLabel borrows its target and publishes label and required metadata");
        check(Trigger().Node.Name == "User name 추가 정보" && measured.Y == 24 && Trigger().Node.Bounds is { Width: 24, Height: 24, Y: 80 },
            $"InfoLabel medium button preserves the full official 24px target inside its native allocation : {measured} / {Trigger().Node.Bounds}");
        var stable = requests;
        label.Text = label.Text;
        label.Target = target;
        label.InformationLabel = label.InformationLabel;
        label.Options = label.Options;
        Frame();
        check(requests == stable && !context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "InfoLabel unchanged replay and setters preserve event wait");
        var trigger = Trigger();
        var point = new UiVector2(trigger.Node.Bounds.X + 12, trigger.Node.Bounds.Y + 12);
        Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
        check(!label.IsOpen && context.State.ActiveId == trigger.Node.Id,
            "InfoLabel information press captures its own trigger without activating the borrowed field label");
        Frame(idle with { MousePosition = point, LeftMouseReleased = true });
        check(label.IsOpen, "InfoLabel actual pointer release opens its supplemental content");
        label.Close();
        Frame();
        Request(trigger, UiAccessibilityAction.Focus);
        Frame(idle with { KeyEvents = [new(UiKey.Enter, true, default)] });
        check(label.IsOpen && Nodes().Any(e => e.Node.Role == UiAccessibilityRole.Note),
            "InfoLabel Enter opens a real Note popover with owned arbitrary content");
        var note = Nodes().Single(e => e.Node.Role == UiAccessibilityRole.Note);
        var provider = native.Find(note.Node.Id)!;
        provider.GetPropertyValue(30003, out var controlType);
        provider.GetPropertyValue(30101, out var ariaRole);
        try
        {
            check(controlType.Data.Integer == 50026 && ariaRole.Type == 8 && Marshal.PtrToStringBSTR(ariaRole.Data.Pointer) == "note",
                "InfoLabel Note projects the native UIA Group control type and note ARIA role");
        }
        finally
        {
            Marshal.ThrowExceptionForHR(UiaNative.VariantClear(ref ariaRole));
        }
        check(Nodes().Any(e => e.Node.Name == "Supplemental command" && e.Node.Focused),
            "InfoLabel opening moves focus into its real supplemental content");
        Frame(idle with { KeyEvents = [new(UiKey.Escape, true, default)] });
        check(!label.IsOpen && Trigger().Node.Focused && !Nodes().Any(e => e.Node.Role == UiAccessibilityRole.Note),
            "InfoLabel Escape retires the Note and restores the information trigger");
        Request(Trigger(), UiAccessibilityAction.Invoke);
        var destination = Nodes().Single(e => e.Node.Name == "After information");
        Request(destination, UiAccessibilityAction.Focus);
        Frame();
        check(!label.IsOpen && Nodes().Single(e => e.Node.Name == "After information").Node.Focused,
            "InfoLabel focus leave dismisses without stealing destination focus");
        label.Text = "정세일\n사용자";
        Frame();
        check(Trigger().Node.Name == "정세일 사용자 추가 정보", "InfoLabel normalizes multiline visible text for its localized information name");
        var previous = label.Text;
        var rejected = false;
        try { label.Text = "Invalid\0label"; }
        catch (ArgumentException) { rejected = true; }
        check(rejected && label.Text == previous, "InfoLabel rejects invalid text before changing its association or trigger name");
        foreach (var (size, height, edge, margin) in new[] { (UiLabelSize.Small, 16, 20, 2), (UiLabelSize.Medium, 20, 24, 2), (UiLabelSize.Large, 22, 24, 1) })
        {
            label.Text = "Name";
            label.Options = new(Size: size);
            Frame();
            check(measured.Y == height + margin * 2 && Trigger().Node.Bounds is var b && b.Width == edge && b.Height == edge && b.Y == 80,
                $"InfoLabel uses the independent official size fixture : {size} / {measured} / {Trigger().Node.Bounds}");
        }
        label.Options = default;
        label.Text = "Name";
        width = 40;
        Frame();
        check(Trigger().Node.Bounds.X == 20 && Trigger().Node.Bounds.Y >= 98 && measured.Y >= 40,
            "InfoLabel flows its information button to the next text line when the available width is exhausted");
        direction = UiFlowDirection.RightToLeft;
        Frame();
        check(Trigger().Node.Bounds is var wrapped && wrapped.X == 20 + measured.X - wrapped.Width && wrapped.Y >= 98,
            "RTL InfoLabel wraps its information button to the right-hand start of the next line");
        width = 300;
        label.Options = new(Required : true);
        Frame();
        var rtlButton = Trigger().Node.Bounds;
        check(rtlButton.X == 20 && rtlButton.Width == 24 && rtlButton.Height == 24,
            "RTL InfoLabel places its full information button after the label and required marker");
        var rtlPointer = idle with { MousePosition = new(rtlButton.X + 12, rtlButton.Y + 12) };
        Frame(rtlPointer with { LeftMouseDown = true, LeftMousePressed = true });
        Frame(rtlPointer with { LeftMouseReleased = true });
        check(label.IsOpen && Nodes().Any(e => e.Node.Role == UiAccessibilityRole.Note),
            "RTL InfoLabel opens its Note through the mirrored button position");
        Frame(idle with { KeyEvents = [new(UiKey.Escape, true, default)] });
        check(!label.IsOpen && Trigger().Node.Focused, "RTL InfoLabel restores its mirrored button focus on Escape");
        var borrowed = label.Target;
        label.Dispose();
        target.AccessibleName = null;
        var ownedDisposed = false;
        try { content.AccessibleName = null; }
        catch (ObjectDisposedException) { ownedDisposed = true; }
        check(ReferenceEquals(borrowed, target) && ownedDisposed,
            "InfoLabel disposal owns supplemental content and leaves the borrowed target alive");
        using var replacement = Fluent.Label("Replacement", target);
        check(replacement.Target == target, "InfoLabel disposal releases the standalone label association");
        using var plain = new UiInfoLabel("Plain");
        check(plain.Info is null && !plain.IsOpen && plain.Close(), "InfoLabel without supplemental content has no information trigger");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}

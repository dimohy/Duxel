using Duxel.Core;

namespace Duxel.Gallery;

internal static class ObserverMessageBarControlProbe
{
    private static readonly string[] Switches = ["여러 행 배치", "각진 모서리", "오른쪽에서 왼쪽 배치", "동작 줄이기"];

    internal static bool TryQueue(UiAccessibilityTree tree, bool enabled)
    {
        var nodes = tree.CaptureSnapshot().Elements;
        var switches = Switches.Select(name => nodes.Single(x => x.Node.Name == name && x.Node.Role == UiAccessibilityRole.Switch)).ToArray();
        var slider = nodes.Single(x => x.Node.Name == "MessageBar 너비" && x.Node.Role == UiAccessibilityRole.Slider);
        if (slider.Node.Offscreen || switches.Any(item => item.Node.Offscreen))
        {
            if (tree.RequestAction(slider.Node.Id, slider.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("The actual MessageBar option probe could not reveal its controls.");
            return false;
        }
        if (switches.Any(x => x.Node.Checked != !enabled) || slider.Node.Range is null)
            throw new InvalidOperationException("The actual MessageBar option probe is not at its expected input state.");
        foreach (var item in switches)
            if (tree.RequestAction(item.Node.Id, item.RuntimeId, UiAccessibilityAction.Toggle, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("The actual MessageBar option switch rejected its queued input.");
        if (tree.RequestRangeValue(slider.Node.Id, slider.RuntimeId, enabled ? 500 : 800, wait: false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("The actual MessageBar width slider rejected its queued input.");
        return true;
    }

    internal static bool RevealMessages(MessageBarPage page, UiAccessibilityTree tree, bool requestReveal)
    {
        var nodes = tree.CaptureSnapshot().Elements;
        var messages = page.DynamicMessages.Items.ToArray().Select(item => nodes.Single(element =>
            element.Node.Role == UiAccessibilityRole.Group && element.Node.Name == item.Key)).ToArray();
        if (messages.Length != 3)
            throw new InvalidOperationException("The actual MessageBar option probe requires its three inserted messages.");
        if (!requestReveal && messages.All(message => !message.Node.Offscreen)
            && messages.Select((message, index) => message.Node.Bounds.Width
                == ((UiFluentMessageBar)page.DynamicMessages.Items[index].Widget).Options.Width).All(value => value))
            return true;
        var target = messages[^1];
        if (tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("The actual MessageBar option probe could not reveal its inserted messages.");
        return false;
    }

    internal static ObserverMessageBarOptionSnapshot Verify(MessageBarPage page, UiAccessibilityTree tree, bool enabled)
    {
        var messages = page.FixedMessages.Children.ToArray().Concat(page.DynamicMessages.Children.ToArray())
            .Cast<UiFluentMessageBar>().ToArray();
        var layout = enabled ? UiMessageBarLayoutMode.MultiLine : UiMessageBarLayoutMode.Auto;
        var shape = enabled ? UiMessageBarShape.Square : UiMessageBarShape.Rounded;
        var width = enabled ? 500 : 800;
        if (messages.Length != 7 || messages.Any(x => x.Options.Layout != layout || x.Options.Shape != shape
            || x.Options.RightToLeft != enabled || x.Options.Width != width
            || x.Body is not UiMessageBarBodyWidget body
            || body.HorizontalAlignment != (enabled ? UiItemHorizontalAlign.Right : UiItemHorizontalAlign.Left))
            || page.FixedMessages.Options.ReducedMotion != enabled || page.DynamicMessages.Options.ReducedMotion != enabled)
            throw new InvalidOperationException("Actual presented MessageBar options differ between fixed and dynamic messages : expected "
                + enabled + "; groups " + page.FixedMessages.Options.ReducedMotion + "/" + page.DynamicMessages.Options.ReducedMotion
                + "; messages " + string.Join(" | ", messages.Select(message => message.Options.ToString()
                    + "; body " + (message.Body is UiMessageBarBodyWidget body ? body.HorizontalAlignment : null))));
        var nodes = tree.CaptureSnapshot().Elements;
        foreach (var item in page.DynamicMessages.Items)
        {
            var root = nodes.Single(x => x.Node.Role == UiAccessibilityRole.Group && x.Node.Name == item.Key);
            if (root.Node.Offscreen || root.Node.Bounds.Width != width || enabled && root.Node.Bounds.Height <= 36)
                throw new InvalidOperationException($"Actual published MessageBar bounds do not reflect the requested option layout : {item.Key}, expected width {width}, bounds {root.Node.Bounds}, offscreen {root.Node.Offscreen}.");
            var body = (UiMessageBarBodyWidget)((UiFluentMessageBar)item.Widget).Body;
            var title = nodes.Single(x => x.Node.Id == root.Node.LabeledBy);
            var text = nodes.Single(x => x.Node.ParentId == root.Node.Id && x.Node.Name == body.Text);
            if (enabled ? MathF.Abs(root.Node.Bounds.X + width - text.Node.Bounds.X - text.Node.Bounds.Width - 41) > .05f
                : MathF.Abs(title.Node.Bounds.X - root.Node.Bounds.X - 41) > .05f)
                throw new InvalidOperationException("Actual owned inline body bounds do not follow the MessageBar content alignment edge.");
        }
        page.VerifyDynamicState("add");
        var actual = messages[0].Options;
        return new(actual.Width!.Value, actual.Layout, actual.Shape, actual.RightToLeft,
            page.DynamicMessages.Options.ReducedMotion, messages.Length,
            ((UiMessageBarBodyWidget)messages[0].Body).HorizontalAlignment);
    }
}

internal sealed record ObserverMessageBarOptionSnapshot(float Width, UiMessageBarLayoutMode Layout,
    UiMessageBarShape Shape, bool RightToLeft, bool ReducedMotion, int Messages, UiItemHorizontalAlign BodyAlignment);

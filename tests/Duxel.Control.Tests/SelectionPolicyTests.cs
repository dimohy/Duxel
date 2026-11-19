using Duxel.Core;

internal static class SelectionPolicyTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        void Frame(bool first, bool second, bool required = true)
        {
            tree.BeginFrame();
            if (tree.Take("a", UiAccessibilityAction.Deselect) is { } request)
                request.Result = UiAccessibilityResult.Success;
            tree.Add(new("list", "", UiAccessibilityRole.ListBox, "Required list", new(0, 0, 100, 50), true, false, false,
                Focusable: false, ValuePattern: false)
            {
                CanSelectMultiple = true,
                SelectionRequired = required
            });
            tree.Add(new("a", "list", UiAccessibilityRole.Option, "A", new(0, 0, 100, 20), true, false, false,
                ValuePattern: false)
            {
                Selected = first
            });
            tree.Add(new("b", "list", UiAccessibilityRole.Option, "B", new(0, 25, 100, 20), true, false, false,
                ValuePattern: false)
            {
                Selected = second
            });
            tree.EndFrame(null);
        }
        Frame(true, true);
        var snapshot = tree.CaptureSnapshot();
        var a = snapshot.Elements.Single(e => e.Node.Id == "a");
        var b = snapshot.Elements.Single(e => e.Node.Id == "b");
        check(tree.RequestAction(a.Node.Id, a.RuntimeId, UiAccessibilityAction.Deselect, wait: false) == UiAccessibilityResult.Success,
            "Mandatory multiple selection permits deselecting one of several selected children");
        Frame(false, true);
        check(tree.RequestAction(b.Node.Id, b.RuntimeId, UiAccessibilityAction.Deselect, wait: false) == UiAccessibilityResult.InvalidOperation,
            "The common action gate prevents removing the final required option");
        check(tree.RequestAction(a.Node.Id, a.RuntimeId, UiAccessibilityAction.Deselect, wait: false) == UiAccessibilityResult.Success,
            "A required selection permits an already-unselected child to remain unselected");
        Frame(false, true, required: false);
        check(tree.RequestAction(b.Node.Id, b.RuntimeId, UiAccessibilityAction.Deselect, wait: false) == UiAccessibilityResult.Success,
            "An optional selection permits removing its final selected option");
    }
}

using Duxel.Core;

internal static class ListLifetimeTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var first = CreateContext();
        using var second = CreateContext();
        using var firstTree = new UiAccessibilityTree();
        using var secondTree = new UiAccessibilityTree();
        first.SetAccessibilityTree(firstTree);
        second.SetAccessibilityTree(secondTree);
        var text = new UiState<string>("abcd");
        var selected = new UiState<string[]>([]);
        var actions = 0;
        var commands = 0;
        var input = Fluent.Input("Lifetime editor", text.Binding, 100, 180);
        var row = new UiListItem("edit", "Editable row", input, _ => actions++, selectionLabel: "Choose editable");
        using var list = Fluent.List("Lifetime list", selected.Binding,
        [
            row,
            new("action", "Action only row", Fluent.Button("Child command", () => commands++), _ => actions++, disabledSelection: true),
            new("disabled", "Disabled row", Fluent.Body1("Disabled content"), disabledSelection: true)
        ], new(Navigation: UiListNavigation.Composite, Width: 300));
        using var after = Fluent.Button("After lifetime list");
        var shown = true;
        var disabled = false;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiContext context, UiInputState? state = null)
        {
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)), state ?? idle, new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(10, 20));
                if (shown)
                {
                    ui.BeginDisabled(disabled);
                    try
                    {
                        list.Render(ui);
                    }
                    finally { ui.EndDisabled(); }
                }
                ui.SetCursorScreenPos(new(10, 250));
                after.Render(ui);
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(UiAccessibilityTree tree, string name)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var element))
                {
                    if (element.Node.Name == name)
                        found ??= element;
                    Visit(element.FirstChild);
                    id = element.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException("Missing List lifetime node: " + name);
        }
        void Focus(string name)
        {
            var node = Find(firstTree, name);
            firstTree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Focus, wait: false);
            Frame(first);
        }
        void Key(UiKey key)
        {
            Frame(first, idle with
            {
                KeyEvents = [new(key, true, default)]
            });
            Frame(first, idle with
            {
                KeyEvents = [new(key, false, default)]
            });
        }
        Frame(first);
        Focus("Editable row");
        Key(UiKey.RightArrow);
        var edit = Find(firstTree, "Lifetime editor");
        check(edit.Node.Focused, "List composite enters its owned editor");
        Key(UiKey.Home);
        Key(UiKey.RightArrow);
        check(first.State.GetCursor(edit.Node.Id, -1) == 1 && Find(firstTree, "Lifetime editor").Node.Focused,
            "List leaves editor Home and horizontal arrows in the source input");
        Key(UiKey.UpArrow);
        Key(UiKey.DownArrow);
        check(Find(firstTree, "Lifetime editor").Node.Focused && actions == 0 && selected.Value.Length == 0,
            "List does not steal vertical arrows or select rows while editing");
        Frame(first, idle with
        {
            CharEvents = [new((uint)'X')]
        });
        check(text.Value == "aXbcd", "List editor typing preserves its independently moved caret");
        Key(UiKey.Escape);
        check(Find(firstTree, "Editable row").Node.Focused, "List editor Escape returns focus to the owning row");
        Key(UiKey.DownArrow);
        check(Find(firstTree, "Action only row").Node.Focused, "Selection-disabled rows retain their explicit action navigation");
        Key(UiKey.Space);
        check(selected.Value.Length == 0 && actions == 0, "Space cannot select an action-only disabled-selection row");
        Key(UiKey.Enter);
        check(actions == 1 && selected.Value.Length == 0, "Enter invokes an action-only row without changing selection");
        Key(UiKey.DownArrow);
        check(Find(firstTree, "Action only row").Node.Focused && !Find(firstTree, "Disabled row").Node.Enabled,
            "List directional navigation skips fully disabled rows");
        Focus("Lifetime editor");
        disabled = true;
        Frame(first);
        Frame(first, idle with
        {
            CharEvents = [new((uint)'Y')],
            KeyEvents = [new(UiKey.Enter, true, default)]
        });
        check(text.Value == "aXbcd" && actions == 1 && !Find(firstTree, "Lifetime editor").Node.Focused,
            "Disabling a List retires editor focus and prevents queued edits and row actions");
        disabled = false;
        Frame(first);
        Focus("Lifetime editor");
        var retired = Find(firstTree, "Lifetime editor");
        shown = false;
        Frame(first);
        Frame(first, idle with
        {
            CharEvents = [new((uint)'Z')]
        });
        check(text.Value == "aXbcd" && first.State.FocusedId != retired.Node.Id && first.State.ActiveId != retired.Node.Id,
            "Hiding a List retires its focused editor without replaying hidden input");
        check(firstTree.RequestAction(retired.Node.Id, retired.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Unavailable,
            "Hidden List descendants reject stale accessibility focus requests");
        shown = true;
        Frame(first);
        Focus("Action only row");
        Key(UiKey.Tab);
        check(Find(firstTree, "After lifetime list").Node.Focused, "Restored List preserves one outward Tab step");
        Focus("Lifetime editor");
        Frame(second);
        check(first.State.ActiveId != retired.Node.Id && first.State.FocusedId != retired.Node.Id,
            "Moving a List to another context releases its old editor focus immediately");
        check(!Find(secondTree, "Lifetime editor").Node.Focused && text.Value == "aXbcd",
            "Transferred List does not inherit another context's editor focus or alter its binding");
        var transferred = Find(secondTree, "Lifetime editor");
        secondTree.RequestAction(transferred.Node.Id, transferred.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame(second);
        list.Dispose();
        check(second.State.ActiveId != transferred.Node.Id && second.State.FocusedId != transferred.Node.Id,
            "Disposing a List releases its owned editor's active and focused state");
        var released = false;
        try
        {
            row.Action = _ => actions++;
        }
        catch (ObjectDisposedException) { released = true; }
        check(released, "Disposing a List disposes its rows and rejects new callbacks");
        check(row.Action is null && row.Owner is null, "Disposed List rows release their action and containing state references");
        shown = false;
        Frame(second);
        check(secondTree.RequestAction(transferred.Node.Id, transferred.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Unavailable,
            "Disposed List descendants retire from the next accessibility publication");
        check(commands == 0 && selected.Value.Length == 0, "List lifetime transitions do not synthesize child commands or selection");
    }

    private static UiContext CreateContext()
    {
        var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        return context;
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}

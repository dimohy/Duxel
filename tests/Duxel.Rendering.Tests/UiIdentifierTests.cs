using Duxel.Core;

internal static class UiIdentifierTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        var input = new UiInputState(default, false, false, false, false, false, false, 0, 0, [], [], default, default);
        UiImmediateContext? lastUi = null;
        void Frame(Action<UiImmediateContext> render)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui => { lastUi = ui; render(ui); }));
            context.GetDrawData().ReleasePooled();
        }
        string? first = null;
        Frame(ui =>
        {
            ui.PushID("window");
            ui.PushID("item");
            first = ui.GetID("action");
            check(first == "window/item/action" && ReferenceEquals(first, ui.GetID("action")),
                "scoped identifiers preserve their value and reuse the same immutable string");
            ui.PushID("");
            check(ui.GetID("action") == first && ui.GetID("") == "", "empty ID scopes preserve existing identifier semantics");
            ui.PopID(); ui.PopID(); ui.PopID();
            check(ui.GetID("root") == "root", "popping ID scopes restores the root identifier");
        });
        Frame(ui =>
        {
            ui.PushID("window"); ui.PushID("item");
            check(ReferenceEquals(first, ui.GetID("action")), "scoped identifier reuse survives UI frame boundaries");
            ui.PopID(); ui.PopID();
            ui.PushID("a/b"); var left = ui.GetID("c"); ui.PopID();
            ui.PushID("a"); var right = ui.GetID("b/c"); ui.PopID();
            check(left == right && left == "a/b/c", "identifier caching preserves slash-containing legacy ID values");
        });
        Frame(ui =>
        {
            ui.PushID("stress");
            for (var i = 0; i < 9000; i++) ui.GetID("item-" + i);
            var state = ui.ResolvedIdentifierCacheState;
            check(state.Entries <= 4096 && state.Characters <= 1_048_576, "identifier admission stays within entry and character budgets");
            var largeId = new string('x', 600_000);
            check(ui.GetID(largeId).Length == largeId.Length + "stress/".Length
                && ui.ResolvedIdentifierCacheState == state, "oversized IDs preserve their value without entering retained storage");
            ui.PopID();
        });
        for (var i = 0; i < 240; i++) Frame(_ => { });
        check(lastUi!.ResolvedIdentifierCacheState == (0, 0), "unused identifier entries expire instead of accumulating across changing screens");
        Frame(ui => { ui.PushID("dispose"); ui.GetID("item"); ui.PopID(); });
        context.Dispose();
        check(lastUi.ResolvedIdentifierCacheState == (0, 0), "context disposal releases retained identifier strings");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}

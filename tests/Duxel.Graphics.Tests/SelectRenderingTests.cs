using Duxel.Core;

internal static class SelectRenderingTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(640, 420));
        using var tree = new UiAccessibilityTree(); fixture.Context.SetAccessibilityTree(tree);
        fixture.Context.SetDesign(UiCompiledDesign.Windows11Dark);
        var value = new UiState<string?>(null);
        using var select = Fluent.Select("지역 선택", value.Binding,
            [new("kr", "대한민국"), new("us", "United States"), new("jp", "日本"), new("de", "Deutschland", false)],
            new(Width : 260, ExpandLabel : "목록 열기"));
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 16);
            new UiControlContext(ui).Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                new UiControlContext(ui).Theme.WindowBg, default, 0, 0);
            select.Render(ui);
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == name && e.Node.Role == role);
        void Click(UiRect bounds)
        {
            var point = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            fixture.Capture(Draw, Input(point, true)); fixture.Capture(Draw, Input(point, false));
        }
        fixture.Capture(Draw);
        Click(Find("목록 열기", UiAccessibilityRole.Button).Node.Bounds);
        var dark = fixture.Capture(Draw);
        check(select.IsOpen && !Find("지역 선택", UiAccessibilityRole.ListBox).Node.Offscreen
            && Find("대한민국", UiAccessibilityRole.Option).Node.Selected == true,
            "rendered Select chevron opens a single-choice popup with the selected option");
        check(!Find("Deutschland", UiAccessibilityRole.Option).Node.Enabled,
            "rendered Select preserves disabled option semantics");
        fixture.WriteCapturePng(Path.Combine(output, "dark-popup.png"), dark);
        Click(Find("United States", UiAccessibilityRole.Option).Node.Bounds); fixture.Capture(Draw);
        check(value.Value == "us" && !select.IsOpen && Find("지역 선택", UiAccessibilityRole.ComboBox).Node.Value == "United States",
            "actual popup pointer selection updates the source binding and closes Select");
        select.Options = select.Options with { RightToLeft = true };
        fixture.Capture(Draw);
        var root = Find("지역 선택", UiAccessibilityRole.ComboBox).Node.Bounds;
        check(Find("목록 열기", UiAccessibilityRole.Button).Node.Bounds.X < root.X + root.Width / 2,
            "Select RTL moves its interactive chevron to the logical end");
        fixture.Context.SetDesign(UiCompiledDesign.Windows11);
        fixture.Capture(Draw); Click(Find("목록 열기", UiAccessibilityRole.Button).Node.Bounds);
        var light = fixture.Capture(Draw);
        check(select.IsOpen && Find("지역 선택", UiAccessibilityRole.ListBox).Node.SelectionRequired,
            "light RTL Select keeps the same required selection contract while expanded");
        fixture.WriteCapturePng(Path.Combine(output, "light-rtl-popup.png"), light);
        Click(Find("日本", UiAccessibilityRole.Option).Node.Bounds); fixture.Capture(Draw);
        check(value.Value == "jp" && !select.IsOpen, "light RTL popup pointer selection commits the original stable option value");
        foreach (var size in Enum.GetValues<UiDropdownSize>())
        {
            select.Options = select.Options with { Size = size };
            fixture.Capture(Draw);
            var height = Find("지역 선택", UiAccessibilityRole.ComboBox).Node.Bounds.Height;
            check(height == (size == UiDropdownSize.Small ? 24 : size == UiDropdownSize.Large ? 40 : 32),
                "Select uses official Fluent field height for " + size);
        }
    }
    private static UiInputState Input(UiVector2 point, bool down) => new(point, down, down, !down,
        false, false, false, 0, 0, [], [], default, default);
}

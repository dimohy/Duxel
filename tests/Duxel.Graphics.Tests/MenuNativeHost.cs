using Duxel.Core;
using Duxel.Platform.Windows;

internal static class MenuNativeHost
{
    internal static void Run(Action<bool, string> check)
        => Run<UiThemeMenuPresentation>(check);
    internal static void RunFluent(Action<bool, string> check)
        => Run<UiFluentMenuPresentation>(check);
    internal static void RunScroll(Action<bool, string> check)
        => Run<UiFluentMenuPresentation>(check, longMenu : true);
    internal static void RunCheckbox(Action<bool, string> check)
        => Run<UiFluentMenuPresentation>(check, checkbox : true);
    internal static void RunRadio(Action<bool, string> check)
        => Run<UiFluentMenuPresentation>(check, radio : true);
    internal static void RunContent(Action<bool, string> check)
        => Run<UiFluentMenuPresentation>(check, content : true);
    private static void Run<TPresentation>(Action<bool, string> check, bool longMenu = false, bool checkbox = false, bool radio = false, bool content = false) where TPresentation : IUiMenuPresentation
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 400, Title = "Duxel owned menu fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        var values = new List<string>();
        var finish = false;
        var checkedState = new UiState<bool>(false);
        var radioState = new UiState<string?>("one");
        var viewState = new UiState<string?>("compact");
        var sortGroup = new UiMenuRadioGroup("Sort", radioState);
        var viewGroup = new UiMenuRadioGroup("View", viewState);
        var menuIcon = UiMenuIcon.Create<UiFluentAddIcon, UiFluentAddFilledIcon>();
        UiMenuOption[] items = content
            ? [new("one", "One") { Icon = menuIcon, SecondaryContent = "ZIP" },
                new("disabled", "Disabled", false) { Icon = menuIcon, SecondaryContent = "Locked" },
                new("finish", "Finish") { SecondaryContent = "Done" }]
            : radio
            ? [UiMenuOption.Radio("one", "One", sortGroup), UiMenuOption.Radio("two", "Two", sortGroup),
                UiMenuOption.Radio("disabled", "Disabled", sortGroup, false), UiMenuOption.Radio("compact", "Compact", viewGroup),
                UiMenuOption.Radio("wide", "Wide", viewGroup), new("finish", "Finish")]
            : checkbox
            ? [UiMenuOption.Checkbox("one", "One", checkedState), UiMenuOption.Checkbox("disabled", "Disabled", checkedState, false), new("finish", "Finish")]
            : longMenu
            ? [new("one", "One"), .. Enumerable.Range(0, 24).Select(i => new UiMenuOption("item-" + i, "Item " + i)), new("finish", "Finish")]
            : [new("one", "One"), new("disabled", "Disabled", false), new("finish", "Finish")];
        using var menu = new UiMenu<TPresentation>("Actions", items,
            value => { values.Add(value); finish = value == "finish"; Console.WriteLine("MENU_CALLBACK : " + value); },
            TPresentation.DefaultOptions with { MaximumHeight = longMenu ? 138 : null });
        using var outer = longMenu ? Dux.ScrollView(Dux.Column(new(Spacing : 0, MainAxisSize : UiMainAxisSize.Min), menu,
            Dux.SizedBox(width : 200, height : 600)), new(Width : 300, Height : 80)) : null;
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 16);
            if (outer is null) menu.Render(ui); else outer.Render(ui);
        });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var deadline = Environment.TickCount64 + 20000;
        var ready = false;
        while (!finish && !platform.ShouldClose && Environment.TickCount64 < deadline)
        {
            platform.PollEvents();
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)), idle, new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_MENU_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finish && values.SequenceEqual(radio ? ["two", "finish"] : checkbox ? ["one", "one", "finish"] : ["one", "finish"]),
            "external UIA menu actions execute the exact actual callback sequence");
        if (checkbox) check(!checkedState.Value, "external native Toggle updates the shared binding twice");
        if (radio) check(radioState.Value == "two" && viewState.Value == "compact", "external native radio selection updates only its own group binding");
        check(!menu.IsOpen, "external menu action closes the actual popup before host completion");
        if (outer is not null) check(outer.Offset.Y == 0, "external floating-menu scroll does not move its logical outer viewport");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}

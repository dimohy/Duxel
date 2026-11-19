using Duxel.Core;
using Duxel.Gallery;

internal static class TextFieldTests
{
    internal static void RunGallery(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(1100, 900));
        using var accessibility = new UiAccessibilityTree();
        fixture.Context.SetAccessibilityTree(accessibility);
        fixture.Context.SetDesign(UiCompiledDesign.Windows11Dark);
        using var page = new TextFieldPage();
        using var frame = new GalleryPageFrame(page);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 10);
            var paint = new UiControlContext(ui);
            paint.Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), paint.Theme.WindowBg, default, 0, 0);
            frame.Render(ui);
        }
        fixture.Capture(Draw);
        var command = accessibility.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button
            && element.Node.Name == "이메일 확인");
        accessibility.RequestAction(command.Node.Id, command.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        fixture.Capture(Draw);
        page.Samples[1].SynchronizeSource();
        check(page.Samples.Count == 2 && page.Samples.All(sample => sample.Source.Contains("Fluent.TextField(", StringComparison.Ordinal))
            && page.Samples[1].Source.Contains("ValidationMessage = \"@ 앞뒤에 주소를 입력하세요.\"", StringComparison.Ordinal),
            "the two TextField previews publish adjacent live code including validation");
        for (var i = 0; i < page.Samples.Count; i++)
            File.WriteAllText(Path.Combine(output, $"textfield-example-{i}.cs"),
                "using Duxel.Core;\nUiImmediateContext ui = null!;\n" + page.Samples[i].Source);
        using var screen = new ObserverScreen(ObserverServices.CreateWindows()) { FreezeDiagnosticText = true, SelectedPage = "TextField" };
        var pixels = fixture.Capture(screen.Render);
        check(screen.SourceCountForPage("TextField") == 2 && !Fluent.MissingComponentNames.Contains("TextField")
            && accessibility.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Link
                && element.Node.Name == "TextField" && element.Node.ItemStatus != "NEW"),
            "TextField remains reachable through the real Gallery");
        fixture.WriteCapturePng(Path.Combine(output, "gallery-textfield.png"), pixels);
    }

    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var accessibility = new UiAccessibilityTree();
        context.SetAccessibilityTree(accessibility);
        var text = new UiState<string>("디모이");
        using var field = Fluent.TextField("이름", text.Binding, inputOptions: new(Width: 280, ClearLabel: "지우기"),
            field: new() { Hint = "표시할 이름", Required = true });
        using var outside = Fluent.Button("다음", () => { });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                field.Render(ui);
                outside.Render(ui);
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(UiAccessibilityRole role, string name) => accessibility.CaptureSnapshot().Elements.Single(
            element => element.Node.Role == role && element.Node.Name == name);
        bool HasClear() => accessibility.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Button && element.Node.Name == "지우기");
        void Request(UiAccessibilityElement element, UiAccessibilityAction action)
            => accessibility.RequestAction(element.Node.Id, element.RuntimeId, action, wait: false);

        Frame();
        var edit = Find(UiAccessibilityRole.Edit, "이름");
        var clear = Find(UiAccessibilityRole.Button, "지우기");
        check(!edit.Node.Focused && clear.Node.ParentId == edit.Node.Id && !clear.Node.Focusable,
            "a nonempty TextField publishes the clear action before focus without adding a Tab stop");
        check(edit.Node.Name == "이름" && edit.Node.Required && edit.Node.Hint == "표시할 이름",
            "the TextField exposes the shared Field label, required state and hint on its editor");
        Request(clear, UiAccessibilityAction.Invoke);
        Frame();
        check(text.Value.Length == 0 && Find(UiAccessibilityRole.Edit, "이름").Node.Focused && HasClear(),
            "clearing a nonfocused TextField commits the bound edit and focuses the editor");
        Request(Find(UiAccessibilityRole.Button, "다음"), UiAccessibilityAction.Focus);
        Frame();
        Frame();
        check(!HasClear(), "an empty and unfocused TextField hides the clear action");
        Request(Find(UiAccessibilityRole.Edit, "이름"), UiAccessibilityAction.Focus);
        Frame();
        check(HasClear(), "an empty TextField exposes the clear action while focused");
        Frame(idle with { CharEvents = [new('디'), new('모'), new('이')] });
        check(text.Value == "디모이", "TextField commits Korean text through the shared editor");
        field.Options = field.Options with { ValidationMessage = "이름을 확인하세요." };
        Frame();
        var invalid = Find(UiAccessibilityRole.Edit, "이름");
        check(invalid.RuntimeId == edit.RuntimeId && invalid.Node.Invalid && invalid.Node.GetHelpText().Contains("이름을 확인하세요.", StringComparison.Ordinal),
            "field validation retains editor identity and publishes its error text");
        var right = Find(UiAccessibilityRole.Button, "지우기").Node.Bounds.X;
        field.InputOptions = field.InputOptions with { RightToLeft = true };
        Frame();
        check(Find(UiAccessibilityRole.Button, "지우기").Node.Bounds.X < right, "RTL moves the clear slot to the leading physical edge");
        field.InputOptions = field.InputOptions with { ReadOnly = true };
        Frame();
        check(!HasClear() && Find(UiAccessibilityRole.Edit, "이름").Node.ReadOnly && text.Value == "디모이",
            "read-only TextField retains its value and omits the clear action");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}

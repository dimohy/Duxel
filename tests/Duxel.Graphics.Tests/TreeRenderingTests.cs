using Duxel.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class TreeRenderingTests
{
    internal static void RunMode(string output, string mode, int slots)
    {
        Directory.CreateDirectory(output);
        var passed = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
            Console.WriteLine("PASS " + name);
            passed.Add(name);
        }

        if (mode == "tree-label-gpu")
            RunLabels(output, Check, slots);
        else
            Run(output, Check, slots);
        using var result = File.Create(Path.Combine(output, "result.json"));
        JsonSerializer.Serialize(result, new TreePixelResult(Environment.Version.ToString(), mode, slots, passed.Count, passed.ToArray()),
            TreePixelJson.Default.TreePixelResult);
    }

    private static void RunLabels(string output, Action<bool, string> check, int slots)
    {
        using var fixture = new PresentationReuseTests.Fixture(slots, true, PresentationTestPlatform.Create(640, 420));
        using var accessibility = new UiAccessibilityTree();
        fixture.Context.SetAccessibilityTree(accessibility);
        const string name = "디모이의 프로젝트 문서와 English file names, 결합 문자 e\u0301";
        UiTreeItem[] items = [new("root", null, "프로젝트"), new("long", "root", name), new("short", "root", "README.md")];
        using var tree = new UiTree<UiFluentTreePresentation>("Plain labels", new UiState<string[]>(["root"]).Binding,
            new UiState<string?>("short").Binding, items, new(Width: 300));
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 16);
            var context = new UiControlContext(ui);
            context.Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), context.Theme.WindowBg, default, 0, 0);
            tree.Render(ui);
        }
        foreach (var small in new[] { false, true })
        {
            fixture.Context.SetDesign(small ? UiCompiledDesign.Windows11 : UiCompiledDesign.Windows11Dark);
            foreach (var overflow in Enum.GetValues<UiTreeTextOverflow>())
            {
                tree.Options = tree.Options with { Size = small ? UiTreeSize.Small : UiTreeSize.Medium, RightToLeft = small, TextOverflow = overflow };
                var pixels = fixture.Capture(Draw);
                var label = (UiFluentTreeLabel)items[1].PlainLabel!.Label;
                var row = accessibility.CaptureSnapshot().Elements.Single(item => item.Node.Role == UiAccessibilityRole.TreeItem && item.Node.Name == name);
                check(overflow == UiTreeTextOverflow.Wrap ? label.Lines.Count > 1 && row.Node.Bounds.Height > (small ? 24 : 32)
                    : label.IsTruncated && label.Lines.Count == 1 && row.Node.Bounds.Height == (small ? 24 : 32),
                    $"GPU plain label: {(small ? "small light RTL" : "medium dark LTR")} {overflow}");
                fixture.WriteCapturePng(Path.Combine(output, $"labels-{(small ? "small-light-rtl" : "medium-dark-ltr")}-{overflow}.png"), pixels);
            }
        }
    }

    private static void Run(string output, Action<bool, string> check, int slots)
    {
        using var fixture = new PresentationReuseTests.Fixture(slots, true, PresentationTestPlatform.Create(640, 420));
        using var accessibility = new UiAccessibilityTree(); fixture.Context.SetAccessibilityTree(accessibility);
        var expanded = new UiState<string[]>(["project", "src"]);
        var checkedLeaves = new UiState<string[]>(["core"]);
        UiTreeItem[] Items() => [new("project", null, "Duxel 소스", Fluent.Body1("Duxel 소스")),
            new("src", "project", "src", Fluent.Body1("src")),
            new("core", "src", "Duxel.Core", Dux.Column(new(Spacing : 0, MainAxisSize : UiMainAxisSize.Min), Fluent.Body1("Duxel.Core"), Fluent.Caption1("위젯과 배치"))),
            new("app", "src", "Duxel.App", Fluent.Body1("Duxel.App")),
            new("readme", "project", "README.md", Fluent.Body1("README.md")),
            new("assets", null, "Assets", Fluent.Body1("Assets"))];
        using var dark = new UiTreeWidget("Source hierarchy", expanded.Binding, Items(), multiple : checkedLeaves.Binding, options : new(Width : 360));
        using var light = new UiTreeWidget("RTL hierarchy", new UiState<string[]>(["project", "src"]).Binding, Items(),
            multiple : new UiState<string[]>(["core"]).Binding, options : new(Size : UiTreeSize.Small, RightToLeft : true, Width : 280));
        UiTreeWidget current = dark;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 16);
            new UiControlContext(ui).Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                new UiControlContext(ui).Theme.WindowBg, default, 0, 0);
            current.Render(ui);
        }
        UiAccessibilityElement Find(string name) => accessibility.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.TreeItem && e.Node.Name == name);
        void Click(UiVector2 point)
        {
            fixture.Capture(Draw, new(point, true, true, false, false, false, false, 0, 0, [], [], default, default));
            fixture.Capture(Draw, new(point, false, false, true, false, false, false, 0, 0, [], [], default, default));
            fixture.Capture(Draw);
        }
        fixture.Context.SetDesign(UiCompiledDesign.Windows11Dark);
        var pixels = fixture.Capture(Draw);
        check(Find("Duxel 소스").Node.ToggleState == UiAccessibilityToggleState.Indeterminate
            && Find("src").Node.ToggleState == UiAccessibilityToggleState.Indeterminate
            && Find("Duxel.Core").Node.ToggleState == UiAccessibilityToggleState.On,
            "GPU Tree publishes mixed ancestors and checked terminal state alongside retained row content");
        check(Find("Duxel.Core").Node.Bounds.Height > Find("Duxel.App").Node.Bounds.Height
            && Find("Duxel.Core").Node.ParentId == Find("src").Node.Id,
            "Tree measures arbitrary two-line row content while preserving the actual hierarchy");
        fixture.WriteCapturePng(Path.Combine(output, "dark-mixed-tree.png"), pixels);
        var root = Find("Duxel 소스").Node.Bounds;
        Click(new(root.X + 36, root.Y + root.Height / 2));
        check(checkedLeaves.Value.SequenceEqual(["core", "app", "readme"])
            && Find("Duxel 소스").Node.Expansion == UiAccessibilityExpansionState.Expanded,
            "actual Tree checkbox hit toggles the subtree without collapsing its branch");
        Click(new(root.X + 12, root.Y + root.Height / 2));
        check(!accessibility.CaptureSnapshot().Elements.Any(e => e.Node.Role == UiAccessibilityRole.TreeItem && e.Node.Name == "src")
            && checkedLeaves.Value.Length == 3,
            "actual Tree chevron hit collapses descendants while preserving their checks");
        current = light; fixture.Context.SetDesign(UiCompiledDesign.Windows11);
        pixels = fixture.Capture(Draw);
        root = Find("Duxel 소스").Node.Bounds;
        check(root.Width == 280 && Find("Duxel.App").Node.Bounds.Height == 24,
            "small RTL Tree renders with constrained width and24px simple rows");
        var labels = accessibility.CaptureSnapshot().Elements.Where(e => e.Node.Role == UiAccessibilityRole.Label).ToArray();
        var rootText = labels.Single(e => e.Node.Name == "Duxel 소스").Node.Bounds;
        var sourceText = labels.Single(e => e.Node.Name == "src").Node.Bounds;
        check(rootText.X + rootText.Width > sourceText.X + sourceText.Width,
            "RTL Tree aligns intrinsic content to logical start so text indentation follows the mirrored hierarchy");
        fixture.WriteCapturePng(Path.Combine(output, "light-small-rtl-tree.png"), pixels);
        Click(new(root.X + root.Width - 12, root.Y + root.Height / 2));
        check(Find("Duxel 소스").Node.Expansion == UiAccessibilityExpansionState.Collapsed,
            "RTL Tree chevron remains clickable at its mirrored physical location");
        current = dark;
        expanded.Value = ["project", "src"];
        foreach (var lightTheme in new[] { false, true })
        {
            fixture.Context.SetDesign(lightTheme ? UiCompiledDesign.Windows11 : UiCompiledDesign.Windows11Dark);
            var samples = new List<uint>();
            foreach (var appearance in Enum.GetValues<UiTreeAppearance>())
            {
                current.Options = current.Options with { Appearance = appearance };
                fixture.Capture(Draw);
                var row = Find("Duxel 소스").Node.Bounds;
                var pointer = new UiVector2(row.X + row.Width - 8, row.Y + row.Height / 2);
                pixels = fixture.Capture(Draw, new(pointer, false, false, false, false, false, false, 0, 0, [], [], default, default));
                var pixelIndex = ((int)(row.Y + 5) * fixture.LastCaptureSize.Width + (int)(row.X + row.Width - 8)) * 4;
                samples.Add(BitConverter.ToUInt32(pixels, pixelIndex));
                fixture.WriteCapturePng(Path.Combine(output, $"{(lightTheme ? "light" : "dark")}-{appearance}-hover.png"), pixels);
            }
            check(samples.Distinct().Count() == 3,
                $"{(lightTheme ? "light" : "dark")} Tree renders distinct subtle/alpha/transparent hover backgrounds on the actual GPU");
        }
    }
}

internal sealed record TreePixelResult(string Runtime, string Mode, int FrameSlots, int Checks, string[] PassedChecks);

[JsonSerializable(typeof(TreePixelResult))]
internal partial class TreePixelJson : JsonSerializerContext;

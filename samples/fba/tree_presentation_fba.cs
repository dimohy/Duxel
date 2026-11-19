// FBA: controlled Tree expansion/selection with replaceable presentation and live options.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

if (args.Length != 0 && (args.Length != 2 || args[0] != "--verify"))
    throw new ArgumentException("Use no arguments, or --verify <fresh output directory>.");
var verification = args.Length == 2;
var output = verification ? Path.GetFullPath(args[1]) : null;
if (output is not null && Directory.Exists(output)) throw new ArgumentException("Verification output must be fresh.");
var session = new DuxelAppSession();
var verified = false;
var frames = 0;
var rootCount = 0;
var rowCount = 0;
var expanded = new UiState<string[]>(["project", "src"]);
var openedFiles = 0;
var selected = new UiState<string?>("readme");
var checkedLeaves = new UiState<string[]>(["core"]);
UiTreeItem[] Items(bool withActions = false) => [
    new("project", null, "Duxel"),
    new("src", "project", "소스"),
    new("core", "src", "Duxel.Core", Dux.Column(new(Spacing : 0, MainAxisSize : UiMainAxisSize.Min),
        Fluent.Body1("Duxel.Core"), Fluent.Caption1("공통 위젯과 배치"))),
    withActions
        ? new("app", "src", "Duxel.App", Dux.Row(new(Spacing : 8),
            Fluent.Body1("Duxel.App"), Fluent.Button("열기", () => openedFiles++)))
        : new("app", "src", "Duxel.App"),
    new("readme", "project", "디모이 프로젝트를 시작하기 전에 읽어 볼 README.md"),
    new("assets", null, "이미지")
];
using var navigation = new UiTree<UiFluentTreePresentation>("프로젝트 탐색", expanded.Binding, selected.Binding,
    Items(withActions : true), new UiTreeOptions(Navigation : true, Width : 360) { RowActions = true });
using var multiple = new UiTree<UiFluentTreePresentation>("작업할 파일", new UiState<string[]>(["project", "src"]).Binding,
    checkedLeaves.Binding, Items(), new(Width : 360));
using var body = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
    Fluent.Title1("Tree"),
    Fluent.Body1("화살표로 계층을 이동하고 이름을 입력해 보세요. 초점과 선택은 별도로 유지됩니다."),
    Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
        Fluent.Button("크기 전환", () =>
        {
            var size = navigation.Options.Size == UiTreeSize.Medium ? UiTreeSize.Small : UiTreeSize.Medium;
            navigation.Options = navigation.Options with { Size = size };
            multiple.Options = multiple.Options with { Size = size };
        }),
        Fluent.Button("배경 표현 전환", () =>
        {
            var appearance = (UiTreeAppearance)(((int)navigation.Options.Appearance + 1) % 3);
            navigation.Options = navigation.Options with { Appearance = appearance };
            multiple.Options = multiple.Options with { Appearance = appearance };
        }),
        Fluent.Button("줄바꿈·말줄임 전환", () =>
        {
            var overflow = navigation.Options.TextOverflow == UiTreeTextOverflow.Wrap ? UiTreeTextOverflow.Ellipsis : UiTreeTextOverflow.Wrap;
            navigation.Options = navigation.Options with { TextOverflow = overflow };
            multiple.Options = multiple.Options with { TextOverflow = overflow };
        }),
        Fluent.Button("방향 전환", () =>
        {
            var rtl = !navigation.Options.RightToLeft;
            navigation.Options = navigation.Options with { RightToLeft = rtl };
            multiple.Options = multiple.Options with { RightToLeft = rtl };
        })),
    Fluent.Subtitle1("단일 선택과 탐색"),
    Dux.ScrollView(navigation, new(Height : 220)),
    Fluent.Body1("Duxel.App 행에서 F2를 누르면 열기 버튼으로 이동합니다. Escape로 행에 돌아옵니다."),
    Fluent.Body1(() => $"선택 : {selected.Value ?? "없음"} / 열기 : {openedFiles}회"),
    Fluent.Subtitle1("하위 항목 함께 선택"),
    Dux.ScrollView(multiple, new(Height : 220)),
    Fluent.Body1(() => $"선택한 말단 항목 : {string.Join(", ", checkedLeaves.Value)}"));
using var viewport = Dux.ScrollView(body);
var options = DuxelApp.Options<TreeDesign>(Dux.App(Dux.View(ui =>
{
    ui.EnableRootViewportContentLayout(contentPadding : 20);
    viewport.Render(ui);
    if (!verification) return;
    frames++;
    if (frames == 1)
    {
        navigation.Options = navigation.Options with { Size = UiTreeSize.Small };
        multiple.Options = multiple.Options with { Size = UiTreeSize.Small };
    }
    else if (frames == 2)
    {
        navigation.Options = navigation.Options with { Appearance = UiTreeAppearance.SubtleAlpha, RightToLeft = true };
        multiple.Options = multiple.Options with { Appearance = UiTreeAppearance.SubtleAlpha, RightToLeft = true };
    }
    else if (frames >= 4)
    {
        var snapshot = ui.AccessibilityTree?.CaptureSnapshot()
            ?? throw new InvalidOperationException("Tree sample verification requires the platform accessibility tree.");
        rootCount = snapshot.Elements.Count(item => item.Node.Role is UiAccessibilityRole.Tree or UiAccessibilityRole.TreeGrid);
        rowCount = snapshot.Elements.Count(item => item.Node.Role is UiAccessibilityRole.TreeItem or UiAccessibilityRole.TreeGridRow);
        var action = snapshot.Elements.Single(item => item.Node.Role == UiAccessibilityRole.Button && item.Node.Name == "열기");
        var actionCell = snapshot.Elements.Single(item => item.Node.Id == action.Node.ParentId);
        var actionParent = snapshot.Elements.Single(item => item.Node.Id == actionCell.Node.ParentId);
        var grid = snapshot.Elements.Single(item => item.Node.Role == UiAccessibilityRole.TreeGrid);
        if (rootCount != 2 || rowCount != 12 || selected.Value != "readme" || !checkedLeaves.Value.SequenceEqual(["core"])
            || actionParent.Node.Role != UiAccessibilityRole.TreeGridRow || actionParent.Node.Name != "Duxel.App"
            || grid.Node.Grid != new UiAccessibilityGridInfo(6, 1) || actionCell.Node.GridItem?.GridId != grid.Node.Id)
            throw new InvalidOperationException("Tree sample did not retain its hierarchy, action ownership and bindings across option changes.");
        verified = true;
        session.Exit();
    }
})), title : "Duxel / Tree", width : 900, height : 740, vsync : false);
DuxelWindowsApp.Run(options with
{
    Frame = options.Frame with
    {
        EnableIdleFrameSkip = true, UseEventDrivenPlatformLoop = true,
        IsAnimationActiveProvider = () => verification && !verified
    }
}, session);
if (output is not null)
{
    if (!verified) throw new InvalidOperationException("Tree sample verification did not finish.");
    Directory.CreateDirectory(output);
    File.WriteAllText(Path.Combine(output, "result.json"),
        FormattableString.Invariant($"{{\"completed\":true,\"frames\":{frames},\"treeRoots\":{rootCount},\"treeItems\":{rowCount},\"bindingsPreserved\":true,\"actionOwnershipPreserved\":true,\"treeGridCells\":6}}"));
}

public readonly struct TreeDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

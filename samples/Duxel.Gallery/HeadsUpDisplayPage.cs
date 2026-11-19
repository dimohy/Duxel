using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class HeadsUpDisplayPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public string Title => "HeadsUpDisplay";
    public string Description => "작업 상태를 화면 중앙에 표시합니다. 진행 표시는 눌러서 닫고, 성공·실패 표시는 1초 후 닫힙니다.";
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();
    private sealed class Content
    {
        internal List<IGallerySample> Samples { get; } = [];
        internal UiWidget Root { get; }
        internal Content()
        {
            var previews = new List<UiHeadsUpDisplayContent>();
            var examples = new List<UiWidget>();
            foreach (var kind in Enum.GetValues<UiHeadsUpDisplayKind>())
            {
                var options = new UiHeadsUpDisplayContentOptions(kind, kind switch
                {
                    UiHeadsUpDisplayKind.Activity => "저장 중",
                    UiHeadsUpDisplayKind.Success => "저장 완료",
                    UiHeadsUpDisplayKind.Failure => "저장 실패",
                    UiHeadsUpDisplayKind.Custom => "사용자 아이콘",
                    _ => throw new ArgumentOutOfRangeException(nameof(kind))
                }, kind == UiHeadsUpDisplayKind.Custom ? UiMenuIcon.Create<StatusIcon, StatusIcon>() : default);
                var sample = GallerySample.Create(() => new UiHeadsUpDisplayContent(options));
                previews.Add(sample.Preview);
                sample.BindSource(() => sample.Preview.Options, state =>
                    "var options = new UiHeadsUpDisplayContentOptions(\n    Kind : UiHeadsUpDisplayKind." + state.Kind
                    + ",\n    Label : " + GallerySample.String(state.Label)
                    + ",\n    ReducedMotion : " + GallerySample.Boolean(state.ReducedMotion)
                    + (state.Kind == UiHeadsUpDisplayKind.Custom ? ",\n    Icon : UiMenuIcon.Create<StatusIcon, StatusIcon>()" : string.Empty)
                    + ");\nvar content = new UiHeadsUpDisplayContent(options);"
                    + (state.Kind == UiHeadsUpDisplayKind.Custom ? "\n\n" + IconSource : string.Empty));
                Samples.Add(sample);
                examples.Add(Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min), Fluent.Subtitle2(kind.ToString()), sample));
            }
            var portalSample = GallerySample.Create(() => Fluent.HeadsUpDisplay(new(Label : "저장 중")));
            var portal = portalSample.Preview;
            portal.Content.Tapped = () => portal.Hide();
            portalSample.BindSource(() => (Content : portal.Content.Options, Options : portal.Options), state =>
                "var content = new UiHeadsUpDisplayContentOptions(\n    Kind : UiHeadsUpDisplayKind." + state.Content.Kind
                + ", Label : " + GallerySample.String(state.Content.Label) + ");\n"
                + "var options = new UiHeadsUpDisplayOptions(\n    IsBlocking : " + GallerySample.Boolean(state.Options.IsBlocking)
                + ", IsPersistent : " + GallerySample.Boolean(state.Options.IsPersistent)
                + ",\n    ReducedMotion : " + GallerySample.Boolean(state.Options.ReducedMotion)
                + ", KeyboardHeight : " + state.Options.KeyboardHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ");\nvar hud = Fluent.HeadsUpDisplay(content, options);\nhud.Content.Tapped = () => hud.Hide();\n"
                + "var show = Fluent.Button(\"표시\", hud.Show);\nvar example = Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min), show, hud);");
            Samples.Insert(0, portalSample);
            examples.Insert(0, Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Subtitle2("화면 중앙에 표시"),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button("진행 표시", () =>
                    {
                        portal.Content.Options = new(Label : "저장 중");
                        portal.Options = portal.Options with { IsPersistent = true };
                        portal.Show();
                    }),
                    Fluent.Button("성공 표시", () => portal.ShowSuccess("저장 완료")),
                    Fluent.Button("실패 표시", () => portal.ShowFailure("저장 실패")),
                    Fluent.Button("HUD 입력 차단 전환", () => portal.Options = portal.Options with { IsBlocking = !portal.Options.IsBlocking }),
                    Fluent.Button("화면 키보드 영역 모의", () => portal.Options = portal.Options with
                    { KeyboardHeight = portal.Options.KeyboardHeight == 0 ? 200 : 0 })),
                portalSample));
            Root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Body1("화면 중앙 표시와 각 상태의 모양을 비교하세요. 화면 키보드 영역 모의는 실제 키보드를 띄우지 않고 아래쪽 영역을 비워 배치를 확인합니다."),
                Fluent.Button("HUD 모션 줄이기", () =>
                {
                    portal.Options = portal.Options with { ReducedMotion = !portal.Options.ReducedMotion };
                    previews.ForEach(preview => preview.Options = preview.Options with { ReducedMotion = portal.Options.ReducedMotion });
                }),
                Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min), examples.ToArray()));
        }
    }
    private readonly struct StatusIcon : IUiButtonIcon
    {
        public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction) =>
            context.Circle(new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), 12, color);
    }
    private const string IconSource = """
        readonly struct StatusIcon : IUiButtonIcon
        {
            public static void Paint(UiControlContext context, UiRect bounds, UiColor color,
                UiButtonInteraction interaction) =>
                context.Circle(new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), 12, color);
        }
        """;
}

using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class TooltipPage : UiWidget, IGallerySourcePage
{
    private readonly UiWidget _root;
    private readonly UiFluentTooltip _controlled;
    internal UiFluentTooltip Controlled => _controlled;
    public IReadOnlyList<IGallerySample> Samples { get; }

    internal TooltipPage()
    {
        var normal = GallerySample.Create(() => Fluent.Tooltip(Fluent.Button("설명 대상"),
            "대상에 포인터를 올리거나 키보드로 포커스를 옮겨 보세요."));
        var arrow = GallerySample.Create(() => Fluent.Tooltip(Fluent.Button("화살표 대상"), "화살표가 있는 설명입니다.", new()
        {
            Positioning = new()
            {
                ArrowHeight = 6
            }
        }));
        var label = GallerySample.Create(() => Fluent.Tooltip(Fluent.Button("?"), "세부 정보", new()
        {
            Relationship = UiTooltipRelationship.Label
        }));
        var controlledDefinition = GallerySample.Define("controlled", () =>
        {
            var tooltip = Fluent.Tooltip(Fluent.Button("제어 대상"), "코드에서 표시 여부를 직접 정하는 Tooltip입니다.");
            tooltip.Visible = false;
            tooltip.VisibleChangeRequested = change => tooltip.Visible = change.Visible;
            return tooltip;
        });
        var controlled = _controlled = controlledDefinition.Value;
        var manual = GallerySample.Create(() => Dux.Wrap(new(Spacing : 24, RunSpacing : 12, CrossAlignment : UiWrapCrossAlignment.Center),
            controlled, Fluent.Button("Tooltip 표시 전환", () => controlled.Visible = controlled.Visible != true)));
        var manualFactory = manual.Source;
        manual.BindSource(() => controlled.Visible, visible => controlledDefinition.Source
            + $"\ncontrolled.Visible = {GallerySample.Boolean(visible is true)};\n\n" + manualFactory);
        Samples = [normal, arrow, label, manual];
        _root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1("마우스를 올리거나 Tab으로 포커스를 옮겨 잠시 뒤 설명이 나타나는지 확인하세요. Escape를 누르면 닫힙니다."),
            Fluent.Subtitle2("자동 설명"), normal,
            Fluent.Subtitle2("화살표가 있는 설명"), arrow,
            Fluent.Subtitle2("대상의 접근성 이름"),
            Fluent.Caption1("설명 모드에서는 대상의 HelpText를, 레이블 모드에서는 접근성 이름인 Name을 제공합니다."), label,
            Fluent.Subtitle2("표시 상태 직접 제어"), manual);
        OwnChild("root", _root);
    }
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
}

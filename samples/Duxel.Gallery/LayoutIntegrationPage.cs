using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class LayoutIntegrationPage : UiWidget, IGallerySourcePage
{
    private readonly UiWidget _root;
    public IReadOnlyList<IGallerySample> Samples { get; }
    internal LayoutIntegrationPage()
    {
        var tabs = new TabOverflowExamples();
        var overflow = new OverflowExamples();
        Samples = [.. tabs.Samples, .. overflow.Samples];
        _root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1("탭과 메뉴를 함께 배치해 보세요. 공간이 부족할 때 항목을 어떻게 숨기는지, 숨겼다 다시 표시해도 상태가 유지되는지 확인할 수 있습니다."), tabs, overflow);
        OwnChild("root", _root);
    }
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
}

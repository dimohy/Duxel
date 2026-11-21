using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ShimmerPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public string Title => "Shimmer";
    public string Description => "Show a placeholder while loading and transition to the actual content when it is ready.";
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();
    private sealed class Content
    {
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal UiWidget Root { get; }
        internal Content()
        {
            var placeholder = Fluent.Skeleton(Dux.Row(new(Spacing : 12),
                Fluent.SkeletonItem(new(Shape : UiSkeletonShape.Circle, Size : 40)),
                Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.SkeletonItem(new(Width : 220)), Fluent.SkeletonItem(new(Size : 12, Width : 160)))));
            placeholder.AccessibleName = "프로필을 불러오는 중";
            var sample = GallerySample.Create(() => Fluent.Shimmer(
                Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Subtitle1("디모이"), Fluent.Body1("프로필이 준비되었습니다."), Fluent.Button("프로필 열기")), placeholder));
            var preview = sample.Preview;
            var factory = sample.Source.Replace("UiWidget example =", "var example =", StringComparison.Ordinal);
            sample.BindSource(() => (preview.IsDataLoaded, preview.Options.ReducedMotion), state =>
                "var placeholder = Fluent.Skeleton(Dux.Row(new(Spacing : 12),\n"
                + "    Fluent.SkeletonItem(new(Shape : UiSkeletonShape.Circle, Size : 40)),\n"
                + "    Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),\n"
                + "        Fluent.SkeletonItem(new(Width : 220)), Fluent.SkeletonItem(new(Size : 12, Width : 160)))));\n"
                + "placeholder.AccessibleName = \"프로필을 불러오는 중\";\n\n" + factory
                + "\nexample.IsDataLoaded = " + GallerySample.Boolean(state.IsDataLoaded) + ";"
                + "\nexample.Options = new(ReducedMotion : " + GallerySample.Boolean(state.ReducedMotion) + ");");
            Samples = [sample];
            Root = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button("불러오기 상태 전환", () => preview.IsDataLoaded = !preview.IsDataLoaded),
                    Fluent.Button("모션 줄이기 전환", () => preview.Options = preview.Options with { ReducedMotion = !preview.Options.ReducedMotion })),
                Fluent.Body1("불러오기 중에는 실제 내용이 입력과 탭 이동에 참여하지 않습니다. 준비가 끝나면 자리 표시자의 애니메이션도 멈춥니다."), sample);
        }
    }
}

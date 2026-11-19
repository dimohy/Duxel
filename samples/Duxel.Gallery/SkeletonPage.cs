using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class SkeletonPage : UiWidget, IGallerySourcePage
{
    private readonly List<IGallerySample> _samples = [];
    public IReadOnlyList<IGallerySample> Samples => _samples;
    private readonly UiState<bool> _loading = new(true);
    private readonly UiState<bool> _pulse = new(false);
    private readonly UiState<bool> _translucent = new(false);
    private readonly UiState<bool> _reduced = new(false);
    private readonly UiState<bool> _paused = new(false);

    protected override IUiView Build(UiBuildContext context)
    {
        _samples.Clear();
        var loading = context.Watch(_loading);
        var pulse = context.Watch(_pulse);
        var translucent = context.Watch(_translucent);
        var reduced = context.Watch(_reduced);
        var paused = context.Watch(_paused);
        var previewDefinition = GallerySample.Define<Func<int, UiWidget>>("preview", () => (int size) =>
        {
            var item = Fluent.Skeleton(Dux.Row(new(Spacing : 16, CrossAxisAlignment : UiCrossAxisAlignment.Center),
                Fluent.SkeletonItem(new(Size : size, Shape : UiSkeletonShape.Circle)),
                Dux.Expanded(Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.SkeletonItem(), Fluent.SkeletonItem(new(Width : 180)),
                    Fluent.SkeletonItem(new(Size : 12, Width : 120))))),
                new(Animation : pulse ? UiSkeletonAnimation.Pulse : UiSkeletonAnimation.Wave,
                    Appearance : translucent ? UiSkeletonAppearance.Translucent : UiSkeletonAppearance.Opaque,
                    ReducedMotion : reduced, Paused : paused));
            item.AccessibleName = "프로필 불러오는 중";
            return item;
        });
        var preview = previewDefinition.Value;
        string Source(string factory) => $"var loading = {GallerySample.Boolean(loading)};\nvar pulse = {GallerySample.Boolean(pulse)};\n"
            + $"var translucent = {GallerySample.Boolean(translucent)};\nvar reduced = {GallerySample.Boolean(reduced)};\nvar paused = {GallerySample.Boolean(paused)};\n\n"
            + previewDefinition.Source + "\n\n" + factory;
        var small = GallerySample.Create<UiWidget>(() => loading ? preview(48) : Fluent.Body1("디모이 · 프로필을 불러왔습니다."));
        var smallFactory = Source(small.Source);
        small.BindSource(() => smallFactory, value => value);
        var large = GallerySample.Create<UiWidget>(() => loading ? preview(96) : Fluent.Body1("콘텐츠를 조작할 수 있습니다."));
        var largeFactory = Source(large.Source);
        large.BindSource(() => largeFactory, value => value);
        _samples.AddRange([small, large]);
        return Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1("로딩할 콘텐츠의 구조를 미리 표시합니다. 화면 밖에 있거나 정지한 항목은 애니메이션을 위해 프레임을 요청하지 않습니다."),
            Dux.Wrap(new(Spacing : 16, RunSpacing : 8),
                Fluent.Switch("로딩 중", _loading.Binding), Fluent.Switch("펄스", _pulse.Binding),
                Fluent.Switch("반투명", _translucent.Binding), Fluent.Switch("모션 줄이기", _reduced.Binding),
                Fluent.Switch("애니메이션 정지", _paused.Binding)),
            small,
            large,
            Fluent.Caption1("함께 표시되는 항목은 같은 타이밍으로 움직입니다. 이전 Linux 성능 검사 결과는 별도로 보관하고 있습니다."));
    }
}

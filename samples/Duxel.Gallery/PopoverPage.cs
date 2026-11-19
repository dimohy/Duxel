using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class PopoverPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    public string Title => "Popover";
    public string Description => "팝업 안에 원하는 위젯을 넣을 수 있습니다. 내용을 입력하고 포커스를 옮기거나 팝업을 닫았다 다시 열어 보세요.";
    internal UiFluentPopover? Preview => _content?.Preview;
    internal string? ModelName => _content?.ModelName;
    internal UiPopover<GalleryPopoverDecoration>? DecorationPreview => _content?.DecorationPreview;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Dispose();

    private sealed class Content : IDisposable
    {
        private string _name = "디모이";
        internal string ModelName => _name;
        private int _count;
        private bool _open, _rejectClose;
        internal UiFluentPopover Preview
        {
            get;
        }
        internal UiWidget Root
        {
            get;
        }
        internal UiPopover<GalleryPopoverDecoration> DecorationPreview
        {
            get;
        }
        internal IReadOnlyList<IGallerySample> Samples { get; }
        private string TriggerText => Preview.Options.TriggerMode switch
        {
            UiPopoverTriggerMode.Click => "클릭",
            UiPopoverTriggerMode.Hover => "호버",
            UiPopoverTriggerMode.Context => "우클릭",
            _ => throw new InvalidOperationException("Unknown Popover trigger mode.")
        };
        internal Content()
        {
            var options = new UiPopoverOptions(Width : 360);
            var sample = GallerySample.Create(() =>
            {
                UiFluentPopover? owner = null;
                var input = Fluent.Input("팝업 이름", new UiBinding<string>(() => _name, value => _name = value), 64, 280);
                owner = Fluent.Popover("프로필 편집 열기", Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Subtitle2("프로필 편집"), Fluent.Label("이름", input), input,
                    Fluent.Button("팝업 명령 실행", () => _count++),
                    Fluent.Menu("팝업 내부 메뉴", [new("execute", "메뉴 명령 실행")], _ => _count++),
                    Fluent.Button("프로필 편집 닫기", () => owner!.Close())), options : options);
                owner.Open = new(() => _open, value => { if (value || !_rejectClose) _open = value; });
                return owner;
            });
            Preview = sample.Preview;
            var factory = sample.Source;
            sample.BindSource(() => (_name, _count, _open, _rejectClose, Preview.Options), state =>
                $"var _name = {GallerySample.String(state._name)};\nvar _count = {state._count};\n"
                + $"var _open = {GallerySample.Boolean(state._open)};\nvar _rejectClose = {GallerySample.Boolean(state._rejectClose)};\n"
                + $"var options = {PopoverGallerySource.Options(state.Options)};\n\n" + factory);

            var decorationOptions = new UiPopoverOptions(Width : 280, DisableAutoFocus : true);
            var decorationSample = GallerySample.Create(() =>
            {
                UiPopover<GalleryPopoverDecoration>? decoration = null;
                decoration = new("넓은 장식 팝업 열기", Fluent.Button("장식 팝업 닫기", () => decoration!.Close()), options : decorationOptions);
                return decoration;
            });
            DecorationPreview = decorationSample.Preview;
            var decorationFactory = decorationSample.Source;
            var declaration = GalleryPageSource.Declaration(nameof(GalleryPopoverDecoration));
            decorationSample.BindSource(() => (DecorationPreview.Options, DecorationPreview.IsOpen), state =>
                $"var decorationOptions = {PopoverGallerySource.Options(state.Options)};\n\n" + decorationFactory
                + (state.IsOpen ? "\n((UiPopover<GalleryPopoverDecoration>)example).Show();" : string.Empty)
                + "\n\n" + declaration);
            Samples = [sample, decorationSample];
            Root = Dux.ScrollView(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button("팝업 포커스 제한", () => Preview.Options = Preview.Options with { TrapFocus = !Preview.Options.TrapFocus }),
                    Fluent.Button("포커스 이탈 시 닫기", () => Preview.Options = Preview.Options with { CloseOnFocusLeave = !Preview.Options.CloseOnFocusLeave }),
                    Fluent.Button("팝업 열기 방식", () =>
                    {
                        Preview.Close();
                        var mode = (UiPopoverTriggerMode)(((int)Preview.Options.TriggerMode + 1) % 3);
                        Preview.Options = Preview.Options with
                        {
                            TriggerMode = mode,
                            DisableAutoFocus = mode == UiPopoverTriggerMode.Hover
                        };
                    }),
                    Fluent.Button("팝업 화살표", () => Preview.Options = Preview.Options with { WithArrow = !Preview.Options.WithArrow }),
                    Fluent.Button("팝업 모션", () => Preview.Options = Preview.Options with { MotionEnabled = !Preview.Options.MotionEnabled }),
                    Fluent.Button("팝업 크기", () => Preview.Options = Preview.Options with { Size = (UiPopoverSize)(((int)Preview.Options.Size + 1) % 3) }),
                    Fluent.Button("팝업 외관", () => Preview.Options = Preview.Options with { Appearance = (UiPopoverAppearance)(((int)Preview.Options.Appearance + 1) % 3) }),
                    Fluent.Button("팝업 닫기 거부", () => _rejectClose = !_rejectClose)),
                Fluent.Body1(() => $"이름 : {_name} / 실행 : {_count} / 열림 : {_open} / 열기 방식 : {TriggerText} / 모션 : {Preview.Options.MotionEnabled} / 포커스 제한 : {Preview.Options.TrapFocus} / 포커스 이탈 닫기 : {Preview.Options.CloseOnFocusLeave} / 닫기 거부 : {_rejectClose}"),
                Fluent.Body1("내부 메뉴에서도 명령을 실행해 보세요. Escape는 메뉴부터 닫습니다. 호버 모드에서는 메뉴 위에 머물러도 팝업이 유지됩니다. 닫거나 탭을 왕복해도 입력 내용은 유지됩니다."),
                Fluent.Caption1("기본 설정은 배경도 조작할 수 있는 비모달 팝업입니다. 화살표와 애니메이션을 켜고 크기와 모양을 비교해 보세요. 열기 방식은 클릭, 마우스 올리기, 우클릭 순으로 바뀝니다. 우클릭 모드에서는 Menu 키나 Shift+F10으로도 열 수 있습니다. 나타날 때는 400ms 동안 전환하고 닫을 때는 즉시 사라집니다. Fluent 디자인과 동작에 완전히 맞추는 작업은 진행 중입니다."),
                sample,
                Fluent.Subtitle2("사용자 정의 표면"),
                Fluent.Caption1("본문보다 넓은 장식도 팝업과 함께 움직이며 서서히 나타나거나 사라집니다. 장식의 바깥 부분을 클릭하면 팝업이 닫힙니다."),
                decorationSample));
        }
        public void Dispose() => Root.Dispose();
    }

}

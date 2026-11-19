using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Linux;
using Duxel.Svg;

namespace Duxel.Linux.Sample;

public sealed class LinuxDemoScreen : UiScreen, IDisposable
{
    public const int WindowWidth = 640, WindowHeight = 520;
    private readonly DuxelAppSession _session;
    private readonly UiState<string?> _single = new("seoul");
    private readonly UiState<string[]> _multiple = new(["seoul"]);
    private readonly UiList<UiFluentListPresentation> _singleList, _multipleList;
    public LinuxDemoScreen(DuxelAppSession session)
    {
        _session = session;
        _singleList = Fluent.List("도시 하나", _single.Binding,
        [
            new("seoul", "서울", Fluent.Body1("서울"), selectionLabel: "서울 선택"),
            new("busan", "부산", Fluent.Body1("부산"), selectionLabel: "부산 선택"),
            new("jeju", "제주", Fluent.Body1("제주 (선택 불가)"), disabledSelection: true, selectionLabel: "제주 선택")
        ], new(Width: 280));
        _multipleList = Fluent.List("도시 여러 개", _multiple.Binding,
        [
            new("seoul", "서울", Fluent.Body1("서울"), selectionLabel: "서울 선택"),
            new("busan", "부산", Fluent.Body1("부산"), selectionLabel: "부산 선택"),
            new("jeju", "제주", Fluent.Body1("제주"), selectionLabel: "제주 선택")
        ], new(Width: 280));
    }
    private readonly SvgStore _store = new();
    private UiImageTexture? _bitmap, _svgBitmap;
    private UiSvg? _svg;
    public SvgStoreStatistics ImageStatistics => _store.Statistics;
    public float SvgRotation => _svg?.RotationDegrees ?? 0;
    private string _value = "";
    private int _level = 25;
    private bool _indeterminate;
    private readonly UiProgress<UiFluentProgressBehavior, UiFluentProgressPresentation> _progress = new(25, 100)
    {
        AccessibleName = "진행 상태",
        Size = new(220, 4)
    };
    private int _renderCount, _clicks, _external;
    public int RenderCount => Volatile.Read(ref _renderCount);
    public int Clicks => Volatile.Read(ref _clicks);
    public string Value => Volatile.Read(ref _value);
    private UiAccessibilityTextInfo? _inputTextInfo;
    public UiAccessibilityTextInfo? InputTextInfo => Volatile.Read(ref _inputTextInfo);
    public int ExternalValue => Volatile.Read(ref _external);
    public string FontNameLanguage { get; set; } = "en";

    public void ChangeFromWorker(int value)
    {
        Volatile.Write(ref _external, value);
        _session.RequestFrame();
    }

    public override void Render(UiImmediateContext ui)
    {
        ui.SetAccessibilityFontNameLanguage(FontNameLanguage);
        var frames = Interlocked.Increment(ref _renderCount);
        ui.SetCursorScreenPos(new(20, 20));
        ui.Text("Linux / Duxel — 한글과 English");
        ui.SetCursorScreenPos(new(20, 70));
        if (ui.Button("linux-counter", "클릭 수 : " + Clicks, new(200, 36)))
            Interlocked.Increment(ref _clicks);
        ui.SetCursorScreenPos(new(20, 125));
        ui.SetNextItemWidth(360);
        ui.InputText("입력", ref _value, 256);
        var textInfo = ui.GetAccessibilityTextInfo("입력");
        Volatile.Write(ref _inputTextInfo, textInfo);
        var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
        _bitmap ??= UiImageTexture.LoadFromFile(Path.Combine(assets, "bitmap.png"), new(50_400));
        if (_svgBitmap is null)
        {
            var data = new SvgImageDecoder(_store, LinuxUiImageDecoder.Shared).Decode(Path.Combine(assets, "shapes.svg"));
            _svgBitmap = new(new(50_401), data.Width, data.Height, data.RgbaPixels);
            _svg = new(_store.Load(Path.Combine(assets, "shapes.svg")))
            {
                Size = new(80, 54)
            };
        }
        ui.SetCursorScreenPos(new(440, 45));
        ui.Text("PNG");
        ui.SetCursorScreenPos(new(540, 45));
        ui.Text("SVG");
        _bitmap.Prepare(ui, UiImageEffects.Default);
        _svgBitmap.Prepare(ui, UiImageEffects.Default);
        ui.SetCursorScreenPos(new(440, 75));
        ui.Image(_bitmap.TextureId, new(80, 54));
        ui.SetCursorScreenPos(new(540, 75));
        ui.Image(_svgBitmap.TextureId, new(80, 54));
        ui.SetCursorScreenPos(new(440, 140));
        if (ui.Button("SVG 회전", new(90, 32)))
            _svg!.RotationDegrees = (_svg.RotationDegrees + 15) % 360;
        ui.SetCursorScreenPos(new(540, 140));
        _svg!.Render(ui);
        ui.SetCursorScreenPos(new(20, 185));
        ui.Text("UI 프레임 : " + frames + " / 외부 값 : " + ExternalValue);
        ui.Text("화면 변화가 없으면 렌더링을 쉽니다. V-Sync : OFF");
        ui.Text("입력란에서 Ctrl+A · Ctrl+C · Ctrl+V로 복사와 붙여넣기를 확인하세요.");
        if (ui.Button("종료", new(100, 32)))
            _session.Exit();
        ui.SetCursorScreenPos(new(140, 291));
        ui.SetNextItemWidth(220);
        ui.SliderInt("수준", ref _level, 0, 100);
        ui.SetCursorScreenPos(new(440, 291));
        if (ui.Button("진행률 모드", new(180, 32)))
            _indeterminate = !_indeterminate;
        ui.SetCursorScreenPos(new(140, 337));
        _progress.Value = _indeterminate ? null : _level;
        _progress.Render(ui);
        ui.SetCursorScreenPos(new(20, 360));
        ui.Text("단일 선택");
        ui.SetCursorScreenPos(new(330, 360));
        ui.Text("다중 선택");
        ui.SetCursorScreenPos(new(20, 390));
        _singleList.Render(ui);
        ui.SetCursorScreenPos(new(330, 390));
        _multipleList.Render(ui);
        ui.SetCursorScreenPos(new(20, 490));
        var textSize = ui.GetAccessibilityTextAppearance("입력")?.Size;
        ui.Text(textInfo is null ? "텍스트 상태 : 없음"
            : "문자 : " + textInfo.Content.CharacterCount + " / 커서 : " + textInfo.CaretCharacterOffset
                + " / 선택 : " + textInfo.AnchorCharacterOffset + " → " + textInfo.CaretCharacterOffset
                + (textSize is { } size ? " / 글자 : " + size.Points.ToString(System.Globalization.CultureInfo.InvariantCulture) + " pt" : ""));
    }

    public void Dispose()
    {
        _singleList.Dispose();
        _multipleList.Dispose();
        _progress.Dispose();
        _svg?.Dispose();
        _store.Dispose();
    }
}

# 사용자 정의 위젯

마지막 동기화: 2026-03-09

## 개요

Duxel의 내장 위젯은 모두 `UiImmediateContext` 메서드로 제공됩니다.

사용자 정의 위젯은 `IUiCustomWidget`을 구현하는 인스턴스 기반 객체이며, `Render(UiImmediateContext ui)`에서 현재 `ui`를 받아 스스로 렌더링합니다.

이 방식은 내장 위젯 표면을 늘리지 않으면서도 동일한 즉시모드 프리미티브를 이용해 재사용 가능한 위젯을 만들 수 있게 합니다.

## 공용 API

- `IUiCustomWidget`
- `MarkdownEditorWidget`
- `MarkdownViewerWidget`

## 최소 예제

```csharp
using Duxel.Core;

public sealed class MarkdownScreen : UiScreen
{
    private readonly MarkdownEditorWidget _editor = new("editor", "Markdown")
    {
        Height = 320f,
        Text = "# Hello\n\nThis is **custom** markdown."
    };

    private readonly MarkdownViewerWidget _viewer = new("viewer")
    {
        Height = 320f,
    };

    public override void Render(UiImmediateContext ui)
    {
        ui.BeginWindow("Markdown Demo");

        ui.Columns(2, false);
        _editor.Render(ui);

        ui.NextColumn();
        _viewer.Markdown = _editor.Text;
        _viewer.Render(ui);

        ui.Columns(1, false);
        ui.EndWindow();
    }
}
```

## 마크다운 뷰어 기능

- 제목, 문단, 인용문, fenced code block
- 글머리 목록, 체크리스트, 중첩 번호 목록
- 표
- 링크, 인라인 코드, 기울임, 굵게
- 미리보기 안에서 체크리스트 토글 가능
- 링크 hover 시 대상 URL 툴팁 표시
- 코드 블록 복사 버튼과 클립보드 연동

## 샘플

쇼케이스 샘플은 `samples/fba/all_features.cs`의 `Markdown Studio` 창에서 확인할 수 있으며, 이제 전용 타이포그래피/레이아웃 및 고급 상호작용 쇼케이스 창과 나란히 교차 검증할 수 있습니다.

실행 명령:

```powershell
./run-fba.ps1 samples/fba/all_features.cs -NoCache
```
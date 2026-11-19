# 팝업 입력 모드 — Popover 기반

후속 [Fluent Popover](fluent2-popover.ko.md)가 소유 콘텐츠·자동 포커스·닫기/복귀·수명 관리를 제공합니다. 아래 기반 단계의 미구현 설명은 당시 범위를 기록합니다.

```csharp
if (ui.Button("Open details")) ui.OpenPopup("details");
ui.SetNextPopupPosition(new(300, 100));
if (ui.BeginPopup("details", UiPopupInputMode.NonModal))
{
    try
    {
        ui.Text("Use Tab to leave and Shift+Tab to return.");
        if (ui.Button("Close details")) ui.CloseCurrentPopup();
    }
    finally { ui.EndPopup(); }
}
```

기존 `BeginPopup(string)`은 키보드 독점 동작을 유지합니다. 새 오버로드는 `UiPopupInputMode.Exclusive` 또는 `NonModal`을 선택합니다. 두 모드 모두 팝업 그리기 순서와 가려진 컨트롤의 포인터 차단을 유지합니다. NonModal은 바깥 Tab 정지점과 네이티브 포커스·명령을 허용하고, Exclusive는 현재 독점 팝업의 최상위 층부터 키보드 이동을 허용합니다. 이는 키보드 격리이며 OS 모달 창이나 ARIA dialog 선언이 아닙니다. 기존 공개 `PreviousPopupBlockingRects` 튜플과 두 인수 등록 API의 계약은 유지합니다. 잘못된 입력 모드는 명시적으로 실패합니다.

사각형마다 별도의 소유자·키보드 격리 정보를 등록하고 프레임 전환·초기화·제거를 함께 수행하므로 같은 사각형도 독립적으로 등록할 수 있습니다. 이전 등록은 프레임 시작 입력을, 현재 독점 등록은 최종 Tab 대상의 최소 층을 결정합니다. 그 위의 비모달 층도 이동할 수 있습니다. 모드·배치 변경은 기존의 이전 프레임 입력 모델을 따르며 이미 처리한 컨트롤을 같은 프레임에 동기 재처리한다고 약속하지 않습니다.

`CloseCurrentPopup`은 현재 등록을 제거하고 해당 소유자의 이전 키보드 격리만 해제한 뒤 후속 프레임을 요청합니다. 이전 포인터 사각형은 현재 입력의 클릭 관통을 막는 기준으로 유지합니다. 같은 닫힌 범위에서 닫기를 반복해도 안전합니다. 같은 위치에 있는 다른 팝업의 입력 모드는 유지하며, 독점 팝업을 닫은 다음 프레임에는 추가 대기 없이 바깥 컨트롤을 다시 사용할 수 있습니다.

포커스 그룹에도 층을 적용했습니다. Card 안에서 그린 비모달 포털은 Card의 NoTab 격리나 Escape 처리에 갇히지 않습니다. Card 자체의 Tab 격리와 Enter에 의한 같은 층 자식 이동은 유지합니다. 팝업은 나중에 그린 기본 층 도형보다 위에 표시되며, 그리기 순서와 키보드 격리는 독립적입니다. 기존 버튼·입력·포커스 경로를 공유하고 새 렌더러·셰이더·파서·네이티브 import·타이머를 추가하지 않습니다.

`./run-gallery.ps1` → **팝업 입력**에서 팝업을 연 뒤 안을 클릭하고 Tab·Shift+Tab을 확인하세요. 닫은 뒤 모드를 전환해 바깥 키보드 이동과 실제 안팎 명령을 비교합니다. 샘플은 기존 고정 크기·위치의 범용 팝업 API를 사용하며 모드·카운터를 유지합니다. 닫기 버튼이나 바깥 클릭으로 닫습니다. 현재는 관찰 가능한 엔진 기반이며 **Fluent Popover 자체는 아직 미구현**입니다. 임의 콘텐츠의 소유권·공식 역할/외관/배치·자동 포커스·Escape와 포커스 복귀·제어된 열기·숨긴 소유자의 정리·전체 적합성은 이어서 구현합니다. 범용 팝업에서 NonModal을 선택한 것만으로 이 동작들을 얻지는 않습니다.

근거는 `artifacts/popup-input-2026-11-13/`, 큰 GPU 결과는 `L:/Cache/Duxel/popup-input-2026-11-13-evidence/`입니다. 관리형 검증과 고정 RC3 NativeAOT·런타임 이미지·실기기·성능 검증을 구분합니다. 고정 [Popover 표면 소스](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-popover/library/src/components/PopoverSurface/usePopoverSurface.ts)는 일반 group과 포커스 격리 dialog를 구분하고, [속성 소스](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-popover/library/src/components/Popover/Popover.types.ts)의 `trapFocus` 기본값은 false입니다. 현재 기반을 전체 React 컴포넌트와 동일하게 보지 않습니다.

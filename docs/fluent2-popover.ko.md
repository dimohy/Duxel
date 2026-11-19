# Fluent Popover

```csharp
var name = new UiState<string>("정세일");
var editor = Fluent.Input("Name", name.Binding, 64, 240);
using var popup = Fluent.Popover("Edit profile", editor, options: new(Width: 320, WithArrow: true));
popup.Render(ui);
```

화면 소유자가 위젯을 한 번 생성하고 매 프레임에는 `Render`만 호출합니다. 화면 수명이 끝나면 Popover를 해제합니다.

Popover는 트리거와 임의 `UiWidget` 콘텐츠를 소유합니다. 전달한 콘텐츠를 다른 부모에 재사용하거나 별도로 렌더링하지 않습니다. 기본은 비모달이며, 콘텐츠를 자연 높이로 측정하고 뷰포트 또는 `MaximumHeight`를 초과하면 소유한 ScrollView로 스크롤합니다. `Width`, `Size`(Small/Medium/Large), `Appearance`(Neutral/Inverted/Brand), `Positioning`을 설정할 수 있습니다. 기본 배치는 위쪽 중앙이며 화면 가장자리에서 기존 배치 엔진이 조정합니다.

`Show()`는 다음 렌더링에 열기를 요청합니다. `Close()`는 닫기 수락 여부를 반환합니다. `Open`에 선택적으로 `UiBinding<bool>`을 연결하면 setter 뒤 getter의 권위 값을 읽으므로 열기·닫기 거부를 유지합니다. `IsOpen`은 실제 표면 상태이고, `OpenChanged`는 실제 전환만 알립니다. 외부 모델이 true여도 숨긴 소유자·비활성 소유자의 표면을 계속 게시하지 않습니다. 숨긴 상태에서 바인딩이 true로 유지되면 다시 표시할 때 열 수 있습니다. 콘텐츠의 입력 모델과 위젯 인스턴스는 닫기·탭 왕복에서 유지하고, 소유자 해제 때 함께 해제합니다.

기본 자동 포커스는 첫 이동 가능한 자식으로 갑니다. 이동 가능한 자식이 없는 정보 표면은 자체 포커스를 받습니다. `DisableAutoFocus`로 기존 포커스를 유지할 수 있습니다. 비모달 Tab은 바깥을 오갈 수 있고 `TrapFocus`는 표면 안에서 순환합니다. 표면 내부의 Escape, 바깥 포인터 누름, 선택적 `CloseOnScroll`의 바깥 스크롤로 닫습니다. IME 조합 중 Escape는 조합 경로에 남깁니다. Escape·명시적 닫기는 트리거로 포커스를 복귀시키며 중첩 표면에서는 가장 안쪽 표면부터 닫습니다.

기본 접근성 표면은 `Group`이고 `TrapFocus` 표면은 `Dialog`와 modal 메타데이터를 게시합니다. 트리거는 실제 ExpandCollapse 상태와 표면 관계를 게시하며 일반 자식의 편집·Invoke 계약을 재사용합니다. ARIA dialog의 Windows UIA 매핑은 [Core AAM](https://www.w3.org/TR/core-aam-1.2/)에 따라 Pane입니다. 별도 OS 창이나 Window 패턴을 만들지 않습니다. [Microsoft Pane 규약](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportpanecontroltype)도 참고하세요.

```powershell
./run-gallery.ps1
```

**Popover** 탭(숨겨졌으면 상단 더 보기)에서 프로필 편집을 열고 한글 입력·명령·닫기·포커스 제한·크기·외관·닫기 거부를 확인합니다. 별도 **팝업 입력** 탭은 기존 범용 엔진의 입력 모드 비교입니다. 상단 FPS가 주기 프레임을 요청하지 않습니다. Popover는 진입 모션 동안만 다음 프레임을 예약하고 완료 뒤 이벤트 대기로 돌아갑니다.

표면 외관과 동작 기준은 고정 [Popover 소스](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-popover/library/src/components/Popover/usePopover.ts) 및 같은 커밋의 PopoverSurface입니다. 크기별 패딩12/16/20에 투명 테두리1px, 반경4px, Shadow16과 Body1을 사용합니다. `WithArrow` 기본값은 false이며 켜면 Small은 6px, Medium/Large는 8px 화살표를 사용합니다. 회전 사각형·2px 둥근 끝·투명 테두리 형상을 본문과 합친 뒤 공식 순차 drop-shadow를 적용합니다. 전체 Tabster·강제 색상·물리 스크린리더 적합성은 미완료이며 전체 React 적합성을 주장하지 않습니다.

근거: `artifacts/popover-enter-2026-11-13/verification.json`. 대형 GPU 결과는 `L:/Cache/Duxel/popover-enter-2026-11-13-evidence/`에 보관합니다. 관리형 .NET11 검증과 원래 고정 RC3 NativeAOT·런타임 이미지·실기기 성능 관문을 구분합니다.

소유자별 `UiPopupSurface`가 현재 모양의 래스터 텍스처를 빌립니다. 같은 크기·색·배율·물리 픽셀 위상은 재사용하고 정수 픽셀 이동은 다시 그리지 않습니다. 배율·부분 픽셀 위상·크기·외관·화살표 형상이 바뀔 때 새 래스터를 준비합니다. 숨김·이전·해제는 빌린 자원을 반환하며 공통 캐시는 미사용 항목을 예산 내에서 유지할 수 있습니다. 텍스처 지표는 업로드·상주량이며 GPU 완료나 앱 전체 메모리 측정은 아닙니다. `IUiPopoverPresentation.PaintSurface`는 `UiPopupSurface`와 `UiPopupLayout`을 받아 사용자 표현에도 실제 화살표 배치를 전달합니다.

화살표 크기는 `Size`의 공식 값으로 정하며 `Positioning.ArrowHeight`를 덮어씁니다. 기본 `WithArrow: false`는 화살표를 생략합니다. 자체 닫기 판정은 공유한 화살표 형상을 사용하고, 화살표 배치 사각형도 별도로 아래층 포인터 가림에 등록합니다. 본문과 화살표 등록은 같은 소유자·층에서 함께 해제합니다. [공식 화살표 형상](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-positioning/library/src/createArrowStyles.ts).

`TriggerMode`는 `Click`(기본), `Hover`, `Context` 중 하나입니다. Hover는 트리거 진입 시 즉시 열고 기존 클릭·키보드 토글도 유지합니다. 트리거·표면·중첩 표면 밖으로 나가면 `MouseLeaveDelaySeconds`(기본0.5초) 뒤 닫고, 다시 진입하면 취소합니다. 거부된 열기 요청은 진입마다 한 번, 거부된 지연 닫기는 이탈마다 한 번만 시도하여 과거 시각을 반복 예약하지 않습니다. 모드 변경·숨김·해제와 같은 프레임의 후속 닫기도 예약을 취소합니다. 예약은 기존 `RequestFrameAt` 경로로 프레임 종료에 선언하며 새 스레드·CLR Timer·무한 렌더링 루프를 추가하지 않습니다.

Context는 일반 왼쪽 클릭을 무시하고 완결된 우클릭을 놓은 위치에 가상 점을 둡니다. 트리거에 포커스를 준 뒤 Menu 키 또는 Shift+F10으로 열 수도 있습니다. UIA Invoke·Expand와 `Show`는 접근 가능한 트리거 중앙 점에서 엽니다. 우클릭 모드는 `CloseOnScroll`을 따로 켜지 않아도 바깥 스크롤로 닫습니다. 공유 배치 엔진은 가로·세로가0인 점 대상을 허용하며 본문·경계의 양수 크기 계약은 유지합니다. 음수·비유한 대상은 거부합니다. [Windows VK_APPS](https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes)는 기존 enum 값을 유지한 새 `UiKey.ContextMenu`로 연결합니다.

`CoverTarget`은 화살표를 자동으로 생략합니다. 기본 화살표 여백은 공식 반경4의 두 배인8px입니다. 공식 [트리거](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-popover/library/src/components/PopoverTrigger/usePopoverTrigger.ts)와 같은 커밋의 Popover·PopoverSurface를 기준으로 삼습니다.

관찰 샘플에서 팝업을 닫고 **팝업 열기 방식**을 누르면 클릭→호버→우클릭으로 전환합니다. 호버 데모는 자동 포커스를 꺼서 정적인 본문의 대기 상태를 확인합니다. 입력창을 직접 선택하면 기존 커서 깜박임 예약이 발생할 수 있습니다. CPU는500ms 예약·취소·거부·수명을, GPU는0초 닫기 지연으로 실제 호버/우클릭 픽셀·위치를, 네이티브는 실제 마우스·UIA·Menu 키를 검증합니다. 네이티브 검사 창은 입력 검증용 수동 렌더 루프이며 앱 전체 유휴 CPU/전력 측정으로 보지 않습니다.

소유 메뉴의 포털은 호버 유지와 바깥 클릭 판정에 포함됩니다. 메뉴 위에서는 닫기 예약을 취소하며 메뉴 항목 클릭은 부모 Popover를 닫지 않고 명령을 한 번 실행합니다. Escape는 메뉴부터 닫고 다음 Escape에 부모를 닫습니다. 포커스 제한에서도 열린 메뉴의 트리거 포커스를 유지합니다. 관찰 샘플 → Popover → 팝업 내부 메뉴에서 확인합니다.

`MotionEnabled`의 기본값은 `true`입니다. 공식 진입 모션은 400ms, `cubic-bezier(0,0,0,1)`, 배치 방향에 따른 10px 이동과 전체 표면의 페이드입니다. 닫기는 애니메이션 없이 즉시 적용합니다. `MotionEnabled: false`는 움직임을 줄이거나 정적 표면을 사용할 때 선택합니다. 관찰 샘플의 **팝업 모션**으로 전환할 수 있습니다.

진입 중에도 실제 화면 좌표에서 콘텐츠를 한 번 렌더링하므로 클릭·포커스·IME·접근성 경계가 함께 이동합니다. 겹치는 자식은 기존 이중 배경 합성으로 하나의 그룹처럼 페이드합니다. 움직이는 동안 텍스트는 Projected 모드를 사용하고 정착 뒤 일반 경로로 돌아갑니다. 같은 시점·그리기·참조 텍스처는 그룹 결과를 재사용하며 무관한 텍스처 갱신은 캐시를 무효화하지 않습니다. 본문·화살표는 정착 위치의 래스터를 재사용합니다. 실제 이동이나 콘텐츠 변경으로 그룹 장면을 다시 그릴 수 있으므로 모든 모션이 사각형 하나의 이동만으로 처리된다고 주장하지 않습니다.

모션은 최대 400ms 동안 기존 프레임 예약 경로로 다음 프레임 하나만 예약합니다(60Hz). 종료·숨김·해제·모션 끄기는 남은 예약을 제거하고 임시 합성 자원은 기존 지연 해제 경로로 반환합니다. 소유 입력창을 선택하면 정상적인 커서 깜박임 예약은 유지됩니다. 2D paint-group의 기존 제한(3D 장면·외부 draw callback·가변 GPU particle 소스 미지원, 두 타깃 합계64MiB)을 적용합니다. 해당 콘텐츠는 `MotionEnabled: false`로 일반 경로를 명시적으로 선택합니다.

고정 근거: [PopoverSurfaceMotion](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-popover/library/src/components/Popover/PopoverSurfaceMotion.ts), [시간·곡선 토큰](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-motion/library/src/motions/motionTokens.ts), [물리 방향](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-positioning/library/src/usePositioningSlideDirection.ts).

사용자 정의 표면은 IUiPopoverPresentation.PaintBounds(context, layout, options)로 화면 좌표의 그리기 범위를 선언합니다. 기본 구현은 현재 뷰포트 전체이므로 넓은 장식도 캡처됩니다. 기본 Fluent 표현은 기존88px 그림자 범위를 명시해 작은 타깃을 유지합니다. 명시 범위는 유한한 양수 크기이며 보이는 본문을 포함해야 합니다. 범위와 기존64MiB 타깃 예산은 표면 그리기 전에 검사합니다. 모든 장식이 선언 범위 안에 있도록 구현하는 책임은 표현에 있습니다. 이 범위는 시각적 합성에 사용하며 장식 클릭은 기존 본문 밖 클릭처럼 닫습니다. 관찰 샘플의 넓은 장식 팝업 열기에서 확인합니다. 근거 artifacts/popover-paint-bounds-2026-11-13/verification.json.


`UiPopoverOptions.CloseOnFocusLeave`는 기본 `false`입니다. 켜면 트리거·콘텐츠·중첩 표면 안에서 머물던 포커스가 바깥의 다른 컨트롤로 이동할 때 닫으며, 이동한 포커스를 유지합니다. 자동 포커스를 꺼도 트리거에서 출발한 이탈을 감지합니다. 닫기 바인딩이 거부하면 표면을 유지하고, 영역에 다시 들어왔다가 나갈 때 재시도합니다. 새 Timer나 주기 프레임은 요청하지 않습니다. 관찰 샘플 → Popover → **포커스 이탈 시 닫기**에서 전환합니다. InfoLabel의 공식 InfoButton 동작을 위한 공통 기반이며 InfoLabel 자체의 구현 완료를 뜻하지 않습니다.

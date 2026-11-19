# 생성된 Fluent Slider

Last synced: 2026-11-11

## 수치 접근성 구현과 남은 검증

생성형·제네릭 Slider가 `UiAccessibilityRole.Slider`와 `UiAccessibilityNode.Range` (`UiAccessibilityRangeInfo`)로 논리 값·최솟값/최댓값·작은/페이지 변경량·수직 방향을 게시합니다. Label을 접근성 이름으로 사용하며 AccessibleName·Field 이름의 기존 우선순위를 유지합니다. Windows 공급자는 RangeValue10003·컨트롤 종류50015·수치 속성30047–30052·방향30023을 제공합니다. Slider에 Invoke나 문자열 Value를 광고하지 않습니다.

`UiAccessibilityTree.RequestRangeValue(id, runtimeId, doubleValue, wait)`는 기존 UI 깨우기 경로로 타입 지정 요청을 큐에 넣습니다. 렌더링에서 현재 범위·활성·클립·실행 정체성을 다시 확인하고 같은 Slider 스냅·바인딩 콜백으로 반영합니다. 유한하지 않거나 범위를 벗어난 값은 거부하며 같은 값이면 콜백·이벤트를 추가하지 않습니다. step 미지정은 기존1단위 동작을 유지하고 명시적 step은 Minimum 기준과 마지막 유효 단계 규칙을 따릅니다. 수직·반전 배치여도 논리 범위를 뒤집지 않습니다. `RangeChanged`는 트리 잠금 밖에서 게시가 확정된 불변 노드 상태를 알리며 변화 없는 프레임은 알리지 않습니다.

관리·직접 공급자28개와 엄격한 외부 클라이언트13개/호스트3개 검사가 통과했습니다. 수치 ABI 조회6종·공통 스냅/콜백 순서·동일 값·잘못된 값/비활성 변경·속성 알림·범위/방향 변경·제거 후 보관한 getter 거부를 확인합니다. 제거된 자식 공급자는 UIA 해제에 필요한 고정 COM 설정·실행 정체성·소속 루트·빈 HWND 호스트 연결을 유지하고, 실제 수치 조회와 동작은 거부합니다. HWND 루트의 정보는 호스트 수명에 맞춰 만료되어 비동기 TooltipClosed 스냅샷 수명을 보존합니다. 클라이언트 응답 대기가 없는 최종 Tooltip 종료 회귀도 통과했습니다. 이전 실패 실험은 artifacts/slider-range-2026-11-11에 보존하며 lifetime-verification.json에 수정과 현재 검증을 기록합니다. 발화·기존 Slider 이관·NativeAOT는 별도 미완료 항목입니다.

`./run-gallery.ps1` → Fluent 또는 SVG에서 실제 컨트롤을 확인하세요. SVG 이동은1px 범위를 게시하고 XML·래스터 작업 없이 수치 접근성 이동을 받습니다. 아래의 생성형·기존 Slider 수치 접근성을 검증했습니다. 전체 발화·기존 탭 키보드/플래그 전체 지원·NativeAOT는 남아 있습니다.

`UiSliderOptions.ShowStepMarkers`는 기본 true입니다. false로 지정하면 포인터/방향키의 이산 값 단위를 유지하면서 눈금만 숨깁니다. 관찰 화면 SVG의1px·1°·1%p 조절은 이 옵션을 사용해 촘촘한 눈금이 연속 막대를 덮지 않게 합니다. 두 테마·MSAA1/4에서 눈금 없는 기준 GPU 픽셀과 일치하는지 확인합니다.

`Fluent.Slider(label, binding, minimum=0, maximum=100, step=null)`는 실제 상속 가능한 `UiFluentSlider`를 생성한다. `UiSlider<TBehavior,TPresentation>`은 `UiControlWidget`과 같은 수명·정체성·입력 경로를 사용한다. 사용자는 OnChanged를 상속하거나 IUiSliderBehavior/IUiSliderPresentation을 각각 교체한다. 생성기는 특성의 구체 타입을 닫힌 제네릭으로 직접 연결하며 런타임 구현 사전·리플렉션·테마 분기를 추가하지 않는다.

형식 지정 즉시 API는 `ui.Slider<UiFluentSliderBehavior,UiFluentSliderPresentation>(id, ref value, options, size)`다. Label은 정체성/의미용이며 별도 텍스트를 그리지 않는다. 화면 설명은 Text/Field와 조합한다. 기존 비제네릭 SliderFloat/VSlider와 기존 Dux.Slider 호환 경로의 전체 전환은 남아 있다.

UiSliderOptions는 유한한 min/max·양수 step·크기·수직·반전을 지정한다. step 미지정은 키보드/입력1단위를 사용하고 마커를 그리지 않는다. 명시한 step은 min 기준으로 정렬하며 동일 거리에서는 큰 값을 선택한다. max가 단계에 맞지 않으면 마지막 유효 단계까지 간다. 모델의 범위 밖 값은 표시에서만 클램프하며 사용자 입력의 실제 변경만 한 번 콜백한다.

동작: 트랙 클릭·루트 밖 드래그 캡처, Tab/Shift+Tab 포커스, 화살표·Home/End·PageUp/PageDown, 비활성·제거 취소를 지원한다. Space/Enter를 버튼 활성화로 처리하지 않는다. 변경 후 드래그 해제의 edited 상태를 보존하고 제거된 공통 컨트롤의 캡처를 해제한다. 바인딩 getter는 렌더마다 한 번 실행한다.

표현: small16px thumb/2px rail/24px root, medium20/4/32와 최소 축120px, 수직·반전·단계·hover/pressed/disabled/focus를 그린다. 단계 마커는 현재 클립과 주기를 기준으로 필요한 구간만 제출한다. 간격이1px 이하이면 주기적1px 마커가 덮는 연속 면을 그린다. 분석적 원/둥근 사각형과 기존 레이어·캐시·부분 갱신을 사용한다. 현재 Slider 상태 색상은 아래의 고정 Fluent 토큰으로 교정했으며 전체 재질·브라우저 픽셀 일치·고대비/OS설정·Field/UIA는 미완료다.

공식 기준은 고정 커밋8830c10bfb2f4c1bdd540287b09713b9679cd1ad의 react-slider API와 useSlider/useSliderState/useSliderStyles 원본이다. artifacts/fluent-slider-2026-11-05에 URL·해시와 함께 보존한다. 공식 CSS의 크기/슬롯과 네이티브 range 상태를 대조했다. 실제 지원 이름24개·알려진 요구289개·누락265개는 전체 적합성 분모/지원률이 아니다.

검증: NET10전체995·관리/고정RC3 Native 공통99검사, 기존423이미지 동일, 새Slider5이미지. 상속 콜백·포인터·키보드·드래그 제거·단계·반전·수직·잘못된 입력·사용자 표현과 생성/즉시 GPU 동치를 확인했다. 초기 테스트의 잘못된 원형 버퍼 가정은 실제 음수 반경 사각형 슬롯으로 수정했다. 전체 앱 성능·할당·Private 개선은 미측정이다. [실행 가이드](widget-gallery-run.ko.md)의 `--slider`로 확인한다.

## hover 색상 수정 — 2026-11-11

일반 버튼의 회색 hover 슬롯을 사용하던 매핑을 제거했습니다. `UiFluentSliderPalette.Resolve`는 기존 compound-brand 팔레트를 재사용해 채운 트랙과 thumb를 일반·hover·누름·비활성으로 구분합니다. 고정한 Fluent8830c10b의 `colorCompoundBrandBackground`, `Hover`, `Pressed`를 실제 `brandWeb` 값으로 해석합니다. 소스의 오래된 색상 주석을 값으로 사용하지 않습니다. 어두운 테마의 일반/hover/누름은 각각 #479ef5/#62abf5/#2886de이며 비활성은 #5c5c5c입니다.

남은 트랙·thumb 테두리·안쪽 면·단계 마커도 해당 Slider의 neutral 토큰을 사용합니다. `UiSliderPaint`는 Rail/Fill/ThumbBorder/Surface를 구분합니다. 아래 고정 ProgressBar는 Slider의 hover 때문에 색이 바뀌지 않습니다. 입력·값·레이아웃 계약과 프레임 예약은 그대로입니다. 공식 forced-colors·OS 접근성/완전한 브라우저 동치는 미완료입니다.

실행: `./run-gallery.ps1` → Fluent → 진행률 Slider에 포인터를 올리세요. 일반·hover·누름은 파란 계열이고 비활성에서만 회색이 됩니다. 근거: artifacts/slider-hover-2026-11-11/verification.json. 공식 스타일과 독립한 색상 fixture로 Light/Dark·MSAA1/4·단계 유무의 thumb·트랙 실제 픽셀과 고정 ProgressBar의 불변 영역을 검사했습니다.


기존 SliderFloat/SliderInt·스칼라/벡터/배열 래퍼·SliderAngle·VSliderFloat/VSliderInt가 타입 지정 수치 큐와 현재 활성/클립/범위 재검증을 공유합니다. float는 연속값을 유지하며 수평 step은 수치 스냅 격자가 아닌 키보드 증감량입니다. 연속 컨트롤은 SmallChange0, 정수는1을 게시하며 기존 float의 중간값 짝수 반올림을 따릅니다. 각도는 도 단위를 게시하고 호출자에게 라디안을 반영하며 정수 게시 값은 래퍼의 최종 변환 결과입니다. 기존 정수/double의 float 기반 정밀도와 키보드/포커스 동작을 유지하므로 전체 정밀도나 새 Focus 기능을 주장하지 않습니다. 기존 탭 선택/본문 관계는 별도로 검증했으며 키보드/플래그 전체 지원은 남아 있습니다. 기존 API 집중16개(통합44개)·관찰127개·외부 기존 API 클라이언트6개/호스트3개·실제 포인터 선택 Immediate를 포함한 관찰 GPU47개가 통과했습니다. ./run-gallery.ps1 → 기존 API → Hello / Immediate에서 확인합니다. 근거: artifacts/slider-range-2026-11-11/legacy-verification.json.

2026-11-12 — 결정론적인 기본 제공 리프 표현을 기존 부모 측정 캐시에 연결했으며 그리기와 입력은 계속 실행합니다. 사용자 특성은 명시적 동의가 필요하고 라벨·옵션·크기·항목·환경 변경은 배치를 무효화합니다. [공통 리프 측정 계약](widget-cpu-retention.ko.md)과 artifacts/leaf-layout-retention-2026-11-11/verification.json을 참고하세요. 전체 NativeAOT·성능 게이트는 남아 있습니다.

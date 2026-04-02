# Version History

Duxel의 버전별 변경 내역 누적 기록.

## 0.2.2-preview (2026-04-03)

### 주요 기능 추가

- **[기능]** DSL 스크린 런타임 (`UiDslScreen`) — 핫리로드(managed)와 소스 생성(NativeAOT) 통합 DSL 렌더링, `.duxel-theme` 핫리로드, `RequestTheme()` API, 이벤트/값 바인딩 통합.
- **[기능]** DSL 이벤트/값 바인딩 — `UiDslEventBinder`(fluent per-id 버튼/체크박스 콜백)와 `UiDslValueBinder`(fluent `IUiDslValueSource` 래퍼)로 인터페이스 수동 구현 제거.
- **[기능]** DSL 제어 흐름 — `If`/`ElseIf`/`Else`, `Visible`, `ForEach`(범위 기반 템플릿 확장), `Switch`/`Case`/`Default`, `Set`, `Text Bind="key"` 데이터 바인딩.
- **[기능]** 테마 프리셋 10종 — `ImGuiDark`, `ImGuiLight`, `ImGuiClassic`, `Nord`, `SolarizedDark`, `SolarizedLight`, `Dracula`, `Monokai`, `CatppuccinMocha`, `GitHubDark` 즉시 전환 지원.
- **[기능]** ThemeDemo 샘플 — DSL 기반 테마 핫리로드 데모 앱, 테마 선택 Combo, 제어 흐름 데모, NativeAOT 지원.
- **[기능]** 테마 시스템 전면 개편 — `UiTheme` struct를 110색 `InlineArray`로 재설계, 위젯별 색상 토큰(`UiStyleColor` enum), `InitWidgetDefaults()` 기본값 캐스케이드, `.duxel-theme` 파일 파서의 베이스 프리셋 상속.

### 주요 개선 사항

- **[개선]** 위젯 테마 세분화 — 기존 ~35개 글로벌 색상에서 110개 위젯별 토큰으로 확장, Button/Checkbox/RadioButton/Input/Slider/Drag/Combo/Selectable/MenuItem/Tab/TreeNode/Table/Tooltip/ListBox/ProgressBar/Separator의 border·state 개별 지정.
- **[개선]** 위젯 border 렌더링 — Button, Checkbox, RadioButton, Input, Slider, Drag, Combo, ListBox, ProgressBar 위젯이 hover/active 상태별 독립 border 색상 렌더링.
- **[개선]** 텍스트/불릿 수직 중앙 정렬 — glyph metrics 기반 시각 중심 계산으로 기존 heuristic ascent offset 대체.
- **[개선]** `Combo` 위젯 이벤트 알림 — 값 변경 시 `EventSink.OnButton(id)` 발동, DSL 이벤트 핸들러 반응형 처리 가능.
- **[개선]** `DuxelAppOptions.Screen` — nullable optional에서 `required`로 변경, 폐기된 `DuxelDslOptions` 진입점 및 `UiDslBindings` 클래스 제거.
- **[개선]** ProgressBar accent 색상 — `ProgressBarFill`이 `SliderGrab` 대신 `PlotHistogram` 사용 (Dear ImGui 기준 일치).

### 주요 버그 수정

- **[버그]** Text `Bind` 위치 인자 간섭 — `ReadOptionalString("Bind")`가 텍스트 내용까지 소비하던 문제, `ReadNamedString("Bind", null)`로 수정.
- **[버그]** 제어 흐름 스킵 경로 스타일 leak — 스킵된 컨테이너의 `EndNode`에서 `StyleColorPushCounts` 미정리 수정.
- **[버그]** Selectable 이벤트 누락 — `Selectable` 상태 변경 시 `OnButton`과 `OnCheckbox` 이벤트 모두 발동하도록 수정.

## 0.2.1-preview (2026-03-25)

### 주요 기능 추가

- **[기능]** 다중 윈도우 지원 (모달/모들리스) — `ShowModal()`로 소유자 윈도우 비활성화 차단형 대화상자, `ShowModalAsync()`로 비동기 모달, `ShowModeless()`로 독립적인 비차단 윈도우를 각각의 `DuxelAppSession` 수명주기로 구동.
- **[기능]** 시스템 트레이 아이콘 지원 — `WindowsTrayIconHost`로 트레이 아이콘, 툴팁, 컨텍스트 메뉴, 더블클릭 핸들러, 최소화 시 트레이 숨기기, 닫기 시 숨기기를 Win32 Shell API로 구현.
- **[기능]** 순수 Vulkan P/Invoke 바인딩 레이어 — Silk.NET Vulkan을 `LibraryImport` 기반 직접 바인딩(`VulkanApi`, `VulkanStructs`, `VulkanEnums`, `VulkanHandles`, `VulkanExtensions`, `VulkanMarshaling`)으로 대체, NativeAOT 완전 호환.
- **[기능]** ClearType 서브픽셀 텍스트 렌더링 셰이더 — DirectWrite ClearType 품질을 위한 RGB 채널별 coverage 출력 프래그먼트 셰이더(`imgui_subpixel.frag`) 추가.
- **[기능]** Windows WIC 이미지 코덱 — `System.Drawing.Common`을 순수 COM 기반 Windows Imaging Component 디코더로 대체, GIF 애니메이션 프레임 compositing과 알파 블렌딩 지원.

### 주요 개선 사항

- **[개선]** 세션 기반 앱 수명주기 — `DuxelApp.RunCore()`에서 `DuxelAppSession` 분리, 독립적 세션 인스턴스의 이중 스레드 렌더 루프, idle frame skip, 증분 폰트 아틀라스 스케줄링 지원.
- **[개선]** 윈도우 옵션 확장 — `MinWidth`/`MinHeight`, `Resizable`, 최소화/최대화 버튼 표시, `CenterOnScreen`/`CenterOnOwner`, 소유자 윈도우 핸들, 커스텀 아이콘(파일/메모리), `WindowCreated` 콜백 추가.
- **[개선]** 플랫폼별 진입점 전환 — FBA 샘플이 `DuxelApp.Run()` 대신 `DuxelWindowsApp.Run()`과 `Duxel.$(platform).App` 패키지 지시문 사용.

### 주요 버그 수정

- **[버그]** NativeAOT 게시를 방해하던 `System.Drawing.Common` 의존성 제거.

### Packaging / Release

- Silk.NET Vulkan 및 `System.Drawing.Common` 패키지 의존성 제거.
- 실험적 레이어 텍스처 캐시 백엔드(`UiLayerCacheBackend.Texture`) 아카이브 처리.
- 내장 데모 윈도우(`UiImmediateContext.DemoWindows.cs`) 제거.
- 패키지 버전을 `0.2.1-preview`로 상향 (`Duxel.App`, `Duxel.Windows.App`).

## 0.2.0-preview (2026-03-09)

### 주요 기능 추가

- **[기능]** 쇼케이스/샘플 범위 확장 — `samples/fba/all_features.cs`를 레이아웃, 타이포그래피, 팝업/컨텍스트 패턴, 선택/상태 도구, 마크다운 편집/뷰어, 내장 데모 검증을 포함하는 더 풍부한 종합 워크스페이스로 확장.
- **[기능]** API와 샘플 양쪽의 확장 포인트 강화 — 인스턴스 기반 커스텀 위젯 조합(`IUiCustomWidget`), 마크다운 편집/뷰어 위젯, 애니메이션 이미지 재생 기반, 관련 문서/샘플을 함께 정리해 고급 UI 조합 경로 확인성 확대.

### 주요 개선 사항

- **[개선]** 쇼케이스 배치와 레이아웃 정리 — 일반 도구 창 메뉴 하단 배치 복원, 소형 창 중앙 배치, compact hero 줄바꿈 높이 실측, nested child 내부 columns 폭 계산 child-local 기준으로 안정화.
- **[개선]** 내장 데모 창 동작 정리 — `Closable Window`의 초기 위치/크기 조정, 데모 창 간 동작 일관성 개선.
- **[개선]** 릴리스 문서 동기화 — README 링크, FBA 가이드 동기화 날짜, 커스텀 위젯 문서, 패키지/버전 메타데이터를 `0.2.0-preview` 기준으로 정리.

### 주요 버그 수정

- **[버그]** 멀티라인 입력과 마크다운 편집기의 줄바꿈 처리 수정 — CRLF/LF/CR 입력 정규화, 보이지 않는 carriage return 잔류 제거.
- **[버그]** 한글 IME 입력 지연 수정 — Windows IME 업데이트 즉시 프레임 wake, live composition 텍스트 우선 반영, 끊기던 입력 반영 제거.
- **[버그]** 쇼케이스 레이아웃 회귀 수정 — row 텍스트 clipping, compact hero 겹침, child 영역 내 columns 폭 혼선 등 샘플 확장 과정에서 드러난 배치 문제 해결.
- **[버그]** 라이브러리 레벨의 same-line 행 높이 전파 수정 — 높이가 다른 인라인 항목이 섞여도 `SameLine`, `NewLine`, columns, tables가 최대 행 높이를 올바르게 이어받도록 조정, 다음 콘텐츠 겹침/오배치 제거.
- **[버그]** 라이브러리 레벨의 top-most 동작 수정 — `Closable Window`의 일반 창 상단 유지, click-through와 잘못된 스택 순서 제거, 실제 top-most semantics 구현.

### Packaging / Release

- 패키지 버전을 `0.2.0-preview`로 상향.

## 0.1.15-preview (2026-03-05)

### 주요 기능 추가

- **[기능]** 플랫폼 텍스트 래스터라이제이션 추상화 도입 — `IPlatformTextBackend`와 request/result 계약 추가, 아틀라스 파이프라인과 분리된 크로스 플랫폼 텍스트 경로 구성.
- **[기능]** DWrite 텍스트-런 백엔드와 기본 폰트 크기 제어 추가 — `WindowsPlatformTextBackend`의 혼합 스크립트 폰트 런 단위 래스터라이즈 지원, `SetDirectTextBaseFontSize`로 line height와 독립적인 DWrite em 크기 제어 가능.

### 주요 개선 사항

- **[개선]** 위젯 텍스트 스택의 DWrite 대응 경로 통합 — 핵심 위젯이 `MeasureTextInternal` / `AddTextInternal`을 공통 사용, 글리프별 COM 호출을 텍스트-런 단위 호출로 대체.
- **[개선]** 텍스트 캐시/진단 체계 일원화 — direct-text 사전 캐싱, 저할당 cache trim, atlas 디스크 캐시 토글, atlas/Vulkan 폰트 진단, codepoint signature/snapshot 추적을 함께 도입.

### 주요 버그 수정

- **[버그]** DWrite 정확도 문제 종합 수정 — 공백 런 한글 미출력, 잘못된 기본 em 크기 사용, 측정된 행 높이 대비 수직 중앙 정렬 오차 해결.
- **[버그]** GPU 텍스트 리소스 안정성 수정 — 동적 아틀라스/DWrite 텍스처 ID 범위 분리, staging 업로드 순서 보정, 텍스처 데이터 크기 정규화 검증 보강.
- **[버그]** 스왑체인 재생성 실패 처리 경로 수정 — `TryRecreateSwapchain()`이 surface-lost 이후에도 연쇄 Vulkan 오류 없이 안전하게 실패 반환.

### Packaging / Release

- NuGet 패키지 버전을 `0.1.15-preview`로 상향.

## 0.1.14-preview (2026-02-28)

### 주요 개선 사항

- **[개선]** 종료 시점 렌더 루프 소모 축소 — `ShouldClose` / `stopRequested` 감지 후 즉시 종료, 창 종료 중 불필요한 추가 프레임 생성 억제.

### 주요 버그 수정

- **[버그]** 한글 fallback과 DWrite 아틀라스 선택 경로 수정 — 한글 코드포인트의 보조 폰트 fallback 복원, `DUXEL_DIRECT_TEXT=0`이 아틀라스 래스터라이저까지 TTF로 강등시키지 않도록 정리.
- **[버그]** 동적 아틀라스 캐시 일관성 종합 수정 — stale atlas 재사용, fuzzy size 매칭, 프레임 중간 코드포인트 드리프트, 새 글리프 발견 시 캐시 미무효화 제거.
- **[버그]** 종료 시점 Vulkan surface-lost 처리 경로 수정 — 스왑체인 재생성의 안전한 실패 반환 보장, close-time 렌더 스레드 오류의 크래시 연쇄 차단.

### Packaging / Release

- NuGet 패키지 버전을 `0.1.14-preview`로 상향.

## Documentation Update (2026-02-26)

### 주요 개선 사항

- **[개선]** 문서 표면 일괄 재정비 — 영문/국문 README 정리, UI DSL 가이드의 현재 파서/런타임 기준 재작성, FBA 가이드의 현행 샘플 지시문 기준 동기화, 문서별 동기화 시점 추가.

## 0.1.13-preview (2026-02-20)

### 주요 버그 수정

- **[버그]** `net10.0` 소비자용 FBA 호환성 수정 — `Duxel.Windows.App`, `Duxel.Platform.Windows`를 `net10.0-windows`에서 `net10.0`으로 재타깃팅, NU1202 오류 제거, 향후 크로스플랫폼 FBA 검증 경로 유지.

### Packaging / Release

- NuGet 패키지 버전을 `0.1.13-preview`로 상향.

## 0.1.12-preview (2026-02-20)

### 주요 기능 추가

- **[기능]** 첫 DirectWrite 텍스트 파이프라인 추가 — 새 DWrite 래스터라이저, Direct Text 런타임 토글 API, 환경변수 제어, 텍스트 캐시 관리 도입.
- **[기능]** Windows 플랫폼/백엔드 분리 완료 — `WindowsPlatformBackend` 도입, GLFW 플랫폼 경로 완전 제거.
- **[기능]** UI 런타임 기반 기능 확장 — 애니메이션 트랙, 런타임 폰트 크기 제어, 레이아웃/정렬 헬퍼, 아이콘 렌더링, canvas/overlay/card 헬퍼 API를 재사용 가능한 기반으로 승격.
- **[기능]** Windows 중심 쇼케이스 앱 추가 — 계산기 스타일 FBA 샘플로 반투명 UI, FX 상호작용, 멀티베이스/RPN 시나리오 시연.

### 주요 개선 사항

- **[개선]** 코어 위젯과 플랫폼 동작 정리 — 필요한 위젯 API에 명시적 ID 추가, IME 처리 안정성 강화, 조합 일관성 개선.
- **[개선]** 샘플/벤치 보일러플레이트의 라이브러리 API 흡수 — 10개 이상의 FBA 샘플이 FPS, overlay, parsing, card-rendering 로직을 공통 API로 공유하도록 전환.
- **[개선]** Direct Text 성능 향상 실측 확인 — ON/OFF A/B 벤치에서 평균 약 5.87% FPS 향상(375→397) 검증.

### Packaging / Release

- NuGet 패키지 버전을 `0.1.12-preview`로 상향.

## 0.1.11-preview (2026-02-17)

### 주요 개선 사항

- **[개선]** 전역 정적 캐시와 레이어 dirty 전략 중심의 성능 실험 체계 정리 — 벤치 샘플이 static/dynamic invalidation 경로를 더 재현 가능하게 비교하고, 핫패스 실험은 측정된 개선만 유지, 회귀 시도는 즉시 롤백.
- **[개선]** 벤치 자동화와 기록 체계 보강 — clip/layer A/B 스크립트에 timeout/process cleanup 추가, 반복 성능 비교 리포트의 평균/분산/개선율 표준화, 최적화 세션 로그 추적성 강화.

### Packaging / Release

- NuGet 패키지 버전을 `0.1.11-preview`로 상향.

## 0.1.10-preview (2026-02-15)

### 주요 개선 사항

- **[개선]** Vulkan 레이어 캐시 태그 처리 보강 — opacity suffix가 있는 경우에도 texture compose 재사용 유지, backend/opacity 조합의 재사용 태그 정합성 검증 일관화.
- **[개선]** 렌더링 회귀 벤치 제어 확장 — opacity 고정 검증 옵션과 더 풍부한 충돌 반응 모델 추가, 성능 샘플 비교의 현실성과 반복 가능성 강화.

### Packaging / Release

- NuGet 패키지 버전을 `0.1.10-preview`로 상향.

## 0.1.9-preview (2026-02-15)

### 주요 개선 사항

- **[개선]** 배포 구조 단순화 — 패키지 설명을 최신 구조에 맞추고 NuGet 배포를 `Duxel.App`, `Duxel.Windows.App` 두 패키지에 집중.
- **[개선]** 샘플 표면의 DSL 검증 중심 축소 — `samples/Duxel.Sample`만 유지, 이전 샘플 프로젝트 제거, FBA 패키지 지시자를 `Duxel.Windows.App` 기준으로 통일.
- **[개선]** 축소된 샘플/패키지 구조에 맞춘 문서 정리 — README 표, 삭제 샘플 참조, ImGui 설계 문서, `docs/todo.md`를 한 번에 재구성.

## 0.1.8-preview (2026-02-15)

### 주요 기능 추가

- **[기능]** 배포 패키지 구조의 2패키지 모델 단순화 — `Duxel.App`, `Duxel.Windows.App`를 공개 배포 표면으로 두고 하위 패키지는 내부 번들링으로 전환.
- **[기능]** `Duxel.App`의 플랫폼 주입 훅 추가 — key repeat, clipboard, IME 서비스를 Windows 직접 종속 대신 옵션 주입으로 연결 가능.
- **[기능]** 앱 패키지에 DSL 소스 생성기 포함 — `Duxel.Core.Dsl.Generator` analyzer가 단일 패키지 설치에서도 동작.

### 주요 개선 사항

- **[개선]** 앱 계층의 Windows 결합도와 문서 책임 축소 — 직접 Windows 서비스 참조 제거, 누적 변경 이력을 `README.md` 밖의 전용 문서로 분리.

---

## 0.1.7-preview

### 주요 기능 추가

- **[기능]** 플랫폼 중립 이미지 API와 Windows 런타임 등록 경로 추가 — `UiImageTexture`, `UiImageEffects`, `IUiImageDecoder`로 이미지 지원을 Windows 구현과 분리하고, `Duxel.App`이 Windows 디코더를 런타임에 연결하도록 구성.
- **[기능]** FBA 이미지 쇼케이스 확장 — 웹 이미지 소스 선택과 GIF 프레임 애니메이션 재생 지원.

### 주요 개선 사항

- **[개선]** Vulkan AA 토글 처리와 검증 흐름 개선 — 런타임 TAA/FXAA 전환 시 리소스 재구성이 더 안전해지고, MSAA/FXAA 비교 절차도 반복 가능하게 정리.
- **[개선]** 접힘/확장 UI 표현 정리 — 접힘 상태의 작은 body peek 유지, 캔버스 overflow 보정.

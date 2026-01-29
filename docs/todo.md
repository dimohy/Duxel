# Duxel TODO 목록

- [x] Core API 정교화
   - [src/Duxel.Core/UiTypes.cs](../src/Duxel.Core/UiTypes.cs)의 `UiDrawData`/`UiDrawList`/`UiDrawCommand` 구조를 ImGui 동등 기준으로 검토하고 필요한 필드(예: TotalVtxCount/TotalIdxCount 대응, DisplayPos/FramebufferScale 해석)를 보완.
   - NativeAOT 친화성을 유지하며 public API 고정.
   - 변경 사항을 [docs/design.md](design.md)에 반영.

- [x] 텍스처 업데이트 흐름 구현
   - 최신 ImGui 텍스처 흐름(`ImDrawData::Textures`)에 대응하는 리소스 업데이트 파이프라인 정의.
   - Vulkan 백엔드에서 `UiTextureUpdate`의 Create/Update/Destroy 처리, 디스크립터 관리 및 수명 정책 설계.
   - [docs/design.md](design.md)에 규약 문서화.

- [x] GLFW 플랫폼 백엔드 완성
   - [src/Duxel.Platform.Glfw/GlfwPlatformBackend.cs](../src/Duxel.Platform.Glfw/GlfwPlatformBackend.cs) 창 옵션/리사이즈/닫기 처리 완성.
   - 입력 스냅샷 정확도(모디파이어/키맵핑/스크롤) 검증 및 보완.

- [x] GLFW FBA 샘플 실행 검증
   - [samples/fba/glfw_window_basic.cs](../samples/fba/glfw_window_basic.cs)
   - [samples/fba/glfw_input_dump.cs](../samples/fba/glfw_input_dump.cs)
   - [samples/fba/glfw_timing.cs](../samples/fba/glfw_timing.cs)
   - 정책에 따라 변경 후 최소 1개 샘플 실행.

- [x] Vulkan 렌더러 초기화
   - [src/Duxel.Vulkan/VulkanRendererBackend.cs](../src/Duxel.Vulkan/VulkanRendererBackend.cs)에서 인스턴스/디바이스/스왑체인/렌더패스/프레임버퍼/커맨드버퍼/동기화 객체 생성.
   - Vulkan 스킬 가이드 준수 및 Validation Layer 옵션 반영.
   - [x] Vulkan 인스턴스 생성
   - [x] Surface 생성
   - [x] Physical device/queue 선택
   - [x] Swapchain/RenderPass/Framebuffer/CommandBuffer/Sync
   - [x] Vulkan 초기화 FBA 샘플 실행: [samples/fba/vulkan_init_basic.cs](../samples/fba/vulkan_init_basic.cs)

- [x] ImGui 스타일 파이프라인 구축
   - ImGui 동등 블렌딩/샘플링/셰이더 규약을 Vulkan 파이프라인으로 구현.
   - ClampToEdge 샘플러 및 알파 블렌딩/스캐서 적용.
   - [x] 디스크립터 셋 레이아웃 생성
   - [x] 파이프라인 레이아웃 생성
   - [x] 폰트 샘플러 생성
   - [x] 셰이더 모듈 로딩 및 파이프라인 생성

- [ ] 폰트 아틀라스 업로드
   - 폰트 아틀라스 생성/업로드 경로 구현(스테이징 버퍼 → 이미지 → 레이아웃 전환).
   - 텍스트 품질 기준(HiDPI 포함) 충족 확인.
   - [x] `UiTextureUpdate` 기반 스테이징 업로드/레이아웃 전환 경로 구현
   - [x] ImGui 폰트(TTF) 기반 아틀라스 생성 경로 추가
   - [x] TTF 컴파운드 글리프 지원

- [x] RenderDrawData 구현
   - `UiDrawData` 소비 렌더링 루프 구현: 정점/인덱스 버퍼 업로드, `ClipRect` 스캐서 적용, 드로우 기록.
   - VtxOffset/IndexOffset 처리 포함.
   - [x] `TextureUpdates` 처리 경로 구현

- [x] 샘플 렌더링 앱 추가
   - FBA 형태 Vulkan 렌더링 샘플 추가(예: `samples/fba/vulkan_imgui_basic.cs`).
   - GLFW 창 + Vulkan 렌더러 + 기본 UI 렌더링 흐름 연결.

- [ ] NativeAOT 게시 검증
   - 샘플 앱 NativeAOT publish/run 검증.
   - `PublishAot`, `SelfContained`, `StripSymbols`, `RuntimeIdentifier` 유지 및 트리밍 경고 해결.
   - [ ] Silk.NET.Core 단일 파일 경고(IL3000/IL3002) 해소
   - [ ] Silk.NET.Windowing.Common/Input.Common 트리밍 경고(IL2072) 해소

- [x] 문서화 업데이트
   - [docs/design.md](design.md)에 구현 완료 항목과 검증 결과 추가.
   - ImGui 동등성 기준/1픽셀 비교 정책 유지.

- [x] ImGui 스킬 참조 규칙 검증
   - [.github/skills/imgui/SKILL.md](../.github/skills/imgui/SKILL.md)와
      [.github/skills/imgui/imgui](../.github/skills/imgui/imgui) 참조 규칙 최신화.

- [ ] ImGui 위젯 미구현 목록
   - [x] 텍스트/라벨: `TextColored`, `TextDisabled`, `TextWrapped`, `LabelText`, `TextUnformatted`, `Bullet`
   - [x] 버튼류: `SmallButton`, `InvisibleButton`, `ArrowButton`, 사이즈 인자 `Button` 오버로드
   - [x] 체크/옵션: `CheckboxFlags`
   - [x] 입력: `InputText`(flags/콜백)
   - [x] 입력: `InputScalar`, `InputScalarN`
   - [x] 드래그: `DragScalar`, `DragScalarN`
   - [x] 드래그: `DragFloat2/3/4`, `DragInt2/3/4`
   - [x] 슬라이더: `VSliderFloat`, `VSliderInt`
   - [x] 슬라이더: `SliderAngle`
   - [x] 슬라이더: `SliderScalar`, `SliderScalarN`
   - [x] 슬라이더: `SliderFloat2/3/4`, `SliderInt2/3/4`
   - [x] 색상: `ColorPicker3/4`, `SetColorEditOptions`
   - [x] 색상: `ColorButton`
   - [x] 콤보: getter 기반 `Combo` 오버로드
   - [x] 콤보: `BeginCombo/EndCombo`
   - [x] 리스트박스: getter 기반 `ListBox` 오버로드
   - [x] 리스트박스: `BeginListBox/EndListBox`
   - [x] 선택/트리: `Selectable`(size), `Selectable`(flags), `TreeNodeEx`, `TreeNodePush`, `CollapsingHeader`(flags/close 버튼)
   - [x] 탭: `TabItemButton`, `SetTabItemClosed`, `BeginTabBar`/`BeginTabItem` flags 오버로드
   - [x] 테이블: `TableSetupScrollFreeze`, `TableSetColumnEnabled`, `TableGetColumnFlags`, `TableSetBgColor`, `TableNextRow`(flags/height), `TableSetupColumn`(flags), `TableGetRowIndex`
   - [x] 메뉴/팝업: `BeginPopupContextItem/Window/Void`, `OpenPopupOnItemClick`, `CloseCurrentPopup`
   - [x] 메뉴/팝업: `BeginMainMenuBar/EndMainMenuBar`
   - [x] 툴팁: `BeginTooltip/EndTooltip`
   - [x] 레이아웃/유틸: `SetNextItemWidth`, `PushItemWidth/PopItemWidth`
   - [x] 레이아웃/유틸: `BeginGroup/EndGroup`
   - [x] 레이아웃/유틸: `GetCursorPos/SetCursorPos`, `GetContentRegionAvail`, `CalcTextSize`
   - [x] 레이아웃/유틸: `Indent/Unindent`, `AlignTextToFramePadding`
   - [x] 플롯: `PlotLines`, `PlotHistogram`

- [ ] ImGui API Coverage 로드맵 (상단부터 순차 구현)
   1. 컨텍스트/IO/스타일: `GetCurrentContext`, `SetCurrentContext`, `GetIO`, `GetPlatformIO`, `GetStyle`, `EndFrame`, `GetVersion`, `StyleColors*`
   2. 윈도우/커서/스크롤: 창 상태 조회/설정, 커서 위치/가시 영역, 스크롤 API
   3. 입력/키/마우스/클립보드: 키/마우스 상태, 단축키, 클립보드 연계
   4. 아이템 상태/포커스/네비게이션: Hover/Active/Focus, ItemRect/ID
   5. 테이블/컬럼/탭/팝업 보완: 누락된 Table/Columns/Popup 세부 API
   6. 로그/디버그/INI/메모리: Log/Debug/Settings/Allocator API
   7. 기타 유틸/색 변환/키 이벤트: ColorConvert, Input 이벤트 큐 API


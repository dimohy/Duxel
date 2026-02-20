# Duxel GUI 디자인 문서

이 문서는 Duxel의 설계 기준과 ImGui 호환성 기준/현황/로드맵을 단일 기준으로 통합해 관리한다.

## 목표
- .NET 10 전용 즉시 모드 GUI 라이브러리.
- Vulkan 렌더러 기반, 1차 플랫폼은 Windows.
- 입력/윈도우는 Windows 네이티브 백엔드 사용.
- NativeAOT 게시/실행을 기본 품질 기준으로 유지.

## 설계 원칙
- 코어/플랫폼/렌더러 책임을 명확히 분리한다.
- 렌더러는 `ImDrawData` 소비에만 집중한다.
- 최신 텍스처 흐름(`ImDrawData::Textures`)만 사용한다.
- fallback 경로/대체 구현은 두지 않으며, 미지원은 명시적으로 실패시킨다.
- AOT/트리밍 친화성(리플렉션/동적 로딩 회피)을 기본값으로 한다.

## 아키텍처
### Core
- `UiContext` 프레임 라이프사이클 관리
  - `CreateContext()` → `NewFrame()` → 사용자 UI → `Render()` → `GetDrawData()`

### Renderer (Vulkan)
- 입력: `ImDrawData`
- 책임
  - 파이프라인/디스크립터/샘플러/폰트 텍스처 생명주기
  - 클립 사각형(`ClipRect`) 기반 scissor 적용
  - 정점/인덱스 업로드 및 드로우 호출 기록
  - `TotalVertexCount`/`TotalIndexCount` 기반 버퍼 사이징
  - 동기화/스왑체인 재생성 경로 분리

### Platform
- `IPlatformBackend` (플랫폼별 백엔드 확장 가능)
- 1차/현재 목표: Windows 네이티브 백엔드

## ImGui 호환성 통합 문서

### 호환성 목표
- API 명세/동작/입력 UX를 Dear ImGui와 가능한 범위에서 동등하게 유지.
- 텍스트/블렌딩/클리핑/스케일링 품질은 ImGui 레퍼런스 기준을 따른다.

### 구현 범주(요약)
- Context/IO/Style
- Window/Cursor/Scroll
- Text/Widget(Input, Slider, Drag, Color)
- Tree/Selectable/ListBox
- Menu/Popup/Tooltip
- Table/Columns/TabBar
- DragDrop/Clipboard/Key/Mouse
- Debug/Log/INI/Memory

### 텍스처/렌더링 규약
- `UiDrawData.TextureUpdates` 기반 Create/Update/Destroy 수명 관리
- 텍스처 포맷: `Rgba8Unorm`/`Rgba8Srgb`
- `ClampToEdge` 샘플러 + 알파 블렌딩 기준 유지

### 품질/검증 기준
- 샘플 기반 검증: 변경 후 최소 1개 샘플 실행
- UI 동등성 검증: 동작 기준 + 스크린샷 1픽셀 허용 오차
- 플랫폼/렌더러 책임 분리 위반 시 실패 처리

### NativeAOT 기준
- `PublishAot=true`, `SelfContained=true` 기본
- 리플렉션/동적 로딩/플러그인 방지
- 트리밍 경고는 잔여 원인과 해결 계획을 반드시 기록

### 현재 상태
- ImGui 스타일 API 400+ 항목 구현
- DSL 런타임은 즉시 모드 프레임 흐름과 동등 라이프사이클 유지
- NativeAOT 잔여 경고
  - Silk.NET.Core 단일 파일 경고(IL3000/IL3002)
  - Silk.NET.Windowing.Common/Input.Common 트리밍 경고(IL2072)

### 남은 작업(호환성 관점)
- NativeAOT 트리밍 경고 해소
- Idle Frame Skip 정책 확정(입력/애니메이션/외부 이벤트)
- 멀티뷰포트/도킹 및 멀티컨텍스트 확장

## 참조 우선순위 (ImGui)
1. `imgui.h` / `imgui.cpp`
2. `imgui_draw.cpp`
3. `backends/imgui_impl_vulkan.*`
4. `docs/BACKENDS.md`

## 운영 규칙
- 호환성 관련 신규 기록은 이 문서에만 추가한다.
- 중복 문서(체크리스트/부분 요약)는 만들지 않는다.


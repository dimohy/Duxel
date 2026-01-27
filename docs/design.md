# Dux GUI 디자인 문서 (ImGui 동등 목표)

## 목표
- .NET 10 전용 즉시 모드 GUI 라이브러리.
- Vulkan 렌더러 기반, 1차 플랫폼은 Windows.
- 입력/윈도우는 Silk.NET `Windowing`/`Input` + GLFW 백엔드 사용.
- 오디오는 `Silk.NET.OpenAL` 사용(크로스플랫폼, macOS 대응).
- NativeAOT로 게시 및 정상 실행 필수.
- Dear ImGui와 동등 수준의 UX/텍스트 품질.

## 설계 원칙
- ImGui 코어/백엔드 분리 모델 준수.
- 렌더러는 `ImDrawData` 소비에만 집중.
- 최신 텍스처 흐름(`ImDrawData::Textures`)만 사용.
- fallback 경로/대체 구현 금지(미지원은 명시적 실패).
- AOT/트리밍 친화적: 리플렉션/동적 로딩 금지.

## 아키텍처
### Core
- `UiContext`: 프레임 라이프사이클 관리
  - `CreateContext()` → `NewFrame()` → 사용자 UI → `Render()` → `GetDrawData()`
- 위젯/스타일은 ImGui 동등 수준의 동작/레이아웃/입력 UX 목표.

### Renderer (Vulkan)
- 입력: `ImDrawData` 전용
- 책임
  - 파이프라인/디스크립터/샘플러/폰트 텍스처 생명주기
  - 클립 사각형(`ClipRect`) → Vulkan scissor 적용
  - 정점/인덱스 버퍼 업로드 및 드로우 호출 기록
  - 총 정점/인덱스 카운트(`TotalVertexCount`/`TotalIndexCount`)를 기준으로 버퍼 사이징
- 동기화/스왑체인 재생성 경로를 명확히 분리

### Platform
- `IPlatformBackend` (GLFW/SDL 확장 가능)
  - 1차 목표: GLFW
  - 입력 이벤트 → `ImGuiIO` 매핑

## 텍스트 품질 (ImGui 동등 목표)
- 폰트 아틀라스 생성/업로드 경로는 ImGui와 동일 흐름
- 샘플러는 `ClampToEdge` + 적절한 필터링
- 블렌딩은 ImGui 레퍼런스와 동일한 알파 블렌딩
- HiDPI 스케일링을 지원

## 텍스처 업데이트 흐름
- 최신 텍스처 흐름은 `UiDrawData.TextureUpdates`만 사용한다.
- `UiTextureUpdateKind.Create/Update/Destroy`로 수명 관리한다.
- 텍스처 데이터는 `Rgba8Unorm` 또는 `Rgba8Srgb`로 전달한다.
- Vulkan 렌더러는 `ClampToEdge` 샘플러와 알파 블렌딩 기준을 유지한다.

## 품질 동등성 기준
- 기본 위젯 세트는 ImGui와 동일한 동작/입력 UX 유지
- 스크린샷 픽셀 비교 허용 오차: 1픽셀

## NativeAOT 기준
- `PublishAot=true`, `SelfContained=true` 기본
- 리플렉션/동적 로딩/플러그인 방지
- 트리밍 경고는 반드시 해결

## 구현/검증 정책
- 구현은 기능별 FBA 샘플로 즉시 검증한다.
- 각 변경 후 최소 1개 샘플을 실행해 동작성을 확인한다.

## 2차 목표(추적 목록)
- 멀티뷰포트 및 도킹 지원
- 멀티 컨텍스트 분리/공유
- 창/플랫폼 확장(Silk.NET SDL 백엔드 포함)
- 렌더러 확장 대비(추가 백엔드)

## 참조 우선순위 (ImGui)
1. `imgui.h` / `imgui.cpp`
2. `imgui_draw.cpp`
3. `backends/imgui_impl_vulkan.*`
4. `docs/BACKENDS.md`

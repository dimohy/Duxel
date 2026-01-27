---
name: "ImGui Reference"
description: "Dear ImGui 레퍼런스를 참조해 즉시 모드 GUI와 Vulkan 렌더러를 설계·구현한다."
version: "1.0"
owner: "team"
---

# 목적
- Dear ImGui 레퍼런스를 기준으로 즉시 모드 GUI 동작과 렌더링 품질을 맞춘다.
- Vulkan 백엔드와 텍스트 품질을 ImGui 수준으로 정렬한다.

# 적용 시점
- ImGui 유사 UI 구현/개선 요청
- Vulkan 기반 UI 렌더링 구현
- 텍스트 렌더 품질/아틀라스/샘플링 개선

# 레포지토리 참조 규칙
- ImGui 레포는 다음 경로에 클론해서 사용한다:
  - `.github/skills/imgui/imgui`
- 참조는 **동작/구조/품질 기준**에 한정하며, 원본 소스의 직접 복사/붙여넣기는 금지한다.

# 참조 우선순위
1. `imgui.h` / `imgui.cpp`
2. `imgui_draw.cpp`
3. `backends/imgui_impl_vulkan.h/.cpp`
4. `docs/BACKENDS.md`

# 구현 가이드
- 프레임 흐름: `CreateContext` → `NewFrame` → UI 구성 → `Render` → `GetDrawData`.
- 렌더러는 `ImDrawData`만 소비하고 UI 로직과 분리한다.
- 최신 텍스처 흐름(`ImDrawData::Textures`)만 사용한다.
- 텍스트 품질: 폰트 아틀라스/샘플러/블렌딩/HiDPI를 ImGui 수준으로 맞춘다.

# 금지 사항
- fallback 경로/대체 구현 추가 금지
- ImGui 원본 코드 복사 금지

---
name: "Vulkan"
description: "Vulkan 기반 렌더링(인스턴스/디바이스/스왑체인/렌더패스/동기화) 구현 지침"
version: "1.0"
owner: "team"
---
# 목적

- Vulkan 렌더링 초기화 및 프레임 루프 구현을 표준화한다.
- 최신 Vulkan 문서 기준의 권장 패턴을 따른다.

# 적용 시점
- Vulkan 인스턴스/디바이스/스왑체인/렌더패스/커맨드버퍼 구현 작업 시
- Vulkan 동기화, 메모리, 디스크립터 설계 시

# 핵심 원칙
- Validation Layer를 초기 단계에서 활성화
- 스왑체인 재생성 경로를 명확히 설계
- 프레임 리소스(커맨드버퍼/세마포어/펜스) 분리
- 불필요한 상태 변경 최소화
- (예외) 본 프로젝트는 `Silk.NET Vulkan` 사용을 **허용**한다.
- 구현 시 Silk.NET 소스는 참고만 하고 직접 구현한다

# 권장 구현 순서

1. Vulkan 인스턴스 생성
2. Physical/Logical Device 및 Queue 선택
3. Surface 생성 (Win32)
4. Swapchain 생성 및 이미지 뷰
5. Render Pass/Framebuffers
6. Command Pool/Command Buffer
7. 동기화 객체(Semaphore/Fence)
8. 프레임 루프 및 Present

# 참고 문서(공식)

- Vulkan Guide: https://docs.vulkan.org/guide/latest/
- Vulkan Documentation: https://docs.vulkan.org/
- Vulkan Spec (HTML): https://registry.khronos.org/vulkan/specs/latest/html/vkspec.html
- Vulkan Samples: https://github.com/KhronosGroup/Vulkan-Samples

# UI 동작 참고

- Dear ImGui (UI 상호작용/입력 동작 참고): https://github.com/ocornut/imgui

# Nuri.Duxel 라이브 리사이즈 통합 제안서

작성일: 2026-07-28
대상: Nuri.Duxel 개발자님
상태: 구현 전 설계 제안

## 1. 결론

Nuri의 리사이즈 문제를 해결하기 위해 Duxel의 현재 스왑체인 정책을 변경할 필요는 없습니다.

권장 방향은 다음과 같습니다.

1. **Duxel이 스왑체인과 present 정책을 계속 단독 소유**합니다.
2. **Nuri는 논리 좌표로 UI geometry를 생성**하고 Duxel의 기존 비례 매핑 경로에 제출합니다.
3. 드래그 중 1px 선, 텍스트, 오프스크린 텍스처처럼 물리 픽셀 정보가 꼭 필요한 경우에만 **읽기 전용 `RenderTargetMapping` 계약**을 Nuri 통합 계층에 제공합니다.
4. Nuri는 이 매핑을 픽셀 정렬과 합성에만 사용하고, 스왑체인 재생성을 요청하거나 기다리지 않습니다.
5. 드래그가 끝난 뒤 Duxel이 최종 크기의 스왑체인을 확정하면 Nuri도 최종 해상도로 한 번 정확히 다시 렌더링합니다.

이 방식이면 Nuri의 시각적 문제를 해결하면서 Duxel의 현재 라이브 리사이즈 속도와 프레임 진행 방식을 보존할 수 있습니다.

> 범위 제외: geometry가 없는 clear-only frame의 submit/present 문제는 별도의 Duxel 자체 버그이며, 이 문서의 Nuri 통합 설계와 분리해서 다룹니다.

## 2. Duxel이 현재 보장하는 동작

Duxel은 창 크기를 조절하는 동안 렌더링을 멈추지 않습니다.

- `WM_SIZING`마다 다음 클라이언트 크기를 예측합니다.
- 해당 sequence의 draw data가 실제로 present될 때까지 창 크기 단계의 확정을 기다립니다.
- 드래그 중 draw-data 크기와 실제 스왑체인 크기가 다르면, 최신 draw data를 현재 스왑체인 viewport 전체에 비례 매핑합니다.
- clip rectangle도 같은 비율로 변환합니다.
- WSI가 `OUT_OF_DATE` 또는 surface loss를 보고하면 스왑체인을 재생성합니다.
- 대화형 리사이즈가 끝난 첫 프레임에서 최종 extent로 정확히 재생성합니다.

따라서 현재 정책은 “리사이즈 중 렌더링 생략”이 아니라 다음 정책입니다.

> **매 단계 렌더링은 수행하되, 매 pointer sample마다 스왑체인을 재생성하지 않는다.**

관련 구현:

- [Windows 대화형 리사이즈 정책](../docs/duxel-agent-reference.ko.md#windows-대화형-리사이즈-흐름)
- [draw-data와 현재 swapchain extent의 매핑](../src/Duxel.Vulkan/VulkanRendererBackend.CommandRecording.cs)
- [실제 swapchain 재생성 경로](../src/Duxel.Vulkan/VulkanRendererBackend.Swapchain.cs)

## 3. 왜 이런 정책을 사용하는가

Vulkan 스왑체인 재생성은 단순한 width/height 값 변경이 아닙니다.

Duxel의 현재 재생성 경로에는 다음 작업이 포함됩니다.

- `vkDeviceWaitIdle`에 해당하는 device idle 대기
- 기존 pending GPU resource 정리
- 새 swapchain 및 image view 생성
- MSAA image 재생성
- image-in-flight 및 semaphore 상태 재구성

이를 마우스가 움직일 때마다 반복하면 다음 문제가 생길 수 있습니다.

- CPU/GPU 병렬 실행 중단
- 드라이버 및 WSI 비용 증가
- 리사이즈 끊김과 입력 지연 증가
- GPU나 창 시스템에 따른 불규칙한 hitch
- Duxel 전체 사용자에게 불필요한 성능 비용 전가

현재 Duxel의 비례 매핑은 이 비용을 피하면서도 창 외곽과 내부 콘텐츠가 함께 움직이도록 만든 의도적인 성능 정책입니다.

## 4. 피해야 할 구현

다음 변경은 Nuri 통합의 기본 해법으로 사용하지 않는 것을 권장합니다.

### 4.1 pointer sample마다 스왑체인 재생성

드래그 중 매 크기를 실제 스왑체인 extent로 즉시 확정하면 시각적으로는 1:1 대응하기 쉽지만, 반복적인 device idle과 WSI 재구성이 발생합니다.

현재 Duxel과 동일한 속도를 보장할 수 없습니다.

### 4.2 `ProposedSize`와 `CommittedFramebufferSize`를 플랫폼 전역 상태로 공개

플랫폼이 제안한 창 크기와 렌더러가 실제로 사용하는 target extent는 소유자와 생명주기가 다릅니다.

이를 하나의 범용 `PlatformFrameState`에 넣으면 다음 문제가 생깁니다.

- 플랫폼 상태와 렌더러 상태의 책임 혼합
- 어느 값이 실제 렌더 대상인지 해석이 모호해짐
- Nuri 이외 소비자에게 불필요한 공개 API 노출
- 프레임 경계와 스레드 간 일관성 보장이 어려워짐

특히 `CommittedFramebufferSize`라는 이름은 “OS가 확정한 크기”, “Duxel이 요청한 크기”, “현재 Vulkan swapchain extent”를 혼동하게 만들 수 있습니다.

### 4.3 Nuri 전용 특수 projection을 Duxel 핵심 경로에 추가

Duxel에는 이미 논리 좌표를 현재 render target 전체에 매핑하는 projection과 clip 보정이 있습니다. Nuri 전용 projection을 병렬로 추가하면 두 변환 경로가 서로 달라질 가능성이 커집니다.

### 4.4 timer debounce로 최종 렌더링 추정

“마지막 resize 후 N ms” 방식은 사용자가 아직 드래그 중인지 정확히 표현하지 못합니다. 최종 고해상도 재렌더링은 timer가 아니라 실제 대화형 리사이즈 종료 경계에 연결해야 합니다.

## 5. 권장 구조

핵심은 크기 상태를 늘리는 것이 아니라 **두 좌표 공간 사이의 변환을 명확히 하는 것**입니다.

```text
Nuri 논리 레이아웃
    │
    │ geometry: 논리 좌표
    ▼
Duxel draw data
    │
    │ RenderTargetMapping
    ▼
현재 Vulkan render target
    │
    ▼
present
```

### 5.1 좌표 공간

#### 논리 레이아웃 공간

- Nuri가 layout, hit testing, widget 배치를 계산하는 공간
- 최신 클라이언트 크기를 기준으로 함
- 창 크기 변경마다 갱신 가능

#### 실제 렌더 타깃 공간

- 현재 프레임이 기록되는 Vulkan swapchain image의 extent
- 대화형 리사이즈 중에는 논리 레이아웃 크기와 일시적으로 다를 수 있음
- Duxel renderer가 소유

두 공간이 다르다는 것은 오류가 아닙니다. 둘 사이의 매핑이 일관되지 않을 때만 오류가 됩니다.

## 6. 1단계 권장안: 공개 API를 추가하지 않는 통합

Nuri가 일반 UI geometry만 생성한다면 별도 API가 필요하지 않을 가능성이 큽니다.

1. Nuri는 최신 논리 크기로 layout을 계산합니다.
2. vertex와 clip rectangle을 동일한 논리 좌표계로 생성합니다.
3. Duxel에 draw data를 제출합니다.
4. Duxel은 기존 projection과 clip scale 보정으로 현재 swapchain extent 전체에 렌더링합니다.

이 경로가 가장 단순하고 Duxel의 현재 성능 특성을 그대로 유지합니다.

먼저 이 방식으로 실제 Nuri 증상이 사라지는지 확인하는 것이 좋습니다.

## 7. 2단계 권장안: 픽셀 민감 렌더링용 좁은 계약

Nuri가 아래 기능을 수행한다면 실제 render target 정보가 필요할 수 있습니다.

- 1px 선 또는 테두리 정렬
- glyph origin의 물리 픽셀 정렬
- offscreen texture 해상도 결정
- custom shader에 전달할 texel 크기 계산
- scissor 또는 custom clip의 물리 픽셀 변환

이 경우 범용 플랫폼 상태가 아니라, Nuri.Duxel 통합 계층에 다음과 같은 읽기 전용 계약을 두는 것을 권장합니다.

```csharp
public readonly record struct RenderTargetMapping(
    float DisplayPositionX,
    float DisplayPositionY,
    float LayoutWidth,
    float LayoutHeight,
    float FramebufferScaleX,
    float FramebufferScaleY,
    uint RenderTargetWidth,
    uint RenderTargetHeight)
{
    public float LayoutToTargetScaleX => RenderTargetWidth / LayoutWidth;
    public float LayoutToTargetScaleY => RenderTargetHeight / LayoutHeight;
}
```

위 코드는 API의 의미를 설명하기 위한 제안이며, 타입명과 namespace는 Nuri.Duxel 구조에 맞게 조정할 수 있습니다.

계약의 필수 조건:

- 불변 snapshot이어야 합니다.
- 해당 draw-data 프레임과 동일한 시점의 값이어야 합니다.
- `RenderTargetWidth/Height`는 요청 크기가 아니라 **실제로 command buffer가 그려질 target extent**여야 합니다.
- Nuri가 값을 변경하거나 swapchain recreate를 요청할 수 없어야 합니다.
- 값을 읽기 위해 렌더 스레드나 window procedure를 동기 대기시키면 안 됩니다.
- 일반 소비자가 없으면 Duxel의 범용 공개 API로 승격하지 말고 Nuri 통합 계층 내부에 둡니다.

가장 안전한 전달 시점은 frame 준비 또는 draw-data 변환 callback입니다. 임의 시점에 읽는 전역 mutable property보다 프레임과 함께 전달되는 값이 적합합니다.

## 8. 픽셀 정렬 방법

Duxel의 현재 매핑과 같은 변환을 사용해야 합니다.

```text
scaleX = RenderTargetWidth  / LayoutWidth
scaleY = RenderTargetHeight / LayoutHeight

targetX = (logicalX - DisplayPositionX) * scaleX
targetY = (logicalY - DisplayPositionY) * scaleY
```

target space에서 픽셀 정렬한 뒤 필요하면 논리 좌표로 되돌립니다.

```text
snappedTargetX = round(targetX)
snappedLogicalX = snappedTargetX / scaleX + DisplayPositionX
```

주의할 점:

- 채움 영역의 경계는 보통 정수 픽셀 경계에 맞춥니다.
- 1px stroke의 중심은 rasterization 규칙에 따라 `N + 0.5` 정렬이 필요할 수 있습니다.
- 텍스트는 모든 vertex를 무조건 반올림하지 말고 glyph origin과 atlas sampling 정책을 함께 확인해야 합니다.
- X/Y 비율이 일시적으로 다를 수 있으므로 하나의 uniform scale로 가정하지 않습니다.
- clip, geometry, custom shader가 서로 다른 scale을 사용하면 안 됩니다.

픽셀 정렬은 render target 좌표에서 해결할 문제이지, 픽셀을 맞추기 위해 스왑체인을 매번 재생성할 문제는 아닙니다.

## 9. Nuri가 offscreen 렌더 타깃을 사용하는 경우

Nuri가 자체 canvas나 offscreen texture에 UI를 먼저 그린다면 다음 이중 정책을 권장합니다.

### 드래그 중

- 현재 유효한 offscreen texture를 유지합니다.
- 최신 레이아웃의 콘텐츠를 기존 texture에 다시 그리거나, 비용이 크면 기존 결과를 합성 단계에서 현재 target 전체로 스케일합니다.
- 매 pointer sample마다 offscreen image와 swapchain을 동시에 재생성하지 않습니다.

### 드래그 종료 후

- Duxel이 최종 swapchain extent를 확정합니다.
- Nuri가 최종 물리 크기에 맞는 offscreen texture를 한 번 재생성합니다.
- 최종 크기로 콘텐츠를 다시 렌더링합니다.

이렇게 하면 드래그 중 연속성과 종료 후 픽셀 정확도를 함께 확보할 수 있습니다.

## 10. 역할 분리

### Duxel 책임

- 최신 resize sequence 처리
- draw-data와 실제 target extent의 일관된 매핑
- clip 보정
- acquire, command recording, submit, present
- WSI 오류와 resize 종료 시 swapchain 재생성
- 실제 render target extent 제공

### Nuri.Duxel 책임

- 최신 논리 크기로 layout 갱신
- 논리 좌표 기준 geometry 생성
- 필요할 때 동일한 `RenderTargetMapping`으로 pixel snapping
- offscreen resource의 캐시 및 최종 크기 재생성
- Duxel의 swapchain 생명주기를 직접 제어하지 않음

## 11. 구현 순서

1. Nuri에서 문제가 발생하는 최소 재현 화면을 만듭니다.
2. Nuri geometry와 clip이 모두 최신 논리 좌표로 제출되는지 확인합니다.
3. 별도 API 없이 Duxel의 기존 매핑만 사용해 증상을 다시 확인합니다.
4. 남은 문제가 1px 정렬 또는 offscreen 해상도 문제인지 분류합니다.
5. 필요한 경우에만 프레임 한정 `RenderTargetMapping`을 통합 계층에 추가합니다.
6. Nuri의 pixel-sensitive 경로가 이 매핑 하나만 사용하도록 정리합니다.
7. resize 종료 시 최종 extent 재렌더링을 연결합니다.
8. 아래 검증 기준으로 기존 Duxel과 A/B 비교합니다.

## 12. 완료 기준

### 기능

- 느린 드래그와 빠른 드래그 모두 창 외곽과 콘텐츠가 함께 움직임
- layout, clip, hit testing이 같은 좌표계를 사용함
- resize 종료 후 실제 client size와 swapchain extent가 정확히 일치
- 최소화, 복원, 최대화 후 정상 렌더링
- DPI가 다른 모니터 사이 이동 후 scale과 clip이 정상

### 시각 품질

- 1px 선과 테두리가 불필요하게 흔들리지 않음
- 텍스트 origin 또는 atlas sampling이 resize 중 일관됨
- custom clip/scissor가 콘텐츠와 어긋나지 않음
- offscreen 합성 결과가 창 경계 밖으로 잘리거나 빈 영역을 만들지 않음

### 성능

기존 Duxel을 baseline으로 동일한 물리 드래그를 비교합니다.

- `WM_ENTERSIZEMOVE`부터 `WM_EXITSIZEMOVE` 사이 `DeviceWaitIdle` 호출 횟수가 증가하지 않아야 함
- 정상 드래그 중 swapchain recreate 횟수가 증가하지 않아야 함
- present cadence가 baseline보다 유의미하게 나빠지지 않아야 함
- CPU frame time, GPU frame time, 최장 hitch를 Before/After로 기록
- 메모리 할당과 offscreen image 재생성 횟수를 기록

### 회귀

- Duxel 전체 Release 빌드 경고 0, 오류 0
- Managed 실행 경로 정상
- NativeAOT 실행 경로 정상
- 기존 샘플 및 회귀 테스트 정상
- Vulkan validation error 없음

## 13. 성능 보장에 대한 경계

다음 두 요구를 동시에 절대적으로 보장할 수 있다고 전제하면 안 됩니다.

1. 마우스의 모든 pointer sample마다 실제 swapchain extent를 새 창 크기와 1:1로 즉시 일치
2. 현재 Duxel과 동일한 라이브 리사이즈 성능 보장

1번을 반복적인 swapchain recreate로 구현하면 2번은 플랫폼, 드라이버, GPU에 따라 깨질 수 있습니다.

만약 Nuri의 필수 요구가 “비례 매핑된 결과”가 아니라 “드래그 중에도 매 sample 실제 swapchain 1:1”이라면, 이는 별도 opt-in 모드로 다뤄야 합니다. 병합 전에 실제 장비에서 A/B 성능 자료가 필요하며 Duxel 기본값으로 넣어서는 안 됩니다.

## 14. Nuri 측에서 먼저 확인할 정보

구현 전에 아래 세 가지를 최소 재현과 함께 알려주면 정확한 통합 지점을 결정할 수 있습니다.

1. 실제 증상이 무엇인지

   - 콘텐츠 지연
   - clip 불일치
   - 1px 선 흔들림
   - 텍스트 흐림
   - offscreen canvas 크기 불일치
   - resize 종료 후에도 잘못된 크기 유지
2. Nuri의 렌더링 경로가 무엇인지

   - Duxel 기본 primitive만 사용
   - Nuri 자체 vertex/clip 생성
   - custom Vulkan command
   - offscreen texture 합성
3. 픽셀 정확도가 필요한 시점이 언제인지

   - 드래그 중에도 필수
   - 드래그 종료 후에만 필수

이 정보가 확인되기 전에는 `PlatformFrameState`, proposed/committed 이중 크기, 반복 swapchain recreate를 먼저 추가하지 않는 것이 안전합니다.

## 15. 최종 권고

Nuri의 문제는 Duxel의 빠른 리사이즈 정책을 제거해서 해결할 문제가 아니라, **Nuri의 논리 좌표와 Duxel의 실제 render target 사이 계약을 명시해서 해결할 문제**로 보는 것이 적절합니다.

우선 공개 API 없이 기존 draw-data 매핑으로 통합하고, 실제로 픽셀 정보가 필요한 경로가 확인될 때만 좁은 `RenderTargetMapping`을 추가하십시오. 이 경계가 유지되면 Nuri의 요구를 수용하면서 Duxel 전체 사용자의 성능 비용을 늘리지 않을 수 있습니다.

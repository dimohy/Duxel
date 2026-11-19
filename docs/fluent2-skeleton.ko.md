# Fluent 2 Skeleton

`Fluent.Skeleton(child, options)`는 로딩 영역을 소유하고 하위 `Fluent.SkeletonItem(options)`에 표현을 전달합니다. 옵저버의 **스켈레톤** 탭에서 로딩 완료, Wave/Pulse, 반투명, 모션 줄이기, 정지를 전환할 수 있습니다.

```csharp
var loading = Fluent.Skeleton(
    Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
        Fluent.SkeletonItem(new(Size: 48, Shape: UiSkeletonShape.Circle)),
        Fluent.SkeletonItem(new(Width: 240)),
        Fluent.SkeletonItem(new(Width: 160))),
    new(Animation: UiSkeletonAnimation.Wave));
loading.AccessibleName = "프로필 불러오는 중";
```

## 동작과 배치

기본값은 Wave/Opaque/16px/Rectangle입니다. Animation·Appearance·Size·Shape·ReducedMotion·Paused의 null 값은 가장 가까운 Skeleton에서 상속합니다. Width는 상속하지 않는 로컬 폭입니다. 중첩 루트는 해당 범위만 덮어쓰며 측정 재사용 키에 해결된 스타일을 반영합니다. 바깥 범위는 정상 종료와 예외 후 복원됩니다.

크기는 8, 12, 14, 16, 20, 22, 24, 28, 32, 36, 40, 48, 52, 56, 64, 72, 92, 96, 120, 128px를 지원합니다. Rectangle은 높이 Size·기본 반경4px이며 유한한 부모 폭을 사용합니다. Square/Circle의 자연 크기는 Size×Size입니다. 원에 비정사각형을 할당하면 공통 아핀 페인트 경로로 타원이 됩니다. 무한 폭의 Rectangle 자연 폭은0이므로 호출자가 유한 폭을 제공해야 합니다. Width는 유한한0 이상이어야 합니다.

Wave는3초, Pulse는1초이며 CSS ease-in-out 곡선을 공통 시간축으로 계산합니다. 보이는 애니메이션만 공통60Hz 예약 경로를 사용합니다. Paused는 현재 위상을 보관하는 일시정지가 아니라 안정된 초기 외형을 표시하는 정지 옵션입니다. ReducedMotion도 정적 외형을 표시합니다. 화면 밖·정지·제거한 항목은 지속 프레임을 요청하지 않습니다. 다른 위젯의 활성 애니메이션은 별개입니다.

색은 공식 Light/Dark stencil 토큰과 반투명 토큰을 사용합니다. 기존 분석적 둥근 사각형·그라데이션·변환 경로를 재사용하며 별도의 CPU 이미지 래스터화 경로를 추가하지 않습니다. 사용자 표현은 `IUiSkeletonPresentation`의 정적 Measure/Paint를 구현합니다. 측정 재사용은 명시적으로 동의한 표현만 허용합니다.

## 접근성과 검증

루트는 이름을 가진 미확정 ProgressBar와 Busy=true를 게시합니다. 항목은 조작 가능한 가짜 컨트롤이나 수치 진행률을 게시하지 않습니다. Linux AT-SPI busy 상태와 Windows UIA AriaProperties의 busy=true를 실제 독립 클라이언트에서 확인했습니다. 로딩 콘텐츠는 호출자가 완료 데이터로 교체합니다.

2026-11-15 집중 검증: CPU43개, 생성기131개, Windows GPU396개/384원시 이미지 쌍, Linux 고정 .NET11 RC3 NativeAOT GPU 슬롯1·3 각각396개/384쌍, Linux AT-SPI8개·호스트3개, Windows 관리형 UIA6개·호스트1개 통과. 옵저버31개 탭과 SVG 카메라3조건의34개 표시 단계·입력·유휴·복원을 확인했습니다. 전체 Fluent 적합성·Windows RC3 NativeAOT는 별개이며 미완료입니다. 이 집중 검증 당시 일반 독립 Fluent Text는 네이티브 텍스트 노드를 게시하지 않았습니다. 후속 [일반 텍스트 접근성 수정](fluent2-text.ko.md)에서 실제 게시와 내용 갱신을 교정했습니다. 완료 데이터 검사는 실제 읽기 전용 한글 입력 컨트롤을 사용했습니다.

기준은 [공식 가이드](https://fluent2.microsoft.design/components/web/react/core/skeleton/usage)와 Fluent UI 커밋 `f9720c35ba893c9630f4c0389d07dc6d6fd6068d`입니다. 공식 브라우저의 Wave/Pulse 동작·계산 스타일을 확인했으며 브라우저와 네이티브 간 픽셀 동치를 주장하지 않습니다. 출처·해시·검증 범위는 `artifacts/fluent-skeleton-2026-11-15/`에 있습니다. Linux 실행은 [옵저버 가이드](../samples/Duxel.Gallery.Linux/README.ko.md)를 따릅니다.

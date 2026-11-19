# Shimmer

`./run-gallery.ps1` → **알림과 진행 상태 → Shimmer**에서 확인합니다. **불러오기 상태 전환**으로 로딩·완료를 바꾸고 **모션 줄이기 전환**으로 즉시 전환과 정지된 자리 표시자를 비교합니다. 바로 아래 소스도 현재 설정을 반영합니다. 가장 최근 추가 메뉴이므로 NEW는 Shimmer에 표시합니다.

```csharp
var placeholder = Fluent.Skeleton(Fluent.SkeletonItem(new(Width : 240)));
placeholder.AccessibleName = "내용을 불러오는 중";
var example = Fluent.Shimmer(Fluent.Body1("준비된 내용"), placeholder);
example.IsDataLoaded = true;
example.Options = new(ReducedMotion : true);
```

`UiShimmerWidget`은 자리 표시자와 실제 내용을 함께 소유하고, `UiFluentShimmer`는 생성된 Fluent 어댑터입니다. 자리 표시자는 기존 `UiSkeletonWidget`을 받으므로 원·선·여백 등을 기존 배치 위젯으로 조합할 수 있습니다. 생략하면 기본 선을 표시합니다. 색상·파형·모양은 Skeleton의 기존 표시 경로를 사용하며, 사용자 정의 SkeletonItem 표시 구현도 사용할 수 있습니다.

로딩 중에는 실제 내용을 그리지 않으므로 입력·탭 이동·접근성 트리에 나타나지 않습니다. 완료로 바꾸면 실제 내용을 활성화하고, 마지막 자리 표시자 화면만 200ms 동안 서서히 지웁니다. 전환을 되돌리면 실제 내용의 입력·포커스를 퇴장 경로에서 정리하고 마지막 화면만 사용합니다. 중간에 다시 전환해도 현재 불투명도에서 이어집니다. 공통 `UiFloatTransition`과 `UiPresencePaint`를 재사용하며 새 타이머나 무한 루프를 만들지 않습니다. 완료 후에는 자리 표시자의 파형과 추가 프레임 요청이 멈춥니다.

`Options.Transition`으로 시간·곡선·프레임 간격을 지정합니다. `ReducedMotion`은 전환을 즉시 끝내고 상속되는 Skeleton 파형을 정지합니다. 사용자 정의 자리 표시자에 ReducedMotion을 명시하면 기존 Skeleton의 하위 설정 우선 규칙을 따릅니다. 기본 배치는 두 자식에 필요한 크기를 미리 확보해 전환 중 주변 내용이 뛰지 않게 합니다. 소유한 자식은 완료·로딩 전환만으로 폐기하지 않으므로 입력값도 유지됩니다.

이 구현은 Microsoft의 Fluent v8 Shimmer가 제공하는 로딩→콘텐츠 전환을 기존 Fluent Skeleton과 결합한 네이티브 위젯입니다. CSS 길이 문자열·DOM 옵션을 그대로 복제하거나 모든 Fluent 상태·접근성·색상 적합성 검증을 완료했다는 뜻은 아닙니다. 현재 카탈로그 연결은 44/55이고 미연결은 11개입니다.

검증 기록: `artifacts/fluent-shimmer-2026-11-17/result.json`. 집중 CPU 14건, Vulkan 8건(MSAA 1/4), 카탈로그 4건, 갤러리 GPU 270건 통과. Windows·Linux 호스트 빌드를 확인했으며 실제 Linux 실행과 고정 RC3 NativeAOT는 실행하지 않았습니다. 이전 글꼴·Tooltip·공통 엔진 검사는 해당 소스가 바뀌지 않아 반복하지 않았습니다.

공식 API: https://github.com/microsoft/fluentui/blob/master/packages/react/src/components/Shimmer/Shimmer.types.ts
공식 전환 구현: https://github.com/microsoft/fluentui/blob/master/packages/react/src/components/Shimmer/Shimmer.base.tsx

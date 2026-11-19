# ProgressIndicator

`./run-gallery.ps1` → **알림과 진행 상태 → ProgressIndicator**에서 확인합니다. 진행률 증가·대기 전환·막대 숨김·모션 줄이기를 조작하면 각 예제 바로 아래 소스도 현재 옵션을 반영합니다. 최신 추가 메뉴이므로 NEW는 ProgressIndicator에만 표시합니다.

```csharp
var progress = Fluent.ProgressIndicator(new(
    Label : "파일 업로드",
    Description : "선택한 파일을 서버로 보내고 있습니다.",
    PercentComplete : .25f,
    Width : 360,
    ValueText : "파일 1/4개"));
progress.Options = progress.Options with { PercentComplete = .75f };
```

ProgressBar가 막대 자체라면 ProgressIndicator는 작업 이름·막대·설명을 함께 배치하는 위젯입니다. UiProgressIndicatorWidget이 자식을 소유하고 UiFluentProgressIndicator가 생성된 Fluent 어댑터 역할을 합니다. PercentComplete는 0~1이며 유한한 범위 밖 값은 0~1로 제한합니다. null이면 미확정 진행률입니다. 기본 너비는 180, 막대 높이는 2 논리 픽셀입니다. Width·BarHeight는 양의 유한 값이어야 합니다. NaN과 무한대는 렌더링 전에 거부합니다.

ProgressHidden을 켜면 막대를 그리거나 접근성 요소로 게시하지 않으며 막대 애니메이션도 예약하지 않습니다. 이름과 설명은 그대로 남습니다. 설명은 너비에 맞춰 줄바꿈합니다. 기존 ProgressBar의 300ms 진행률 보간과 대기 파형을 재사용합니다. ReducedMotion은 확정 값 보간을 즉시 끝내고 미확정 상태의 가로 이동을 불투명도 변화로 바꿉니다. 완전 정지 옵션은 아니므로 대기 상태의 불투명도 변화는 계속됩니다.

진행률 요소는 포커스를 받지 않는 읽기 전용 요소입니다. 이름은 일반 AccessibleName, 보이는 Label, Label이 없을 때 AccessibleLabel 순으로 결정합니다. 확정 값의 범위는 0~100이고 목표 값을 즉시 게시하므로 시각적 보간 중에도 실제 진행률을 읽습니다. Description은 도움말, ValueText는 네이티브 ItemStatus와 값 텍스트에 제공합니다. 미확정 상태는 Busy로 표시하고 숫자 범위는 생략합니다.

현재 문자열 레이블·설명과 네이티브 상태 메타정보를 지원합니다. React 노드·CSS 슬롯·aria-valuetext의 브라우저 동작 전체를 복제하거나 모든 색상·모션·외부 접근성·모달 조합의 적합성 검증을 완료했다는 뜻은 아닙니다. 전체 목표에는 이 검증과 남은 기능이 계속 포함됩니다. 카탈로그 연결은 45/55이며 미연결은 10개입니다.

검증: artifacts/fluent-progress-indicator-2026-11-17/result.json. CPU 14건과 기본 GPU 8건은 동일 Core 지문을 확인해 재사용했습니다. 모션 줄이기를 정지로 가정했던 검사 1건은 현재 공식 구현의 불투명도 변화에 맞춰 보완했고, 해당 CPU 1건과 추가 Vulkan 4건만 확인했습니다. 카탈로그 4건·갤러리 GPU 270건·Windows/Linux 호스트 빌드 통과. 실제 Linux와 고정 RC3 NativeAOT는 실행하지 않았습니다.

공식 API: https://github.com/microsoft/fluentui/blob/master/packages/react/src/components/ProgressIndicator/ProgressIndicator.types.ts
현재 공식 모션 구현: https://github.com/microsoft/fluentui/blob/master/packages/react-components/react-progress/library/src/components/ProgressBar/useProgressBarStyles.styles.ts

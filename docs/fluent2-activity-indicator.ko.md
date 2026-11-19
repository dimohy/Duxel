# ActivityIndicator

`./run-gallery.ps1` → **알림과 진행 상태 → ActivityIndicator**에서 실행합니다. 다섯 크기의 예제마다 바로 아래 소스가 있으며 시작·중지·정지 시 숨김·색상·모션 줄이기를 바꾸면 소스도 함께 바뀝니다. NEW는 최신 추가 메뉴인 ActivityIndicator에만 표시합니다.

```csharp
var activity = Fluent.ActivityIndicator(new(
    Size : UiActivityIndicatorSize.Medium,
    IsAnimating : true,
    AccessibilityLabel : "문서를 불러오는 중"));
activity.Stop();
activity.Options = activity.Options with { HidesWhenStopped = false };
activity.Start();
```

UiActivityIndicator<TPresentation>이 상태와 배치를 소유하고 UiFluentActivityIndicator가 생성된 Fluent 어댑터입니다. 기본값은 Medium·정지·정지 시 숨김입니다. 숨기더라도 원래 정사각형 공간을 유지하므로 주변 내용이 움직이지 않습니다. Stop은 회전을 0으로 돌리고 추가 프레임 요청을 멈춥니다. HidesWhenStopped가 false이면 정지된 원호를 보여줍니다. Start는 0부터 다시 시작합니다.

Fluent iOS 원본의 둥근 끝 270도 원호, 750ms 선형 회전과 토큰을 사용합니다. ExtraSmall·Small·Medium·Large·ExtraLarge의 지름은 12·16·24·32·36, 선 두께는 1·1·2·3·4 논리 픽셀입니다. 기본 색상은 밝은 테마 #8F8F8F, 어두운 테마 #B8B8B8이며 Color로 바꿀 수 있습니다. 시스템 대비 색상은 사용자 색상보다 우선합니다. 지원하지 않는 크기와 색상 모드는 명시적으로 거부합니다.

ReducedMotion은 Duxel의 확장 옵션으로, 활동 중이라는 의미는 유지하면서 회전을 정지하고 프레임 요청을 멈춥니다. 접근성에는 원본의 이미지 역할을 사용하고 활동 여부를 Busy로 제공합니다. 숫자 범위나 키보드 포커스는 없습니다. AccessibilityLabel과 일반 AccessibleName으로 이름을 지정할 수 있으며, 숨겨진 상태에서는 의미 요소를 게시하지 않습니다.

이 위젯은 UIKit에 의존하지 않는 Duxel의 네이티브 구현입니다. iOS 런타임을 지원하거나 SwiftUI 실제 화면과 모든 픽셀이 같다는 뜻은 아닙니다. 전체 공식 접근성·모션·플랫폼 수용 비교는 남아 있습니다. 카탈로그 연결은 46/55이고 미연결은 9개입니다.

검증 기록: artifacts/fluent-activity-indicator-2026-11-17/result.json. CPU 14건·Vulkan 12건·카탈로그 4건·갤러리 GPU 270건 통과. 26개 집중 검사는 정상 크기·지원 팔레트에 대한 결과입니다. 이후 지원하지 않는 색상 모드를 명시적으로 거부하도록 분기를 정리했고 지원 팔레트의 색상 값은 유지했습니다. 최종 Windows/Linux 호스트 빌드는 경고·오류 0입니다. 기존 위젯·종료 A/B·전체 네이티브 검사는 반복하지 않았습니다. 실제 Linux와 고정 RC3 NativeAOT는 실행하지 않았습니다.

공식 원본: https://github.com/microsoft/fluentui-apple/blob/main/Sources/FluentUI_iOS/Components/ActivityIndicator/ActivityIndicator.swift
공식 토큰: https://github.com/microsoft/fluentui-apple/blob/main/Sources/FluentUI_iOS/Components/ActivityIndicator/ActivityIndicatorTokenSet.swift

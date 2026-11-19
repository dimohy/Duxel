# Fluent 2 Divider

## 측정과 부모 할당 — 2026-11-10

`UiDivider`는 `UiControlContext.MeasureDivider<TPresentation>`을 통해 순수 `MeasureContent`를 구현합니다. `IUiDividerPresentation.Measure` 기본 구현은 미지원 예외를 냅니다. 사용자 표현은 이를 구현하고 변경된 할당을 받을 때 `CanArrange`를 명시합니다. 참여한 렌더는 공통 `PlaceText` 루트 하나를 지정된 경계에 배치하고 그 경계로 그리기를 잘라야 합니다. 공통 경로가 루트를 검사하며 성공/실패 뒤 표·열 상태를 복원합니다. `MeasureFluentDivider`는 배치·그리기 없이 기본 기하를 제공합니다.

```csharp
using var divider = Fluent.Divider("Section", new(Length : 220));
using var box = Dux.SizedBox(divider, 80, 64);
// The actual root is 80 x 64. Length describes the natural layout.
```

실제 부모 너비로 수평 캡션을 줄바꿈합니다. 양 축으로 선 길이·교차축 중앙 정렬·루트 클립을 결정하며 짧거나 0인 루트도 반영합니다. Regular 12px·행 상자 16px는 유지합니다. 기존 즉시 오버로드와 독립 위젯은 자연 크기를 보존하며 즉시 호출은 `ui.Divider<TPresentation>(text, options, constrainLayout : true)`로 참여합니다. 읽기 전용 콘텐츠 제공자는 명시적 측정/렌더 단계마다 한 번 읽고 부모 배치가 별도 측정 단계를 호출할 수 있습니다. 측정은 그리기·입력 처리를 하지 않습니다. 위젯/컨텍스트 필드·캐시는 추가하지 않고 기존 공통 텍스트 배치 스코프를 재사용합니다.

원본에서 SizedBox 측정/렌더 예외와 독립 루트 220 x 16을 재현했습니다. 고정 .NET11 RC3 관리 검사 525개 통과: CPU·수명 64, 기존 GPU 19, 할당 픽셀 비교 400, 서로 다른 주기의 유지 프레임 비교 40, 재사용 횟수 2. 래스터/벡터·MSAA 1/4·Light/Dark·프레임 슬롯 3·정렬·RTL·inset·빈/다중행/리터럴 캡션·짧은/0 경계·사용자 생성 표현을 포함합니다. 준비 프레임을 포함한 readback 911개와 로그·소스 해시·verification.json은 `artifacts/divider-allocation-2026-11-10/`에 보존합니다.

새 로컬 `layout_divider_allocation_fba.cs`는 Width : 100, Height : 120, Orientation, Short/Empty/Reset을 제공합니다. 로컬 참조 빌드·첫 표시·15초 종료를 수동 조작과 구분해 확인했습니다. 현재 수정의 Native 검증은 표준 VS C++ 도구 탐색 부재로 미검증이며 아래 과거 Native 근거는 당시 리비전에만 적용됩니다. FPS·메모리·할당 개선은 주장하지 않습니다. 전체 수치·리사이즈 보류를 유지합니다.

2026-11-05. 구현 범위는 [공식 사이트 기준](fluent2-scope.ko.md)을 따른다. Divider는 기존 상위55개 이름 안에 있으며 범위를 확장한 항목이 아니다.

공식 [사용 지침](https://fluent2.microsoft.design/components/web/react/core/divider/usage)과 Microsoft Fluent UI `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`의 Divider 타입·스타일을 고정했다. 원본과 해시는 `artifacts/fluent-divider-2026-11-05/source-manifest.json`에 있다.

## 실제 API와 표현

```csharp
using var plain = Fluent.Divider();
using var caption = Fluent.Divider("Section", new(
    Alignment : UiDividerAlignment.Start,
    Appearance : UiDividerAppearance.Brand,
    Inset : true));
using var vertical = Fluent.Divider("Details", new(Vertical : true, Length : 120));
using var live = Fluent.Divider(() => currentCaption);
caption.Text = "Updated";
```

가로·세로, 시작/중앙/끝 정렬, 기본/약한/강한/브랜드 표현, inset, 내용 없는 선과 실제 Regular12px/16px 내용 줄 상자를 지원한다. 내용 간격과 inset은12px, 시작/끝 짧은 선은8px이며 세로 최소 높이는 내용 없을 때20px·있을 때84px다. Length는 레이아웃에서 명시하는 길이이며0은 가로 가용 너비 또는 세로 최소 높이를 사용한다. 가로 RTL은 논리적 시작/끝을 반전한다. 여러 줄 내용은 감싸며 생략하지 않는다. 선은 표 셀·상위 클립을 지킨다.

`UiDivider<TSource,TSourcePolicy,TPresentation>`는 기본/사용자 위젯의 공통 기반이다. 내용 공급과 표현 구현을 정적으로 분리한다. 생성된 `UiFluentDivider`/`UiFluentDynamicDivider`, `Fluent.Divider`, `Dux.Divider`와 `ui.Divider`가 같은 권위 있는 표현을 소비한다. 사용자 상속은 ReadText를 확장하고, IUiDividerPresentation은 구성·측정·그리기 알고리즘을 교체할 수 있다. Source/Options는 변경된 값 게시 후 유휴 프레임을 요청하며 Dispose는 공급자를 해제한다.

기존 Separator 구현·호환 API는 보존했다. 모든 기존 비표준 경로의 비노출 완료를 의미하지 않는다. `UiTheme.FluentColorScheme`은 공통 색 데이터 선택이며 이전 BadgeColorScheme은 같은 저장값의 호환 별칭이다. 위젯 구현 선택은 소스 생성기의 닫힌 제네릭 연결로 고정되고 런타임 테마 레지스트리를 추가하지 않는다. 색 선택 변경은 정적 레이어를 무효화한다.

## 검증과 남은 범위

.NET10 Divider43개, 전체1170개 검사가 통과했다. 고정 .NET11 RC3 NativeAOT EXT/core·프레임 자원2개 구성은 각각317개 통과했다. Divider MSAA1/4·라이트/다크·방향/정렬/표현 행렬의 관리 이미지30장을 Native 두 경로와 전체 바이트 대조(60건), 기존 Badge 검증 이미지179장을 각 경로와 대조(358건)해 모두 일치했다.

초기 GPU 비교는 root-MSAA 인수 누락과 검사기의 과거 프레임 MSAA를 현재 설정으로 복원하는 오류를 발견했다. 실제 요청 설정을 읽는 테스트 전용 관측값으로 복원을 고쳤고 같은 조건으로 다시 비교했다. 생산 Vulkan 동작을 변경해 비교를 맞춘 것이 아니다. 기존 Badge 관리 할당 검사의 한 차례 실패는 관측 추가 실행의 통과만으로 원인이 해결됐다고 판단하지 않는다.

공식 브라우저 참조와의 시각적 동등성, 실제 Windows UIA, 강제 색상 및 표준 임의 자식 콘텐츠 슬롯은 미완료다. 현재 사용자 표현 교체 API를 해당 슬롯 완료로 대신하지 않는다. 새 앱 전체 성능·Private·Pi 개선율은 측정하지 않았다. 명시적 정적 레이어 ContentVersion 계약은 유지한다. 실행·소스/바이너리 해시·미해결 사항은 `artifacts/fluent-divider-2026-11-05/verification.json`을 따른다.

갤러리 실행은 [실행 가이드](widget-gallery-run.ko.md)의 `--divider`를 사용한다. 시작/첫 프레임/자동 종료 확인과 사용자 육안·입력 확인은 구분한다.


## 부모 측정 재사용 — 2026-11-12

정적 텍스트 소스와 결정적인 Divider 표현은 기존 부모 측정 재사용에 참여합니다. getter 소스와 알 수 없는 표현은 기본적으로 동적 측정을 유지합니다. 소스·옵션 변경은 소유한 부모를 무효화하며 실제 렌더링은 각 프레임에서 계속 실행합니다. 파생 클래스가 가변 측정 상태를 추가하면 재사용을 해제하거나 RequestRebuild로 무효화해야 합니다.

근거: artifacts/form-layout-retention-2026-11-12/verification.json. 관찰: ./run-gallery.ps1 → 측정 재사용.

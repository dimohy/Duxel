# Fluent 2 Spinner

## 부모 할당 — 2026-11-10

Spinner가 순수 측정과 실제 부모 할당 렌더에 같은 경로를 사용합니다. `IUiSpinnerPresentation.Arrange`가 자연 배치와 실제 루트 크기를 받습니다. Fluent는 링 지름·타이포그래피·8px 간격을 유지하면서 링과 라벨을 할당 루트 중앙에 배치하고 작거나 0인 영역은 클립합니다. 고정한 [Fluent 루트 중앙 정렬·링 축소 금지 규칙](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-spinner/library/src/components/Spinner/useSpinnerStyles.styles.ts)을 따르며 전체 브라우저 외형 일치를 주장하지 않습니다.

```csharp
var spinner = Fluent.Spinner("Working", new() { Width = 260 });
var allocated = Dux.SizedBox(spinner, 80, 96);
```

루트 크기가 달라지는 사용자 표현은 정적 `Arrange`를 구현해야 합니다. 기본 구현은 예외를 던지고 잘못된 반환 크기는 측정 중 거부합니다. 사용자 컨트롤은 `UiControlContext.MeasureSpinner<TPresentation>`와 명시적 `Spinner(..., constrainLayout : true)` 오버로드를 사용할 수 있습니다. 기존 즉시 오버로드의 자연 크기는 유지합니다. 표시 지연 중에는 콘텐츠 없이 할당된 루트 영역만 유지하며 자연 높이 0과 숨겨진 접근성 상태를 보존합니다. 측정은 애니메이션 동작을 실행하거나 시작하지 않습니다. 위젯 필드와 배열은 추가하지 않았습니다.

원본에서 부모 측정 80x96과 실제 루트 260x32 불일치를 재현했습니다. 현재 .NET11 RC3 관리 검증은 CPU·의미 정보·예약 프레임 180개와 정확한 픽셀 비교 340개로 총 520개가 통과했습니다. 크기·라벨 위치·RTL·Light/Dark·MSAA1/4·프레임 슬롯3을 포함하며 준비 이미지를 포함한 readback은 617개입니다. 동적 부모 변경·획득 이미지 재생·생성 사용자 표현·미지원/잘못된 Arrange·0크기·지연 루트를 검사했습니다. 빌드 경고·오류는 0개입니다. C++ Build Tools 부재로 Native는 미검증입니다. 근거: `artifacts/spinner-allocation-2026-11-10/verification.json`. 전체 목표 수치와 리사이즈 보류를 유지하며 성능 개선은 주장하지 않습니다.


Spinner는 [공식 Fluent 2 사이트](https://fluent2.microsoft.design/components/web/react/core/spinner/usage)의 **Preview** 컴포넌트다. 구현 기준은 Microsoft Fluent UI 커밋 `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`(MIT)이며 조회한 현재 Spinner 스타일도 이 파일과 같다. 출처 URL·해시는 `artifacts/fluent-spinner-2026-11-06/source-manifest.json`에 있다.

## 구성과 정적 구현 선택

`Fluent.Spinner(label, options)`와 `Dux.Spinner(label, options)`는 상속 가능한 생성 클래스 `UiFluentSpinner`를 만든다. 기반은 `UiSpinner<UiFluentSpinnerBehavior, UiFluentSpinnerPresentation>`이다. 사용자 위젯도 같은 상속·`[UiWidgetStyle]` 소스 생성 계약을 사용한다. 동작은 지연/모션 수명, 표현은 타이포그래피·배치·링 구성을 담당한다. 런타임 테마 레지스트리나 리플렉션으로 구현을 선택하지 않는다.

Build에서 인스턴스를 유지한다. 매 프레임 새로 만들면 지연과 모션이 다시 시작한다.

```csharp
protected override IUiView Build(UiBuildContext context)
{
    var spinner = context.Child("connection", () =>
        Fluent.Spinner("Connecting to data …"));
    spinner.Options = new(UiSpinnerSize.Large,
        ReducedMotion: true) { Width = 260 };
    return spinner;
}
```

즉시 모드 API의 동작 상태는 호출자가 소유한다.

```csharp
private UiFluentSpinnerBehavior _spinner;
// Render에서:
ui.Spinner<UiFluentSpinnerBehavior, UiFluentSpinnerPresentation>(
    "connection", ref _spinner, "Connecting to data …");
```

## 변형과 수명

크기는 논리 픽셀 기준 ExtraTiny16, Tiny20, ExtraSmall24, Small28, Medium32, Large36, ExtraLarge40, Huge44이며 선 두께는2/3/4다. Primary·Inverted는 고정한 brandWeb·중립 토큰을 사용하고 Inverted는 강조 표면 위에 배치한다. 라벨은 Before/After/Above/Below와8px 간격을 지원한다. 작은 크기는 Body1, Medium부터 ExtraLarge는 Subtitle2, Huge는 Subtitle1이다. RTL은 가로 순서와 링 방향을 반전한다.

`Width=0`은 최소 콘텐츠 폭을 지키며 가용 폭을 사용한다. 양수는 같은 최소 폭 조건으로 루트 폭을 지정한다. 순수 측정과 렌더는 같은 정적 표현을 사용한다. 라벨이 없으면 `AccessibleName`을 지정할 수 있다. 명시적 Parent 정체성은 라벨을 사용하므로 라벨이 없거나 중복되면 키로 인스턴스를 유지하거나 Instance 정체성을 선택한다.

지연은 인스턴스의 첫 렌더에서 시작한다. 대기 중 지연을 바꾸면 시점을 다시 잡고 이미 표시한 인스턴스는 표시를 유지한다. 지연 중 자연 루트 높이는 0이며 부모가 할당한 영역은 유지한다. 일반1500ms 모션은 고정한135도 꼬리 두 개·105도 마스크·EasyEase 곡선을 따른다. 모션 감소는 공식1800ms 회전과 원추형 페이드를 유지한다. 제거/해제하면 이 인스턴스는 애니메이션 프레임을 요청하지 않는다. 표시 지연 중에는 공통 [예약 프레임 경로](frame-scheduling.ko.md)를 사용하며 표시된 모션은 연속 애니메이션을 요청한다.

공통 `UiDrawListBuilder.AddAnnularSector`는 각도별 정점색,1픽셀 반경 방향 커버리지와 최대0.01 논리 픽셀의 현 오차를 사용한다. 큰 메시도 정밀도를 낮추지 않고 나눈다. 샘플 전용 최적화가 아닌 일반 그리기 기능이다. 애니메이션 기하의 CPU 구성·업로드 비용은 남아 있으며 앱 FPS·Private·Pi 향상은 주장하지 않는다.

## 접근성과 검증 범위

공통 의미 정보 트리는 확정되지 않은 진행률의 UIA ProgressBar, 라벨/명시 이름과 클립 영역을 제공한다. 키보드 포커스나 가공한 RangeValue를 제공하지 않는다. 실제 갤러리16개 예제의 외부 네이티브 UIA33개 검사를 통과했다. 이벤트·알림·전체 접근성 검증은 남아 있다.

.NET10 전체3,140·집중 GPU1,312, 고정 .NET11 RC3 NativeAOT EXT2/core3 각각1,312개를 통과했다. 이미지1,137개가 각 Native 모드에서 .NET10과 일치한다. 여덟 크기·표현·위치·색 구성·MSAA1/4·래스터/벡터·RTL/모션 감소와 독립적인 내부색/중앙 구멍 표본을 검사했다. Duxel 경로와 실행기 비교이며 **공식 브라우저 픽셀과의 동치는 아니다**. 공식 전체 룩앤필·강제 색상·임의 슬롯 교체·남은 접근성 검증은 미완료다. 원래 전체 목표를 활성 상태로 유지한다.

갤러리 실행:

```powershell
./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -DotnetPath 'C:/Users/dimohy/.cache/duxel-dotnet-10.0.301/dotnet.exe' -ExtraArgs @('--spinner')
```

## 일시 정지 — 2026-11-11

`UiSpinnerOptions.Paused`의 기본값은 `false`입니다. `true`이면 현재 위상을 유지하고 연속 프레임을 요청하지 않습니다. 다시 `false`로 바꾸면 정지 시간을 제외하고 이어서 회전합니다. `ReducedMotion`은 느린 회전이며 정지 옵션과 별개입니다. 표시 지연은 정지 중에도 한 번의 기한을 예약합니다.

```csharp
var spinner = Fluent.Spinner("대기", new() { Paused = true });
spinner.Options = spinner.Options with { Paused = false };
```

관찰 샘플은 기본 정지로 시작하고 재생 전환 버튼을 제공합니다. 고정된 determinate 진행률은 연속 렌더링의 원인이 아닙니다. 근거: `artifacts/observer-idle-panel-2026-11-11/`의 Spinner·실제 Fluent 페이지·예약 프레임 검사. 이 변경은 NativeAOT 검증이나 처리 속도 개선 측정이 아닙니다.

2026-11-12 후속 — [콘텐츠 측정 계약](widget-cpu-retention.ko.md): 결정론적 소스·표현은 부모 재사용에 참여하고 동적 문자열/배지 getter와 사용자 특성은 기본 재측정합니다. getter Progress의 선언 기하와 Spinner의 안정된 표시는 별도 약속이며 그리기·getter·애니메이션·이동 처리는 계속 실행합니다. 집중26개와 실제 GPU 비교는 이 범위의 근거이며 전체 NativeAOT·성능 완료가 아닙니다. 근거: artifacts/content-layout-retention-2026-11-12/verification.json.

# 위젯 자연 측정과 Field 배치

2026-11-10 [Field 부모 할당](fluent2-field.ko.md#부모-할당--2026-11-10): 실제 너비로 측정하고 정적 FieldWidth/Arrange·지역 복사본으로 설명 행을 예약한 나머지 높이를 실제 편집기에 줍니다. 80 x 64 측정·80 x 32 실제 경계 결함을 수정했습니다. Field 관리1018·별도 좌표회귀1563검사 통과, 자연 캐시·편집기/undo·Core UIA 메타데이터 유지. Panel 측정 거부/닫힌 독립 루트 경계 확인. Native 미검증·컨테이너3/6 유지. 로컬 샘플:layout_field_allocation_fba.cs.

2026-11-10 [할당 자식 좌표](widget-containers.ko.md#할당-자식-좌표--2026-11-10): 공통 자식 렌더가 상위 클립을 보존하고 기존 표/열 좌표 적용을 격리·복원합니다. 중첩 Padding의 실제 오프셋을 말단 14변형에서 반영합니다. 관리 1529검사(CPU 1113, 래스터/벡터 정확 GPU 비교 416) 통과. 당시 확인한 Field 부모 80 x 64·실제 80 x 32 결함은 위의 후속 Field 할당 체크포인트에서 수정했습니다. 확인한 표준 경로에서 Native 전제 도구가 없고 대체 L: 경로는 접근 거부되었습니다.

2026-11-10 [Badge 측정·할당](fluent2-badge.ko.md#측정과-부모-할당--2026-11-10): 공통 기하·루트 하나, 고정 글자/아이콘 중앙 정렬·클립·숨은 카운터 예약을 연결합니다. 읽기 전용 제공자와 기존 숫자 메모를 명시적 단계에서 공유합니다. 관리 374검사 통과, Native 미검증. 필드/새 캐시 없음. 로컬 샘플: layout_badge_allocation_fba.cs.

2026-11-10 [Link 측정·부모 할당](fluent2-link.ko.md#측정과-부모-할당--2026-11-10): 실제 너비로 줄바꿈하고 정적 Arrange는 지역 복사본을 반환하며 제한된 자연 캐시를 보존합니다. 입력·그리기·클립이 실제 루트를 공유합니다. CPU/입력·래스터/벡터·MSAA 1/4·Light/Dark·서로 다른 유지주기를 포함해 관리 495검사 통과. 필드/새 캐시 추가 없음; Native 미검증. 로컬 샘플: layout_link_allocation_fba.cs.

2026-11-10 [Divider 측정·부모 할당](fluent2-divider.ko.md#측정과-부모-할당--2026-11-10): 정적 Measure·CanArrange로 공통 루트 하나를 배치합니다. 실제 너비로 캡션을 줄바꿈하고 양 축으로 선·중앙 정렬·클립을 결정합니다. 읽기 전용 콘텐츠 제공자는 명시적 측정/렌더 단계마다 한 번 읽습니다. 관리525검사 통과, Native 미검증. 위젯/컨텍스트 필드·캐시 추가 없음. 로컬 샘플:layout_divider_allocation_fba.cs.

2026-11-10 [텍스트 부모 할당](fluent2-text.ko.md): CanArrange로 공통 텍스트 루트 하나를 배치하며 할당/측정 너비로 줄바꿈하고 폰트는 유지합니다. 기존 콘텐츠 제공자 읽기를 보존합니다. 래스터/벡터·MSAA1/4·Light/Dark·16/3프레임 유지 갱신을 포함해 관리321검사 통과. 위젯 필드 추가 없이 컨텍스트 카운터 하나를 추가했고 Native는 미검증입니다. 새 로컬 샘플:layout_text_allocation_fba.cs.

2026-11-10 [Progress 측정·할당](fluent2-progress.ko.md): 정적 소스/표현 측정으로 getter 호출을 분리합니다. getter 높이를 선언하거나 부모 강제 높이를 사용하며 정적 Arrange와 공통 렌더가 실제 루트를 반영합니다. 관리306검사 통과, Native 미검증입니다. 새 로컬 샘플:layout_progress_allocation_fba.cs. 전체 수치·리사이즈 보류를 유지합니다.

`widget.Measure(ui, availableSize)`는 현재 컨텍스트에서 원하는 크기를 구한다. 기본/사용자 위젯은 같은 `protected override UiVector2 MeasureContent(UiControlContext context)`를 사용한다. 가용 크기는 유한한0이상의 제약이며, 원하는 크기를 무조건 잘라 반환하는 값이 아니다. 최종 클립은 할당 영역에 적용한다.

현재 Button·Toggle 계열(Checkbox/Switch/ToggleButton)·Input·Slider·RadioGroup·Spinner·Progress·Text·Divider·Link·Badge/CounterBadge·Field가 실제 표현의 측정을 제공한다. getter 기반 Progress는 높이 선언이나 강제 부모 높이를 요구하며 사용자 소스는 순수 배치 메타데이터를 제공할 수 있다. 지원하지 않는 위젯은 명시적으로 실패한다. 모든 레거시 뷰·Dux 컨테이너를 자동 측정한다고 광고하지 않는다. 새 사용자 위젯은 순수 측정 구현을 제공해야 하며 렌더링을 대신 실행해서 크기를 얻지 않는다.

입력·값 컨트롤 측정은 값 바인딩·이벤트 콜백·키보드/포인터 처리를 실행하지 않는다. 텍스트는 콘텐츠가 고유 크기를 결정하므로 읽기 전용 콘텐츠 제공자를 읽으며 렌더·입력·이벤트를 실행하지 않는다. Input의 장식 영역과 RadioGroup의 줄 배치도 실제 렌더링과 같은 권위 있는 측정을 사용한다. 컨텍스트의 커서·다음 항목 너비·위젯 정체성·가용 영역을 복원한다. 잘못된 크기·재진입·측정 중 위젯 렌더링/공통 입력 처리를 거부하며 실패 후 다음 측정이 가능하다. 사용자 구현도 그리기·이벤트 호출·상태 변경을 하지 않는 계약을 지켜야 한다.

Build와 측정은 다른 단계다. 측정이 Build를 암묵적으로 실행하거나 임의 IUiView를 렌더링하지 않는다. 다음처럼 미리 소유한 자식을 측정과 Build 양쪽에서 사용할 수 있다.

```csharp
public sealed class ProfileEditor : UiWidget
{
    private readonly UiFluentField _name;
    public ProfileEditor(UiBinding<string> value)
    {
        _name = Fluent.Field(Fluent.Input("Name", value, 256));
        _name.Options = new() { Label = "Display name", Required = true };
        OwnChild("name", _name);
    }
    protected override IUiView Build(UiBuildContext context)
        => context.Child("name", () => _name);
    protected override UiVector2 MeasureContent(UiControlContext context)
        => context.Measure(_name, context.AvailableSize);
}
```

`Fluent.Field(control)`·`Dux.Field(control)`·`new UiFluentField(control)`는 자연 측정을 사용한다. Width가0이면 현재 가용 너비를 사용한다. Width를 지정하면 바깥 Field 너비이며 가로 배치는33% 라벨 열을 제외한 너비를 자식에 제약으로 전달한다. 자식의 실제 원하는 높이를 할당하고 Field 설명을 그 아래 배치한다. 자식은 양수 높이를 제공해야 한다.

```csharp
var field = Fluent.Field(Fluent.Input("Email", email.Binding, 256));
field.Width = 220;
field.Options = new() { Label = "Work email", Hint = "Use your work address" };
```

기존 `Fluent.Field(control, new UiVector2(220,32), options)`는 그대로 명시적 할당 방식이다. `ControlSize`를 설정하면 명시적 방식, `Width`를 설정하면 자연 측정을 선택한다. 자연 모드에서 `ControlSize`는 마지막 명시 설정이며 현재 측정 결과가 아니다. 직접 API는 `ui.AutoField<TBehavior,TPresentation>(control,width,options,content)`다. 기존 target-typed new 호출의 모호성을 피하려고 같은 이름의 float 오버로드를 추가하지 않았다.

소스 생성기가 새1인수 생성자와 기본 팩터리, 닫힌 동작/표현 연결을 생성한다. 런타임 레지스트리나 타입 문자열 탐색은 사용하지 않는다. Field는 자식 크기를 매번 확인하므로 상태나 장식 변경 뒤 부모 캐시가 오래된 높이를 유지하지 않는다. 현재 측정의 CPU·정체성 범위 처리·텍스트 작업 비용과 추가 검사 분기는 있다. 앱 CPU/할당/Private/FPS 개선을 주장하지 않으며 동일 조건 반복 측정은 별도 과제다.

GPU 검사는 입력 세 크기·Checkbox·Switch·Slider·RadioGroup·사용자 위젯·높은 Input 자식을 실제로 먼저 렌더링한 크기와 비교한다. 자연 Field와 그 크기를 사용한 명시적 Field의 픽셀이 일치해야 한다. 두 너비·가로/세로·라이트/다크·MSAA1/4·래스터/벡터를 검사한다. 좁은 가로 라벨은 자신의 열로 클립하며 라벨 없는 대조 화면과 이웃 영역을 비교해 침범을 검출한다. 공식 브라우저와의 전체 동치·UIA·임의 슬롯·모션·전체 컨테이너 배치·Pi 실측은 남아 있다.

근거: `artifacts/widget-measurement-2026-11-06/`. `--field` 샘플은 고정 높이를 제거했고 가용 폭을 사용하는 Field도 제공한다. [실행 가이드](widget-gallery-run.ko.md).

최종 검토에서는 내비게이션 편집이 내부 편집기의 거부 전에 항목·hover·포커스를 바꾸는 사례를 먼저 작은 실패 검사로 재현했다. 바깥 진입점에서 상태 변경 전에 거부하도록 수정하고 라벨·캐럿 진입점도 검사한다. 초기 통과 결과와 이 수정이 포함된 최종 검증은 별도 경로로 보존한다. 사용자 표현이 레이아웃 재사용을 선택한다면 선언한 캐시 의존성을 지켜야 하며 임의 외부 상태를 읽는 측정을 그대로 캐시하면 안 된다.

## 소유한 조합의 명시적 위임 — 2026-11-12

사용자 조합 UiWidget은 기본적으로 측정을 유지하지 않습니다. 측정을 소유한 UiWidget 하나에 위임하는 래퍼는 protected CanRetainOwnedMeasurement(child)로 CanRetainMeasurement를 명시적으로 재정의할 수 있습니다. 이 도우미는 살아 있는 자식의 소유권을 검증하고 미소유·다른 소유자·폐기된 자식을 거부하며, 자식을 재구성하거나 측정하지 않고 기존 계약을 확인합니다. 임의의 Build 의존성을 추측하지 않습니다. 래퍼가 추가 측정 입력을 사용하면 RequestRebuild 또는 관찰한 상태로 무효화해야 합니다. 고정 크기만으로는 참여를 허용하지 않습니다. 다음 예제는 기본 제약 조합 측정을 사용하므로 MeasureConstrained를 호출하고 Build에서 같은 자식 키를 유지합니다. 불투명한 IUiView 루트는 여전히 명시적인 측정 구현이 필요합니다.
```csharp
public sealed class OwnedContent : UiWidget
{
    private readonly UiWidget _content;
    public OwnedContent(UiWidget content)
    {
        OwnChild("content", content);
        _content = content;
    }
    protected override bool CanRetainMeasurement => CanRetainOwnedMeasurement(_content);
    protected override IUiView Build(UiBuildContext context) => context.Child("content", () => _content);
}
```

## 검증된 소유 자식 크기 — 2026-11-12

자연 크기 Field 위젯과 Tooltip이 자식의 명시적 제약으로 기존 UiRetainedLayout을 공유합니다. 살아 있는 자식이 측정 재사용에 동의하고 텍스트 환경도 허용할 때만 소유자별 단일 크기 결과 캐시를 선택적으로 생성합니다. 기본 UiWidget 저장소는 늘리지 않습니다. 최초 크기 확인은 자식 검증·측정을 실행합니다. 이후 리소스·텍스트 키와 제약이 맞아야 재사용하며, 앞서 검증한 자연 크기와 같은 렌더링용 고정 할당도 재사용할 수 있습니다. 조회 전마다 소유권·폐기 상태를 확인합니다. 알 수 없는 자식·글리프 관찰·기존 즉시 모드 AutoField에는 이 캐시를 적용하지 않습니다.

자식·소유자 무효화는 참조를 해제하고 기존 리비전을 증가시킵니다. 무효화·실패한 시도는 등록할 수 없으며, 크기 소비 경로는 같은 프레임의 오래된 준비 기하도 빌리지 않습니다. 기존 컨테이너의 준비 배치 동작은 별도로 유지합니다. 실제 렌더링·입력 값·필드 메타데이터와 비활성 범위·편집/되돌리기·팝업 예약/그리기/동작은 계속 처리합니다. Dispose는 단일 결과 캐시를 해제합니다. 추가 소유 캐시의 저장량은 제한되지만 전체 앱 Private·작업 집합·장시간 증가 및 고정 RC3 NativeAOT 성능은 별도 미완료 항목입니다.

./run-gallery.ps1 → 측정 재사용의 `소유 자식 크기 측정`에서 실제 자연 크기 Field 입력과 Tooltip 대상을 확인합니다. 모든 내부 텍스트·표현 연산이 아니라 위젯 측정 호출 수입니다. 근거: artifacts/owned-child-sizing-2026-11-12/verification.json.

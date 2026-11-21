# 컴파일된 레이아웃 컨테이너

Linux x64 .NET 11 NativeAOT에서 외부 레이아웃 샘플의 상태를 유지할 때와 계속 바꿀 때를 비교했습니다. Tooltip을 여는 프레임마다 UI 렌더링에서 1,293,904바이트를 할당했습니다. 할당 원인과 전체 컨테이너 통합 성능은 확인이 남아 있습니다. [측정 조건과 결과](archive/performance/optimization-session-2026-11-19.md#external-custom-container-steady-and-changing-profiles)

2026-11-11 할당된 배치의 무효화: 부모가 다시 측정하지 않고 같은 크기를 할당해도 Grid·Stack·단일 자식 컨테이너는 다음 프레임에 옵션 변경을 반영합니다. 위젯을 그리는 도중 변경하면 그 프레임은 부모가 준비한 형제 배치를 유지합니다. 명시적 측정 결과를 재사용할 때도 준비 프레임을 갱신하며 중첩 렌더링은 이전의 일시적 문맥으로 복원합니다. 변경 없는 배치는 자식 측정을 계속 재사용합니다. 공통 커널의 기존 유지 기록에 준비 프레임 정수 하나를 추가하며 위젯마다 새 캐시를 만들지 않습니다. 새 CPU7검사·컨테이너 GPU252검사와 기존 readback169개 동일을 확인했습니다. ScrollView75·가상 목록75·수명101·접근성49검사도 통과했습니다. 근거는 `artifacts/assigned-arrangement-invalidation-2026-11-11/verification.json`입니다. 컨테이너3/6을 유지하며 속도 향상·NativeAOT·전체 완료를 주장하지 않습니다.

2026-11-10 [사용자 정의 컨테이너 계약](custom-container-contract.ko.md): 외부 생성 레이아웃 공개 API 15검사 통과, 실제 루트 클립으로 사용자 표현·입력 누출을 수정했습니다. 컨테이너 GPU 회귀 237개·좌표 회귀 2,317개 통과. 샘플: `layout_custom_contract_fba.cs`. 현재 Native·성능 게이트는 미완료, 컨테이너 3/6 유지.

2026-11-10 [키 가상 목록](virtual-list.ko.md): 세로 고정 높이 행을 double 오프셋·뷰포트 근처 좌표로 배치해 10억 항목을 처리합니다. CPU43·확대 좌표2317검사 통과, 범위 밖 행은 기존 소유권으로 해제합니다. 가변 높이/다른 축·교차 키 재활용 정책·외부 UIA·현재 Native는 미완료이며 컨테이너3/6 유지. 샘플: `layout_virtual_list_fba.cs`.

2026-11-10 [소유 ScrollView](scroll-view.ko.md): Dux.ScrollView가 유한 뷰포트 측정·실제 자식 배치·오프셋·스크롤바 수명과 무효화를 소유합니다. CPU47·별도 GPU/좌표1933검사 통과. 월드 좌표 정밀도 부족은 명시적으로 거부하며 큰 콘텐츠 재배치/가상화·현재 Native는 미완료입니다. 컨테이너3/6 유지. 샘플: `layout_scroll_view_fba.cs`.

2026-11-10 [중첩 스크롤 전달](scroll-routing.ko.md): 독립 휠 축의 경계 전달, 호출자 클립·팝업·창 순서 히트 검사, 공통 스크롤바 캡처/해제와 DirectText 실제 줄 높이를 반영합니다. 확대 관리 회귀 1,645검사·readback 1,025회 통과, 컨테이너 3/6 유지. 소유 스크롤 위젯·가상화·현재 Native는 미완료입니다. 샘플: `layout_scroll_routing_fba.cs`.

2026-11-10 [Field 부모 할당](fluent2-field.ko.md#부모-할당--2026-11-10): 실제 너비로 측정하고 정적 FieldWidth/Arrange·지역 복사본으로 설명 행을 예약한 나머지 높이를 실제 편집기에 줍니다. 80 x 64 측정·80 x 32 실제 경계 결함을 수정했습니다. Field 관리1018·별도 좌표회귀1563검사 통과, 자연 캐시·편집기/undo·Core UIA 메타데이터 유지. Panel 측정 거부/닫힌 독립 루트 경계 확인. Native 미검증·컨테이너3/6 유지. 로컬 샘플:layout_field_allocation_fba.cs.

2026-11-10 [Badge 측정·할당](fluent2-badge.ko.md#측정과-부모-할당--2026-11-10): 공통 기하·루트 하나, 고정 글자/아이콘 중앙 정렬·클립·숨은 카운터 예약을 연결합니다. 읽기 전용 제공자와 기존 숫자 메모를 명시적 단계에서 공유합니다. 관리 374검사 통과, Native 미검증. 필드/새 캐시 없음. 로컬 샘플: layout_badge_allocation_fba.cs.

2026-11-10 [Link 측정·부모 할당](fluent2-link.ko.md#측정과-부모-할당--2026-11-10): 실제 너비로 줄바꿈하고 정적 Arrange는 지역 복사본을 반환하며 제한된 자연 캐시를 보존합니다. 입력·그리기·클립이 실제 루트를 공유합니다. CPU/입력·래스터/벡터·MSAA 1/4·Light/Dark·서로 다른 유지주기를 포함해 관리 495검사 통과. 필드/새 캐시 추가 없음; Native 미검증. 로컬 샘플: layout_link_allocation_fba.cs.

2026-11-10 [Divider 측정·부모 할당](fluent2-divider.ko.md#측정과-부모-할당--2026-11-10): 정적 Measure·CanArrange로 공통 루트 하나를 배치합니다. 실제 너비로 캡션을 줄바꿈하고 양 축으로 선·중앙 정렬·클립을 결정합니다. 읽기 전용 콘텐츠 제공자는 명시적 측정/렌더 단계마다 한 번 읽습니다. 관리525검사 통과, Native 미검증. 위젯/컨텍스트 필드·캐시 추가 없음. 로컬 샘플:layout_divider_allocation_fba.cs.

2026-11-10 [텍스트 부모 할당](fluent2-text.ko.md): CanArrange로 공통 텍스트 루트 하나를 배치하며 할당/측정 너비로 줄바꿈하고 폰트는 유지합니다. 기존 콘텐츠 제공자 읽기를 보존합니다. 래스터/벡터·MSAA1/4·Light/Dark·16/3프레임 유지 갱신을 포함해 관리321검사 통과. 위젯 필드 추가 없이 컨텍스트 카운터 하나를 추가했고 Native는 미검증입니다. 새 로컬 샘플:layout_text_allocation_fba.cs.

## 부모 할당과 순수 측정 — 2026-11-10

Progress가 정적 소스/표현 측정과 Arrange로 실제 부모 루트를 반영합니다. 정적 값은 실제 두께를 사용하고 getter 측정은 선언한 Size.Y 또는 강제 부모 높이를 사용하며 렌더 콜백을 실행하지 않습니다. 사용자 소스는 순수 배치 메타데이터를 제공할 수 있습니다. 기존 즉시 API는 자연 크기를 유지하고 명시적 constrainLayout으로 할당에 참여합니다. [Progress 전체 계약과 예제](fluent2-progress.ko.md)를 따릅니다.

.NET11 RC3 관리306검사가 MSAA1/4·Light/Dark·프레임 슬롯3·readback465개로 통과했습니다. 위젯 필드·배열은 추가하지 않았습니다. C++ 도구 탐색 파일 부재로 Native·런타임 비교는 미검증이며 전체 수치·리사이즈 보류를 유지합니다. 근거:artifacts/progress-allocation-2026-11-10/verification.json. 샘플:layout_progress_allocation_fba.cs.

## 부모 할당 — 2026-11-10

Spinner가 순수 측정과 실제 부모 할당 렌더에 같은 경로를 사용합니다. `IUiSpinnerPresentation.Arrange`가 자연 배치와 실제 루트 크기를 받습니다. Fluent는 링 지름·타이포그래피·8px 간격을 유지하면서 링과 라벨을 할당 루트 중앙에 배치하고 작거나 0인 영역은 클립합니다. 고정한 [Fluent 루트 중앙 정렬·링 축소 금지 규칙](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-spinner/library/src/components/Spinner/useSpinnerStyles.styles.ts)을 따르며 전체 브라우저 외형 일치를 주장하지 않습니다.

```csharp
var spinner = Fluent.Spinner("Working", new() { Width = 260 });
var allocated = Dux.SizedBox(spinner, 80, 96);
```

루트 크기가 달라지는 사용자 표현은 정적 `Arrange`를 구현해야 합니다. 기본 구현은 예외를 던지고 잘못된 반환 크기는 측정 중 거부합니다. 사용자 컨트롤은 `UiControlContext.MeasureSpinner<TPresentation>`와 명시적 `Spinner(..., constrainLayout : true)` 오버로드를 사용할 수 있습니다. 기존 즉시 오버로드의 자연 크기는 유지합니다. 표시 지연 중에는 콘텐츠 없이 할당된 루트 영역만 유지하며 자연 높이는 0입니다. 측정은 애니메이션 동작을 실행하거나 시작하지 않습니다. 위젯 필드와 배열은 추가하지 않았습니다.

원본에서 부모 측정 80x96과 실제 루트 260x32 불일치를 재현했습니다. 현재 .NET11 RC3 관리 검증은 CPU·의미 정보·예약 프레임 180개와 정확한 픽셀 비교 340개로 총 520개가 통과했습니다. 크기·라벨 위치·RTL·Light/Dark·MSAA1/4·프레임 슬롯3을 포함하며 준비 이미지를 포함한 readback은 617개입니다. 동적 부모 변경·획득 이미지 재생·생성 사용자 표현·미지원/잘못된 Arrange·0크기·지연 루트를 검사했습니다. 빌드 경고·오류는 0개입니다. C++ Build Tools 부재로 Native는 미검증입니다. 근거: `artifacts/spinner-allocation-2026-11-10/verification.json`. 전체 목표 수치와 리사이즈 보류를 유지하며 성능 개선은 주장하지 않습니다.


## RadioGroup 부모 할당 — 2026-11-10

실제 부모 너비로 텍스트를 줄바꿈합니다. 정적 `IUiRadioGroupPresentation.Arrange`는 자연 옵션 기하를 변경하지 않고 할당된 루트 크기를 반환합니다. Fluent 옵션의 자연 크기와 시작 정렬 입력 영역을 유지하므로 루트의 빈 공간은 선택되지 않습니다. 루트 클립은 그리기와 입력에 적용됩니다. 높이만 바뀌면 `ReuseLayout=true`인 표현의 자연 측정을 재사용하며 높이에 의존하는 사용자 표현은 false를 유지합니다. 미지원 또는 잘못된 Arrange는 명시적으로 실패합니다.

현재 소스의 .NET11 RC3 관리 검증264개가 MSAA1/4·프레임 슬롯3에서 통과했고 readback245이미지를 생성했습니다. 검사기와 새 `layout_radio_allocation_fba.cs` 샘플 빌드는 경고·오류0개입니다. 샘플은 세 배치·공유 선택·Width/Height를 제공하며 첫 프레임 제출과15초 정상 종료를 확인했습니다. 수동 입력·외형 승인은 별도입니다.

Visual Studio C++ Build Tools/vswhere.exe 부재로 Native 게시가 실패했습니다. Native 실행과 관리/Native 이미지 비교는 미검증이며 이전 L:/Cache 근거는 접근할 수 없습니다. 근거:`artifacts/radio-resume-2026-11-10/verification.json`. 엔진15/27·위젯9/21·컨테이너3/6·통합8/15·이식5/8을 유지하며 Fluent/전체 분모는 미확정입니다. 리사이즈 보류를 유지하며 성능 개선은 주장하지 않습니다.


Last synced: 2026-11-10

## 측정·배치 결과 유지

공통 결과 저장소를 Row/Column·Container·Stack에도 연결했습니다. 부모 데이터 Expanded/Flexible/Spacer/Positioned는 부모가 정한 강제 크기에 바로 렌더하여 자식을 두 번 측정하지 않습니다. Row/Column은 순회 중 자식 위치를 계산하므로 전체 증분 배치 완료를 주장하지 않습니다. `IUiContainerLayout`·사용자 축·사용자 flex맞춤 정책은 기본 비캐시이고 기본 박스 배치/축/맞춤만 명시적으로 참여합니다. 추적하지 않은 크기 입력을 추가한 파생 클래스는 해당 `CanRetainArrangement` 또는 `CanRetainMeasurement`를 꺼야 합니다.

모든 컨테이너 계열이 선택적 `UiRetainedLayout<TResult>` 한 개를 공유합니다. 변경 횟수는 그 기록 안에 두어 비캐시 배치 인스턴스의 상시 변경 횟수 필드를 없앴습니다. 아직 참여하지 못한 하위 트리는 자식이 유지 결과를 확보한 다음 측정부터 참여합니다. 측정 중 자식이 변경된 최초 결과를 잘못 등록하지 않습니다. Container에는 참조 필드 한 개가 추가되므로 생성 할당 비용을 유지 측정 시간과 분리해 기록합니다. 최신 결과:`artifacts/container-retention-2026-11-06/verification.json`.

최신 관리/고정RC3 Native 각각175검사·MSAA1/4·3슬롯121이미지가 일치합니다. 변경 없는 Row/Column/Container/Stack 렌더에서 추가 측정이 없고 가중치·옵션·위치 변경과 사용자 동적 정책이 계속 반영됨을 횟수 검사로 확인했습니다. 이전 검증 Native를 기준으로 재사용해 기준 빌드를 반복하지 않았고 같은 측정 소스를3회씩 ABBAAB 비교했습니다. 기존 Wrap/Grid 유지 측정 중앙값은양쪽0.3µs이며 변경 후1회1.7µs와p95/p99 중앙값0.3→0.4µs를 포함합니다. 범위가 겹쳐 속도 개선을 확정하지 않습니다. Container 소유권 생성1만 개는 추가 참조 때문에6,000,000→6,080,000B(+80,000B,+1.33%)이고 첫 Build880,000B·변경 없는 Build0B는 유지합니다. Private/작업 집합 범위도 겹칩니다. 정확한 재사용 범위를 넓힌 결과이며 전체 메모리·성능 최적화 완료로 보고하지 않습니다.

Wrap/Grid는 제약·설정된 타이포그래피·무효화된 위젯 속성이 그대로면 기존 자식 사각형을 재사용합니다. `RequestRebuild`·관찰 상태·자식 채택/제거·해제가 부모 측정을 무효화하며 부모 구성을 다시 만들지는 않습니다. 변경 없는 형제 배치는 결과를 유지할 수 있습니다. 그리기·입력·콜백은 계속 실행합니다. 실제 렌더 할당이 측정 크기와 같으면 그 측정에서 정한 배치를 사용해 강제 크기로 다시 측정하는 중복을 줄입니다.

사용자 위젯/표현은 기본 비캐시입니다. 말단은 `CanRetainMeasurement`,사용자 `UiArrangedChildrenWidget`은 `CanRetainArrangement`로 명시적으로 참여합니다. 크기는 박스 제약·설정된 타이포그래피·변경 시 `RequestRebuild`하는 위젯 속성에만 의존해야 합니다. 시간·포인터·임의 테마/스타일 조회·추적하지 않은 외부 데이터에 의존하면 비캐시 측정이 필요합니다. 측정을 바꾼 Fluent 버튼 파생 클래스도 상속된 계약을 지키거나 명시적으로 끕니다.

```csharp
protected override bool CanRetainMeasurement => false;
```

`IUiButtonPresentation.CanRetainMeasurement`는 컴파일 시점 정책이며 기본 false입니다. Fluent 중간/작은/큰 버튼 표현이 참여하며 동작·그리기는 독립적으로 유지합니다. 문맥 정체성·백엔드 세대·타이포그래피/폰트 자원·콘텐츠/텍스트 배율·폰트/줄 설정·제약을 재사용 조건에 포함합니다. 글리프 관찰자가 있으면 재사용하지 않습니다. 자원 토큰은 렌더 문맥·백엔드·아틀라스를 역참조하지 않습니다. 참여하는 배치 위젯당 선택적 기록 한 개만 두고 기존 영역 배열을 재사용합니다. 측정 중 변경되면 결과를 등록하지 않으며 배치 실패 시 이전 결과도 무효화합니다. 현재 위 컨테이너 계열도 공통 저장소를 사용하며 전체 증분 배치는 남은 작업입니다.

근거:`artifacts/layout-retention-2026-11-06/verification.json`. 관리/고정RC3 Native 각각159검사·GPU109이미지 동치에 실제 레이블 크기/글리프 변경과 여러 획득 이미지 재사용을 포함합니다. CPU 전용 측정은 **무텍스트** Fluent 버튼 Wrap1만+Grid1만,요청1x1,준비96회·순수 측정1000회입니다. 독립 Native3회씩 ABBAAB 순서로 비교한 실행별 중앙값의 중앙값은1827.4µs→0.2µs,자식 측정2천만 회→0회이며 양쪽 구간 할당0B·GC0입니다. 개선 쪽은 타이머 해상도에 가까우며 전체 프레임·가시 버튼 부하·FPS·draw·Pi 처리량 결과가 아닙니다. Private/작업 집합 범위는 겹쳐 프로세스 메모리 개선을 확정하지 않습니다. 기존 실제 앱/2048텍스트 메모리 미해결 범위도 유지합니다. W:공간 검사로 실행 전에 빌드를 거부한 뒤 SDK의 명시적 `UseArtifactsOutput`/`ArtifactsPath`로 중간 파일까지 L:에 분리했으며 기존 소스 캐시를 삭제하거나 옮기지 않았습니다.

## Wrap과 균일 Grid

검증 결과:관리/고정RC3 Native 각각136검사,MSAA1/4·프레임 슬롯3에서93이미지가 정확히 일치합니다. Wrap1만+Grid1만 자식의 준비 후 순수 측정 할당은0B입니다. 새 Native 샘플의 첫 실제 표시와15초 정상 종료도 확인했습니다. Stack/Positioned/Wrap/Grid 묶음 단계를 완료하여 컨테이너는2/6이며 전체 컨테이너 목표·전체 프레임 성능 완료를 의미하지 않습니다.

`UiWrap`/`UiVerticalWrap`은 `UiWrapWidget<UiHorizontalAxis>`/`<UiVerticalAxis>`를 컴파일 시점에 선택합니다. `UiGrid`/`UiHorizontalGrid`는 `UiUniformGridWidget<UiVerticalAxis>`/`<UiHorizontalAxis>`를 선택하며 그리드 축은 행·열이 진행하는 방향입니다. `[UiWidgetStyle]`로 생성한 사용자 배치도 같은 구현을 사용합니다. 네 위젯은 기반 기능 특성으로 Fluent 시각 카탈로그에서 제외합니다. 기존 델리게이트 `Dux.Grid` 오버로드는 그대로이며 소유된 위젯 트리는 `Dux.UniformGrid`로 구성합니다.

```csharp
var chips = Dux.Wrap(new(Spacing: 12, RunSpacing: 8),
    Fluent.Button("One"), Fluent.Button("Two"));
var cells = Dux.UniformGrid(new(3, CrossAxisSpacing: 12,
    MainAxisSpacing: 12, MainAxisExtent: 40),
    Fluent.Button("A"), Fluent.Button("B"), Fluent.Button("C"));
```

Wrap은 주축을 제한하고 교차축은 제한하지 않은 상태로 자식을 측정합니다. 다음 자식과 간격이 들어가지 않으면 새 줄을 만들며 가장 긴 줄과 줄 전체 교차 크기에 맞춥니다. 부모 제약이 더 큰 크기를 강제하면 `Alignment`는 줄 안의 자식을,`RunAlignment`는 줄들을 분배합니다. 두 속성은 주축6가지 정렬을 사용하고 `CrossAlignment`는 Start/Center/End입니다. 주축이 제한 없으면 한 줄로 구성합니다. 옵션을 바꿔도 자식 인스턴스를 보존합니다.

Grid는 유한한 교차축 최대 크기와 양수 `CrossAxisCount`가 필요합니다. 교차축 간격을 뺀 공간을 같은 크기의 셀로 나눕니다. `ChildAspectRatio`는 교차 크기/주축 크기이고 `MainAxisExtent`를 지정하면 셀 크기에 그 값을 우선합니다. 주축 제한은 없어도 되며 마지막 행은 일부 셀만 있어도 됩니다. 모순되거나 유한하지 않은 옵션·제한 없는 교차축·과한 간격·좌표 범위 초과는 명확히 실패합니다. `new UiUniformGridOptions()` 또는 유효한 명시 옵션을 사용합니다. `default(UiUniformGridOptions)`는 개수0이므로 잘못된 값이며 임의 열 수로 대체하지 않습니다.

`UiArrangedChildrenWidget`이 재사용하는 자식 사각형·그리기/입력 클립·배타적 소유권을 공유합니다. 파생 배치는 유한한 영역을 반환하고 부모 제약을 만족해야 합니다. Wrap은 자식당 최대 한 개의 줄 기록을 보유하고 Grid는 별도 셀 배치 객체를 만들지 않습니다. 비캐시 측정은 자식을 순회하며 조건을 만족하는 변경 없는 Wrap/Grid는 위의 측정 유지 경로를 사용합니다. 전체 증분 배치·전체 프레임0B·앱 처리량 향상을 입증한 것은 아닙니다. 제한된 영역 밖은 잘리며 스크롤·가상화는 별도 작업입니다. RTL·역방향 흐름·픽셀별 입력 정책까지 Flutter와 같다는 주장은 하지 않습니다.

고정.NET11 도구 디렉터리에서 `samples/fba/layout_flow_containers_fba.cs`를 `run-fba.ps1 -NoCache -TargetFramework net11.0-windows7.0`으로 실행합니다. **Columns**는2/3열을 바꾸면서 여섯 셀의 카운터를 보존합니다. 넘침/스크롤 통합 전까지 최소480x560을 사용하며 샘플 제약을 엔진 최적화로 세지 않습니다. 정확한 검증 범위와 해시는 `artifacts/flow-containers-2026-11-06/verification.json`을 따릅니다.2026-11-06 확인한 설계 자료:[Wrap](https://api.flutter.dev/flutter/widgets/Wrap-class.html),[고정 개수 그리드](https://api.flutter.dev/flutter/rendering/SliverGridDelegateWithFixedCrossAxisCount-class.html). 이 배치 Grid가 Flutter GridView의 스크롤·지연 생성을 제공하는 것은 아닙니다.

## Stack과 Positioned

`UiStack`과 `UiPositioned`는 상속 가능한 생성 기반 위젯입니다. 생성기가 실제 구현 `UiStackWidget`·`UiPositionedWidget`을 선택합니다. Row/Column과 `UiMultiChildWidget` 소유권 및 공통 측정 보호 아래 구성 루트 조회를 공유합니다. 런타임 테마 등록부를 추가하지 않습니다.2026-11-06 확인한 공식 자료는 [Stack](https://api.flutter.dev/flutter/widgets/Stack-class.html)과 [Positioned](https://api.flutter.dev/flutter/widgets/Positioned-class.html)입니다.

```csharp
var overlay = Dux.Positioned(Fluent.Button("Move"),
    new(Right: 12, Top: 8, Width: 140, Height: 32));
var layers = Dux.Stack(new(Fit: UiStackFit.Expand),
    Fluent.Body1("Background"), overlay);
```

Loose는 일반 자식의 측정 크기로 Stack 크기를 정하고 Expand는 두 축이 제한되어야 하며 Passthrough는 부모 제약을 전달합니다. 위치를 지정한 자식은 이후 측정합니다. 양쪽 가장자리를 지정하면 해당 크기가 강제되고 Right/Bottom은 반대편 가장자리를 기준으로 배치합니다. 모든 위치 속성이 null이면 일반 자식으로 크기 결정에 참여합니다. 위치 지정 자식만 있으면 두 축이 제한되어야 합니다. 기본 정렬은 왼쪽 위입니다. 음수 위치는 허용하고 모순되거나 유한하지 않은 속성은 소유권 변경 전에 거부합니다. `Options` 변경은 자식을 보존하며 프레임을 요청합니다.

삽입 순서로 그리며 Stack 영역 밖의 그리기와 표준 컨트롤 포인터 입력을 자릅니다. 포인터가 겹친 경우 마지막 자식의 할당 사각형이 아래 자식의 호버 처리를 막아 누름 즉시 동작 버튼도 중복 실행하지 않습니다. 사각형 할당 기준이며 투명 픽셀별 판정·포인터 통과 정책·표준 입력 경로를 우회하는 사용자 컨트롤은 이 계약에 포함하지 않습니다. 키보드·접근성 경로는 별개입니다. 물리 배치 위젯은 부모 데이터 전달 경계이고 Build 구성 위젯은 데이터를 전달합니다. Row 아래 Positioned와 Stack 아래 Expanded는 명확히 실패합니다.

샘플의 **Move overlay** 버튼으로 위치를 바꿀 수 있습니다. 근거는 `artifacts/stack-containers-2026-11-06/verification.json`을 따릅니다. Wrap/Grid는 위의 최신 흐름 배치 근거로 검증되어 묶음 단계를 포함해 전체2/6입니다. 증분 배치·가상화 스크롤·가변 키 자식 목록·전체 Flutter 동등성은 남아 있습니다.

## 가중치 배치와 생성 위젯

`UiExpanded`는 남은 공간의 자기 몫을 채우고, `UiFlexible`은 그 몫보다 작아질 수 있습니다. `UiSpacer`는 비율에 따른 빈 공간입니다. Flutter의 [Flex](https://api.flutter.dev/flutter/widgets/Flex-class.html), [Expanded](https://api.flutter.dev/flutter/widgets/Expanded-class.html), [Flexible](https://api.flutter.dev/flutter/widgets/Flexible-class.html), [Spacer](https://api.flutter.dev/flutter/widgets/Spacer-class.html) 공식 문서를2026-11-06에 확인해 설계에 반영했습니다. Flutter 전체 동치를 주장하지 않습니다.

```csharp
var save = Dux.Expanded(Fluent.Button("Save"));
var row = Dux.Row(new(Spacing: 12),
    save, Dux.FlexSpacer(), Dux.Expanded(Fluent.Button("Cancel"), 2));
// After rendering, this requests a new frame and keeps the same child instance.
save.Flex = 3;
```

소스 생성기는 `UiFlexibleWidget<UiTightFlexFit>`·`UiFlexibleWidget<UiLooseFlexFit>`·`UiFlexSpacer`를 각각 직접 상속하는 생성 클래스와 생성자를 만듭니다. 맞춤 정책과 Row/Column의 축 구현은 컴파일 시점에 선택합니다. 가중치와 크기 제약은 실행 중 배치 데이터입니다. 런타임 테마 검색이나 구현 레지스트리를 추가하지 않습니다. 기본 위젯과 사용자가 특성으로 선언한 위젯이 같은 구현을 사용합니다. 이들은 `Infrastructure` 특성이 있는 배치 기반이며 Fluent 시각 컴포넌트 목록에는 들어가지 않습니다.

고정 크기 자식과 간격을 먼저 반영한 뒤 남은 주축 공간을 비율대로 나눕니다. 주축 최소 크기에서는 느슨한 자식이 실제 사용한 크기로 줄일 수 있습니다. 교차축 `Stretch`는 유한한 최대 크기가 필요합니다. 제한 없는 주축에서는 느슨한 Flexible과 `MainAxisSize.Min` 조합으로 자연 크기를 사용할 수 있으며, 채워야 할 유한한 공간이 없는 Expanded는 명확히 실패합니다. 가중치0은 자연 크기로 취급하고 Spacer는 양수만 허용합니다. `Flex` 변경은 자식을 재생성하지 않고 다음 배치를 요청합니다.

가중치 위젯은 Row/Column의 자식이거나, 그 사이에 배치 장벽이 없는 `UiWidget.Build` 구성만 있어야 합니다. `UiControlWidget` 기반의 물리적 컨테이너 안쪽에 Expanded를 숨겨 놓으면 부모 데이터가 전달되지 않습니다. 구성 트리의 메타데이터 확인도 공통 측정 경로를 사용하여 일반 위젯 렌더·입력 호출을 차단하고 실패 후 상태를 복원합니다.

생성된 버튼은 할당 크기를 실제 그리기·클릭·접근성 범위에 반영합니다. 사용자 말단 위젯은 측정에서 `context.LayoutConstraints`를 따르고 그릴 때 `context.ConstrainToLayout(desired)`를 사용할 수 있습니다. 사용자 컨트롤 내부의 즉시 모드 버튼에 명시한 크기와 기존 입력 슬롯의 크기 계약은 보존합니다. 기존 `Dux.Spacer(float height)`는 고정 간격 API입니다. 가중 빈 공간은 이름이 겹치지 않는 `Dux.FlexSpacer(int flex)` 또는 `new UiSpacer()`를 사용합니다.

관리/고정RC3 Native에서67검사씩 통과하고 GPU 이미지37개가 모두 일치합니다. MSAA1/4·프레임 슬롯3에서 가중치 변경 후 여러 획득 이미지도 독립 참조와 같습니다. 자식 상태·해제·입력·Build 전달·실패 복원과1만 가중 자식의 준비 후 순수 측정0B 할당을 확인했습니다. 이1만 자식은 작은 측정 위젯이며1만 버튼 렌더링·처리량·앱 Private/FPS 결과가 아닙니다. 처음 필요한 부모 데이터 배열은 자식 수에 맞춰 한 번 확보하고 해제 시 참조를 끊습니다. 증분 레이아웃은 아직 구현하지 않았습니다.

`layout_containers_fba.cs`는1:1:2 배치를 보여 줍니다. 고정 가중치 샘플의 첫 표시·15초 정상 종료는 가중치 setter 확장 직전의 바이너리로 검증했고, 이 샘플이 사용하지 않는 setter 확장은 최종 Native67검사로 확인했습니다. 호환되는 고정 배치 실행 근거를 재사용했으며 수동 승인·리사이즈 검증은 아닙니다. 정확한 소스·실행 파일 해시와 범위는 `artifacts/flex-containers-2026-11-06/verification.json`을 따릅니다. 가중 Row/Column 시점에는 여섯 단계 중1개가 완료됐습니다. 최신 흐름 배치 검증 후에는2개가 완료됐으며 전체 컨테이너 목표는 계속 진행합니다.

Flutter의 제약 기반 배치를 참고합니다. 부모가 제약을 전달하고 자식이 원하는 크기를 반환하면 부모가 위치를 정합니다.2026-11-06 확인한 공식 자료: [제약](https://docs.flutter.dev/ui/layout/constraints), [레이아웃 목록](https://docs.flutter.dev/ui/widgets/layout), [Container](https://api.flutter.dev/flutter/widgets/Container-class.html), [Row](https://api.flutter.dev/flutter/widgets/Row-class.html).

`UiContainer`·`UiRow`·`UiColumn`은 상속할 수 있는 생성 위젯입니다. 닫힌 기반 타입이 컨테이너 배치/표현 알고리즘 또는 배치 축을 컴파일 시점에 선택합니다. Fluent 기본 위젯과 사용자 위젯을 같은 `UiWidget` 자식으로 소유합니다. 이들은 공통 배치 기반이며 공식 Fluent 카탈로그에 임의의 컴포넌트를 추가하지 않습니다.

```csharp
protected override IUiView Build(UiBuildContext context) =>
    context.Child("root", () => new UiContainer(
        new UiRow([Fluent.Button("Save"), Fluent.Button("Cancel")],
            new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min)),
        new(Padding: new(24), Margin: new(12))));
```

컨테이너는 자식을 소유합니다. 다른 Build 컨텍스트나 컨테이너에 이미 소유된 자식은 다시 채택할 수 없습니다. 소유 루트를 `context.Child`로 등록하고 중첩 자식은 각 컨테이너가 소유하도록 구성합니다. 해제할 때 소유 트리도 해제합니다. 초기 컨테이너의 자식 목록 교체·순서 변경은 아직 구현하지 않았습니다.

`UiBoxConstraints`는 최소·최대 크기와 제한 없는 최대 크기를 표현합니다. `MeasureConstrained`를 기존 `Measure(available)`와 구분해 호출 해석과 기존 측정 동작을 보존합니다. Build만 구현한 사용자 위젯도 루트가 측정 가능한 `UiWidget`이면 키로 유지한 트리를 준비해 제약 기반 측정에 참여합니다. 측정은 렌더링·입력을 실행하지 않으며 실제 렌더링에서는 자식 입력과 그리기를 계속 실행합니다. 델리게이트 기반 기존 뷰는 명시적 측정을 제공해야 합니다. 제한 없는 축에서 유한한 크기를 결정하지 못하는 말단 위젯은 명확히 실패합니다.

현재 Container는 패딩·바깥 여백·정렬·요청 너비/높이·추가 제약을 제공합니다. 정렬을 지정하면 제한된 공간을 사용하고 제한 없는 축은 내용 크기에 맞춥니다. 정렬이 없으면 요청/부모 크기를 반영하면서 왼쪽 위에 자연 크기의 자식을 배치합니다. 자식 그리기는 할당 영역으로 잘립니다. Row/Column은 간격·주축 크기·주축6가지 분배·교차축 시작/중앙/끝/늘이기를 제공합니다. 공간 넘침은 명확히 알립니다. 모든 말단 위젯의 늘이기 대응, 가변 키 자식 목록, RTL·기준선 정렬, 스크롤/가상화, 전체 변경 범위별 유지 레이아웃은 남아 있습니다. 개별 단일 자식 래퍼는 아래에 구현·검증 내용을 기록했습니다.

사용자 배치는 `IUiContainerLayout`, 표면 그리기는 `IUiContainerPresentation`으로 분리합니다. `[UiWidgetStyle]`이 `UiContainerBox<TLayout, TPresentation>`의 타입 연결을 컴파일 시점에 확정합니다. 샘플에는 생성된 사용자 표면과 실제 Fluent 버튼 콜백이 있습니다.

고정.NET11 도구 디렉터리에서 절대 경로로 실행합니다.

```powershell
Set-Location -LiteralPath 'W:/MyWorks/Duxel/artifacts/optimization-evaluation-2026-11-04/toolchain'
$layoutRun = @{
    Path = 'W:/MyWorks/Duxel/samples/fba/layout_containers_fba.cs'
    NoCache = $true
    TargetFramework = 'net11.0-windows7.0'
    DotnetPath = 'C:/Program Files/dotnet/dotnet.exe'
}
& 'W:/MyWorks/Duxel/run-fba.ps1' @layoutRun
```

개발 검증은.NET11만 사용하고.NET9·10은 최종 완료 시 배포용 컴파일만 확인합니다. 근거와 남은 단계는 `artifacts/widget-containers-2026-11-06/plan.json`에 있습니다.1만 자식의 순수 측정 할당 계약과 컨테이너 처리량·전체 렌더 성능은 구분합니다. 후자는 아직 검증하지 않았습니다.

초기 샘플은 스크롤/넘침 배치가 미완성이므로 네이티브 창의 최소 크기를480x400으로 지정합니다. 샘플의 제약이며 엔진 성능 개선이나 스크롤 목표를 대체하는 결과가 아닙니다. 갤러리 검사는 실제 첫 프레임 표시와 렌더 예외 없음을 함께 확인하며 종료 코드0만으로 성공 처리하지 않습니다.

## 공통 위젯 Build 저장 비용 개선 — 2026-11-06

UiWidget의 소유권은 기존 키별 자식 저장소에서 검증하고, 부모 범위 키는 기존 정체성 저장 공간을 재사용한다. 의존 상태와 자식 정리에 쓰는 임시 목록도 기존 Dictionary 객체에 둔다. 위젯 필드8바이트와 의존 없는 Build 문맥24바이트를 줄이며 별도 래퍼 객체를 만들지 않는다. 기본·생성·사용자 파생 위젯에 같은 경로를 적용한다. 구독,실패한 Build 정리,정체성,입력,해제와 측정 무효화는 유지한다.

고정.NET11 RC3 NativeAOT에서 세 모드별 전후 독립3회씩 ABBAAB 순서로 총18개 프로세스를 실행했다. 생성 Container1만 개의 생성 할당6,080,000→5,760,000B(-5.26%),최초 Build880,000→640,000B(-27.27%)다. 서로 다른 상태를 관찰하는 루트1만 개의 최초 Build5,280,000→5,200,000B(-1.52%),키별 자식을 만드는 최초 Build4,800,000→4,560,000B(-5%)다. 관찰 상태의 변경 없는 Build·상태 변경 뒤 Build와 소유 위젯의 변경 없는 Build는0B를 유지한다. 의존 측정 구간에서 루트 객체·상태·배열 준비와 해제는 제외하되 자식 팩터리 생성은 포함한다. 생성 비용과 최초 배치 유지 결과 확보 비용은 별개다.

관리/Native 각각 트리·소유권·수명101개와 컨테이너175개 검사 통과,GPU121이미지 전부 일치. 기존 팩터리25% 감소 조건은 완화하지 않았고,Native 비소유 버튼1024개에서114,688→81,920B(-28.57%)로 통과했다. 유지된 Wrap/Grid 순수 측정 중앙값0.2→0.2µs,p950.3→0.3,p990.3→0.3이며 실행별 범위가 겹쳐 속도 향상은 확정하지 않는다. 작은 진단의 Private·작업 집합 감소는 실제 앱·장기 메모리 개선의 근거가 아니다. 강제GC·부하·품질 저하는 없다. 이전 Container 생성 추가 비용80,000B보다 크게 줄었으며 과거 기록은 보존한다.

근거:[검증](../artifacts/build-store-2026-11-06/verification.json),[전후 비교](../artifacts/build-store-2026-11-06/comparison.json). 원본과 초기 실패 진단은 L:/Cache/Duxel/build-store-2026-11-06/에 보존한다. Fluent2 전체·나머지 컨테이너 계약·고부하 메모리·이식성은 미완료이며 리사이즈는 별도 보류다. 개발 검증은.NET11만,NET9·10 배포 컴파일은 전체 목표 최종 완료 시 수행한다.

## 생성된 단일 자식 배치 위젯 — 2026-11-06

`UiPadding`, `UiAlign`, `UiCenter`, `UiSizedBox`, `UiConstrainedBox`를 공통 `UiSingleChildBox<TLayout,TOptions,TPresentation>`의 소유권·유지 경로에 연결했다. 기존 `UiContainerBox`도 컴파일 시점 어댑터로 같은 기반을 사용하며 옵션·필수 자식·사용자 배치/표면 확장을 유지한다. `[UiWidgetStyle]`은 기본·사용자 위젯의 실제 닫힌 기반 타입과 생성자를 만든다. 다섯 위젯은 Infrastructure이며 Fluent 시각 컴포넌트 수에 더하지 않는다. 런타임 테마 탐색이나 빈 자리를 위한 가짜 자식은 없다.

```csharp
var counter = Fluent.Button("Count", OnCount);
var body = Dux.SizedBox(
    Dux.Padding(Dux.Align(counter, UiAlignment.BottomRight), new(12)),
    width: 240, height: 80);
var centered = Dux.Center(Fluent.Button("Reset", OnReset), widthFactor: 2);
var bounded = Dux.ConstrainedBox(centered, new(100, 300, 40, 100));
```

Padding은 자식의 최소·최대 제약을 여백만큼 줄인다. Align/Center는 자식 최소 제약을 풀고,배수가 없으면 유한 축을 채우고 무한 축은 자식 크기를 따른다.0이상의 배수는 축별로 적용한다. SizedBox는 부모 제약 안에서 지정한 축을 강제하고 나머지는 자식 크기,자식이 없으면0을 따른다. `Dux.Expand()`는 유한 축이 필요하다. ConstrainedBox는 부모 범위 안에서 추가 제약을 적용한다. 자식이 없어도 대체 위젯을 만들지 않고,넘치는 그리기는 표면에서 자른다. 옵션은 채택·변경 전에 검증하고 변경 시 자식 정체성을 보존하며 부모 측정을 무효화한다. 기존 Container의 자연 크기 자식 배치는 유지하며,자식 크기를 강제할 때 SizedBox를 사용한다.

고정RC3 관리/Native 각각 컨테이너·GPU229검사(이전175+54),MSAA1/4·프레임 슬롯3에서GPU159이미지가 모두 일치했다. 강제/느슨한/무한 제약,배수,빈 공간,중첩,실제 Fluent 클릭,실시간 옵션 변경,해제,사용자 생성 위젯,사용자 배치의 보수적 캐시 참여,획득 이미지 반복을 확인했다. 변경 없는 기존 수명101검사는 소스·실행 경로 호환성을 확인해 재사용했다. 전체 검사·고부하 앱을 다시 실행하거나 새 속도 향상을 주장하지 않는다.

별도 Native1만 Padding 저장 진단:생성5,280,000B,최초 측정 확보2,320,000B,변경 없는 측정과 무효화 후 재측정0B,자식1만 개 해제. 최초 측정 비용은 정체성 준비와 유지 결과를 포함하며 캐시만의 크기나 프로세스 Private가 아니다. 배열·문맥·프레임·해제는 할당 측정 구간 밖이며 강제GC는 없다. 관리 생성은5,280,384B로 관측했고 최초 측정·이후 값은 같다. 런타임별1회 저장 진단이며 전후 성능·메모리 향상 비교가 아니다. 기존 Container 생성5,760,000B/첫Build640,000B/변경 없는Build0B도 Native 집중 회귀 진단에서 유지됐다.

[검증 기록](../artifacts/single-child-2026-11-06/verification.json). 컨테이너3/6단계를 완료했고,말단 확장·스크롤/가상화·전체 통합/성능은 남아 있다. 리사이즈는 별도 보류,개발 검증은.NET11만 수행한다.2026-11-06 조회한 공식 참고:[Padding](https://api.flutter.dev/flutter/widgets/Padding-class.html),[Align](https://api.flutter.dev/flutter/widgets/Align-class.html),[SizedBox](https://api.flutter.dev/flutter/widgets/SizedBox-class.html),[ConstrainedBox](https://api.flutter.dev/flutter/widgets/ConstrainedBox-class.html). 갱신되는 API 문서이며 Flutter 소스 커밋을 고정하지 않았고 Flutter 전체 API 동등성을 주장하지 않는다.

## Slider·Toggle의 부모 할당 반영 — 2026-11-06

SizedBox 측정은80x64인데 생성 Slider의 실제 그리기·입력은120x32인 불일치를 재현했다. 짧은 레일의 끝이 할당된 클립 밖에 있어 의도한 끝값에 도달하지 못했다. 생성 Slider·Toggle이 기존 공통 렌더에 명시적으로 배치 참여를 전달하도록 수정했다. Slider/Checkbox/Switch/ToggleButton의 동작과 시각 부품 크기를 유지하면서 부모 크기에 루트·레일·표시기/레이블 구성을 배치한다. 짧은 레일도 원래 썸 크기를 유지하고 넘침을 자르며 잘못된 clamp 범위·0나눗셈을 막는다. 포인터 이동 거리가0이어도 키보드로 조작한다.

`IUiSliderPresentation.Arrange`·`IUiTogglePresentation.Arrange`는 기존 닫힌 제네릭 표현에서 선택하는 정적 가상 계약이다. 부모가 다른 크기를 주면 표현이 Arrange를 구현해야 하며,미지원 사용자 표현은 다른 영역을 몰래 그리지 않고 측정 중 명확히 실패한다. 할당된 크기를 무시한 결과도 거부한다. 기본 표현은 계약을 구현한다. 생성 사용자 다이얼은 같은 Slider 동작에 다른 실제 배치/그리기를 연결해 검증했다. 런타임 표현 탐색이나 위젯별 추가 필드는 없다.

기존 즉시 API는 자연 크기 의미를 보존한다. 사용자 컨트롤은 새 `Button(...,size,constrainLayout:true)`, `Slider(...,options,size,constrainLayout:true)`, `Toggle(...,ref value,constrainLayout:true)` 오버로드로 같은 배치 경로에 참여한다. 공개 `UiControlContext.MeasureSlider<TPresentation>`·`MeasureToggle<TPresentation>`도 같은 제약 측정을 사용한다. 고정 입력 자식 슬롯은 기존의 명시적인 배치 참여 구분을 유지한다.

고정RC3 관리/Native 각각172개 집중 검사에 기존 Slider/Checkbox/Switch 계약,할당 입력 영역·끝값·세로/0길이 레일·키보드·사용자 참여/실패·실제 생성 Arrange 선택을 포함했다. MSAA1/4·프레임 슬롯3에서GPU43이미지 전부 일치했다. 독립적인 할당 기하로 썸/표시기 크기와 부모 변경을 확인했다. 원본 엔진의 재현 실패,새 검사의 DirectText 준비 누락,중립 기대 화면과 실제 hover 입력이 달랐던 이미지는 로그에 보존했다. 마지막 검사는 실제·기대 화면 모두 포인터를 화면 밖에 두어 같은 rest 상태로 비교하며 픽셀 기준을 완화하지 않았다.

[검증 기록](../artifacts/leaf-constraints-2026-11-06/verification.json). 기능·배치·입력 수정이며 속도 향상 주장은 아니다. 컨테이너3/6을 유지하고 Input·RadioGroup·텍스트·진행/스피너 등 나머지 말단 계약은 전체 점검이 필요하다. Fluent2 전체·장기 메모리·이식성은 미완료이며 리사이즈는 별도 보류한다.

## 편집 입력창의 부모 할당 반영 — 2026-11-06

기존 엔진은 부모80x64로 측정해도 Input 필드를80x32로 그렸다. 별도 원본 소스 빌드로 불일치를 재현했고,개선본은 기존 형식화 입력 엔진에서 명시적으로 배치에 참여한다. `IUiInputPresentation.Arrange`가 배치·편집 전에 부모 크기에 맞는 표현을 정적으로 선택한다. 미지원 사용자 표현은 측정 중 실패하며 Place가 할당된 루트 크기를 바꾸는 것도 거부한다. 기본 Fluent 외곽선/밑줄/채움·크기·장식 구현은 같은 기하 경로를 쓰고,호환 문맥 표현도 Arrange를 구현한다.

필드·편집 영역·텍스트 줄 위치·앞뒤 슬롯이 실제 할당 크기를 따른다. 폰트/줄 지표와 명시한 자식 슬롯 크기는 유지하고 좁거나0인 영역에서는 내용을 자른다. Arrange는 장식을 다시 측정하지 않고 이미 측정된 크기를 쓴다. 부모 크기를 바꿔도 입력 소유자·활성 편집·undo 이력·실제 자식 콜백을 보존한다. 새 공개 형식화 InputText 오버로드는 constrainLayout 참여를 명시하고,기존 즉시 오버로드는 자연 크기를 유지한다. 공개 UiControlContext.MeasureInput도 같은 제약 측정 경로를 사용한다.

고정RC3 관리/Native 각각212개 검사에 기존 InputKernel·Fluent Input·장식·소유 자식·Field 검사를 포함했다. MSAA1/4·프레임 슬롯3에서GPU25이미지 전부 일치했다. 독립적인80x64/160x48 기하·타이포그래피·밑줄 기준선·24px 소유 자식·획득 이미지 반복을 확인했다. 원본 빌드는 실제 선택한5개 파일과 이전 검증본에 일치하는 해시를 기록했다. 초기에 겹친 빌드는 중단·무효 처리했고,기준 빌드는 별도 중간 경로를 사용한 뒤 개선본을 직렬 전체 재빌드했다. 최초 수명 검사는 다른 살아 있는 입력 소유자를 빠뜨렸으며,수정한 검사는 각 소유자 상태의 유지/해제와 모두 해제한 뒤 저장소0개를 확인한다. GPU 기준 입력의 빈 키도 유효한 숨김ID로 고쳤다. 초기 기록은 보존한다.

[검증 기록](../artifacts/input-allocation-2026-11-06/verification.json). 기능·편집·배치 수정이며 처리량/FPS/Private 향상 주장은 아니다. 컨테이너3/6을 유지하고 RadioGroup·텍스트·진행/스피너·스크롤/가상화·전체 말단/통합 계약은 남아 있다. 리사이즈는 별도 보류,개발 검증은.NET11만 수행했다.

## 할당 자식 좌표 — 2026-11-10

공통 RenderAllocatedChild는 기존 표·열 좌표 적용을 격리하기 전에 호출자의 클립을 확보합니다. 할당 경계에 이미 부모 위치가 반영되어 있으므로 중첩 Padding과 말단이 기존 위치를 다시 적용하면 안 됩니다. 성공/예외 뒤 좌표 적용 플래그·빌더·배치·항목·다음 항목 상태를 복원합니다. 배치 자식과 같은 경로를 쓰는 Field/입력 소유 자식에 적용하며 필드·새 캐시는 추가하지 않습니다.

원본은 표·열과 말단 14변형의 28조건 모두 실패했습니다. 표 자식은 기대 (10, 10) 대신 (0, 0), 열 자식은 기대 (167, 10) 대신 (157, 208)이었습니다. 수정 후 두 Padding 오프셋을 반영하고 예외 뒤 호출자의 표 위치도 복원합니다. 관리 1529검사 통과: CPU/소스/입력/수명/컨테이너 1113, 정확 GPU 비교 416. GPU는 안정된 13변형·표/열·보통/좁은 상위 클립·래스터/벡터·MSAA 1/4·Light/Dark·슬롯 3을 포함하며 준비 포함 readback 833개입니다. Spinner는 CPU 좌표 검사에 포함하고 시간에 따라 달라지는 그림은 이번 새 정확 비교에 포함하지 않았습니다.

첫 GPU 기준은 행 크기 증가 전에 확보된 실제 상위 표 클립을 빠뜨렸습니다. 기준을 수정하여 실제 호출자 클립을 독립적인 화면 좌표 기준에 전달했습니다. 차이를 없애려고 제품 렌더 조건을 바꾸지 않았습니다. 실패 당시 이미지/로그는 fixture-initial에 보존하며 최종 로그·결과·native-toolchain-audit.json·소스 해시는 artifacts/allocation-routing-2026-11-10/verification.json 및 인접 파일에 있습니다.

말단 전체 게이트 완료는 아닙니다. 별도 현재 진단에서 Field는 부모 측정 80 x 64 대비 실제 루트 80 x 32임을 확인했습니다. 이 Field 결함은 후속 Field 할당 체크포인트에서 수정했으며 기존 좌표 근거는 당시 리비전에만 적용합니다. Panel은 임의 IUiView를 감싼 독립 내부 패널 루트로 일반 제약 말단 측정을 거부하며 이 경계의 명시적 검증/문서화도 남아 있습니다. 컨테이너 단계 3/6 유지.

Native는 PATH·표준/선별 대체 폴더·환경·레지스트리를 재탐색했으나 도구 설치를 확인하지 못했습니다. L:/Runtimes·L:/Cache·L:/Containers 열거는 접근 거부되었으며 비어 있다고 판정하지 않습니다. 이전 게시 로그 2개는 0바이트이고 과거 갤러리 로그의 네이티브 생성 기록은 현재 설치 상태의 증거가 아닙니다. 탐색 우회·설치·반복 게시 없이 현재 Native/런타임 비교 미검증을 유지합니다. 현재 관리 재빌드/실행은 Badge 샘플 본문을 유지한 로컬 프로젝트 지시문으로 확인하며 수동 입력·공식 외형과 구분합니다. FPS/Private/할당/Native 코드 크기 개선은 주장하지 않습니다. 리사이즈 사용자 보류와 .NET9/10 최종 배포 컴파일 보류 유지.

# Fluent 2 Badge·CounterBadge

Last synced: 2026-11-10

공통 상속 위젯과 소스 생성기를 사용하는 Badge·CounterBadge 구현이다. 공식 화면 전체 동등성·Windows UIA·RTL/강제 색상은 아직 검증·구현이 남아 있으며 두 컴포넌트 전체 완료나 Fluent 전체 완료로 보고하지 않는다.

## 공식 기준

Microsoft Fluent UI의 현재 master를 조회해 기존 고정 커밋 `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`와 동일함을 확인했다. [공식 Badge 사용 지침](https://fluent2.microsoft.design/components/web/react/core/badge/usage/)과 해당 커밋의 Badge/CounterBadge 타입·동작·스타일, 실제 brandWeb 및 light/dark 색 토큰을 비교했다. 원문 URL·SHA256은 `artifacts/fluent-badge-2026-11-05/source-manifest.json`에 있다. 사용 지침은 Preview로 표시되고 React 주 진입점은 Badge를 내보낸다. Windows `InfoBadge`는 별도 요구 항목으로 유지하며 이것을 구현한 것으로 대신하지 않는다. 색상 별칭 파일의 오래된 주석값 대신 실제 brandWeb 값을 사용한다.

## 사용과 컴파일 연결

```csharp
using var ready = Fluent.Badge("Ready", new(Shape: UiBadgeShape.Rounded));
using var count = Fluent.CounterBadge(108); //99+
using var live = Fluent.CounterBadge(() => new UiCounterBadgeValue(unread));
using var dot = Fluent.CounterBadge(0, dot: true);

ui.Badge("Ready");
// Immediate callers own the formatting memo; retained widgets own it themselves.
ui.CounterBadge(new UiCounterBadgeValue(unread), ref counterTextCache);
```

`UiBadge<TSource,TBehavior,TPresentation>`는 `UiControlWidget`의 실제 구현이다. `IUiBadgeBehavior<TSource,TSelf>`가 데이터 읽기·카운트·숨김·허용 변형을 담당하고 `IUiBadgePresentation`이 구성·배치·그리기를 담당한다. 기본 위젯과 사용자 상속 위젯은 같은 경로를 쓴다. 사용자 표현은 전혀 다른 구성과 그리기 알고리즘을 선택할 수 있다. `IUiBadgeIcon`도 닫힌 제네릭 표현에 정적으로 연결하므로 아이콘을 위한 런타임 구현 레지스트리나 프레임별 델리게이트를 만들지 않는다.

```csharp
[UiWidgetStyle(typeof(UiBadge<,,>), typeof(string), typeof(UiTextBadgeBehavior),
    typeof(UiFluentBadgePresentation<MyVectorIcon>))]
public partial class MyIconBadge;
```

생성된 `UiFluentBadge`는 닫힌 `UiBadge<string,UiTextBadgeBehavior,UiFluentBadgePresentation>`를 직접 상속하며 생성된 `Fluent.Badge` 팩터리는 이를 직접 생성한다. `generated` 폴더에 실제 생성 코드를 보존했다. 기존 `Dux.Badge(text)`와 `DuxelView.Display.Badge(text)`도 이 경로를 사용한다. 기존 `UiBadgeOptions`를 받는 두 인수 경로는 원본 구현과 시그니처를 보존하고 Legacy 특성·EditorBrowsable(Never)로 표시했다. 명시적 `default`/기존 옵션 인수는 이전 경로를 유지한다. 새 표준 변형은 `Fluent.Badge(text,new UiFluentBadgeOptions(...))`로 지정한다. 전체 비표준 비노출 작업은 별도로 남아 있다.

## 표현·동작·수명

Badge는6크기·3모양·4표현·8색을 지원한다. 높이는 tiny6,extra-small10,small16,medium20,large24,extra-large32이다. Segoe UI Semibold의 실제4/6/10/12px 글자 크기와 공통 DirectText 지표·벡터 경로를 사용한다. 아이콘은 앞/뒤/단독으로 배치할 수 있으며 단독 아이콘에는 AccessibleLabel이 필요하다. 기본 배지는 수동 표시이며 키보드 탭 순서에 추가되지 않는다. 라벨은 자동 최대 폭을 강제하지 않는다.

CounterBadge는 공식 계약에 따라 circular/rounded,filled/ghost,brand/danger/important/informative만 허용한다. 기본0은 숨기고 showZero는0을 표시하며 기본 overflow99보다 크면99+를 표시한다. dot은0이어도6px로 표시한다. 명시적 텍스트가 있으면 우선 사용한다. C# 카운트는 int이며 임의 fractional JavaScript number의 완전한 대응을 주장하지 않는다. 음수와 명시적 overflow0도 보존한다. 한정된 숫자 문자열 하나만 보관하고 변경 없는 카운트를 매 프레임 재포맷하지 않는다. Dispose는 숫자 메모·소스 델리게이트·접근성 라벨을 해제한다.

`UiTheme.BadgeColorScheme`은 Light/Dark 색 데이터만 선택하며 위젯 구현이나 테마 타입을 런타임에 교체하지 않는다. 기본 Light·Dark 디자인이 값을 명시한다. 메타데이터 변경은 다른 기존 색 필드가 같아도 정적 레이어를 무효화한다. 관련 색64조합은 `expected-colors.json`에서 확인할 수 있다.

## 검증과 남은 범위

.NET10 집중49개와 최종 전체1095개 통과. GPU 검사는 Light/Dark·MSAA1/4에서32개 색/표현 조합의 실제 내부 픽셀과 투명도를 검사하고, 즉시 API·생성 위젯·상속 위젯·유지된 위젯의 픽셀 동치를 비교한다. 카운트 변경·overflow·dot·숨김 복귀도 실제 GPU로 검사한다. 고정 .NET11 RC3 NativeAOT 결과와 이미지 비교는 `verification.json`을 따른다. 구성·API 검증과 실제 공식 브라우저/WinUI 화면의 동등성은 구분한다.

AccessibleLabel/숫자 설명은 현재 데이터 계약이며 Windows UIA 연결 완료가 아니다. 공식 화면·아이콘 비교,RTL·강제 색상,전체 플랫폼별 차이는 미완료다. 새 Badge를 추가한 이후 이전 DirectText 벤치 수치를 현재 전체 앱 성능으로 재해석하지 않는다. 기존 크기 변경 사용자 승인과 미확정 Native 입력 시간 초과도 보존한다.

갤러리는 `samples/fba/widget_tree_theme_fba.cs --badge`다. [현재 소스로 실행하는 방법](widget-gallery-run.ko.md)을 참고한다.


동작별 상태 저장소를 닫힌 제네릭에 포함한다. 정적 텍스트 동작은 비어 있는 값 형식이며 숫자 메모를 갖지 않는다. CounterBadge 동작만 숫자 문자열을 보관하고, 실제 카운트가 변해도 표시가99+로 같으면 같은 문자열을 재사용한다. 글자가 없는 dot/벡터 아이콘은 글꼴 측정이나 텍스트 백엔드를 요구하지 않는다. 객체 크기·전체 앱 메모리 개선율을 새로 측정한 결과는 아니다.


최종 고정 RC3 NativeAOT는 EXT/core 표시 수명에서 각각243개 통과했다. 상태 저장소 변경 전 관리179이미지가 두 Native 구성과 같고, 현재 관리 실행의 Badge22이미지도 양쪽 Native와 같다. 이전 공통 GPU157이미지도 유지된다. 모두320×240 전체 PPM 바이트 비교다. 최신 --badge Native 실행은 첫 프레임 제출과3초 후 정상 종료를 확인했으며 수동 화면·입력 검증은 아니다. 소스·실행 파일 해시는 verification.json에 보존했다. Source/Options/AccessibleLabel 변경은 값을 게시한 뒤 프레임을 요청하고 같은 값의 재할당은 유휴 상태를 유지한다.

Native GPU에서 얻은 Light/Dark 매트릭스 PNG도 직접 확인했다. Subtle의 ghost/outline은 흰색 전경 토큰이므로 배경과의 조합을 고려해야 한다. 이 이미지 확인은 공식 참조 화면 비교를 대신하지 않는다. 생성 코드 감사 빌드는 증분 생략을 확인한 뒤, 제품 코드에서 사용하지 않는 감사용 define으로 실제4개 어댑터와 카탈로그 출력을 확인했다. 일반 빌드 설정으로 복원한 솔루션도 경고0/오류0이다.

## 할당 검사 경계와 대조 검사 (2026-11-06)

Input 후속의 관리 GPU 전체 실행에서 기존 Badge 반복 읽기에12336B가 관측됐다. 같은 원문의 집중49개·JIT 진단49개·추적을 켠 전체GPU2427개는 통과했으므로 정확한 발생 원인은 아직 미확정이다. 재현되지 않았다는 이유로 결함 해결을 선언하지 않는다. JIT 원문에는 Tier0/OSR 두 버전이 있지만 이것만으로 원인을 확정하지 않는다.

제품 캐시를 바꾸지 않고 측정 루프를 별도 NoInlining 메서드로 분리했다. 짧은 별도 루프 대신 실제 측정 메서드를 워밍업하며 첫 호출 수치도 기록한다. 정상 기대값0B를 유지하고 같은 루프에서 캐시를 매번 무효화하는 대조 검사로 실제 포맷 할당을 검출한다. 임계값 완화·성공할 때까지 재시도·검사 통과를 위한 런타임 스위치·측정 중 강제 GC는 사용하지 않는다.

.NET10 독립3회에서 각50개 통과, 첫 호출/정상 반복0B·무효화 대조320000B다. 수정 후 관리GPU전체2428개와 고정RC3 NativeAOT 집중50개가 통과했다. Native에서도 같은0B/320000B를 관측했다. 현재 관리 전체2097이미지가 이전 Native EXT/core 양쪽과 정확히 같다. 제품 소스9개의 해시가 같으므로 관련 없는 전체관리2913·Native각2427·생성기115는 이전 기록을 범위대로 재사용한다. 이는 제품의 할당·FPS·Private 개선 주장이 아니다.

근거는 `artifacts/badge-allocation-2026-11-06/verification.json`·`plan.json`, 원본 실패/수정 전 소스·JIT·추적·실행 로그다. .NET10 집중 재현은 `dotnet run --project tests/Duxel.Rendering.Tests -c Release -- artifacts/badge-check --badge-only`, 고정RC3 게시 실행은 해당 폴더의 Native 실행 파일에 `artifacts/badge-native badge-only`를 전달한다. 실행 SDK는 기존 전용 경로로 고정한다. 같은 실패의 전체 진단을 새 근거 없이 반복하지 않는다. 원래12336B 원인은 미해결로 유지하며 Input 슬롯/Field/UIA/모션과 나머지 전체 목표를 이어간다.

## 측정과 부모 할당 — 2026-11-10

UiBadge·CounterBadge는 UiControlContext.MeasureBadge<TPresentation>을 통해 순수 MeasureContent를 구현합니다. IUiBadgePresentation.Measure 기본 구현은 미지원 예외를 내고 CanArrange는 공통 PlaceText 루트 하나와 해당 루트 클립에 참여합니다. 기본 MeasureFluentBadge<TIcon>는 배치·그리기 없이 렌더와 자연 기하를 공유합니다. 잘못된 크기·누락된 측정·여러 할당 루트는 명시적으로 거부하며 공통 경로가 표·열 상태를 복원합니다.

```csharp
using var badge = Fluent.Badge("Status");
using var box = Dux.SizedBox(badge, 80, 64);
// Actual frame/root is 80 x 64; glyph and icon metrics remain unchanged.
```

명시적 부모는 프레임/루트 크기를 바꾸고 자연 글자·아이콘을 그 안에 중앙 정렬하며 모든 프레임·글자·아이콘 그리기를 자릅니다. 폰트·아이콘 크기는 기존 Badge 토큰을 유지합니다. 명시적으로 할당된 dot도 이 규칙을 따르지만 독립 자연 크기는 여전히 6 x 6입니다. 기존 즉시 오버로드와 독립 위젯은 자연 기하를 유지합니다. 사용자 즉시 호출은 content, options, constrainLayout : true로 참여합니다.

숨겨진 0 CounterBadge의 자연 크기는 0이고 강제 부모는 표현 호출·그리기 없이 지정 루트를 예약합니다. 기존 독립 숨김은 공간을 소비하지 않습니다. 읽기 전용 콘텐츠 제공자와 ReadContent는 명시적 측정/렌더 단계마다 한 번 읽으며 부모가 별도 측정 단계를 요청할 수 있습니다. 기존 소스 동작의 제한된 숫자 문자열 메모를 이 단계에서 준비·재사용할 수 있습니다. 측정은 그리기·입력·이벤트 콜백을 호출하지 않습니다. 위젯/컨텍스트 필드·새 캐시·런타임 등록부·소스 스냅샷 추가 없음.

고정 .NET11 RC3 관리 374검사 통과: CPU/소스/수명 73, 기존 GPU 11, 할당 픽셀 248, 서로 다른 유지주기 픽셀 40, 재사용 횟수 2. 래스터/벡터·Light/Dark·MSAA 1/4·슬롯 3·준비 포함 readback 599개입니다. 기존 숫자 메모의 불변/오버플로 10000회 steady 읽기 0B와 메모 무효화 대조군 320000B가 유지됩니다. 이는 제한된 캐시 불변식이며 앱 전체 Before/After 개선이 아닙니다. 기존 소스 해제·아이콘 라벨 검증·팔레트 무효화 검사도 포함합니다.

새 로컬 layout_badge_allocation_fba.cs는 Width : 100, Height : 64, Increment/Zero/Overflow, Dot을 제공합니다. 지시문 외 동일본문 로컬 참조 빌드·첫 표시·15초 종료를 수동 조작과 구분해 기록했습니다. 근거: artifacts/badge-allocation-2026-11-10/verification.json 및 인접 소스 해시·로그. 표준 VS C++ 탐색 부재로 현재 Native·런타임 이미지 검증은 미검증이며 이전 Native 근거는 당시 리비전에만 적용됩니다. 전체 수치·리사이즈 보류 유지. 새 할당/FPS/Private/Native 코드 크기 개선은 미측정입니다.

2026-11-12 후속 — [콘텐츠 측정 계약](widget-cpu-retention.ko.md): 결정론적 소스·표현은 부모 재사용에 참여하고 동적 문자열/배지 getter와 사용자 특성은 기본 재측정합니다. getter Progress의 선언 기하와 Spinner의 안정된 표시는 별도 약속이며 그리기·getter·애니메이션·이동 처리는 계속 실행합니다. 집중26개와 실제 GPU 비교는 이 범위의 근거이며 전체 NativeAOT·성능 완료가 아닙니다. 근거: artifacts/content-layout-retention-2026-11-12/verification.json.

UiControlContext.PaintFluentBadge(bounds, content, options)는 별도의 배치 항목을 만들거나 커서를 이동하지 않고 지정한 경계 안에 장식 배지를 그립니다. 독립 Badge와 타이포그래피·팔레트·클리핑·강제 색상 경로를 공유합니다. 접근성 의미 정보는 포함하는 컨트롤이 제공하며 TabList는 배지 문구를 항목의 도움말로 전달합니다.

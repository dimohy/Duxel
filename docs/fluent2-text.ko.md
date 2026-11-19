# 생성된 텍스트 위젯과 공통 표현

## 일반 텍스트의 네이티브 접근성 — 2026-11-15

`Fluent.Body1`, `Fluent.Title2`, `Fluent.Text` 등 `UiText` 계열은 표시한 문자열을 정적 Text/Label 노드로 게시합니다. 문구가 같은 두 위젯도 서로 구분하며, 같은 인스턴스의 `Source`를 바꾸면 정체성은 유지하고 이름을 갱신합니다. `AccessibleName`으로 이름을 지정할 수 있습니다. 공개 `UiWidgetIdentity.Parent` 기본값과 입력 정체성은 바꾸지 않습니다. 수동 즉시 모드의 `ui.Text` 호출은 이 위젯 게시 경로와 별개입니다.

텍스트 소스와 표현은 한 번만 실행하고, 기존 공통 텍스트 배치에서 실제 루트 영역을 수집합니다. 부모 할당·줄바꿈·클립·여백·회전에 같은 영역을 사용합니다. 비활성·화면 밖 상태와 접근성 부모를 유지하고 제거한 위젯은 트리에서 퇴장합니다. 정적 텍스트는 키보드 포커스·편집 Value·숫자 Range를 제공하지 않습니다. 접근성 트리가 없으면 의미 정보 게시와 정체성 문자열 생성을 수행하지 않습니다. 트리를 사용하는 텍스트 인스턴스는 지연 생성한 정체성을 보관하며 공통 유한 식별자 캐시를 재사용합니다.

생성된 정적 Fluent 텍스트의 내용은 `widget.Source = "바뀐 내용"`으로 바꿉니다. `UiText<TPresentation>` 호환 클래스의 `Text` 프로퍼티와 구분하세요. 사용자 표현은 `UiControlContext.LayoutText`/`PlaceText` 또는 공통 타이포그래피 경로로 실제 텍스트 루트를 배치해야 합니다. 접근성을 켠 상태에서 루트를 배치하지 않는 표현은 추측한 경계를 게시하지 않고 명시적으로 실패합니다.

[Microsoft Text 제어 계약](https://learn.microsoft.com/windows/win32/winauto/uiauto-supporttextcontroltype)의 정적 텍스트 이름·경계·비편집 패턴을 따릅니다. 선택 가능한 풍부한 Text 패턴·문자별 영역·스크린리더 전체 적합성은 이 수정의 완료 범위가 아닙니다.

집중 검증: CPU64개(기존 배치·소스 수명 검사 포함), Windows 원시 GPU24쌍, Windows UIA10개 중 텍스트4개/기존 Skeleton6개, Linux 고정 .NET11 RC3 NativeAOT AT-SPI12개 중 텍스트4개/기존 Skeleton8개를 통과했습니다. Windows는 관리형 .NET11이며 Windows RC3 NativeAOT와 별개입니다. 실제 옵저버34개 표시 단계·유휴·복원과 실행기 경유4개 작업 스레드를 확인해 현재 배포에 반영했습니다. 성능3쌍의 CPU·입력 범위는 겹쳤고 일부 중앙값은 악화했으므로 새 속도 향상이나 무퇴행을 확정하지 않습니다. 이전 실행물은 보존합니다. 최신 상태는 `artifacts/text-accessibility-2026-11-15/plan.json`과 `artifacts/linux-observer-delivery.json`을 확인하세요.


## 부모 할당과 콘텐츠 측정 — 2026-11-10

텍스트 위젯이 짧은 문자열·빈 문자열·0크기에서도 실제 부모 루트를 예약합니다. 기본 Fluent 텍스트·타이포그래피 프리셋·문맥 폰트 표현이 정적 `IUiTextPresentation.CanArrange`에 참여합니다. 참여한 표현은 공통 PlaceText/LayoutText/타이포그래피 함수로 루트 하나를 배치해야 하며 렌더 후 실제 경계를 검사합니다. 미참여 사용자 표현은 측정 중 변경된 할당을 거부합니다. 기존 단독 렌더와 타입 지정 즉시 API는 자연 배치를 유지하며 마지막 `constrainLayout` 인수로 사용자 컨트롤이 명시적으로 참여합니다.

공통 배치 지점이 할당 크기를 예약하되 글리프 크기·기준선·행 높이는 유지합니다. 부모/측정 너비가 줄바꿈을 결정하고 해당 배치 안에서는 기존의 절대 PushTextWrapPos보다 우선합니다. 더 작은 콘텐츠 너비는 좁은 자식 박스나 명시적 WrapText(text, width)로 선언합니다. 할당 배치 밖의 기존 줄바꿈 위치는 유지합니다. 할당 범위에서 표·열의 배치 상태를 분리·복원하여 정렬을 두 번 적용하지 않습니다. 기본 표현의 렌더에 추가 표현 측정을 넣지 않았습니다. UiImmediateContext마다 정수 범위 카운터 하나가 추가되며 위젯 필드·새 텍스트 캐시는 추가하지 않습니다.

텍스트는 콘텐츠가 고유 크기를 결정하므로 기존 소스 의미를 유지합니다. 측정은 콘텐츠 제공자를 한 번, 위젯 렌더는 한 번 읽습니다. 제공자는 읽기 전용이어야 하며 측정에서 이벤트·입력·렌더를 실행하지 않습니다. Progress의 소스 독립 기하 측정과 다르므로 텍스트 제공자 호출도 모두 생략한다고 설명하면 안 됩니다. 정적 문자열·함수 소스·상속한 ReadText의 수명과 무효화 계약을 보존합니다.

```csharp
var text = Fluent.Text("Several words wrap at the assigned parent width.");
var allocated = Dux.SizedBox(text, 80, 64);
// Custom pure measurement uses the same static presentation contract:
// context.MeasureText<MyTextPresentation>(content);
```

원본에서 부모 80x64 대비 실제 13.330078x20 텍스트 루트를 재현했습니다. 현재 .NET11 RC3 관리 검증은 CPU47·변형 픽셀 비교232·서로 다른 주기의 유지 프레임 비교40·재사용 횟수2로 총321개가 통과했습니다. 래스터/벡터·Light/Dark·MSAA1/4·프레임 슬롯3에서 준비 이미지를 포함한 readback545개를 생성했습니다. FPS16프레임/NF3프레임 갱신과 클립 너비·높이 변경도 검사합니다. 폰트·기하 품질·부하는 낮추지 않았습니다. FPS·Private·할당 개선을 주장하지 않으며 컨텍스트 크기·Native 코드 크기 비용은 미측정입니다. C++ 도구 탐색 파일 부재로 Native·런타임 교차 검증은 미검증입니다. 근거:artifacts/text-allocation-2026-11-10/verification.json.

새 로컬 샘플:layout_text_allocation_fba.cs(Width/Height·Short/Empty/Reset). 현재 로컬 소스가 필요하며 기존 NuGet 패키지에 포함된 API/샘플은 아닙니다. [검증한 샘플 실행](widget-gallery-run.ko.md). 공식 텍스트 꾸밈·정렬·접근성 전체 적합성은 이번 할당 검증과 별도입니다.


Last synced: 2026-11-10

## Text 수명과 유휴 갱신 보정

공통 `UiText<TSource,TSourcePolicy,TPresentation>`의 Source/Color 변경은 새 값을 먼저 게시한 뒤 프레임을 요청한다. 같은 값은 요청하지 않는다. 정적 문자열은 null을 빈 문자열로 정규화한 뒤 비교하며, getter 교체 시 내용을 평가하지 않는다. Dispose는 소유 문자열/getter를 해제하고 이후 변경을 거부한다. 생성된 Text·타이포그래피 프리셋과 사용자 상속 위젯에 동일 적용된다. 엔진 공급자 해제는 사용자 OnDispose 훅과 분리되어 기반 호출 생략·사용자 훅 예외에도 실행된다.

명시적 정적 레이어의 ContentVersion 계약과 기존 렌더 표현·폰트·품질은 유지한다. 이전 소스를 명시적으로 재빌드한 대조군은 새 유휴 알림 검사에서 실패했다. 단순 복사한 첫 대조 시도는 타임스탬프 때문에 수정본 어셈블리를 재사용했으므로 근거에서 제외했다.

수정본 .NET10 집중63개(텍스트31+타이포그래피32), 전체1108개 및 고정 .NET11 RC3 NativeAOT 텍스트31개 통과. 같은 내용/색10000회 재할당은 추가 할당0B·프레임 요청0회다. 앱 전체 FPS/Private/Pi 개선율은 아니다. net8/9/10 빌드는 경고0·오류0. 로그·소스/실행파일 해시: `artifacts/text-lifecycle-2026-11-05/verification.json`.

## 기본 타이포그래피 연결 후속

`Fluent.Text`와 `Dux.Text/TextColored/TextWrapped`의 생성된 기본 표현은 이제 실제 Regular 글꼴의 Body1(14px/20px)과 기본 줄바꿈을 사용한다. 내용 소스·표현 타입은 기존 소스 생성기가 닫힌 제네릭에 직접 연결한다. 기본/동적/상속 위젯과 `ui.Text<UiFluentTextPresentation>`은 같은 표현을 사용하며, 줄바꿈도 해당 글꼴을 선택한 범위에서 측정한다. 현재 글꼴 설정을 검사해 런타임 테마를 고르는 분기는 추가하지 않았다.

`TypographyWrapped<TTypeface,TMetrics>`로 사용자 표현도 같은 경로를 사용한다. 실제 텍스트 메트릭·글꼴 설정이 없는 백엔드는 명시적으로 실패한다. 앱 기본 설정은 Segoe UI와 명시적인 한글 글꼴을 제공한다. 저수준 UiContext 사용자는 DirectText·메트릭 백엔드·UiTypographyFonts를 구성해야 한다.

기존 비제네릭 `ui.Text/TextColored/TextWrapped`는 현재 컨텍스트 글꼴을 유지하는 명시적 `UiContextTextPresentation/UiContextWrappedTextPresentation` 경로로 보존했다. 그 API 전체의 Fluent 기본 전환은 아직 끝나지 않았다. 이 구분은 호환 경로이며 지원되지 않는 Fluent 표현을 자동 대체하는 처리가 아니다. 아래 초기 구현·검증 수치는 역사적 기록이다.

공식 고정 커밋의 `useText.ts`는 wrap 기본값 true, `useTextStyles.styles.ts`는 기본300 크기·행높이·Regular를 지정한다. 원본과 해시는 `artifacts/fluent-default-text-2026-11-05/`에 보존한다. 기본 Text와 Body1의 실제 2D 래스터·3D 벡터 픽셀, 한글, 함수 변경, 좁은 줄바꿈, 글꼴 범위 복원을 검증한다. 이 후속은 italic·밑줄·취소선·정렬·truncate·의미 정보·전체 공식 화면 일치를 완료하지 않는다. 전체 앱 FPS/Private 개선은 측정하지 않았다.

`Dux.Text`·`TextColored`·`TextWrapped`의 문자열/함수 입력을 실제 상속 위젯으로 전환했다. 기존 `IUiView` 반환 계약은 유지한다. 표준 `Fluent.Text(string)`는 `UiFluentText`, `Fluent.Text(Func<string>)`는 `UiFluentDynamicText`를 직접 생성한다. 줄바꿈은 생성된 `UiFluentWrappedText`와 `UiFluentDynamicWrappedText`를 사용한다. 카탈로그의 Text 이름은 입력 변형마다 중복하지 않는다.

## 컴파일 시점 연결과 확장

`UiText<TSource,TSourcePolicy,TPresentation>`은 내용 소스와 표현을 각각 구체 타입으로 연결한다. 문자열은 `UiStaticTextSource`, 함수는 `UiGetterTextSource`다. 프레임마다 문자열/함수 중 무엇을 사용할지 고르는 분기나 사용하지 않는 함수 필드를 두지 않는다. 함수 소스는 매 렌더에 한 번 실행하며, 그 콜백을 생략하는 최적화는 하지 않는다. 잘못된 소스/정책 조합은 생성기 DUXWGT004로 거부한다.

사용자는 `UiFluentText` 또는 `UiText<TPresentation>`을 상속하여 `ReadText()`를 확장할 수 있다. 표현은 `IUiTextPresentation.Render`로 선택한다. `UiControlContext`의 MeasureText·PlaceText·LayoutText·WrapText·PaintText와 기존 도형 기능을 사용하면 측정·배치·배경·표식·그리기 구성을 바꿀 수 있다. `widget_tree_theme_fba.cs`의 ShowcaseNoticeText는 생성된 사용자 위젯으로, 여백·배경·표식을 가진 별도 구성을 사용한다. 색 숫자만 바꾸는 프리셋이 아니다.

기존 즉시 Text/TextColored/TextDisabled/TextUnformatted와 TextWrapped는 컨텍스트 글꼴 정책으로 보존한다. Fluent의 형식 지정 즉시 API와 생성 위젯은 Body1 정책을 사용한다. 두 정책 모두 테이블 정렬·아이템 클립·레이어·텍스처와 공통 DirectText/atlas 그리기를 공유한다. 별도 렌더러나 자동 표현 대체를 만들지 않았다.

## 공식 기준과 미완료 범위

[공식 Text 지침](https://fluent2.microsoft.design/components/web/react/core/text/usage)을2026-11-05 확인했다. 글꼴·정렬·타이포그래피 프리셋·의미 정보를 다루는 기준이며, 현재 전환이 그 전체를 충족한 것은 아니다. 참조 API는 고정 커밋8830c10bfb2f4c1bdd540287b09713b9679cd1ad의 react-text.api.md다.

일반 Fluent Text의 글꼴·행높이는 위 후속에서 [타이포그래피17종](fluent2-typography.ko.md)의 Body1과 연결했다. italic·underline·strikethrough, 정렬·truncate, 네이티브 의미 정보/접근성과 실제 공식 화면 일치는 아직 남아 있다. 웹 페이지의 Preview 제목을 패키지 릴리스 채널로 해석하지 않는다. 아래 검증 시점 카탈로그5이름은 후속23이름으로 늘었지만 알려진 요구289이름은 최종 전체 분모가 아니다.

## 검증과 비용

기본 타이포그래피 후속: NET10전체963·관리/고정RC3 Native 공통67검사·net8/9/10 Release 경고0·오류0. 직전417이미지가 모두 같고 새 기본Text6이미지는 Body1의 래스터/벡터 출력 및 내용 변경을 보여준다. Native에만 축소된 이전 팩터리 할당 때문에 검사 설명의 수치 한 곳이 다르다.1,024개에서 관리114,688→90,112B, Native98,304→90,112B이며 같은 런타임 내부 비교만 유효하다. 이는 이미 구현된 생성 팩터리의 구성 비용 재확인이며 이번 글꼴 변경의 속도 개선이 아니다.

공통 줄바꿈은 들어맞는 단일 행의 원본 문자열을 반환하여 단어별 문자열·출력 빌더를 생략한다. 개행에서 측정 폭을 초기화하지 않던 오류도 수정했다. 새 래스터 검사가 생성한 유휴 글꼴 자원의 정상 회수를 기존 상주 텍스트 검사에서 오인했으므로, 상주 목록이 실제 참조하는 텍스처만 파괴 여부를 검사하도록 고쳤다. 상주650프레임과 픽셀 검사는 유지했다. 처음 한글 벡터 테스트에는 명시적 한글 글꼴이 없었고 앱 기본값과 같은 글꼴 세트를 지정한 뒤 통과했다. 부하를 줄이거나 글리프를 대체하지 않았다. 최종 근거는 artifacts/fluent-default-text-2026-11-05/verification.json이다.

.NET10 전체815검사·최종 집중103검사·생성기66검사, net8/9/10 솔루션 경고0·오류0이다. 공통 경로의 vertex/index/primitive/명령(clip·texture 포함) 동치, 줄바꿈·표·클립, 매 프레임 getter, 사용자 상속·표현 교체, 가변 내용과 커서를 검사했다. 직전 구현의 기존 이미지323개가 같다. 이는 공식 Fluent 스크린샷 대조가 아니다.

실제 이전 팩터리의 매개변수 캡처 본문과 같은 .NET10 구성 진단에서1,024개 할당114,688→90,112B(-21.43%)다. 프레임 성능·Private·NativeAOT 속도의 증거가 아니다. 최초 테스트는 상수 람다를 사용해 실제 이전 팩터리와 달랐으므로, 기존 캡처 본문을 복원한 fixture로 수정했다. 새 PaintText 이름은 기존 Text의 target-typed new 호출과 오버로드 충돌을 피한다.

고정 RC3 NativeAOT의 실제 native code generation·첫 GPU present·exit0을 확인했다. 최종 소스는 소스 종류도 닫힌 타입으로 고정한다. 이전 프로토타입 B2C61 실행 파일과 결과는 보존했다. 시작 성공을 전체 상호작용·룩앤필·성능 검증으로 확대하지 않는다. 기존 RC3 전체 GPU 비교 실패와 유지 ID 캐시의 추가 상주 비용은 이번 전환으로 해결됐다고 보고하지 않는다.

근거: artifacts/fluent-text-2026-11-05/full-final-net10, widgets-final-typed-net10, generators-final-net10, image-comparison-final.json, native-final-run.json 및 verification.json. 실행은 `./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache`이며, 저장된 RC3 비교는 전용 고정 도구·출력 경로를 사용했다.

2026-11-12 후속 — [콘텐츠 측정 계약](widget-cpu-retention.ko.md): 결정론적 소스·표현은 부모 재사용에 참여하고 동적 문자열/배지 getter와 사용자 특성은 기본 재측정합니다. getter Progress의 선언 기하와 Spinner의 안정된 표시는 별도 약속이며 그리기·getter·애니메이션·이동 처리는 계속 실행합니다. 집중26개와 실제 GPU 비교는 이 범위의 근거이며 전체 NativeAOT·성능 완료가 아닙니다. 근거: artifacts/content-layout-retention-2026-11-12/verification.json.

# Fluent 2 Button 타이포그래피와 secondary 표현

Last synced: 2026-11-06

범위 기준은 [공식 Button 지침](https://fluent2.microsoft.design/components/web/react/core/button/usage)이다. 고정 참조 구현과 독립적으로 해석한 색 검사 데이터는 `artifacts/fluent-button-typography-2026-11-05/source-manifest.json`에 기록했다.

생성된 기본 `UiFluentButton`은 `UiFluentButtonPresentation`을 선택한다. Segoe UI Semibold 14px/행높이20px, 최소96×32다. `UiFluentSmallButtonPresentation`은 Regular12/16·최소64×24, `UiFluentLargeButtonPresentation`은 Semibold16/22·최소96×40이다. 갤러리 `--buttons`처럼 `UiButtonStyle`로 기존 동작과 표현을 컴파일 시점에 연결한다. Button 크기 구현이며 카탈로그 컴포넌트를 추가한 것이 아니다.

공통 Button 커널은 측정 전에 값 형식 타이포그래피 범위를 열고, 측정·그리기 예외에도 주변 폰트를 복원한다. 직접 제네릭 호출과 생성 위젯은 같은 구현을 소비한다. 비제네릭 `ui.Button()`은 아직 호환 표현을 사용한다. 텍스트에는 설정된 폰트와 DirectText 폰트 메트릭 백엔드가 필요하며 미지원은 명시적으로 실패한다. 빈 라벨은 텍스트 백엔드가 필요 없다.

secondary 라이트/다크의 기본·호버·누름·비활성 표현에 고정 참조의 중립 색을 적용했다. 키보드 포커스에는 안쪽 선을 추가한다. 실제 기본 폰트의 ascent/descent와 선택한 행높이로 글자를 배치하고 버튼·현재 항목/표 셀에 맞춰 자른다. 글자의 둥근 모서리 마스킹은 남아 있다.

폰트 메트릭은 컨텍스트의 인라인4칸에 폰트·물리 크기·백엔드 세대로 구분해 보관한다. 각 경로는 최대1024자다. 폰트 설정 변경은 정적 레이어를, 백엔드 버전 변경은 메트릭을 무효화하며 컨텍스트 해제 시 회수한다. 버튼별 메트릭 객체를 추가하지 않았다. 같은 크기·교차 크기 버튼1만개, 예외 복원, 백엔드 버전·폰트 교체, 수명과 실제 글자 색을 검사한다.

검증: .NET10 집중20개/전체1228개, 생성기83개, net8/9/10 빌드 경고0·오류0. 고정.NET11 RC3 NativeAOT EXT/core각375개. 새 Button 이미지14개는 두 Native 경로 모두 관리 렌더와 같고, 이전 Native 이미지209개도 각 대응 조건에서 바이트가 같다. 넓은 관리/Native 파일 교집합에는 Progress/Window 이미지8개의 차이도 있다. 두 실행기는 같은 장면 이력을 보장하지 않으므로 원인 미분류로 보존하고 일치 주장에서는 제외했다. 루트 MSAA 전환 전에 캡처되므로 그 설정만으로 설명하지 않는다. 해당 폴더의 `verification.json`과 이미지 비교 파일을 참조한다.

GPU 상태 행렬은 명시적 검사 동작 정책을 사용한다. 표현·생성/직접/유지 경로의 일치 검사이며 실제 포인터 전이·접근성 검증이 아니다. 갤러리 시작·3초 자동 종료는 통과했으나 전경·수동 입력·공식 브라우저 픽셀 동등성은 미검증이다.

후속 구현으로 아래 primary/outline/subtle/transparent 정책을 추가했다. 남은 일: 모양·아이콘·compound/menu/split, 모션·비활성 포커스, Windows UIA·공식 브라우저 동일 조건 비교. 소수 DPI의 래스터 크기 반올림도 기존 백엔드 한계다. Button 전체나 Fluent 2 완료를 뜻하지 않는다. 새 앱 FPS·Private·Pi 성능 개선 주장은 없다. 과거 CPU 측정은 당시 폰트 조건의 결과이므로 변경된 폰트 정책의 성능을 입증하려면 같은 조건으로 새 대조가 필요하다.

## 컴파일 시점 appearance 구현

`IUiButtonAppearance.Paint`는 프레임 그리기를 담당하고 글자 색을 반환한다. 사용자 구현은 그리기 알고리즘 자체를 바꿀 수 있다. 전체 `IUiButtonPresentation`을 교체하면 타이포그래피·측정·배치도 바뀐다. appearance 인터페이스가 이 더 넓은 확장 지점을 대체하지 않는다.

정책은 `UiFluentSecondaryButtonAppearance`, `UiFluentPrimaryButtonAppearance`, `UiFluentOutlineButtonAppearance`, `UiFluentSubtleButtonAppearance`, `UiFluentTransparentButtonAppearance`다. 각각 medium `UiFluentButtonPresentation<TAppearance>`, small `UiFluentSmallButtonPresentation<TAppearance>`, large `UiFluentLargeButtonPresentation<TAppearance>`에 연결한다. 닫힌 제네릭 호출로 선택하며 appearance enum·런타임 구현 레지스트리·위젯별 정책 객체가 없다. Primary는 추가 안쪽 포커스 선을 직접 그리고 공통 프레임은 기존 secondary 픽셀을 보존한다. Primary 포커스 shadow2는 미구현이므로 브라우저와 시각적으로 같다고 보고하지 않는다.

```csharp
[UiButtonStyle(typeof(UiReleaseButtonBehavior),
    typeof(UiFluentSmallButtonPresentation<UiFluentPrimaryButtonAppearance>))]
public partial class SaveButton;
```

Build에서 `context.Child("save", () => new SaveButton("Save", Save))`로 생성 위젯을 유지한다. 직접 호출은 `ui.Button<UiReleaseButtonBehavior, UiFluentSmallButtonPresentation<UiFluentPrimaryButtonAppearance>>("Save")`다. 동작·폰트 범위·렌더링이 생성 위젯과 같다. 크기·appearance를 상위 카탈로그 이름으로 추가하지 않는다. 비제네릭 호환 API 전환은 남아 있다.

독립 검사 데이터 `ButtonAppearances.json`은 실제 brandWeb·grey 토큰과 비활성 덮어쓰기·투명 표면을 해석한다. 토큰 주석에는 과거 브랜드 색이 있으므로 색 기준으로 쓰지 않는다. 소스 해시·해석 스크립트·검증 결과는 `artifacts/fluent-button-appearance-2026-11-05`에 있다. GPU 비교는5표현·3크기·4상태·라이트/다크·MSAA1/4이며 별도 실제 입력 검사는 포인터 유지·해제·경계 밖 취소, Tab·Enter·Space 반복·비활성을 확인한다. 표현 상태 주입은 검사 전용이다. UIA·강제 색상·공식 브라우저 래스터 일치·모션·나머지 Button 변형의 완료 근거는 아니다.

Appearance 검증: .NET10 입력32개/전체1320개, 고정RC3 Native EXT/core각467개, net8/9/10 빌드 경고0·오류0. 새 이미지100개는 각 Native 경로와 같아200건 비교가 통과했다. 이전 Native223개씩과 관리500개도 그대로다. 최신 갤러리 경로·해시는 `artifacts/fluent-button-appearance-2026-11-05/gallery-smoke.json`에 있으며 시작·첫 제출·자동 종료가 통과했다.

## 모양 그리기 정책

`IUiButtonShape.Paint`는 포커스 inset을 포함한 프레임 전체를 그린다. 기본 구현은 `UiFluentRoundedButtonShape`, `UiFluentSquareButtonShape`, `UiFluentCircularButtonShape`이며 기본 선택은 rounded다. circular 직사각형 버튼은 캡슐이고 정사각형이면 원이 된다. 공식10000px 반경을 짧은 변에 맞춰 정규화하며 아주 큰 버튼에서도 이 상한을 유지한다.

appearance 타입 안에 모양을 선택한다.

```csharp
[UiButtonStyle(typeof(UiReleaseButtonBehavior),
    typeof(UiFluentButtonPresentation<UiFluentPrimaryButtonAppearance<UiFluentCircularButtonShape>>))]
public partial class ConfirmButton;
```

small/large 표현 및5종 appearance에도 같은 조합을 사용한다. 생성기가 닫힌 타입을 확정하므로 런타임 모양 레지스트리·위젯별 모양 객체가 없다. 기존 비제네릭 appearance는 rounded로 연결해 이전 픽셀을 보존한다. 사용자 모양은 서로 떨어진 두 표면 등 다른 기하 알고리즘도 같은 확장 지점으로 그릴 수 있다. GPU 검사에서 이를 확인하며 반경 숫자만 바꾸는 확장으로 제한하지 않는다. 사용자 확장이 기본 카탈로그 컴포넌트로 추가되지는 않는다.

검증: .NET10 전체1450개, 고정RC3 Native EXT/core각597개, net8/9/10 경고0·오류0. 새 이미지210개가 각 Native 경로와 같아420건 비교가 통과했다. 이전 Native323개씩·관리600개도 그대로다. 모양/표현/크기/상태/라이트·다크/MSAA1·4, 포커스, 독립 모서리 픽셀과 큰 버튼의 반경 상한을 검사한다. 표현 상태 행렬과 실제 입력·UIA·공식 브라우저 동등성은 구분한다. Primary shadow2, 크기별 rounded 포커스의 정확한 처리, 둥근 글자 마스킹, 아이콘·compound/menu/split·모션·강제 색상·비제네릭 즉시 경로 전환은 남아 있다. 새 FPS·Private·Pi 개선 주장은 없다. `artifacts/fluent-button-shapes-2026-11-05/verification.json` 참조.

## 컴파일된 아이콘 구성

`UiFluentIconButtonPresentation<TSize,TAppearance,TIcon,TPosition>`은 실제 아이콘 그리기 구현을 텍스트·프레임·입력 동작과 조합한다. 크기는 `UiFluentSmallButtonIconSize`, `UiFluentMediumButtonIconSize`, `UiFluentLargeButtonIconSize`이며 아이콘20/20/24px·텍스트 간격4/6/6px다. 표시 라벨이 비어 있으면24/32/40px 정사각형 아이콘 전용 버튼이다. 고정 공식 참조의 small 아이콘은16px가 아닌20px다. 명시적 Duxel Size 설정은 우선한다.

`IUiButtonIcon.Paint`에서 전달받은 영역·색·입력 상태로 그린다. `UiButtonIconBefore`와 `UiButtonIconAfter`는 물리적 배치를 컴파일 시점에 선택하며 논리 RTL 자동 매핑은 미구현이다. appearance는 아이콘 색도 별도로 결정한다. subtle의 호버/누름에서는 텍스트의 중립 색을 유지하면서 아이콘은 브랜드 색을 사용한다. 사용자 아이콘은 전달받은 상태로 regular/filled 기하를 선택할 수 있다.

```csharp
[UiButtonStyle(typeof(UiReleaseButtonBehavior),
    typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,
        UiFluentPrimaryButtonAppearance,MyIcon,UiButtonIconBefore>))]
public partial class AddButton;
```

Button 커널은 숨김 `##` 접미사를 처리한 실제 라벨 유무를 불변 `UiControlContext.ButtonHasText`로 전달한다. 측정 폭으로 추측하지 않는다. 기존 표현 인터페이스는 유지한다. 그리기에 전달하는 컨텍스트 값에 bool이 추가되며 위젯별 필드·힙 정책 객체는 추가하지 않았다. 앱 전체 성능 영향은 미측정이다.

`UiControlContext.Clip`은 Button·항목/표 셀·현재 clip의 교집합에 적용하는 값 형식 범위다. 아이콘 예외에도 클립과 주변 폰트를 복원한다. 텍스트 없는 벡터 아이콘에는 텍스트 백엔드가 필요 없다. 글자는 아직 사각형 클립이므로 정확한 둥근 마스킹은 남아 있다.

검증: .NET10 아이콘 기능39개/전체1809개, 고정RC3 Native EXT/core각956개. 새 이미지480개가 각 Native와 같아960건 비교가 통과했으며 이전 Native533개씩·관리810개도 그대로다. 검사 아이콘은 배치·픽셀 색·생성/직접/유지 경로를 확인하는 작은 단색 도형이다. 갤러리는 사용자가 그린 체크 아이콘과 공통 콜백·상태 경로를 시연한다. 어느 쪽도 공식 Fluent 시스템 아이콘 자산이라고 보고하지 않는다. 후속 구현으로 공식 폰트와 Add 생성 타입을 연결했다. 전체 심볼 API·아이콘 전용 접근성 이름/UIA·공식 SVG/브라우저 일치는 여전히 필요하다. 위의 다른 Button 미완료 항목도 유지한다. 새 앱 FPS·Private·Pi 개선 주장은 없다. 근거: `artifacts/fluent-button-icons-2026-11-06/verification.json`.

공식 폰트와 `[UiFluentIcon]` 생성 경로는 [공식 아이콘](fluent2-icons.ko.md)을 참조한다. Add regular/filled/interactive를 제공하며 전체 심볼 API·공식 SVG/브라우저 일치·UIA 완료를 뜻하지 않는다.

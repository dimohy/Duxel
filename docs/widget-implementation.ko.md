# 위젯 상속과 컴파일된 구현 연결

[RadioGroup](fluent2-radio-group.ko.md)은 그룹 입력과 세 배치 알고리즘을 분리하고 같은 생성/상속 경로로 연결한다. `UiControlTextBlock`은 측정한 줄을 보존해 줄별 기준선과 실제 잉크를 그리며 RadioGroup의 줄바꿈에 사용한다. 레이아웃 캐시는 컨텍스트·폰트·폭 무효화와 부분 실패 정리를 검증한다.

[Checkbox 구현](fluent2-checkbox.ko.md)은 크기별 마크·프레임 형태·라벨 배치를 서로 다른 구현으로 연결하는8조합 예다. `IUiTogglePresentation.BeginText`의 기본 스코프는 기존 사용자 표현을 보존하며 Fluent 구현만 명시적인14/20 타이포그래피를 선택한다. 기본·사용자 생성 위젯과 제네릭 즉시 경로가 같은 입력/그리기를 소비한다.

Last synced: 2026-11-06

## 공통 버튼 갱신과 해제

후속 감사에서 Progress·Slider·Toggle도 같은 자원 해제 단계에 연결했다. Progress Source/Size, Slider Label/Options/Size, Toggle Label은 변경된 값 게시 후 유휴 프레임을 요청하며 같은 값은 요청하지 않는다. 검증에 실패한 값은 기존 상태와 프레임 요청을 바꾸지 않는다. Progress 동작 정책 해제가 실패해도 공급자와 정책 상태의 참조 정리를 계속한다.

Slider/Toggle은 해제 시 바인딩 참조를 놓고, 이후 Value 바인딩 접근은 ObjectDisposedException이다. 외부에서 받은 UiBinding/UiState 자체를 폐기하거나 값을 변경하지 않는다. 외부 바인딩 또는 임의 getter 내부 값의 변경은 기존 Watch/명시 프레임 요청 계약을 따른다. 이번 수정이 모든 getter의 변경을 자동 추적한다는 뜻은 아니다.

현재 생성된 표준 카탈로그27개 이름을 공통 기반7계열에 대조했다. 이는 현재 연결된 위젯의 소유권 참여 확인이며, 공식 전체 컴포넌트·모든 기능·모든 레거시 위젯의 완료 수가 아니다. .NET10 집중17개/전체1199개, 고정RC3 Native EXT/core각346개, 기존 이미지418건 일치. 폐기한 위젯을 보유한 상태의 바인딩 그래프 회수는 별도 수명 단위 검사이며 그 GC 호출을 Private 메모리·성능 개선 측정에 사용하지 않는다. 근거: `artifacts/control-ownership-2026-11-05/verification.json`.

`UiButton<TBehavior,TPresentation>`의 Label/Size 변경은 새 값을 먼저 게시하고 유휴 프레임을 요청한다. 같은 값은 요청하지 않는다. Activated 교체는 다음 입력에 적용되며 시각 변경이 없으므로 별도 재그리기를 요청하지 않는다. 누른 상태에서 라벨·콜백을 교체해도 유지된 입력 정체성을 보존하고 새 콜백을 release에서 한 번 호출한다. null 라벨은 생성자와 동일하게 거부한다. Dispose 후 Label/Size/Activated 변경도 거부한다.

엔진 소유 자원은 사용자 `OnDispose`와 분리된 공통 해제 단계에서 회수한다. 사용자 훅이 기반 호출을 생략하거나 예외를 던져도 Button 콜백·라벨, Text/Badge/Divider 공급자가 해제된다. 사용자 훅과 자원 해제의 오류는 기존 AggregateException 계약으로 관측되며 이후 소유권·프레임 콜백·정체성 정리를 계속한다. 사용자 확장 자체가 소유한 자원은 자신의 훅에서 정리해야 한다.

원본 버튼을 명시적으로 재빌드한 대조군은 유휴 알림 검사에서 실패했다. 현재 검사는 정상 입력·동일값 재할당·해제 오류 경로를 검증한다. 동일값 재할당 검사는100회 준비 후10000회에서 추가 할당0B·프레임 요청0회다. 새로운 앱 전체 FPS/Private/Pi 개선 측정은 아니다. .NET10 할당 결합 검사의 초기 실패는 관측 추가·준비 후 통과만으로 원인이 해결됐다고 기록하지 않는다. 근거와 정확한 검증 범위: `artifacts/button-lifecycle-2026-11-05/verification.json`.

후속 범위·현재 기본 팩터리·노출 정책은 [Fluent 전체 전환](fluent2-coverage.ko.md)을 우선한다. `Dux.Button()`은 이제 생성된 `UiFluentButton` 팩터리를 사용하며 아래 초기 단계의 숫자 스타일 호환 설명은 `ui.Button()`·레거시 `UiButton`에 해당한다.

첫 위젯 테마는 **Microsoft Fluent 2**다. Windows 11·WinUI의 지침을 같은 테마의 Windows 표현 기준으로 사용한다. 별도 Windows 테마를 먼저 만들거나 Material을 첫 테마로 선택하지 않는다.

## 현재 구현과 사용

`UiWidget`은 사용자 구성 위젯의 Build·키·상태 구독·수명을 담당한다. 기본 리프 위젯의 기반인 `UiControlWidget`도 같은 기반을 사용한다. `UiButton<TBehavior, TPresentation>`은 상속 가능한 기본 버튼이며, 상태별 입력 처리와 측정·그리기를 서로 다른 계약으로 연결한다.

```csharp
[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentButtonPresentation))]
public partial class ProductButton;

public sealed class SaveButton : ProductButton
{
    public SaveButton() : base("Save") { }
    protected override void OnActivated() { /* application action */ }
}
```

생성기는 `ProductButton : UiButton<UiReleaseButtonBehavior, UiFluentButtonPresentation>`과 생성자를 만든다. 실행 중 타입을 검색하거나 테마 이름으로 구현을 선택하지 않는다. 잘못된 대상은 DUXWGT003, 잘못된 구현 조합은 DUXWGT004 컴파일 오류다. 구체 타입은 `IUiButtonBehavior`와 `IUiButtonPresentation`의 정적 계약을 구현한다. 인터페이스·추상 클래스·열린 제네릭·접근할 수 없는 타입을 선택할 수 없다.

`ui.Button<TBehavior, TPresentation>()`과 상속 버튼은 동일한 입력·레이아웃·표현 경로를 사용한다. 기존 `ui.Button()`과 `Dux.Button()`도 이 공통 경로에 연결된다. 기본 `UiButton` 클래스는 기존 외형·숫자 스타일 범위와의 호환 정책을 사용한다. Fluent 클래스는 구체 Fluent 표현을 명시적으로 선택하며 레거시 숫자 스타일에 의존하지 않는다.

`UiControlContext`는 현재 레이어·클립·텍스트 경로를 이용하는 Frame·Circle·Text 그리기를 제공한다. 표현 구현은 기존 버튼의 반경만 바꾸는 것에 제한되지 않는다. 원형 그리기로 구현을 교체하고 같은 표현에 press/release 동작을 각각 적용하는 검증을 포함한다. 위젯 상속은 인스턴스 훅을 제공하고, 프레임의 입력·표현 호출은 정적 제네릭 계약으로 연결된다. 프레임마다 구현 객체나 런타임 레지스트리를 만들지 않는다. 이 사실만으로 최고 성능 또는 앱 FPS 향상을 입증한 것은 아니다.

`UiBuildContext.Child()`로 버튼을 소유하면 부모가 재구성되어도 같은 키의 인스턴스·입력 ID·상태를 유지하고 제거 시 Dispose한다. 실행 예제는 `samples/fba/widget_tree_theme_fba.cs`다.

## 공식 기준과 적용 경계

2026-11-05 조회한 [Fluent Windows 안내](https://fluent2.microsoft.design/components/windows)는 Windows 11의 Fluent 표현과 WinUI를 연결한다. [Fluent 버튼 지침](https://fluent2.microsoft.design/components/web/react/core/button/usage/)은 일반 버튼·복합 버튼·메뉴 버튼·토글 동작을 구분한다. 현재 구현은 medium 일반 버튼의 기본 측정·프레임·중앙 텍스트와 입력 정책 분리 단계다. 전체 Fluent 위젯, 접근성·키보드 동작·애니메이션의 완전 지원을 뜻하지 않는다. 버튼 수치의 고정 소스와 SHA-256은 `artifacts/widget-tree-theme-2026-11-05/sources/manifest.json`에 있다.

[Mica](https://learn.microsoft.com/en-us/windows/apps/design/style/mica)는 앱 바탕과 콘텐츠 계층, [Acrylic](https://learn.microsoft.com/en-us/windows/apps/design/style/acrylic)은 주로 일시적 표면에 적용하는 공식 기준이다. 현재 Duxel의 이 신규 테마에서 실제 Mica·Acrylic 재질 처리가 구현됐다는 뜻은 아니다. 구현·GPU 비용·정책 지원을 별도로 검증해야 한다. Windows App SDK·WinUI를 Duxel 렌더러로 교체한다는 결정도 아니다.

## 검증과 남은 범위

- `.NET10` 솔루션 Release 빌드: 경고 0, 오류 0. Core는 net8/net9/net10 컴파일을 유지한다.
- 생성기 검증: 27건(기존 숫자 설정 16건 + 구현 타입 연결 11건). 숫자 설정 성공을 구현 교체 완료로 집계하지 않는다.
- 집중 검증: 57건, 전체 GPU 포함 회귀: 769건. `artifacts/widget-implementation-2026-11-05/`의 결과·로그를 따른다.
- 고정 RC3 전체 회귀도769건 통과. 새 NativeAOT 및 관리 FBA의 첫 GPU present·exit0은 각각 `native-run.json`과 `managed-app.stdout.log`에 기록한다. 새 NativeAOT SHA256은 `7D86831FD1C15DD4042721658C6730BDDA22C11EB0C9FCB66B88C0BD77EECFA4`다. 상호작용·시각 완성·성능 비교 검증과 구분하며, 이전88D4714D의 성공을 새 소스의 검증으로 재사용하지 않는다.
- 텍스트 입력·체크박스·토글·슬라이더·목록·메뉴·팝업 등의 동일 상속·표현 구현 전환은 미완료다. 현재 숫자 기반 컨트롤 코드는 호환 구현이며 이 목표의 완료 증거가 아니다.
- 모든 위젯을 선택하는 테마 단위 생성, 더 넓은 구성·그리기 확장 API, 공식 사양 대응표, 동일 조건 CPU·메모리·프레임 성능, Linux/ARM64 실행과 Pi 실기기 검증이 남아 있다.

기존 최적화 원장은 유지한다. 초기 숫자 설정 생성 성공으로 완료했던 두 조건을 다시 열어 당시34개 조건 중12개를 인정했다. 이후 전체 Fluent 범위가 추가되었으므로 이 수치를 현재 총 진행률로 사용하지 않는다. 현재 전체 분모는 공식 컴포넌트 대응 검토 중이며 미확인이다.

## Button 실제 폰트와 크기

생성 기본 Button은 실제 Semibold14/20을 사용한다. small Regular12/16과 large Semibold16/22는 `UiButtonStyle`로 정적 연결한다. 주변 폰트 복원·상한 있는 메트릭 캐시·정적 레이어 무효화는 공통 경로이며 비제네릭 즉시 버튼은 아직 호환 표현이다. [상태·검증·남은 범위](fluent2-button.ko.md), [실행 가이드](widget-gallery-run.ko.md).

현재 소스의 `--buttons`에는 primary·outline·subtle·transparent도 있다. `UiButtonStyle`과 크기별 `UiFluentButtonPresentation<TAppearance>` 계열로 공통 동작에 정적 연결한다. [정확한 API와 남은 범위](fluent2-button.ko.md).

`--buttons`의 square/circular3크기도 생성된 공통 위젯을 사용한다. 모양은 `UiFluentPrimaryButtonAppearance<UiFluentCircularButtonShape>`처럼 표현 정책 안에 정적으로 조합한다. 사용자 프레임 알고리즘도 같은 확장 지점이며 [정확한 범위와 한계](fluent2-button.ko.md)를 참조한다.

아이콘 Button은 `UiFluentIconButtonPresentation<TSize,TAppearance,TIcon,TPosition>`을 생성 스타일에 연결한다. `--buttons`의 Add 아이콘은 고정 공식 폰트이며 사용자 특성의 생성 경로도 실제 소비한다. 아이콘 전용 UIA는 아직 미완료다. [구성·검증·한계](fluent2-button.ko.md).

공식 폰트와 `[UiFluentIcon]` 생성 경로는 [공식 아이콘](fluent2-icons.ko.md)을 참조한다. Add regular/filled/interactive를 제공하며 전체 심볼 API·공식 SVG/브라우저 일치·UIA 완료를 뜻하지 않는다.

아이콘 생성기는 선택된 메타데이터 값이 같으면 문자열 구성·출력을 재사용한다. 생성기 기능107개와 Core 생성 파일57개 바이트 일치를 확인했다. 같은 파일 선언 제거로 남은 출력이 재생성되는 비용은 미완료이며 [추적·한계](fluent2-icons.ko.md)를 참조한다.

[Switch 정적 크기·배치·상태 표현](fluent2-switch.ko.md)

[Link 탐색·상속·정적 표현·캐시](fluent2-link.ko.md)

[한 줄 입력 동작·표현·수명 기반](input-kernel.ko.md)

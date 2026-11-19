# 위젯 접근성 기반

2026-11-11 — [RadioGroup](fluent2-radio-group.ko.md)이 실제 단일 선택 소유자와 RadioButton 자식을 공통 Selection·SelectionItem으로 공개합니다. 항목은 Toggle·Invoke를 제공하지 않습니다. 네이티브 Select·현재 상태 재검증·값 기반 ID·설명·이동 포커스를 실제 바인딩과 입력 경로에 연결했으며 부모 할당을 유지합니다. 집중18개·기존 입력과 할당91개·실제 Observer137개가 통과했습니다. 외부 검증 범위와 잔여 제한은 artifacts/radio-accessibility-2026-11-11/verification.json을 참고하세요.

2026-11-11 — Checkbox·Switch·ToggleButton이 실제 상태를 게시하고 기존 타입 지정 Toggle 동작을 사용합니다. `UiAccessibilityNode.ToggleState`가 단일 권위인 nullable enum이며 null은 미지원, Off0·On1·Indeterminate2는 지원되는 상태입니다. `Checked`는 같은 값의 기존 두 상태 소스 호환 표현이므로 그 null로 기능 미지원을 판정하지 않습니다. `UiAccessibilityToggleChange.PreviousState`는 이전 세 상태를 보존하며 기존 bool? 생성자·Previous 표현·분해도 유지합니다.

공통 UiToggle 경로는 현재 활성/클립/팝업을 재검증하고 기존 값 정책·바인딩 콜백으로 변경합니다. nullable Checkbox는 혼합→켜짐 이후 기존 켜짐/꺼짐 전이를 유지합니다. 그리기와 의미 정보가 정책을 한 번 읽은 값을 공유합니다. `IUiTogglePresentation.AccessibilityRole`의 기본은 Checkbox이며 내장 Switch·ToggleButton은 정적으로 자기 역할을 선택합니다. Windows는 Checkbox를50002, Switch/ToggleButton을50000과 Toggle10015에 연결하며 상태 있는 컨트롤에 Invoke를 중복 광고하지 않습니다. 정수 상태30086과 기능 지원30041을 구분하므로 혼합을 비활성이나 미지원으로 처리하지 않습니다. [Microsoft CheckBox/Toggle 지침](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportcheckboxcontroltype)과 [WinUI Switch의 Button/Toggle 대응](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.automation.peers.toggleswitchautomationpeer)을 따릅니다.

각 노드는 실제 그려진 스냅샷을 나타냅니다. 변경된 컨트롤보다 먼저 그린 공유 형제는 요청된 다음 프레임에 반영하며 앞서 그린 형제를 트랜잭션처럼 다시 그렸다고 주장하지 않습니다. 이름·Field/Tooltip·포커스 지원·정체성은 기존 공통 계약을 사용합니다. Core21개·관찰134개·생성기115개·외부 Toggle 클라이언트11개/호스트4개·기존 체크 메뉴 클라이언트6개/호스트3개가 통과했습니다. 관찰 GPU47개와 비교 가능한73개 본문은 그대로입니다. 근거: artifacts/toggle-accessibility-2026-11-11/verification.json. 전체 지역화 발화·강제 색상·컴포넌트 완전 일치·고정RC3 NativeAOT는 남아 있습니다.

2026-11-11 [Tooltip 표면 이벤트](tooltip-events.ko.md): 실제 대상 자식 ToolTip 역할과 컨트롤/콘텐츠 뷰 규칙, 게시 전 닫힘·게시 후 열림을 연결했다. 제거된 원본·조상의 이벤트 스냅샷으로 비동기 UIA 캐시를 유지한다. CPU520·외부 클라이언트10/호스트3이 통과했다. 공식 표면·속성 변경/창 종료 이벤트·NativeAOT 전체 검증은 남아 있다.

2026-11-11 [Tooltip 관계](tooltip-accessibility.ko.md): Description은 기존 힌트·검증과 함께 Windows HelpText에, Label은 명시적 자식·Field 이름보다 낮은 우선순위로 Name에 연결한다. Inaccessible은 관계를 게시하지 않는다. 논리 SourceId는 별도 공급자 노드가 아니며 표시된 툴팁 역할·이벤트는 추가 구현이 필요하다.

공통 의미 정보 트리가 Windows UI Automation의 실행/편집/범위/토글 컨트롤·탭/메뉴/본문 관계·스크롤·가상 항목을 제공합니다. 기능별로 검증 범위를 추적하며 Fluent 전체 상태·텍스트 패턴·지역화 발화·런타임 비교는 남아 있습니다.

## 공통 계약

`UiContext.SetAccessibilityTree(tree)`로 트리를 연결한다. 앱 세션은 `IPlatformAccessibilityBackend`가 제공하는 트리를 연결하고 Windows는 자동 제공한다. 이 트리는 플랫폼 소유다. 별도로 소유한 트리는 컨텍스트에서 분리하고 생산자를 중지한 뒤 해제한다.

단일 렌더 생산자가 컨트롤을 준비한 뒤 완료 프레임을 발행한다. 공급자 스레드는 읽기 잠금으로 발행 상태를 조회한다. 변경 없는 항목은 저장소와 리비전을 유지한다. 제거된 ID는 사라지고 재생성하면 새 런타임 ID를 받는다. 렌더 실패 시 이전 발행 상태를 보존한다. 논리 렌더 완료 시점이며 GPU 표시 완료의 증거는 아니다.

최대1,024개 동작 요청을 큐에 넣고 기존 프레임 스케줄러를 깨운다. 렌더 생산자는 기존 컨트롤·바인딩·콜백 경로로 Focus·Invoke·SetValue를 처리한다. 같은 요소의 요청 순서를 보존한다. 생산자 자신의 동기 요청은 Busy, 다른 스레드의 동기 요청 제한 시간은2초다. Invoke는 비동기로 예약한다. 비활성·클립·오래된 요소·읽기 전용·잘못된 한 줄 값은 명시적으로 거부한다. SetValue는 모든 제어 문자를 거부한다. 해제 시 남은 요청을 완료하고 급증한 저장소를 회수한다.

## 이름·자식·지원 기능

`UiWidget.AccessibleName`으로 아이콘 전용 버튼 등의 이름을 지정한다. 지정하지 않은 Field 주 컨트롤은 Field 라벨을 사용하고 일반 컨트롤은 표시 라벨을 사용한다. `##` 식별자를 읽기 이름으로 만들지 않는다. Field 필수·오류·힌트·검증 정보는 주 컨트롤에만 적용한다. Input 장식 위젯은 독립 이름을 가진 실제 의미 정보 자식이다. 발행 영역은 클립한 논리 영역이고 Windows 공급자가 실제 화면 좌표로 변환한다.

`IUiButtonBehavior.AccessibilityFocus`와 입력 동작의 대응 속성은 선택된 실제 동작의 지원 여부를 나타낸다. 사용자 버튼 동작은 공통 탐색 경로를 소비할 때 포커스를 지원한다고 선언한다. 구현 선택은 정적이며 런타임 테마 레지스트리가 필요하지 않다.

AccessibleName은 기존 지연 정체성 저장소를 사용한다. 이름이 없고 아직 렌더하지 않은 Parent 팩터리 객체 크기는 보존한다. 범위 정체성 캐시에 선택적 이름 참조가 추가되며 의미 정보 트리·공급자도 저장 공간을 사용한다. 앱 전체 메모리·FPS 향상은 주장하지 않는다.

## Windows 연결·검증

Windows는 `UiaRootObjectId`의 `WM_GETOBJECT`에 응답한다. `GeneratedComInterface`·`GeneratedComClass`·`LibraryImport`가 NativeAOT 호환 네이티브 연결을 생성한다. 런타임 COM 리플렉션 레지스트리를 사용하지 않는다. ABI 입력은 `artifacts/uia-foundation-2026-11-06/source-manifest.json`에 고정했다.

Button은 Invoke, Input은 Value·읽기 전용·선택된 포커스 기능을 제공한다. Field 정보는 네이티브 UIA 속성으로 조회할 수 있다. 루트는 프래그먼트 탐색과 자손 위치 검색을 제공한다. 해제 중에는 루트를 다시 반환하지 않고 남은 자식 공급자를 처리한 뒤 의미 정보가 유효한 상태에서 등록된 루트를 분리하고 트리를 해제한다. 이미 사용할 수 없는 연결의 HRESULT는 해당 종료 상태로 기록하며 다른 분리 오류는 드러낸다. 접근성 이벤트와 장시간 전체 수명 검증은 미완료다.

기존 검사기를 사용한다.

```powershell
dotnet run --project tests/Duxel.Graphics.Tests -c Release -- artifacts/uia-local accessibility-only
dotnet run --project tests/Duxel.Graphics.Tests -c Release -- artifacts/uia-client uia-client-pid <소유한-검사-프로세스-ID>
./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -ExtraArgs @('--input')
```

클라이언트 모드는 Input 갤러리용이다. 실제 Windows UIA 컨트롤을 조회하고 공유 값 변경·읽기 전용 거부·Clear 실행을 검사한다. `uia-field-client-pid`는 Field 갤러리의 필수·오류·HelpText, 공유 검증 변화, 정체성 보존과 물리 화면 좌표를 검사한다. 정확히 소유한 프로세스 안에서 Duxel 창 클래스를 찾아 IME/도움 창을 선택하지 않는다. 데스크톱 이미지를 읽거나 시각적 일치를 인증하지 않는다. .NET10 관리 실행과 고정 .NET11 RC3 NativeAOT 결과, 초기 실패, 실행 파일 해시는 산출물 디렉터리에 각각 기록한다.

## 제거된 공급자의 수명

`UiAccessibilityTree.ElementsRemoved`는 제거 발행을 완료한 뒤 트리 잠금 밖에서 실행한다. Windows는 이후 클라이언트 조회가 없어도 오래된 약한 맵 키를 즉시 제거하고 급증했던 용량을 줄인다. 이 검사는 제거 프레임에서만 실행한다. 등록된 제거 공급자는 회수 큐에 넣고 전용 Windows 메시지로 한 번에 최대32개를 처리한다. COM 분리는 캐시/발행 잠금 밖에서 렌더 생산자와 분리해 처리한다. 종료 시 남은 자식을 처리한 뒤 루트를 정리한다. 미등록 ABI 전용 공급자에는 분리할 UIA 연결이 없다.

집중 캐시 검사는 실제 공급자4,096개를 보유한 상태에서4,095개를 제거하고 생존 항목1개·용량256이하를 강제 GC 없이 확인한다. 동시 독자4개로 발행1,000회와 해제에서 완전한 항목을 확인한다. 별도 관리/NativeAOT 호스트·클라이언트 프로세스가 실제 위젯을 제거·재생성하며 이전 클라이언트 인터페이스를 보유한다. 이전 요소는 사용 불가이고 새 요소는 다른 런타임 정체성을 가진다. 두 실행 모두 이미 사용 불가인 네이티브 분리 결과1개·회수 대기0개를 기록했다. S_OK 성공 결과나 장시간 프로세스 메모리 실측으로 해석하지 않는다. 네이티브 UIA 이벤트·화면 읽기 프로그램 알림·앱 전체 비용 검증은 남아 있다.

공식 자료: [서버 공급자](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-serversideprovider), [COM 소스 생성](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/comwrappers-source-generation), [공급자 분리](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcoreapi/nf-uiautomationcoreapi-uiadisconnectprovider).

최종 결과: .NET10 전체3,024/집중34, 고정 .NET11 RC3 NativeAOT 집중34, 외부 NativeAOT 클라이언트4 및 대상 정상 종료0. 최종 net8/9/10 솔루션 빌드는 경고·오류가 없다. `artifacts/uia-foundation-2026-11-06/verification.json`에 기록했다. 기존 팩터리 할당 검사가 초기 기본 필드 회귀를 검출했고 수정 후 다시 통과했다. 무변경 노드 할당0은 집중 검사이며 앱 성능의3회 교대 측정 주장이 아니다.

캡처한 이벤트 그래프의 Toggle 조상은 불변 상태 조회만 제공하며 원래 트리 해제 후 동작은 거부합니다. 네이티브 호스트에서 해제 후 실제 COM vtable로 혼합2를 읽고, 응답 대기 없는 일반 TooltipClosed 종료 회귀도 통과했습니다. 다른 동결 패턴·지역화 발화·전체 프로세스 메모리 검증은 별도 게이트로 유지합니다.

# 표시된 Tooltip 노드와 이벤트

Last synced: 2026-11-19

표시 중인 접근 가능한 `UiTooltip`은 실제 주 대상 아래에 `UiAccessibilityRole.Tooltip` 자식을 게시한다. 이름은 해당 프레임의 툴팁 텍스트이며 ID는 대상 관계의 논리 `SourceId`다. 숨은 설명은 표면 노드 없이 대상 HelpText로 유지한다. `Inaccessible`은 표면 노드와 이벤트를 게시하지 않는다.

Windows에서는 컨트롤 타입50022, 포커스 불가능한 일반 텍스트 컨트롤 뷰 요소로 노출한다. 콘텐츠 뷰·Invoke·Value 패턴에는 포함하지 않는다. 뷰와 이름 규칙은 [공식 ToolTip 계약](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supporttooltipcontroltype)을 따른다. [대상 접근성](tooltip-accessibility.ko.md)을 확장하며 `Fluent.Tooltip` 팩터리를 추가하지 않는다.

`UiAccessibilityTree.AutomationEvent`는 `TooltipOpened`·`TooltipClosed`를 전달한다. 프레임을 검증한 뒤, 닫힘 관측자는 제거 전에 실행하고 열림 관측자는 게시 후 실행한다. 두 경우 모두 게시 잠금을 잡지 않는다. 관측자는 조회와 동작 예약을 할 수 있으며 재귀 게시·폐기는 거부한다. 표시가 유지되는 프레임은 재열림을 발생시키지 않고 중단한 프레임은 가시성 전이를 내보내지 않는다. 재열림 시 논리 ID는 유지하고 런타임 식별자는 새로 만든다.

Windows 연결은 [UiaRaiseAutomationEvent](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcoreapi/nf-uiautomationcoreapi-uiaraiseautomationevent)와 열림20000·닫힘20001을 사용한다. UIA는 이벤트 처리기가 끝날 때까지 공급자를 유지한다. 따라서 닫힘 이벤트는 제거 전에 확보한 원본·조상 공급자의 불변 스냅샷을 전달한다. 화면 좌표와 포커스 상태를 저장하고 조각 루트는 네이티브 창 공급자의 COM 참조를 유지한다. 비동기 UIA 캐시는 트리와 HWND가 폐기된 뒤에도 파괴된 창을 호출하지 않고 이 정보를 읽는다. 이벤트 스냅샷은 컨트롤 동작을 제공하지 않으며 실제 공급자 캐시에 넣지 않는다. 일반 제거 공급자는 즉시 사용 불가가 되고 이벤트 정보의 수명은 UIA의 참조를 따른다.

일반 프레임에서 Windows는 Tooltip 알림을 게시 순서대로 모은다. 창에서 기존 UIA 정리 메시지를 처리할 때 제거된 공급자의 연결을 먼저 해제한 뒤 알림을 보낸다. 이렇게 하면 닫힘 알림이 원래 런타임 식별자를 유지하면서도 공급자 해제에 의해 취소되지 않는다. 열림은 같은 식별자의 공급자가 살아 있으면 해당 공급자를 사용하고, 이미 제거되었으면 저장한 속성을 전달한다. 트리를 폐기하면서 내보내는 Core 알림에는 `DuringDisposal`을 표시한다. Windows는 해당 닫힘을 HWND를 파괴하기 전에 보낸다.

ABI 기준은 Microsoft win32metadata 커밋 `15fc870a5e96df7c560877ff30136c0fb5a21e9b`이며 `artifacts/tooltip-events-2026-11-11`에 보존했다. 클라이언트는 소스 생성 COM의 `IUIAutomationEventHandler`를 사용하고 구독 전에 Name·ControlType·포커스 가능 여부·AutomationId를 캐시에 요청한다.

## 검증

관리 검사는 역할·부모·식별자, 뷰, 패턴, 열림/닫힘 순서, 누락, 비노출 모드, 프레임 중단, 화면 밖 전이와 관측자 재진입을 다룬다. 별도 네이티브 UIA 클라이언트가 포커스를 이동해 열림/닫힘을 관측하고 실제 툴팁 자식과 이전 요소의 사용 불가를 확인한다. 이어 대상을 제거해도 두 번째 닫힘의 캐시된 식별자가 전달되는지 검사한다. 전용 호스트는 열림2회·닫힘2회와 남은 소유자가 없음을 확인한다. 실제 HWND·UIA 공급자와 결정적인 Core 입력을 사용하는 검사이며 GPU 외형은 별도로 검증한다.

처음 네이티브 실행에서는 열림은 전달됐지만 원본 제거 후 닫힘이 누락됐다. 로그로 Core 닫힘 전이를 확인했고 이벤트 원본·조상 수명을 유지한 뒤 두 외부 수명 주기가 통과했다. 실패 로그와 최종 근거는 `artifacts/tooltip-events-2026-11-11`에 보존한다.

11월 11일 검증에서는 최소화·복원·숨김·재표시 과정에서 열림, 닫힘, 열림, 닫힘을 한 번씩 받았다. 두 닫힘은 캐시된 Name·ControlType·AutomationId와 `IsOffscreen = true`를 유지했다. 이전 표면 공급자는 조회할 수 없었고 대상 HelpText는 계속 읽을 수 있었다. 근거는 `artifacts/tooltip-uia-host-events-2026-11-11`과 [UIA 속성 식별자](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-automation-element-propids)를 참고한다.

이전 Windows x64 .NET 11.0.0 검증에서 소스 생성 COM 클라이언트는 최소화 시 닫힘 알림을 받았으나 창을 숨길 때는 받지 못했다. 데스크톱 범위를 구독한 별도 관리형 클라이언트는 Duxel의 최소화·복원·숨김 실험에서 일치하는 열림 알림 두 번을 받았고 닫힘은 받지 못했다. 같은 관리형 클라이언트는 Duxel의 `WM_CLOSE`에서 닫힘을 한 번 받았다. WPF 기본 Tooltip을 명시적으로 닫으면 소유 창을 숨기기 전과 후 모두 닫힘 알림을 받았다. 소유 창만 숨겼을 때는 WPF Tooltip이 열린 상태로 남았다. WPF Tooltip은 별도 팝업을 사용한다. 이 비교로 원인을 Duxel의 알림 전달 경로로 좁혔고, 이후 아래의 공급자 해제 순서를 수정했다. 결과는 `W:/Cache/Duxel/gallery-source-20261117/{tooltip-uia-scope-2049,tooltip-wpf-reference-2068,tooltip-shared-client-2071}`에 있다.

`UiAccessibilityTree.TextPropertyChanged`는 변경된 요소를 조회할 수 있게 게시한 뒤 이전 텍스트와 새 텍스트를 전달한다. 계속 열린 Tooltip의 텍스트가 바뀌면 표면의 `Name`이 변경된다. 대상 설명이 바뀌면 Tooltip을 숨긴 상태에서도 대상의 `HelpText`가 변경된다. Tooltip 레이블은 대상의 실제 `Name`에 반영되며 명시적 접근성 이름이 우선한다. 실제 값이 그대로면 알림을 보내지 않는다. Windows는 속성 ID와 BSTR 값으로 [UiaRaiseAutomationPropertyChangedEvent](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcoreapi/nf-uiautomationcoreapi-uiaraiseautomationpropertychangedevent)를 호출한다.

별도 UIA 클라이언트가 두 새 값과 각 원본의 AutomationId를 수신하고 기존 공급자에서도 같은 텍스트를 읽는 것을 확인했다. 관리 검사는 이전 값, 게시 순서, 숨은 설명과 레이블 우선순위를 검증한다. 근거는 `artifacts/tooltip-uia-text-properties-2026-11-11`에 있다.

Windows에서 `WM_CLOSE`는 `ShouldClose`로 종료를 요청한다. 호스트는 프레임 생성을 멈추고 백엔드를 폐기한다. 트리는 대상의 조상 정보를 아직 읽을 수 있을 때 표시 중인 표면마다 관리 `TooltipClosed`를 한 번 알린다. 백엔드는 HWND를 파괴하기 전에 실제 UIA 공급자 연결을 해제하고, `WM_DESTROY`에서 창과 공급자의 연결을 정리한다. 백엔드 루프를 직접 실행하는 호출자는 프레임 생성을 멈춘 뒤 백엔드를 폐기한다.

창을 닫으면 일반 공급자는 사용할 수 없게 된다. 별도 클라이언트는 마지막 `TooltipClosed`를 받으며 캐시된 Name·ControlType·AutomationId는 원본과 같고 `IsOffscreen = true`다. 제품 종료 경로는 클라이언트 신호를 기다리지 않는다. 초기 대조 실행과 로그는 `artifacts/tooltip-shutdown-pump-2026-11-11`에 있다.

동일 시점에 빌드한 Windows x64 .NET 11.0.0 관리형과 NativeAOT에서 `WM_CLOSE` 검사가 통과했다. 더 최근 관리형 빌드에서도 통과했다. 세 실행 모두 외부 닫힘 이벤트 한 번, 캐시된 식별자와 화면 밖 상태, 일반 공급자의 사용 불가, 호스트와 클라이언트의 정상 종료를 확인했다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/tooltip-runtime-1949/verification.json`에 있다. 화면 읽기 프로그램 동작, 다중 주 대상·그룹, 브라우저 포커스·문서 가시성 대조와 성능은 별도로 검증한다.

11월 19일 Windows x64 .NET 11.0.0 관리형 빌드에서 포커스 이동, 대상 제거, 명시적 닫기, 실제 앱 세션의 닫기를 별도 클라이언트로 확인했다. 한 창에서 두 번 여닫아도 닫힘 두 번을 받았고 이전 공급자는 조회할 수 없었다. 최소화·복원·숨김 검사에서는 열림과 닫힘을 두 번씩 받았다. 즉시 `WM_CLOSE`에서도 닫힘을 한 번 받았고 두 프로세스가 정상 종료했다. 수정 전에는 창 안의 세 동작에서 Core 닫힘이 발생했지만 외부 클라이언트가 받지 못했다. 검증용으로 공급자 해제를 미루자 닫힘이 전달되어 원인을 좁혔다. 제품에서는 제거된 공급자를 해제한 뒤 저장한 알림을 보낸다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/tooltip-actions-2085`에 있다. 이번 수정은 Windows x64 관리형으로 확인했다. 현재 NativeAOT 빌드와 실제 화면 읽기 프로그램은 별도 검증이 필요하다.

## Linux AT-SPI

Linux 갤러리에서는 Tooltip을 숨겨도 대상의 `Description`으로 설명을 읽을 수 있고 `has-tooltip` 상태도 유지된다. 레이블 모드의 문구는 대상의 이름에 들어가며 설명에 반복되지 않는다. Tooltip을 열면 포커스를 받지 않는 ToolTip 자식이 `showing` 상태로 나타난다. AT-SPI는 대상에 자식 추가와 제거를 알린다. 닫으면 이전 Tooltip 객체는 폐기 상태가 되고 대상의 설명은 남는다. 다시 열면 새 객체가 생긴다. [AT-SPI 역할](https://gnome.pages.gitlab.gnome.org/at-spi2-core/libatspi/enum.Role.html)과 [상태](https://gnome.pages.gitlab.gnome.org/at-spi2-core/libatspi/enum.StateType.html)

갤러리의 **알림과 진행 상태 → Tooltip**에서 **Tooltip 표시 전환**으로 확인할 수 있다. 외부 검사기는 AT-SPI로 같은 버튼을 눌러 두 번 여닫는 과정과 실제 객체 트리를 확인한다. 갤러리와 같은 Linux 데스크톱 세션에서 `dotnet <Duxel.Linux.App.Tests.dll> atspi-gallery-tooltip <gallery-pid> <new-result.json>`을 실행하면 검사기가 해당 페이지를 직접 연다.

SDK 11.0.100과 런타임 11.0.0으로 새로 만든 Linux x64 관리형 및 NativeAOT 갤러리가 WSLg에서 각각 검사 4건을 통과했다. 갤러리와 외부 클라이언트는 모두 정상 종료했으며 표준 오류 출력은 비어 있었다. 빌드 로그, 실행 파일 해시, 두 검사 결과는 `W:/Cache/Duxel/gallery-source-20261117/tooltip-atspi-2086`에 있다. 실제 화면 읽기 프로그램 동작은 별도 검증이 필요하다.

키보드 검사 명령은 `dotnet <Duxel.Linux.App.Tests.dll> x11-gallery-tooltip <gallery-pid> <new-result.json> <decimal-x11-window-id>`이다. AT-SPI로 자동 설명 대상에 포커스를 준 뒤 검사 창으로 Escape, Tab, Shift+Tab 입력을 보낸다. Escape로 닫아도 포커스와 대상 설명은 남고, 관찰한 750ms 동안 다시 열리지 않았다. Tab으로 다른 곳에 갔다 돌아오면 새 ToolTip 객체가 생겼다. 해시를 확인한 동일한 .NET 11.0.0 관리형과 NativeAOT 갤러리가 WSLg에서 각각 검사 6건을 통과했고, 네 프로세스가 모두 정상 종료했다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/tooltip-x11-2087`에 있다.

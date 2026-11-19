# Toast 알림

현재는 UiToastController와 UiToasterWidget으로 실제 비모달 알림 표면을 표시합니다. 유한 등장·퇴장·재배치 모션, 자동 닫힘·호버/초점 정지, F6 왕복·실행·닫기·Delete/Escape와 알림 이벤트를 연결했습니다. 독립 구성·진행 막대/스피너와 네이티브 창 활성 상태 전달도 연결했습니다. 추가 수용 검증은 남아 있습니다. Fluent.Toast 팩터리와 화면 가장자리 호스트용 Fluent.Toaster를 연결했습니다. 공식 목록 55개 중 50개를 연결했고 5개가 남았습니다. 현재 NEW 배지는 Select에만 표시하며 미구현 배지는 제거했습니다.

## 확인한 공식 기준

[Fluent 사용 안내](https://fluent2.microsoft.design/components/web/react/core/toast/usage)는 작업 결과를 잠시 알리는 표면, 기본7초, 호버 시 정지, 최대4개와16px 간격을 안내합니다. [고정한 Microsoft 구현](https://github.com/microsoft/fluentui/blob/8ee8138daffeb7dbb95aaa7418d20472cc2e6fd1/packages/react-components/react-toast/library/src/state/vanilla/createToaster.ts)의 기본 타이머는3초로 서로 다릅니다. Duxel은 디자인 안내의7초를 기본으로 정했습니다. 타이머는 등장 모션이 끝난 뒤 시작하고 퇴장 완료까지 콘텐츠 소유권을 유지합니다.

## 실제 표면 사용

```csharp
var controller = new UiToastController();
using var toaster = Fluent.Toaster(controller);
controller.Dispatch(new("저장했습니다", "문서가 준비되었습니다", UiToastIntent.Success));
// Render after page content, once each frame. The portal reserves no layout space.
toaster.Render(ui);
```

UiToasterOptions는 여섯 위치, 기본292px 너비·16px 바깥 여유/간격, 논리 시작/끝의 RTL 전환, ReducedMotion과 IsWindowActive를 제공합니다. App 호스트는 네이티브 창 활성 상태를 자동 전달합니다. 사용자 정의 호스트는 UiFrameInfo.IsWindowInactive를 지정합니다. 기본 표면에는 아이콘·굵은 제목·선택적인 본문과 동작을 배치하고, 닫기 버튼은 CanDismiss=true일 때만 표시합니다. CloseLabel과 ActionLabel/Action을 지정할 수 있습니다. 닫을 수 있는 예제는 최근 알림 기록을 별도로 남깁니다.

움직이는 동안에는 투영용 글자를, 정지한 뒤에는 물리 LCD 글자를 사용합니다. 정지한 표면은 GPU 캡처를 재사용하고 자동 닫힘 시각에 한 번 깨웁니다. 페이지가 스크롤되어도 표면은 뷰포트 모서리를 유지합니다. F6으로 동작이 있는 알림에 들어가거나 원래 컨트롤로 돌아옵니다. Escape는 알림들을 닫고 Delete는 초점이 있는 알림을 닫습니다. 외부 화면 읽기 프로그램의 실제 발화는 검증하지 않았습니다.

## 컨트롤러 저수준 계약(진단용)

```csharp
var controller = new UiToastController();
var id = controller.Dispatch(new("저장했습니다", Intent : UiToastIntent.Success));
// Call after the host finishes the actual entry motion.
controller.MarkVisible(id);
controller.SetPaused(id, UiToastPauseReason.Hover, true);
controller.SetPaused(id, UiToastPauseReason.Hover, false);
// The event-driven host wakes at NextTimeout and advances the state.
controller.Advance();
controller.Dismiss(id);
// Call after actual exit completion.
controller.Remove(id);
```

호출은 UI 소유 스레드에서 직렬로 처리합니다. Changed 처리 중 다시 변경하면 명시적으로 거부합니다. 자동 타이머나 백그라운드 폴링은 만들지 않습니다. UiToasterWidget이 NextTimeout을 공통 이벤트 대기 스케줄러에 연결하고 Advance 및 등장·퇴장 완료 신호를 처리합니다. 컨트롤러만 직접 사용할 경우에는 호출자가 이 호스트 역할을 맡아야 합니다.

- 상태는 Queued → Entering → Visible → Exiting이며 Remove가 소유권을 끝냅니다. 기다린 시간은 표시 시간에서 빼지 않습니다.
- 표시 한도는1~4이며 기본4입니다. 보이는 목록은 새 알림부터, 대기는 높은 Priority부터 같은 우선순위 안에서는 입력 순서대로 처리합니다. 닫히는 알림은 퇴장 완료까지 자리를 차지합니다.
- Timeout=null은 명시적인 계속 표시입니다.0 이하 시간은 오류입니다. 내용만 Update하면 남은 시간을 보존하고 restartTimer=true 또는 Timeout 변경이면 새 시간으로 시작합니다.
- Hover·Focus·WindowInactive·Explicit 정지 이유는 독립적입니다. 마지막 정지 이유가 해제될 때 남은 시간부터 계속합니다. 호버·창 비활성 정지는 각각 옵션으로 끌 수 있습니다.
- Visible/Queued는 바뀔 때만 새로 만드는 불변 스냅샷입니다. 아무 일도 없는 Advance는 변경 알림을 보내지 않습니다.
- Info는 Polite, 나머지 intent는 Assertive 우선순위를 제공합니다. UiToasterWidget이 실제 표시와 내용 변경 때 Announce를 전달합니다. 외부 보조공학의 실제 발화 검증은 남아 있습니다.

## 갤러리와 다음 단계

`.\run-gallery.ps1` → **Fluent 컨트롤 → 알림과 진행 상태 → Toast**로 이동하세요. 위쪽의 실제 표면 예제에서 알림을 표시하고, 아래 진단 예제에서는 시간을 수동으로 진행합니다. 각각 바로 아래에 현재 설정의 소스를 표시합니다.

[실제 표면 검증](../artifacts/fluent-toast-surface-2026-11-18/result.json), [수명 기반 검증](../artifacts/fluent-toast-lifecycle-2026-11-18/result.json). 중첩 모달의 포인터·Escape·닫기, 긴 내용 배치와 Windows 혼합 언어 동작 라벨은 집중 검사로 확인했습니다. 실제 Linux·NativeAOT·외부 보조공학 검증은 남아 있습니다. 기존 MessageBar/Popover의 이름만 바꿔 완료 처리하지 않습니다.

## 독립 구성과 진행 알림

`UiToastWidget`은 컨트롤러 없이 본문에 배치할 수 있습니다. `Show()`와 `Hide()`로 표시를 바꾸며, 숨기면 배치 공간도 사라집니다. `Width`, `RightToLeft`, `ReducedMotion`, `AnimationPaused`를 지정할 수 있습니다.

```csharp
using var toast = Fluent.Toast(new("파일 전송 중", "완료 전까지 표시합니다")
{
    Timeout = null,
    Progress = new(.35f, "35% 완료"),
    CanDismiss = true,
    CloseLabel = "알림 닫기"
});
```

`Progress.Value`가 null이면 스피너를, 0~1이면 진행 막대를 표시합니다. 진행 알림은 `Timeout = null`을 명시해야 합니다. 숫자만 갱신하면 같은 알림을 반복 낭독하지 않습니다. 완료 시 `Progress`를 제거하고 제목과 본문을 변경하세요. `DisableAnnouncement`는 화면에 상시 배치한 예제처럼 자동 안내가 필요 없는 경우에 사용합니다. 갤러리는 실제 알림, 독립 구성, 진행 구성, 수명 진단의 네 예제를 각각의 소스와 함께 제공합니다.

## 창 활성 상태

Windows의 `WM_ACTIVATE`와 Linux X11의 FocusIn/FocusOut 상태를 `InputSnapshot.IsWindowInactive`에 담습니다. App은 활성 상태가 달라지면 대기 중인 렌더 루프를 깨우고 `UiFrameInfo.IsWindowInactive`로 전달합니다. 사용자 정의 호스트도 이 필드를 전달할 수 있으며, 생략하면 기존처럼 활성 상태로 취급합니다. 현재 상태는 `ui.IsHostWindowActive`로 확인합니다. `UiToasterOptions.IsWindowActive = false`는 별도의 수동 정지로 계속 사용할 수 있고, true로 설정해도 비활성 상태인 네이티브 창을 강제로 활성 처리하지 않습니다. 비활성 동안에는 자동 닫힘과 진행 스피너를 정지하고 다시 활성화되면 남은 시간부터 이어갑니다.

Windows 활성화 판정은 [WM_ACTIVATE 공식 명세](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-activate)의 하위 워드 값을 사용합니다.

## 긴 내용과 작은 화면

호스트는 화면 높이와 현재 글자 배율에 맞춰 표시 개수를 줄입니다. 공간이 부족한 알림은 대기열에 보관하고, 다시 표시할 때 대기 전 남은 시간부터 이어갑니다. 초점이 있는 알림은 가능한 한 화면에 남깁니다. 닫히는 알림의 자리는 기존 퇴장 모션이 끝날 때까지 유지합니다.

알림 하나의 내용이 할당된 높이를 넘으면 기존 ScrollView로 제목·본문·진행 상태·동작 영역을 스크롤합니다. 닫기 버튼과 상태 아이콘은 고정합니다. 화면 밖 동작은 키보드 또는 접근성 ScrollIntoView로 노출할 수 있습니다. 갤러리의 **긴 알림 표시** 버튼으로 확인하세요. RTL은 아이콘·닫기 버튼·동작의 논리 시작 위치와 제목·본문·진행 문구의 문단 방향 및 줄 정렬을 함께 전환합니다. 동작 버튼 라벨도 공통 네이티브 문단 조판을 사용하며 혼합 언어 순서와 실제 실행을 확인했습니다. 실제 Linux 수용 검증은 남아 있습니다.

## 컨트롤러와 호스트의 소유 관계

컨트롤러 하나에는 살아 있는 `UiToasterWidget` 하나만 연결할 수 있습니다. 중복 연결은 알림이나 대기열을 변경하기 전에 `InvalidOperationException`으로 거부합니다. 여러 창이나 독립 표면에는 각각의 컨트롤러를 사용하세요.

호스트를 `Dispose()`한 뒤 같은 컨트롤러로 새 호스트를 만들 수 있습니다. 컨트롤러는 호출자 소유이며 호스트 해제가 알림을 삭제하지는 않습니다. 화면 크기 때문에 줄어든 표시 한도도 원래 한도로 복구합니다.

렌더링 중이 아니라면 `Changed` 콜백 안에서 호스트를 정리할 수 있습니다. 표시 한도 복구와 그에 따른 알림은 현재 콜백 처리가 끝난 뒤 실행하므로 콜백이 재귀 호출되지 않습니다. 콜백 안에서 새 호스트를 연결하거나 컨트롤러를 다시 변경하는 것은 허용하지 않습니다. 외부 콜백이 예외를 발생시키면 오류를 그대로 전달하면서 호스트가 가진 표면 자원은 정리합니다.

## 문단 방향과 좁은 너비

제목·본문·진행 문구·동작 라벨은 공통 InlineText와 네이티브 문단 조판을 사용합니다. 원문 순서를 뒤집지 않고 문단의 기준 방향을 전달하며, RTL에서는 각 줄을 오른쪽에 정렬합니다. 제목·본문의 14/20 크기와 진행 문구의 12/16 크기는 유지합니다. 진행 막대도 RTL에서 오른쪽부터 채워집니다.

너비가 160px보다 작으면 본문을 아이콘·닫기 버튼 아래로 옮겨 텍스트 열의 폭을 확보합니다. 완전한 헤더에 필요한 76px조차 확보할 수 없으면 호스트는 알림을 대기열에 보관하고, 공간이 생기면 다시 표시합니다. 알림을 버리거나 아이콘과 닫기 버튼을 겹치지 않습니다. 갤러리의 **혼합 언어 알림**, **알림 방향 전환**, **알림 너비 전환**으로 확인하세요. 바로 아래 소스에도 현재 너비와 방향을 표시합니다.

등록 검증: [팩터리·갤러리·예제 컴파일·호스트 빌드](../artifacts/fluent-toast-registration-2026-11-18/result.json). Toaster는 호스트용 보조 함수이며 공식 구성요소 목록에 별도 항목을 추가하지 않습니다.

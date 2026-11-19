# 접근성 포커스 게시

Last synced: 2026-11-11

`UiAccessibilityTree.FocusChanged`는 게시가 끝난 논리 포커스의 획득·이동·상실을 알립니다. `UiAccessibilityFocusChange.Previous`와 `.Current`는 null이 가능한 불변 요소 값입니다. 콜백에서 게시된 트리를 읽거나 동작을 요청할 수 있지만 게시·해제를 재진입할 수는 없습니다. 초기 빈 포커스, 포커스 유지, 위치만 변경, 취소된 프레임은 알리지 않습니다. 대상은 활성 상태이고 포커스를 받을 수 있으며 화면에 있어야 합니다. 직접 트리를 캡처할 때 유효한 대상이 둘이면 게시 전에 거부합니다.

```csharp
tree.FocusChanged += change =>
{
    Console.WriteLine($"Focus : {change.Previous?.Node.Name} -> {change.Current?.Node.Name}");
};
```

Immediate 컨트롤은 앞선 컨트롤을 캡처한 뒤에도 포커스를 바꿀 수 있습니다. 따라서 프레임 끝에서 최종 탐색 대상을 한 번 확정하며 TabList의 부모 탐색 ID를 실제 자식 탭으로 연결합니다. 앞선 컨트롤의 낡은 `Focused` 표시는 지웁니다. 포커스를 받지 않는 TabList 부모가 자식의 포커스를 함께 주장하지 않습니다. 텍스트 입력의 접근성 포커스 요청은 기존 IME 소유권 처리를 유지합니다.

Windows는 논리 상태와 실제 키보드 포커스를 구분합니다. `HasKeyboardFocus`(30008), 프래그먼트 `GetFocus`, `AutomationFocusChanged`(20005)는 전경 스레드의 실제 포커스 HWND를 확인합니다. 배경 창의 논리 이동은 키보드 포커스를 주장하거나 전역 획득 알림을 보내지 않습니다. WM_SETFOCUS·WM_KILLFOCUS는 지연 메시지로 합쳐 처리하며 창으로 돌아오면 논리 상태가 그대로여도 현재 자식의 포커스를 복원해 알립니다. 자식이 사라져 빈 루트로 이동하면 살아 있는 루트를 알리며, 제거된 자식에 속성 이벤트를 만들지 않습니다. 같은 식별자에 대한 반복 알림도 억제합니다.

제품 코드는 전경 활성화를 강제하거나 OS 입력을 주입하지 않으며, 알림 때문에 타이머나 렌더링 요청을 추가하지 않습니다. 공급자 `SetFocus` 전에 호스트 창을 활성화하는 일은 UI Automation이 담당하고, 공급자는 기존 쓰기 스레드 큐로 내부 포커스만 요청합니다. 실제 HWND에 포커스가 있지만 유효한 가상 자식이 없으면 루트 `GetFocus`는 자기 자신을 반환합니다.

근거는 `artifacts/focus-events-2026-11-11/verification.json`입니다. 해당 폴더에서 SDK11.0.100을 roll-forward 없이 고정했으며 런타임은11.0.0입니다. 코어 탭·포커스111, 관찰 샘플39, 접근성49, 스케줄링35, 기존 샘플10, 여러 줄·Tooltip·편집4114 검사가 통과했습니다. 별도 클라이언트는 소유한 네이티브 창 두 개와 실제 Fluent 탭·본문을 사용합니다. 획득, 선택 없는 방향키 이동, 같은 포커스 유지, 다른 창으로 이탈, 배경 논리 이동, 논리 변경 없는 복귀, 본문 포커스와 제거를 검사했습니다. 클라이언트11·호스트3 검사가 통과했고 가상 대상 획득4회와 루트 이동을 수신했습니다. 다른 포커스 가능 컨트롤의 호출이 검사 대상을 바꾸지 않도록 원자적 명령 파일로 단계를 조정합니다. 키 이벤트는 엔진에 직접 전달하며 데스크톱 키보드 입력을 주입하지 않습니다. 이 검사는 GPU 렌더링이나 화면 읽기 음성을 검증하지 않습니다.

초기 실패 실행도 보존했습니다. 클라이언트 COM 위치 오류로 접근 위반이 발생했으며 네이티브 헤더에 따라 등록·해제 위치를39·40으로 수정했습니다. 이후 컨트롤 호출로 단계를 조정하던 검사에서 뒤쪽 컨트롤로 포커스가 이동해도 앞서 캡처한 표시가 남는 제품 결함을 발견했습니다. 프레임 끝의 확정 처리와 별도 양방향 코어 회귀 검사로 고쳤습니다. 전체 Fluent, IME 데스크톱, NativeAOT와 RC3 런타임 이미지 비교의 완료를 주장하지 않으며 이전 GPU 이미지는 별도 근거입니다.

저장소 루트에서 `./run-gallery.ps1`로 관찰 샘플을 실행합니다. 이전 창은 먼저 닫아 현재 빌드를 사용합니다. Tab으로 상단 탭에 진입하고 방향키로 포커스를 옮긴 뒤 Enter·Space로 페이지를 선택합니다. Tab은 선택 본문과 컨트롤로 이어집니다. 다른 창으로 이동했다 돌아오며 복원 동작도 확인할 수 있습니다. 일반 데스크톱 조작과 화면 읽기 음성의 최종 검증은 남아 있습니다.

공식 근거: [공급자 SetFocus](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcore/nf-uiautomationcore-irawelementproviderfragment-setfocus), [GetGUIThreadInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguithreadinfo), [UIAutomationClient.h](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/UIAutomationClient.h).

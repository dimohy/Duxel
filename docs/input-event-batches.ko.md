# 입력 이벤트 묶음

## 입력과 표시 단계

`NewInputBatch`는 기본적으로 입력 이벤트를 순서대로 처리합니다. 중간 이벤트는 `UiRenderPhase.Input`에서, 마지막 이벤트와 일반 `NewFrame`은 `Presentation`에서 처리합니다. 처리하는 단계마다 화면 콜백, 바인딩, 배치 측정, 히트 테스트, 편집, IME, 접근성 로직을 실행합니다. 그리기 빌더와 텍스트 렌더러는 중간 출력을 생략하며 컨텍스트는 최종 `UiDrawData` 하나를 만듭니다. 대기 중인 텍스처·픽셀 명령의 순서와 소유권은 최종 소비자까지 이어집니다. 갱신 범위는 화면에 제출한 결과끼리 비교해 계산합니다.

`UiSliderOptions.CoalescePointerMoves`를 켜면 슬라이더가 왼쪽 마우스 버튼을 잡고 있는 동안 연속으로 들어온 이동을 묶어 마지막 위치에서 처리합니다. 입력 묶음의 첫 단계는 항상 처리하고, 버튼 누름·놓음, 키보드, 휠, 문자 및 별도 전달 작업은 각각 처리합니다. 최신 위치만 필요한 미리보기에 이 옵션을 사용하세요. 갤러리의 SVG 변환 슬라이더에 적용했습니다. `InputProcessingMetrics.RequestedSteps`는 들어온 입력 수, `CompletedSteps`는 실제로 처리한 수를 기록합니다.

읽기 전용 `UiCodeView`를 드래그해 선택할 때도 보이는 코드 안에서 연달아 들어온 이동은 마지막 위치에서 처리합니다. 포인터가 경계를 넘으면 이동을 각각 처리해 자동 스크롤과 선택 시작점을 유지합니다. 변환된 위젯이나 팝업에 가려진 영역에서도 각 이동을 처리합니다. 코드 선택 배경과 줄 번호는 화면을 제출하는 단계에서 그립니다.

UI 스레드가 입력을 가져가기 전에 플랫폼에서 여러 묶음을 보내면 앱은 대기 목록에 입력을 순서대로 붙입니다. UI 스레드에 목록을 넘길 때 대기 슬롯을 비우므로 뒤에 들어온 입력이 이미 넘긴 목록에 섞이지 않습니다. 포인터가 마지막 표시 위치로 돌아와도 중간 이동이 들어오면 이벤트 대기 모드에서 프레임을 처리합니다. [입력 전달 비용 측정](archive/performance/optimization-session-2026-11-19.md#app-input-handoff-during-rapid-pointer-movement)

`ui.RenderPhase`·`ui.IsPresentationPass`와 제어/빌더의 같은 속성으로 공통 단계를 확인합니다. 커스텀 코드는 순수 그리기만 이 값으로 조건부 실행하고 배치·입력 동작은 조건 밖에서 유지합니다. 독립 빌더는 표시 생산자로 동작합니다. 순수 `PaintGroup` 콜백은 최종 내용만 캡처하고 레이어는 비어 있는 중간 캐시를 확정하지 않습니다. 중간 콜백 실패는 부분 입력 개수와 그리기0개로 기록합니다.

옵저버의 FPS는 표시 단계에서만 집계하며 **갱신 관찰 → CPU 구간 측정 → 측정값 확인**에서 입력 단계 수와 그리기 구성 수를 비교할 수 있습니다. Windows App의 순서 있는 네이티브 입력은 이미 이 API를 사용합니다. Linux X11도 입력을 순서대로 보냅니다. 입력 지연은 별도로 측정해야 합니다.

집중 검사는 네이티브/편집 계약·원시 그리기 종류·잘못된 매개변수·보존 레이어·페인트 그룹·중간 이미지 자원을 다룹니다. GPU는 프레임 슬롯1·3에서 순차 전체 그리기와 최종 픽셀을 비교합니다. 기록한 경우의 근거이며 변환된 모든 대화형 위젯이나 실제 IME 서비스 전체 수용 완료는 아닙니다. [검증](../artifacts/goal-audit-2026-11-16/presentation-phase-verification.json).

```powershell
& W:/Cache/Duxel/presentation-phase-20261116/publish-265/Duxel.Graphics.Tests.exe W:/Cache/Duxel/presentation-phase-manual ordered-input-batch-only
```

## 미게시 그리기의 소유권

`UiDrawListBuilder.Build()`는 대기 출력을 호출자에게 이전하고 소비합니다. 반환된 각 소유 그리기 목록은 `ReleasePooled()`로 해제하고 `UiPooledList` 컨테이너는 `Return()`으로 반환하세요. 컨테이너는 `IDisposable`이 아닙니다. 두 번째 Build에는 새로 생성한 출력만 포함됩니다. 내부 표시·지연 목록 결합도 같은 이전 계약을 사용합니다. `CloneOutput()`은 현재 쓰기 청크를 복사하며 이미 소비한 그리기를 다시 내보내지 않습니다.

초기화·메모리 정리·지연 출력 폐기는 활성·비활성 채널의 미게시 소유 버퍼를 반환하고 차용한 보존 버퍼는 유지합니다. 채널 저장소를 비우기 전에 프리미티브 명령의 메타정보를 확정합니다. 호출자에게 이미 이전한 프레임은 빌더 초기화와 컨텍스트 종료 후에도 유효하며 보존 내용은 장면 임대가 보호합니다.

화면 콜백이 실패하면 소유 컨텍스트가 미게시 기본·오버레이·미종료 레이어의 기하를 버리고 레이어 빌더 소유권과 대기 장면 임대를 정리합니다. 예외를 그대로 전달하며 콜백을 재실행하지 않습니다. 이 기하 정리는 대기 텍스처·픽셀 명령을 버리지 않습니다. 다음 프레임에서 같은 레이어 식별자를 다시 사용할 수 있으며 컨텍스트 종료도 같은 소유권 경로를 사용합니다. 이 폐기 경로는 현재 중간 입력 단계에서 사용합니다.

소유권 검사는23개를 추가해 최종 Core168개가 통과했습니다. 최초163개 기준은7개가 실패했고, 이후 세 폐기 경로의 대기 프리미티브 메타정보·채널의 소유/이전 구분·컨텍스트 종료 후 보유 프레임의5개 경계를 추가했습니다. 대기 프레임 자원·래스터 수명 검사와 보존 텍스트/객체 GPU 비교도 통과했습니다. 새 출력 디렉터리로 실행하세요.

```powershell
& W:/Cache/Duxel/input-phase-20261116/publish-254/Duxel.Graphics.Tests.exe W:/Cache/Duxel/unpublished-drawing-manual ordered-input-batch-only
```

[소유권 근거](../artifacts/goal-audit-2026-11-16/unpublished-drawing-verification.json). 수명 정리의 근거이며 입력 전용 렌더링이나 CPU 속도 개선 완료는 아닙니다.

## CPU 처리 구간 측정

`UiContext.InputProfilingEnabled`로 각 `Render`의 시간·할당 측정을 켭니다. `InputProcessingMetrics`는 요청/완료 논리 입력 수, 구성한 그리기 수, 성공 여부, 준비·UI 구성·그리기 마무리·전체 시간과 렌더 스레드 할당 바이트를 제공합니다. 꺼져 있을 때도 개수는 기록하고 시간·할당 값은0입니다. 준비에는 네이티브 전달 소비와 즉시 컨텍스트 초기화가, UI 구성에는 콜백·배치·그리기 생성이 포함됩니다. 배치와 그리기 생성의 개별 시간은 구분하지 않습니다. `NewFrame`·네이티브 수신·GPU·다른 스레드 비용은 제외합니다. 전체 시간에는 계측 자체와 구간 사이 작업이 포함됩니다. 실패한 호출은 완료된 부분 개수를 제공하며 구간 시간은 끝까지 수행된 구간만 포함합니다.

화면에서는 `ui.SetInputProfilingEnabled(enabled)`로 다음 `Render`부터 적용하고, `ui.GetInputProcessingMetrics()`로 직전 종료 호출을 읽습니다. 소유 컨텍스트의 상태만 사용하고 새 프레임을 요청하지 않아 진단 표시가 반복 갱신을 유발하지 않습니다. 콜백·이벤트 순서도 유지합니다. 옵저버 **갱신 관찰**에서 **CPU 구간 측정**을 켠 뒤 **측정값 확인**을 누르세요. 결과 문구는 요청할 때만 바뀌며 측정 기본값은 꺼짐입니다.

관리 진단은 입력1/8/32개와 불변 버튼0/64개, 경우별 워밍업8프레임·측정24프레임을 사용하고 정확한 편집 결과와 콜백 수를 검사합니다. 합성 글리프 아틀라스를 사용하는 Core 전용 부하로 비용 증가를 구분하며 실제 OS 글꼴·네이티브·Linux 지연이나 고정 RC3 NativeAOT 수용 근거가 아닙니다. 독립 진단 프로세스는3회로 제한합니다. [입력 계획](../artifacts/goal-audit-2026-11-16/input-batch-cpu-plan.json).

```powershell
& W:/Cache/Duxel/input-cpu-20261116/publish-247/Duxel.Graphics.Tests.exe W:/Cache/Duxel/input-cpu-manual input-batch-cpu
```

## Windows 포커스 상실과 눌린 키

Windows 입력 백엔드는 전달한 키의 눌림 수명을 폴링의 임시 이벤트와 별도로 관리합니다. `WM_KILLFOCUS`를 받으면 소유한 각 키의 일반 해제 이벤트를 한 번씩 추가하고 소유권을 지웁니다. 이미 쌓인 눌림·반복 순서는 유지하며 이후 눌림은 새 수명입니다. 일반 키 해제도 소유권을 즉시 지웁니다. 포커스 상실이 반복되어도 허위 해제를 만들지 않습니다. IME 포커스 전달은 기존 소유자 계약으로 별도 처리합니다.

순서 있는 App/Core 경로가 이 해제를 소비해 눌림 상태와 엔진 반복 마감을 지웁니다. 네이티브 집계 스냅샷에도 해제가 포함됩니다. 숨김 창 검사는 Engine·Platform 방식에서 폴링 경계·합쳐진 반복·한 묶음의 눌림/상실/눌림·일반 해제20개를 추가합니다. 수정 전373/383개에서 최종383/383개가 통과했습니다. 소유 HWND에 메시지를 전달한 근거이며 실제 키보드·TSF나 전체 포커스 수용 완료를 뜻하지 않습니다. CPU·FPS 개선도 주장하지 않습니다. [근거](../artifacts/goal-audit-2026-11-16/focus-key-lifetime-verification.json).

```powershell
& W:/Cache/Duxel/focus-keys-20261116/publish-243/Duxel.Graphics.Tests.exe W:/Cache/Duxel/focus-keys-manual native-ordered-input-only
```

직접 관찰할 때는 `./run-gallery.ps1`로 **입력·IME**를 열고 방향키를 누른 채 다른 앱으로 전환한 뒤, 그곳에서 키를 놓고 돌아오세요. 새로운 네이티브 눌림 전까지 키가 해제되어 있어야 합니다. 이 수동 경로의 실제 관찰은 아직 필요합니다. 자동 검사는 소유한 숨김 HWND에만 메시지를 전달합니다.

`UiContext.AddKeyEvent`와 `AddInputCharacter`는 다음 `SetInput`/`NewFrame`에 입력을 추가합니다. 키·문자 목록 각각에서 플랫폼 이벤트가 주입 이벤트보다 먼저 옵니다. 재사용 저장소는 용량과 별개로 유효 개수만 노출하므로 작은 묶음이 이전 이벤트를 재전달하지 않습니다. 주입 입력이 없으면 플랫폼 목록을 그대로 사용합니다. 이전 `GetIO()` 목록을 재사용하면 유효한 앞부분을 유지하고 한 번 추가합니다. 목록은 프레임에서 빌려 쓰는 데이터이며 불변 스냅샷이 아닙니다.

Input·Textarea·MarkdownEditor는 수정한 공통 경로를 사용합니다. 입력 묶음 축소, 같은 목록 재사용, 빈 프레임, 키 순서, 실제 바인딩 문자열, 워밍업 후 할당 검사 18개가 통과했습니다. `SetInput` 1,024회 진단의 할당은 병합·키 순회 수정 전 32,768바이트, 수정 후 0바이트였습니다. FPS·전체 프레임 할당·고정 RC3 NativeAOT 성능 결과가 아닙니다. 회귀 검사도 통과했습니다: Input 할당 202개, 여러 줄 편집 4,114개, Observer 316개, 제어된 네이티브 IME 313개, input-kernel GPU 400개. [근거](../artifacts/goal-audit-2026-11-16/input-merge-verification.json).

게시한 검사를 새 출력 디렉터리로 실행합니다.

```powershell
& W:/Cache/Duxel/input-merge-20261116/publish-186/Duxel.Graphics.Tests.exe W:/Cache/Duxel/input-merge-manual queued-input-only
```

## 미제출 그리기의 자원 수명

`Render`·`NewFrame`·`EndFrame`에서 소비자가 가져가지 않은 그리기를 교체하면 텍스처 명령을 다음 그리기로 순서대로 넘깁니다. 생성 명령은 후속 갱신·소멸·재생성보다 먼저 유지됩니다. 픽셀 저장소는 최종 소비자의 프레임 해제, 명시적 렌더러 정리 또는 컨텍스트 해제 때까지 소유됩니다. `GetDrawData`로 소비자에게 넘긴 프레임의 명령은 컨텍스트가 재전달하지 않습니다.

집중 검사 17개가 통과했습니다. 원래 13개 기준 검사에서는 필수 텍스처 생성이 사라져 8개가 실패했습니다. 재생성 순서, 두 픽셀 소유권, 명시적 정리, 실제 이미지가 있는 기준 화면을 확인하는 4개 검사를 추가했습니다. Image 프레임을 교체한 뒤의 320×240 GPU 픽셀은 직접 렌더링한 기준과 바이트 단위로 같으며 SHA256을 기록했습니다. 확인한 자원 경로의 근거이며 전체 혼합 입력 순서나 누적 갱신 범위의 정확성 검증은 아닙니다. [근거](../artifacts/goal-audit-2026-11-16/pending-frame-resource-verification.json).

```powershell
& W:/Cache/Duxel/input-order-20261116/publish-191/Duxel.Graphics.Tests.exe W:/Cache/Duxel/pending-frame-manual pending-frame-resources-only
```

## Core의 순서 있는 입력 소비

`UiContext.NewInputBatch(frameInfo, inputs, clipRect, textSettings)`는 비어 있지 않은 순서 있는 `UiInputState` 목록을 받습니다. `Render(screen)`을 한 번 호출한 뒤 최종 `GetDrawData()`만 가져가 제출합니다. 목록과 내부 키·문자 목록은 렌더가 끝날 때까지 변경하지 않습니다. 일반 `NewFrame`은 대기 중인 배치를 대체합니다. 빈 배치는 호스트 시간을 진행하기 전에 실패합니다.

각 상태는 해당 시점의 포인터·버튼·수정 키 상태를 갖습니다. 모든 단계에서 화면 순회·배치·실제 동작을 수행하며 최종 표시만 출력 기하와 그리기 데이터를 생성합니다. 호스트 시간은 한 번만 진행하고 대기 자원 순서를 유지하며 최종 표시끼리 갱신 이력을 비교합니다. 콜백 실패를 자동 재실행하지 않습니다. 임의의 화면 코드나 배치 작업 전체를 생략하는 기능은 아닙니다.

```csharp
context.NewInputBatch(frameInfo, [textStep, pointerStep], clipRect, textSettings);
context.Render(screen);
using var drawData = context.GetDrawData();
renderer.RenderDrawData(drawData);
```

실행 검사는 71개가 통과했습니다. Core 63개는 실제 편집기 세 종류, 두 탐색 순서, 편집기 간 포커스, 같은 편집기의 커서 이동, Ctrl+A·문자 순서, 호스트 시간, 버튼 연속 클릭, 빈 입력, 콜백 실패를 다룹니다. GPU 8개는 프레임 슬롯 1·3에서 네 가지 문자열·픽셀 대조를 확인합니다. 재사용을 허용한 후보의 최종 GPU 읽기 한 번은 순차적으로 전체 갱신한 기준 픽셀과 같습니다. 실제 GPU 재사용 발생이나 최소 갱신 영역을 판정하는 검사는 아닙니다. [근거](../artifacts/goal-audit-2026-11-16/ordered-input-core-verification.json).

```powershell
& W:/Cache/Duxel/ordered-input-20261116/publish-197/Duxel.Graphics.Tests.exe W:/Cache/Duxel/ordered-input-manual ordered-input-batch-only
```

App은 순서 입력 생산자에 이 API를 사용합니다. Windows App 파사드와 Windows Observer는 네이티브 순서 수집을 활성화했습니다. 저수준 Windows 백엔드는 기존 집계 호스트를 위해 명시적 호환 방식을 기본으로 유지합니다. Linux X11은 입력을 순서대로 전달합니다. 소유한 입력의 제거는 `ReleaseInput`으로 IME 대상을 해제하며 즉시 모드 id를 직접 관리하는 호스트는 제거 시 이를 호출해야 합니다. 키보드 포커스·수명 수용과 입력 전용 CPU 최적화는 남았습니다. 집계 과정에서 이미 잃은 순서는 복원할 수 없습니다.

Linux X11 백엔드는 입력 상태가 바뀌는 이벤트를 순서대로 전달합니다. 각 단계에는 해당 이벤트의 키 변화, 확정 문자, 버튼 누름·뗌, 휠 이동량과 수집 시점 배율이 들어갑니다. X11 화면이 있는 환경에서 `Duxel.Linux.App.Tests x11-ordered-input`을 실행하면 됩니다. 검증기는 자신이 만든 숨김 창에만 이벤트를 보냅니다.

Linux 갤러리를 실행한 뒤 외부 검증기의 `x11-gallery-search-input <갤러리 PID> <새 결과 JSON 경로> <X11 창 ID>`로 SearchBox의 연속 키 입력을 확인할 수 있습니다. AT-SPI로 텍스트와 커서 위치를 읽고 다른 메뉴에 다녀온 뒤에도 값이 유지되는지 검사합니다. 검증용 갤러리 창의 X11 ID는 10진수로 지정합니다.

같은 인자로 `x11-gallery-pixels`를 실행하면 SearchBox, 개요, RadioGroup의 1280 × 900 RGB 화면을 각각 두 번 저장합니다. 갤러리 창의 클라이언트 영역 전체가 X11 화면 안에 있어야 합니다. 빌드마다 두 캡처를 먼저 비교하면 캡처 사이에 달라지는 픽셀을 확인할 수 있습니다.

## App 스냅샷 전달

`InputSnapshot.OrderedInputs`는 네이티브 클라이언트 픽셀 단위의 완전한 입력 상태입니다. null은 기존 집계 방식, 빈 목록은 이벤트가 없는 순서 수집 방식, 비어 있지 않은 목록은 순서 패킷입니다. App 세션 중 수집 방식은 고정합니다. 빈 순서 패킷에 집계 이벤트를 실으면 모순된 입력으로 처리해 전달 전에 실패합니다. 헤더의 포인터·버튼·수정 키 값은 현재 네이티브 상태를 계속 표현합니다.

App은 생산자가 목록을 재사용하기 전에 헤더 이벤트, 순서 상태, 각 상태의 내부 이벤트 목록을 복사해 소유합니다. 대기 패킷을 수집 순서로 붙이고, 작업 스레드가 가진 프레임을 변경하지 않고 소비한 이벤트를 비우며, 모든 포인터를 콘텐츠 배율로 변환하고 비어 있지 않은 패킷에 `NewInputBatch`를 호출합니다. 순서 방식의 유휴 입력은 일반 유휴 프레임 경로를 사용합니다. 글리프 수집은 기존 아틀라스 정책으로 권위 있는 순서 상태를 읽습니다.

현재 집중 검사 33개가 통과했습니다. 스냅샷·계약 17개와 실제 `DuxelAppSession.RunCore` 네 세션의 16개 검사입니다. 배율 1·2와 두 문자·클릭 순서에서 주입 생산자가 실제 작업 스레드·위젯·Vulkan을 구동합니다. 마지막 그리기 콜백 이후 성공한 QueuePresent 수치가 증가했는지 확인합니다. 세션은 Win32 메시지 스레드를 각각 소유하고 창을 표시하거나 전역 입력을 보내지 않습니다. 합성 수집을 이용한 실제 App 경로 근거이며 실제 키보드·IME·픽셀 수용 검사는 아닙니다. [현재 근거](../artifacts/goal-audit-2026-11-16/native-ordered-input-verification.json), [초기 App 근거](../artifacts/goal-audit-2026-11-16/app-ordered-input-verification.json).

```powershell
& W:/Cache/Duxel/native-ordered-20261116/publish-212/Duxel.Graphics.Tests.exe W:/Cache/Duxel/app-ordered-manual app-ordered-input-only
```

## 반복 주체 선택

`UiKeyRepeatSettings.Source`로 키를 길게 눌렀을 때 반복을 누가 만드는지 정합니다. `UiKeyRepeatSource.Engine`은 물리적 누름·해제만 전달하는 호스트에서 기존 타이머 반복을 유지하며, 인자 두 개의 기존 설정 생성자가 사용하는 기본값입니다. `Platform`은 전달된 키 이벤트를 소비하고 엔진 키 타이머를 추가하지 않습니다. Windows 시스템 설정은 `Platform`을 선택합니다. 두 방식 모두 묶인 반복을 포함한 실제 전달 이벤트는 소비하며, 이 선택은 입력을 걸러내지 않고 추가 합성을 제어합니다.

현재 선택은 `UiState.KeyRepeatSource`로 확인합니다. 입력 설정에서 주체를 바꾸면 이전 키 반복 마감을 폐기하고, 플랫폼이 담당할 때 `SetRepeatTime`으로 경쟁하는 엔진 타이머를 만들면 오류로 알립니다. 호스트 시간을 늘리기 전에 입력 묶음 전체의 선택값을 검사합니다. 엔진 방식의 `IsKeyPressed(repeat: true)`도 반복 개수 조회·편집기처럼 첫 누름에서 초기 지연을 시작합니다. 마우스로 누른 스핀 버튼의 반복과 애니메이션 시계는 각자의 담당 주체를 유지합니다.

```csharp
var native = new UiKeyRepeatSettings(.25, .04) { Source = UiKeyRepeatSource.Platform };
var manual = new UiKeyRepeatSettings(.25, .04); // Engine owns additional repetition.
```

Core 검사는 88개에서 129개로 늘었습니다. 소비 경로를 연결하기 전에는 129개 중 19개가 실패했습니다. 플랫폼 입력에 유휴 커서 이동을 추가하고, 질의 타이머 담당이 잘못됐으며, 전환 뒤 이전 마감이 남거나 잘못된 선택을 받아들이는 문제였습니다. Input·Textarea·Markdown, 두 주체, 질의·전환·키 해제·Windows 기본값을 포함해 최종 129개가 통과했습니다. 검사한 반복 정책의 근거이며 전체 키보드 포커스·수명 수용이나 일반 입력 전용 CPU 처리 완료를 뜻하지 않습니다. Linux의 네이티브 선택·실행 검증은 별도로 남았습니다. [근거](../artifacts/goal-audit-2026-11-16/key-repeat-source-verification.json).

```powershell
& W:/Cache/Duxel/repeat-source-20261116/publish-236/Duxel.Graphics.Tests.exe W:/Cache/Duxel/repeat-source-manual ordered-input-batch-only
```

## 네이티브 반복 횟수

Windows는 `WM_KEYDOWN`·`WM_SYSKEYDOWN`·`WM_CHAR`의 하위 WORD 반복 횟수를 보존합니다. 키 누름 레코드의 `UiKeyEvent.IsRepeat`는 이미 눌린 키를 나타내는 30번 비트와 같은 메시지의 추가 반복을 구분합니다. `IsKeyPressed(key, repeat: false)`와 반복하지 않는 단축키는 이 레코드를 제외하고, `GetKeyPressedAmount`는 모든 반복 횟수를 반환합니다. 기존 인자 세 개의 키 이벤트 생성자는 그대로 사용할 수 있습니다. 횟수가 0인 직접 전송 합성 메시지는 기존의 단일 이벤트 동작을 유지합니다. `WM_IME_CHAR`는 조합 확정을 증식시키지 않고 네이티브 단일 결과 횟수를 따릅니다. [키 플래그](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-keydown), [문자 횟수](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-char), [IME 결과 계약](https://learn.microsoft.com/en-us/windows/win32/intl/wm-ime-char).

UTF-16 첫 단위는 프레임이나 포커스가 바뀌어도 입력 대상과 반복 횟수를 함께 유지합니다. 디코더는 완성된 스칼라를 반복해 내보내며 분리된 서로게이트를 반복하지 않습니다. 순서 수집은 마지막 이벤트 하나만 남기지 않고 새로 추가된 범위를 모두 복사합니다. IME는 중복을 확인한 뒤 반복 문자열을 한 버퍼에 만들며, 한 단위 중복과 IMM 결과 전체에 일치하는 반복 문자열 모두를 억제합니다. 취소·제거한 대상·대상 없는 입력의 수명 규칙은 유지합니다.

네이티브 메시지 하나는 논리 입력 한 단계로 유지하며 반복은 기존 이벤트 목록에 담습니다. 0·1·3·65535회, 이전 키 상태, Core 질의와 합쳐진 메시지의 일정한 UI 구성 횟수를 검사합니다. 수정 전 339개 중 37개가 실패했고 수정 후 같은 339개가 모두 통과했습니다. 경계 검사 24개를 더해 네이티브 검사는 363개입니다. 메시지 횟수와 Unicode·IME 전달 검사이며 실제 키보드 서비스 전체 수용·입력 전용 CPU 처리·실측 FPS 개선을 뜻하지 않습니다. 전체 포커스·수명과 Linux 반복 주체 검증은 별도 작업입니다. [근거](../artifacts/goal-audit-2026-11-16/native-input-repetition-verification.json).

```powershell
& W:/Cache/Duxel/input-repeat-20261116/publish-232/Duxel.Graphics.Tests.exe W:/Cache/Duxel/native-repeat-manual native-ordered-input-only
& W:/Cache/Duxel/input-repeat-20261116/publish-232/Duxel.Graphics.Tests.exe W:/Cache/Duxel/ime-repeat-manual ime-owner-only
```

## 이벤트 시간과 프레임 시간

`UiInputState.EventTimeSeconds`는 일정한 입력 시계의 이벤트 시각을 선택적으로 전달합니다. 값은 유한한 0 이상의 초 단위입니다. Core는 호스트 시간을 늘리기 전에 묶음 전체를 검사하고 각 논리 입력을 렌더링할 때 시각을 한 번 소비합니다. 입력 동작은 `UiState.InputTimeSeconds`로 이 시계를 읽습니다. 클릭 연결·키 반복·마우스 유지 시간·텍스트 드래그·스핀 버튼 반복에 사용합니다. 애니메이션과 예약 프레임 마감은 계속 `TimeSeconds`를 사용하며 스핀 버튼의 마감은 두 시계 사이에서 변환합니다. 시각이 없으면 기존 프레임 시계를 사용하고, 시각을 받은 뒤 유휴 프레임에는 프레임 간격만큼 입력 시계를 진행합니다.

메시지 전달 순서에 이전 시각이 포함될 수도 있습니다. 클릭 간격이 음수이면 더블 클릭으로 연결하지 않고 새 클릭으로 처리합니다. 한 줄 입력과 반복 개수 조회도 여러 줄·슬라이더처럼 첫 키 누름에서 반복 타이머를 시작합니다. 소비한 시각은 비워 다음 프레임에 이전 시각을 다시 적용하지 않습니다.

Windows의 일반 메시지 펌프는 게시 시각인 `MSG.time`을 수집 범위에 유지하며 네이티브 `GetTickCount64`를 기준으로 DWORD 순환을 해석합니다. 이 큐 처리 밖에서 직접 전달된 메시지는 네이티브 수집 시각을 사용합니다. 큐 대기 시간과 네이티브 시계 기준을 보존하는 처리입니다. .NET 11에서는 Windows 절전 시간 계산이 달라지는 `Environment.TickCount64` 대신 네이티브 시계를 사용합니다. [MSG 계약](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-msg), [네이티브 가동 시간](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-gettickcount64), [.NET 시계 설명](https://learn.microsoft.com/en-us/dotnet/api/system.environment.tickcount64).

Core 검사는 71개에서 88개로 늘었습니다. 첫 84개 검사에서는 클릭·반복·유효성 검사 8개가 실패했습니다. 이를 고친 뒤 편집기·조회·유휴 검사 4개를 추가해 첫 누름 처리 2개 실패를 더 재현했고 최종 88개가 모두 통과했습니다. 네이티브 수집은 기존 218개에 큐·시계·전달 검사 6개를 더해 224개가 통과했으며 실제 App 연결은 37개를 유지합니다. 입력 정확성 검사이며 CPU 처리량·실제 키보드 서비스 전체 수용을 뜻하지 않습니다. 각 논리 입력마다 UI를 구성합니다. 전체 키보드 포커스·수명, 일반 입력 전용 처리와 Linux 생산자 연결은 남았습니다. [근거](../artifacts/goal-audit-2026-11-16/input-event-time-verification.json).

```powershell
& W:/Cache/Duxel/input-time-20261116/publish-224/Duxel.Graphics.Tests.exe W:/Cache/Duxel/input-time-manual ordered-input-batch-only
& W:/Cache/Duxel/input-time-20261116/publish-224/Duxel.Graphics.Tests.exe W:/Cache/Duxel/native-time-manual native-ordered-input-only
```

## Windows 네이티브 순서 수집

`DuxelWindowsApp`은 Windows Observer를 포함한 App 소비자에 순서 입력을 활성화합니다. `./run-gallery.ps1`로 새로 실행하면 다시 빌드한 런타임을 사용합니다. 이미 실행 중인 세대는 로드한 바이너리를 유지합니다. 저수준 사용자 호스트는 `WindowsPlatformBackendOptions.EnableOrderedInput = true`를 설정하고 해당 백엔드의 IME 처리기를 연결한 뒤 App 또는 올바르게 배율을 변환한 Core 배치로 `OrderedInputs`를 소비할 수 있습니다. 저수준 옵션의 기본값은 기존 집계 전용 호스트와 호환하며 오류가 나서 방식을 바꾸는 경로가 아닙니다.

캡처는 HWND 소유 스레드에서 플래그가 지정한 IMM 문자열을 한 번 읽습니다. 불변 `IUiInputDispatch` 데이터는 UI 스레드에서 해당 논리 단계를 그리기 전에 적용됩니다. 첫 UTF-16 단위의 논리 입력 대상은 이후 포인터·포커스 변경에도 유지하고 대상 없이 시작한 단위는 나중에 연결하지 않습니다. 논리 디코더 초기화가 더 나중의 네이티브 수집 상태를 초기화하지 않습니다. 네이티브 서비스 세션은 논리 상태와 분리해 이미 끝났거나 교체된 세션에 오래된 확정·커서 요청을 보내지 않습니다. 데이터는 원래 IME 처리기를 요구하며 다른 소비자나 처리기 누락은 확정 전에 거부합니다. [WM_CHAR UTF-16 계약](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-char), [조합 플래그](https://learn.microsoft.com/en-us/windows/win32/intl/wm-ime-composition).

네이티브 단계마다 수집 당시 `CoordinateScale`을 유지합니다. App은 최신 호스트 배율 하나를 전부 적용하지 않고 이를 사용한 뒤 비웁니다. App은 렌더 후 최근 확정 문자열 채널을 글리프 수집에 소비해 호스트 프레임마다 해제합니다. 입력별 확정은 위젯이 별도로 소비합니다. 독립 처리기 사용자는 최근 문자열 채널을 계속 직접 소비합니다. 기존 BMP 아틀라스 수집 정책을 유지하며 서로게이트 절반과 제어 문자를 글리프로 추가하지 않습니다.

이전 네이티브 수집·수명 검사는 218개가 통과했으며 시간 처리까지 포함한 당시 합계는 224개였으며 위 반복 횟수 검사까지 포함한 최신 합계는 363개입니다. 편집기·탐색 순서·시나리오 90개에서 문자열·세션·제거 뒤 큐를 검사하고 다른 소비자 거부와 숨김 창을 확인합니다. 문자·클릭 순서, 프레임을 넘는 UTF-16, 대상 없는 입력·초기화, 클릭 뒤 START, 결합 플래그, 수집 시점 문자열, 소유 스레드 확정, 폐기와 숨김 복귀를 다룹니다. 소유한 입력의 폐기는 필수 계약인 `IUiImeHandler.ReleaseInput`을 호출합니다. 네이티브 생산자 App 검사 37개도 통과했고 실제 작업 스레드·Vulkan 네 세션과 최근 채널 소비를 포함합니다. IMM 읽기·확정·커서 응답과 포인터 수집은 제어된 검증 장치입니다. 실제 키보드·TSF·후보 창 픽셀, 광범위한 이벤트 시점·반복 입력 의미, CPU 처리량을 인증하지 않습니다. [수명 계약](windows-ime-routing.ko.md#제거한-입력과-숨긴-입력), [최신 근거](../artifacts/goal-audit-2026-11-16/ime-input-lifetime-verification.json), [첫 수집 근거](../artifacts/goal-audit-2026-11-16/native-ordered-input-verification.json).

```powershell
& W:/Cache/Duxel/ime-lifetime-20261116/publish-220/Duxel.Graphics.Tests.exe W:/Cache/Duxel/native-order-manual native-ordered-input-only
& W:/Cache/Duxel/ime-lifetime-20261116/publish-220/Duxel.Graphics.Tests.exe W:/Cache/Duxel/native-app-manual native-ordered-app-only
```

## 기존 집계 입력 대조

현재 스냅샷은 키·문자 목록과 합쳐진 포인터 상태를 전달하며 종류 간 순서가 없습니다. 제어된 대조 검사는 소유한 숨김 창에 실제 `WM_CHAR`를 보내고 합성 Core 포인터 누름을 두 순서로 준비합니다. 세 편집기와 위젯 렌더 순서를 바꿔 확인하며 창 표시·전역 입력·사용자 포커스 변경은 하지 않습니다.

49개 중 실제 문자 스칼라 24개와 숨김 창 검사 1개는 통과했고, 입력 대상 기대 24개 중 12개는 실패했습니다. 네이티브 IME 처리기가 없으면 클릭 전 문자가 새 입력에, 처리기가 있으면 클릭 뒤 문자가 이전 입력에 들어갑니다. 대조 검사는 실패 종료하며 기대·실제 문자열을 기록합니다.

```powershell
& W:/Cache/Duxel/input-merge-20261116/publish-186/Duxel.Graphics.Tests.exe W:/Cache/Duxel/input-order-manual input-order-probe
```

보존한 실패 비교는 기존 집계 수집 방식의 근거이며 Windows App 파사드의 새 기본 경로를 설명하지 않습니다. 실제 키보드·TSF·네이티브 마우스 수용 검사도 아닙니다. 새 Windows 수집은 실제 위젯 적중 경로와 App·Core 순서 소비를 사용합니다. 접근성 사각형으로 적중을 예측하거나 모든 문자를 최종 포커스로 보내지 않습니다. Linux 생산자 통합, 입력 전용 CPU 최적화, 더 넓은 갱신 범위·서비스 수용 검증은 남았습니다.

## 이벤트 사이 위젯 위치 변경

공통 입력 단계가 이전 이벤트의 배치를 그대로 사용하지 않는지 실제 위젯으로 검사합니다. 같은 입력 묶음 안에서 `Move` 버튼을 눌러 대상 버튼의 여백·레이아웃 회전·왼쪽 위 기준 시각 회전·배율·부모 회전·ScrollView 오프셋을 바꿉니다. 이후 클릭은 접근성 경계에서 계산하지 않은 고정 좌표를 사용합니다. 이전 위치가 새 도형 밖인 사례에서는 이전 위치 클릭을 거부하고 새 위치에서 한 번만 실행합니다. 확대 사례는 이전 중심도 새 도형에 포함되므로 거부 조건을 적용하지 않습니다. 스크롤 사례는 처음에 잘린 버튼이 이동 후 표시되어 클릭되는지 확인합니다.

Input·Textarea·MarkdownEditor는 여백 또는 회전 변경 뒤의 클릭·포커스·문자 삽입을 확인합니다. 모든 사례를 대상/변경 버튼의 두 순회 순서로 실행합니다. 버튼은 7개, 편집기는 8개의 논리 입력을 처리하되 최종 그리기는 하나입니다. 최종 접근성 경계와 식별자는 버튼 사례에서 확인합니다.

2026-11-16 관리형 .NET11 검사345개가 통과했으며 이 중 새 위치 변경 검사는84개입니다. 새 사례는 실제 동작·좌표·최종 그리기를 확인하며 별도의 새 GPU 픽셀 비교는 아닙니다. 기존 순서 편집기4쌍·공통 단계6쌍의 GPU 비교는 같은 통과 검사에 포함됩니다. 전체 위젯 캐시 수명·실제 OS IME·Linux 실행·고정 RC3 NativeAOT 수용은 별도로 남습니다. [검증 기록](../artifacts/goal-audit-2026-11-16/interactive-phase-verification.json).

```powershell
& W:/Cache/Duxel/interactive-phase-20261116/publish-270/Duxel.Graphics.Tests.exe W:/Cache/Duxel/interactive-phase-manual ordered-input-batch-only
```

## 입력 전용 캐시 재생과 표시 준비

`ReplayLayerCache`는 입력 전용 단계에서도 예약 시각·애니메이션 경계·부모 레이어의 텍스트 의존성을 유지합니다. 이후 표시용 명령의 클립·투명도·이동을 조정하는 `EnsureReplay`와 프레임·장면 임대는 마지막 표시 단계에서만 실행합니다. 입력 동작을 생략하거나 빈 캐시로 교체하지 않습니다. `BeginLayer`/`EndLayer`와 `TryReplayLayer`가 같은 공통 재생 경로를 사용합니다.

확장 검사는 기준 쪽의 레이어 캐시를 끄고 모든 상태를 순차 전체 다시 그리기합니다. 후보는 세 입력 상태마다 최종 그리기 하나를 제출합니다. 내용 revision, 부모 클립, 투명도, 이동, 3D 변환, 중첩 내용 revision, 기하가 동일한 텍스처 갱신을 슬롯1/3에서 세 차례 바꿉니다. 변경42쌍과 이후 일반 프레임의 캐시 재생42쌍이 정확히 일치하며, 각 변경은 직전 화면과 실제로 다른지도 확인합니다.

수정 전 표시용 소유권 검사는42개 중30개가 실패했습니다. 입력 단계에서 총70개의 불필요한 소유권 획득을 관측했고 수정 후 같은 검사는 모두0입니다. 이는 특정 검사에서 제거한 준비 작업의 근거이며 시간·할당 바이트·FPS 개선 측정이 아닙니다. 최종 입력555개·Observer355개·자원 수명17/10개·페인트 그룹30개·변환53개가 통과했습니다. 일반 Observer44개 화면은 QueuePresent 확인이며 픽셀 비교는 아닙니다. Linux는 호스트 빌드만 확인했습니다. [근거와 한계](../artifacts/goal-audit-2026-11-16/cache-phase-verification.json).

```powershell
& W:/Cache/Duxel/interactive-phase-20261116/publish-274/Duxel.Graphics.Tests.exe W:/Cache/Duxel/cache-phase-manual ordered-input-batch-only
```

## 중첩 메타데이터와 지연 프레임 제출

입력 전용 재생에서도 캐시 자식의 예약 시각을 각 논리 단계에 복원합니다. `BeginLayer`/`EndLayer`와 `TryReplayLayer` 양쪽에서 다음 프레임 시각을 독립적으로 확인하며, 예약이 지나면 자식을 다시 구성하고 다음 시각을 잡습니다. 부모의 이동·투명도·클립으로 가려진 경계 애니메이션은 비활성이고 프레임을 예약하지 않습니다. 최종 상태가 숨김이면 앞선 입력 단계의 애니메이션 요청을 이어받지 않습니다.

실제 래스터 텍스트를 가진 자식을 새 부모가 재생해 독립 의존성을 얻는지 확인합니다. 자식을 제거한 뒤 일반 캐시 한도를 넘는 비보존 텍스트 압력을 주어도 부모의 키는 유지됩니다. 마지막 부모 제거는 보유를 해제하지만, 이미 소비자가 받은 프레임의 기하·명령·픽셀 데이터는 바뀌지 않습니다. 모든 소비자가 끝나면 프레임 임대와 은퇴 기하가 함께 소진됩니다.

GPU 검사는 원래 프레임을 받되 바로 제출하지 않습니다. 이후 입력 묶음에서 레이어를 삭제하고 새 이미지로 교체한 뒤 원래 프레임과 새 프레임을 순서대로 제출합니다. 프레임 슬롯1/3에서 원래 화면과 새 화면 각각이 독립 직접 다시 그리기와 정확히 일치하고, 입력 단계에서 만든 텍스처도 최종 제출에 남습니다. 픽셀 비교는4쌍입니다.

2026-11-16 관리형 .NET11 검사는595개 통과했고 새 메타데이터·가시성·수명 검사는40개입니다. 기존 제품 소스20개의 해시가 직전 검증과 같아 Windows Observer·Linux 호스트 빌드를 반복하지 않았습니다. 전체 캐시 상한·실제 OS 서비스·Linux 실행·고정 RC3 NativeAOT 수용으로 확대하지 않습니다. [기록](../artifacts/goal-audit-2026-11-16/layer-metadata-verification.json).

```powershell
& W:/Cache/Duxel/interactive-phase-20261116/publish-280/Duxel.Graphics.Tests.exe W:/Cache/Duxel/layer-metadata-manual ordered-input-batch-only
```

## 검증 후 기본 lock 복원

GPU 입력 캐시 검사는 `input-cache-gpu-only` 프로필로 따로 실행할 수 있습니다. 테스트 프레임은 소유한 창의 실제 프레임버퍼 크기를 사용합니다. DPI 125%에서 창 요청 320×240이 실제 400×300이 되는 경우에도 고정 크기의 프레임을 잘못 제출하지 않습니다. 픽셀 비교·캐시 기대값은 유지하며, 대상 확보가 실패하면 요청 크기·실제 크기·스왑체인 크기를 오류에 기록합니다.

관리 검증의 마지막 프로젝트 작업은 Linux와 SVG의 기본 lock 복원입니다. `scripts/RestoreManagedValidationDefaults.ps1`에 HTTP 캐시 디렉터리와 새 로그 디렉터리를 명시합니다. 스크립트는 절대 경로·남은 공간·쓰기 가능 여부·고정 SDK11.0.100을 먼저 확인하고 Linux→SVG 순서, 각 종료 코드, 기대 lock 해시를 검증합니다. 캐시를 자식 프로세스에만 전달하며 사용자·시스템 설정은 바꾸지 않습니다. 취약성 감사와 경고를 오류로 처리하는 옵션도 유지합니다. 지원되는 HTTP 캐시 경로 지정은 [NuGet 공식 문서](https://learn.microsoft.com/en-us/nuget/consume-packages/managing-the-global-packages-and-cache-folders)를 따릅니다.

2026-11-16에 L:의 남은 공간0으로 감사 데이터 쓰기가 NU1900으로 실패했습니다. 패키지용 `--no-http-cache`를 지정해도 같은 실패가 관측됐습니다. 감사 생략이나 경고 무시는 하지 않았습니다. 명시한 W: 검증 캐시에서 정상 복원을 확인했고, 공간 없는 기존 경로·상대 경로·이미 사용한 로그 디렉터리는 실행 전에 거부합니다. 전역 L: 공간 부족 자체를 해결한 것은 아닙니다. [실행 검증](../artifacts/goal-audit-2026-11-16/restore-cache-validation.json).

```powershell
./scripts/RestoreManagedValidationDefaults.ps1 -HttpCacheDirectory W:/Cache/Duxel/nuget-http -OutputDirectory W:/Cache/Duxel/restore-defaults-new
```

로그 디렉터리는 매 실행 새 경로를 지정합니다. 이 스크립트는 관리 검증의 마무리 단계이며 일반 Observer 실행이나 고정 RC3 NativeAOT 성능 기준을 대체하지 않습니다.

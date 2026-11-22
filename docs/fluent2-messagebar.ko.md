# MessageBar — 구현 중 (12/13)

Linux도 공통 스타일 문단을 일반 문자열 Core에 연결했습니다. NativeAOT351/334/114개·옵저버41개·전달38개가 통과했으며 전달본은 `e529777d`입니다. 기반 기능을 검증한 것이며 계약007은 아직 미완료입니다. MessageBar가 소유하는 실제 자식 흐름, 공개 방향과 전체 통합은 남아 있습니다. [현재 범위](styled-paragraphs.ko.md).

2026-11-16: Linux의 동일 스타일 행을 명시적 폰트만 사용하는 전용 Pango 조판·네이티브 위치에 연결했습니다. 네이티브334개·기준114개·옵저버41개·전달38개, Windows 문단29개·조판52개·여러 줄 입력2560개·Markdown312개가 통과했습니다. 전달본은 `7746d0b7`입니다. 계약007은 혼합 스타일 전체 문단과 줄바꿈·기준 방향 통합이 더 필요하므로 미완료입니다. [현재 범위](linux-text-shaping.ko.md).

최신 행 위치 회귀: Windows 동일 스타일 행의 커닝·혼합 bidi 커서 결함4개를 수정했습니다. 실제 커서·클릭·선택 조각·자식 영역 검사29개와 관련 회귀가 통과했습니다. 전체 혼합 스타일·글꼴 문단 조판과 Linux 위치 백엔드가 남아 계약007은 계속 미완료입니다. [검증](../artifacts/fluent-messagebar-2026-11-15/paragraph-line-geometry-verification.json).

현재 검증(2026-11-15): 메시지 탭 높이 초과 종료 수정과 공통 인라인 행 정렬을 확인했습니다. CPU155개·GPU176쌍·Windows38개·Linux NativeAOT41개·전달 실행기38개 시나리오 통과. [집중 검증 기록](../artifacts/fluent-messagebar-2026-11-15/inline-alignment-verification.json).

`UiInlineFlowWidget.HorizontalAlignment`는 물리적 Left·Center·Right 정렬입니다. 각 행의 여유 너비에서 계산한 오프셋을 그림·실제 자식 위치·텍스트 span 영역에 함께 적용합니다. 줄바꿈·자연 크기·원문/글리프 순서와 기존 할당 영역 클립을 유지합니다. MessageBar는 생성과 Options 변경 때 실제 `MessageBarBody`를 LTR이면 Left, RTL이면 Right로 맞춥니다. 임의 사용자 본문 컨테이너의 내부 정렬은 유지합니다.

전체 문단 양방향 조판과 문자별 네이티브 접근성 영역은 미검증입니다. 기존 Windows 조판 실패6개는 공통 DirectWrite 모듈로 교정했으며 확장 37개·독립 TextLayout8개·현대 한글11172개 음절 대조가 통과했습니다. 원문 UTF16 위치를 보존하고 Windows HarfBuzz 의존성을 추가하지 않습니다. [조판 구조와 범위](windows-text-shaping.ko.md). 전체 RTL 계약007은 미완료입니다.

`Fluent.MessageBar`·`MessageBarBody`·`MessageBarTitle`·`MessageBarActions`·`MessageBarGroup`는 실제 자식을 소유하며 공식 MessageBar 상위 항목 하나에 연결됩니다. 전체 문단 RTL·줄바꿈 검증은 남아 있습니다. 현재 옵저버 옵션·콜백 경로는 실제 입력과 표시로 확인했습니다. Windows NativeAOT는 원래 프로젝트의 별도 관문입니다.

## 기본 사용과 배치

```csharp
var retryCount = 0;
using var message = Fluent.MessageBar(
    "연결 상태",
    Fluent.MessageBarBody("작업 내용은 유지됩니다.", Fluent.MessageBarTitle("연결을 확인하세요.")),
    Fluent.MessageBarActions([Fluent.Button("다시 시도", () => retryCount++)]),
    options: new(Intent: UiMessageBarIntent.Warning, Width: 560));
// Create once; call inside UiScreen.Render.
message.Render(ui);
```

`containerAction`에는 실제 닫기 버튼 등의 위젯을 전달합니다. 본문→보조 액션→컨테이너 액션 순서로 탐색합니다. 본문은 배정 너비를 지켜야 합니다. `MessageBarBody(text, title, trailingContent)`는 제목·본문·링크·입력 등을 공통 `UiInlineFlowWidget`에서 연결해 배치합니다. 제목은 600, 본문은 400 굵기를 사용하며 같은 서체는 경계를 넘어 커닝합니다. 자식의 Margin·Transform·클립과 소유권은 공통 위젯 계약을 따릅니다.

`Layout`의 기본값은 `Auto`입니다. 실제 자연 너비와 배정 너비를 비교해 보조 액션을 아래로 이동합니다. `MultiLine`은 넓어도 여러 행 배치를 유지합니다. `SingleLine`에 필요한 너비가 부족하거나 고정 슬롯·여백·필요한 높이가 배정에 들어가지 않으면 예외로 알립니다. 내용이 조용히 잘리는 방식으로 처리하지 않습니다.

Info·Success·Warning·Error는 고정 Fluent 소스의 Light/Dark 색상과 20px InfoFilled·CheckmarkCircleFilled·WarningFilled·DiamondDismissFilled 아이콘을 사용합니다. `Shape`는 Rounded(4px)·Square이며 `HideIcon`은 아이콘과 간격을 함께 제거합니다.

`RightToLeft`는 슬롯 위치를 반전합니다. 생성과 Options 갱신 때 실제 `MessageBarActions`의 공통 `UiWrapOptions.RightToLeft`도 맞춥니다. Wrap은 실제 할당 너비를 기준으로 X 위치만 바꾸며 자식 배열·접근성 탐색·위아래 행 순서·글자 그림은 유지합니다. 세로 Wrap은 가로 교차축 위치를 바꿉니다. 임의 자식의 내부 배치나 문단 전체의 양방향 조판까지 보장하는 옵션은 아닙니다. Wrap의 기존 생성자·분해 인수는 유지합니다.

## 표시·실제 삭제와 모션

```csharp
using var first = Fluent.MessageBar("연결", Fluent.MessageBarBody("연결 상태"));
using var second = Fluent.MessageBar("동기화", Fluent.MessageBarBody("동기화 상태"));
using var group = Fluent.MessageBarGroup([first, second], new(Gap: 10), keys: ["network", "sync"]);
group.SetVisible("network", false);
group.SetVisible("network", true);
group.SetItems([new("sync", second), new("network", first)]);
group.SetItems([new("sync", second)]);
group.Render(ui);
```

`SetVisible(index/key, bool)`는 같은 자식을 보관하며 숨기거나 다시 표시합니다. 닫기 콜백에서 이 메서드를 호출하고 자식을 직접 Dispose하지 마세요. `SetItems(ReadOnlySpan<UiKeyedWidget>)`는 실제 삽입·삭제·재정렬에 사용합니다. 모든 키·인스턴스·유형·소유자를 먼저 검증한 뒤 새 자식을 즉시 채택합니다. 준비된 현재 프레임은 유지하고 다음 준비 프레임부터 새 순서를 적용합니다. `Items`는 요청 집합이며 `Children`에는 종료 중인 항목이 잠시 남을 수 있습니다.

입력·접근성 노드는 즉시 빠지고 마지막 GPU 그림만 250ms 선형으로 사라집니다. 남은 자식은 이어서 250ms OutCubic 재배치 효과로 이동합니다. 포커스는 뒤쪽의 살아 있는 컨트롤, 없으면 앞쪽 컨트롤로 옮기며 그룹 밖의 컨트롤도 대상입니다. 실제 삭제는 종료 그림이 사라진 뒤 한 번만 폐기합니다. 완료 전에 같은 키·같은 인스턴스를 다시 요청하면 종료를 취소합니다. 살아 있거나 종료 중인 키에 다른 인스턴스를 넣거나, 중복 키·공유 자식·다른 소유자의 자식·MessageBar가 아닌 항목을 주면 채택 전에 거부합니다. 완전히 폐기된 키에는 새 인스턴스를 넣을 수 있습니다. 덮어쓴 대기 자식과 일찍 폐기한 그룹도 채택한 자원을 해제합니다.

`AnimateEnter`의 기본값은 false입니다. true이면 이후 진입에 슬라이드·페이드를 적용하되 최초 페이지에는 적용하지 않습니다. `ReducedMotion`은 즉시 최종 상태를 적용하며 안정되면 추가 프레임을 요청하지 않습니다. `Fade`·`Reflow`는 공통 `UiTransitionOptions`로 조정합니다. 일반 위젯에는 기반 `UiPresenceGroupWidget`를 사용할 수 있습니다.

## 제목 관계와 공지

그룹은 명시한 이름을 보존하고 실제 소유 제목을 `LabeledBy`로 연결합니다. 처음 표시할 때 Info는 Polite, 나머지 상태는 Assertive 공지를 요청합니다. 우선순위 변경·다시 표시·사용 중지 후 재사용 때 한 번 요청하며, 표시 중인 본문 편집이나 같은 우선순위의 상태 변경만으로는 반복하지 않습니다.

Duxel의 기본값은 검토 기준으로 삼은 React 버전의 동작을 따릅니다. Fluent 2 설계 가이드에서는 Info와 Success 모두 Polite 공지를 권장합니다. Success에 설계 가이드를 적용하려면 `AnnouncementPriority`를 `UiAnnouncementPriority.Polite`로 설정하세요. [Fluent 2 MessageBar 접근성](https://fluent2.microsoft.design/components/web/react/core/messagebar/usage#accessibility) · [검토한 React 구현](https://github.com/microsoft/fluentui/blob/f9720c35ba893c9630f4c0389d07dc6d6fd6068d/packages/react-components/react-message-bar/library/src/components/MessageBar/useMessageBar.ts#L19-L37)

```csharp
message.Options = message.Options with
{
    AnnouncementPriority = UiAnnouncementPriority.Polite,
    DisableAnnouncement = false,
    AnnouncementText = "연결 상태가 변경되었습니다."
};
```

기본값은 각각 null·false·null입니다. null 우선순위는 Intent에서 결정합니다. null 문구는 이번 프레임에 준비한 본문·액션의 접근성 말단 이름을 사용하며 편집 값·집계 이름은 제외합니다. 정확한 문장이 필요하면 `AnnouncementText`를 지정하세요. 빈 실제 내용에는 임의 문구를 만들지 않으며 명시 문구는 공통 Unicode 검증을 통과해야 합니다.

상태는 접근성 게시 성공 때만 확정합니다. 중단된 프레임은 공지를 소비하지 않고 제거된 소유자의 상태는 해제합니다. 안정된 프레임은 반복 공지나 렌더링을 요청하지 않습니다. `UiImmediateContext.AccessibilityTree`는 플랫폼이 소유한 트리를 빌려 주므로 구독·스냅샷에 사용하고 Dispose하지 마세요. 구독자는 해제 시 이벤트 연결을 끊어야 합니다. 공지 요청이나 네이티브 수신은 화면 읽기 프로그램의 발화 완료를 뜻하지 않습니다.

## 실행과 확인 범위

너비 슬라이더를 끌면 미리보기의 메시지가 너비에 맞춰 배치됩니다. 예제 코드는 슬라이더를 놓을 때 최종 너비로 갱신됩니다. 키보드나 접근성 도구에서 너비를 바꿔도 예제 코드에 반영됩니다.

Windows는 `./run-gallery.ps1`, 검증된 Linux 실행본은 `./artifacts/linux-present-path-2026-11-15/run-observer.ps1`로 실행하고 **메시지 표시**를 여세요. 너비·여러 행·모서리·좌우 배치·닫기·다시 열기·동작 줄이기·자동 공지·차분한 공지 고정을 확인할 수 있습니다. **키를 가진 동적 메시지**에서 추가·삭제·순서 변경을 조작합니다. 페이지와 옵션은 작은 화면에서 스크롤·줄바꿈하며, 공지 진단은 실제 요청 후 한 번만 갱신합니다.

현재 근거는 [원장](../artifacts/fluent-messagebar-2026-11-15/plan.json)과 [네이티브 검증](../artifacts/fluent-messagebar-2026-11-15/native-messagebar-verification.json)입니다. Windows 관리형 .NET 11 UIA 클라이언트 검사 8개·호스트 검사 1개, Linux 고정 RC3 NativeAOT AT-SPI 클라이언트 검사 9개·호스트 검사 3개를 통과했습니다. 실제 제목 대상, 한글·이모지, 표시·우선순위·편집·중지·재사용·재표시·안정 프레임을 검사했습니다. 기존 공통 공지도 양쪽에서 회귀 검증했고 Linux 레지스트리 퇴장을 확인했습니다. 네이티브 프레임 검사는 유한한 테스트 시계를 사용하므로 처리량 측정이 아닙니다.

현재 Core DLL이 이전 픽셀 검증과 같음을 확인해 모델 94개·GPU 회귀 152쌍 근거를 재사용하고, 팩터리·복합 계약 416개를 다시 확인했습니다. 독립 모션 GPU 424쌍과 실제 Windows 36개·Linux 39개·전달 실행기 36개 시나리오는 연결된 이전 검증 기록에 있습니다. 전체 RTL·브라우저 픽셀 동일성·Windows NativeAOT·발화 완료·물리 지연은 완료로 주장하지 않습니다. 전달 실행본의 단일 원장은 `artifacts/linux-observer-delivery.json`이며 현재 전달 실행본은 43119a이며 이전 7aa8ce 실행본을 보존합니다.

Windows 독립 검사는 `scripts/Test-NativeAccessibility.ps1 -Binary <게시한 Duxel.Graphics.Tests.exe> -Profile MessageBar -OutputDirectory <새 경로>`로 재현합니다. Linux 명령과 명시 글꼴은 [네이티브 검사 가이드](../artifacts/fluent-messagebar-2026-11-15/native-probe.ko.md)를 따르세요. 공식 소스·토큰은 `reference-manifest.json`에 고정하며, AT-SPI 관계 수명은 [공식 관계 API](https://docs.gtk.org/atspi2/method.Accessible.get_relation_set.html)와 [대상 API](https://docs.gtk.org/atspi2/method.Relation.get_target.html)에 따릅니다.

현재 옵저버 조작 검증: 너비 RangeValue, 여섯 Switch, 재시도, 각 상태의 닫기·재열기, 동적 삽입·재정렬·키 삭제·첫 항목 삭제, 공지 중지·재사용·우선순위 복귀와 안정 프레임을 확인했습니다. 고정·동적 메시지는 생성 시점부터 하나의 설정 경로를 사용합니다. 여러 행·각진 모서리 설정이 동적 메시지에서 빠지던 문제를 교정했습니다. 새 페이지 검사 34개와 실제 Windows 38개·Linux RC3 NativeAOT 41개·전달 실행기 38개 시나리오가 통과했습니다. 실제 옵션 켜기/끄기 단계는 너비 500/800, 배치·모서리·방향·모션 값을 기록하며 공지 수는 7회를 유지합니다. 이는 OS 포인터 입력이나 새 독립 픽셀 기준을 통한 검증은 아닙니다. [검증 기록](../artifacts/fluent-messagebar-2026-11-15/observer-controls-verification.json).

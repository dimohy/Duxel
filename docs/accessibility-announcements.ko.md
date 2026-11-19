# 접근성 공지

`UiImmediateContext.Announce(message, activityId, priority)`와 `UiControlContext.Announce`는 시각적 트리나 포커스를 바꾸지 않고 보조기술에 공지를 전달하는 공통 경로입니다. 위젯을 렌더링하는 프레임 안에서 명시적으로 한 번 호출합니다.

```csharp
bool queued = ui.Announce("연결되었습니다 🙂", "connection", UiAnnouncementPriority.Polite);
```

반환값은 **이 프레임의 큐에 들어갔는지**를 뜻합니다. 보조기술이 실제로 읽었다는 뜻은 아닙니다. 접근성 트리를 연결하지 않은 모드에서는 false를 반환합니다. 활동 식별자는 현재 위젯/호출자 ID 범위에 들어가며, 표시 언어에 따라 바꾸지 않는 안정된 키를 사용합니다.

Polite는 현재 읽기를 기다리는 일반 공지, Assertive는 우선 전달할 중요한 공지입니다. 같은 내용과 키를 명시적으로 두 번 보내면 두 번 전달합니다. 자동으로 중복을 지우지 않습니다. 위젯이 첫 표시·상태 전환처럼 공지가 필요한 시점을 자신의 수명에서 결정해야 합니다. 매 Render에서 호출하면 매번 새 공지가 됩니다.

## 입력과 수명

메시지와 키는 비어 있지 않은 올바른 UTF-16이어야 하며 NUL을 포함할 수 없습니다. 잘못된 우선순위도 거부합니다. 트리의 현재 렌더 작성자만 큐에 넣을 수 있습니다. 측정·다른 스레드·공지 콜백의 재진입·종료한 트리에는 넣지 않습니다. 외부 작업은 먼저 UI 상태를 갱신하고 다음 렌더 프레임에서 공지하세요.

공지는 트리의 커밋 뒤 FIFO 순서로 전달됩니다. 노드나 트리 revision이 바뀌지 않아도 동작합니다. 취소한 프레임의 공지는 버리고, 전달 후 큐를 비웁니다. 관찰자 오류는 전파하며 다음 프레임에 몰래 재생하지 않습니다. 공지 API는 애니메이션이나 반복 렌더링을 예약하지 않습니다.

## 네이티브 매핑

Windows는 [UiaRaiseNotificationEvent](https://learn.microsoft.com/windows/win32/api/uiautomationcoreapi/nf-uiautomationcoreapi-uiaraisenotificationevent)의 실제 창 제공자를 사용합니다. NotificationKind.Other, Polite→All, Assertive→ImportantAll로 전달하고 메시지·활동 키는 길이를 가진 BSTR로 넘깁니다. 이 API의 최소 Windows 버전은10버전1709입니다. 활동 키는 Windows의 공지 묶음 식별자로 전달됩니다.

Linux는 [AT-SPI Announcement](https://github.com/GNOME/at-spi2-core/blob/main/xml/Event.xml)의 창 객체를 소스로 사용합니다. politeness1/2, 미사용 정수0, 문자열 variant와 빈 속성 사전을 전달합니다. 이 프로토콜에는 Windows 활동 식별자에 해당하는 필드가 없으므로 활동 키를 다른 필드에 끼워 넣지 않습니다. 실제 음성 재생 정책은 보조기술이 결정합니다.

## 확인 범위

2026-11-15 공통 모델23개, 독립 Windows UIA 클라이언트4개/호스트1개, 고정 .NET11 RC3 NativeAOT Linux AT-SPI 클라이언트4개/호스트3개를 통과했습니다. 한글·이모지·우선순위·같은 공지의 명시적 반복·변경 없는 후속8프레임을 확인했습니다. Windows는 관리형 .NET11이며 Windows NativeAOT와는 별개입니다. 사람에게 실제 음성이 들렸다는 검증은 아닙니다.

이 경로는 MessageBar의 공통 기반입니다. MessageBar의 시각 위젯·인라인 본문·자동 줄바꿈·모션·옵저버 페이지는 아직 미완료이며 현재 검증된 Linux 실행본은 바꾸지 않았습니다. 근거: `artifacts/fluent-messagebar-2026-11-15/announcement-verification.json`.

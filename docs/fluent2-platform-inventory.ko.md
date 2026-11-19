# Fluent 2 플랫폼별 참조 범위

Last synced: 2026-11-05

> 이 문서는 이전 저장소 조사 기록이다. 구현 요구 범위는 [공식 사이트 기준](fluent2-scope.ko.md)으로 교정됐다. 아래325/299 수치는 현재 요구 분모로 사용하지 않는다.

기존 공식 Android 웹 카드5개만으로 전체 범위를 정할 수 없었다. 공식 저장소의 Compose-v2 README와 연결된46개 원본 파일을 추가로 조사했고, 기존 요구 목록에서 빠진36개 이름을 찾았다. 현재 생성기 입력은 알려진325개 이름을 보존한다. 이는 최종 전체 분모가 아니며, 실제 표준 팩터리26개와 미연결299개를 구분한다. 미연결은 기존 레거시 기능이나 구현이 전혀 없다는 뜻이 아니다.

## 출처와 버전

| 출처 | 고정 기준 | 확인 범위 |
| --- | --- | --- |
| [Fluent Android](https://github.com/microsoft/fluentui-android) |40095bd360026497c0e92159bc307ac82103e655|README Compose-v2의46개 연결 소스와 공개 선언|
| [Fluent Apple](https://github.com/microsoft/fluentui-apple) |03928222b0fde3621fd5b6ea5c7ff1aeefdc1f95|iOS/macOS README,후보 디렉터리 목록과 일부 하위 경로|
| 기존 Fluent React/Web Components |8830c10bfb2f4c1bdd540287b09713b9679cd1ad|기존 원본·카탈로그·타입 자료 재사용|

Android의 Compose-v2는 저장소의 아키텍처 구분이며, 모든 컴포넌트의 Fluent 2 화면 적합성을 검증했다는 뜻은 아니다. XML-v1을 자동으로 Fluent 2로 승격하거나 검토 대상에서 삭제하지 않았다. iOS41개/macOS10개 후보 디렉터리를 컴포넌트 수로 세지 않는다. 공개 API·기반 기능·디자인 세대 검토가 남아 있다.

원본 URL·커밋·SHA256은 `artifacts/fluent-platform-inventory-2026-11-05/source-manifest.json`에 보존한다. 병렬 HTTP 요청은18개 저장 후 시간 초과가 발생했다. 성공한 파일은 재사용했고 같은 커밋의 Git 원본으로 나머지를 확보했다. Windows checkout 줄바꿈 변환 때문에 비교가 달라진 것을 확인하여 실제 Git 객체 바이트를 사용했으며, 기존18개와 바이트 동일함을 검증했다. 입력 버전을 바꾸거나 소스를 다른 구현으로 대체하지 않았다.

## 분류와 대응

46개 소스 계열을54개 행으로 대응했다. NotificationCommon과 ListContentBuilder는 기반 계약/구성 도구로 분류하여 필수 위젯 팩터리에서 제외했다. Avatar의 공개 ActivityRing/ActivityDot,Drawer의 BottomDrawer,ListItem의 공개 하위 렌더러 등은 요구에서 누락하지 않았다.

| 원본 표기 | 현재 대응 | 의미 |
| --- | --- | --- |
|CheckBox|Checkbox|철자 차이 대응; 구현 완료를 의미하지 않음|
|ToolTipBox|Tooltip|원본 API 이름을 별도 대응표에 보존|
|Pill 파일|PillButton/PillBar|소스 파일명을 가짜 위젯 이름으로 만들지 않음|
|ListItem.Header 등|ListItemHeader 등|공개 하위 렌더러의 요구를 분리해 보존|
|NotificationCommon/ListContentBuilder|infrastructure|위젯으로 잘못 노출하지 않음|

전체 대응은 `android-component-mapping.json`과 저장소의 `tests/Duxel.Generators.Tests/Fixtures/FluentAndroidComponents.json`에 있다. 이전289개 요구와 참조를 삭제하지 않고 Core의 FluentComponents.tsv에54개 행을 추가했다. 중복을 합친 알려진 요구 이름은325개다. AcrylicPane,FileCard,PeoplePicker,PillTabs,SideRail 등의 누락을 더 이상 숨기지 않는다. 이 항목을 다른 컨트롤의 이름만 바꾸어 구현한 것으로 처리하지 않는다.

## 실제 소비 경로와 회귀 검사

Core의 소스 생성기가 이 TSV를 실제로 소비한다. 공식 raw 참조 허용 범위는 Microsoft의 fluentui/fluentui-android/fluentui-apple 세 저장소로 제한했다. 유사 저장소 이름,다른 소유자,다른 호스트,자격 증명이 붙은 URL 및 HTTP는 검사에서 거부한다. 도메인 허용 검사는 해당 코드의 실제 동작·릴리스 상태를 인증하는 기능이 아니다.

생성기81개 검사는 실제 생산 TSV와 커밋된46개 소스 설명/54개 행을 대조한다. 행 삭제,기반 도구의 위젯 승격,기반 행을 남긴 채 가짜 위젯 행 추가를 모두 거부한다. 엄격 완료 모드는 누락된 공식 항목과 draft 범위를 계속 거부한다. .NET10 위젯 트리72개도 통과했다.

.NET10과 고정RC3 NativeAOT의 실제 생성 카탈로그가 모두26/325/299 및 ScopeComplete=false로 일치했다. GPU나 창을 만들지 않는 기존 그래픽 검증 호스트의 catalog-only 모드를 사용했다. 그리기·동작 소스가 이전 Badge 검증 시점과 같은 해시임을 확인하여 GPU/전체 검사를 불필요하게 반복하지 않았다. 이전 전체1095/Native243 결과를 현재 전체 재실행으로 바꾸어 보고하지 않는다.

## 남은 범위

Web Components 세부 공개 API,iOS/macOS 공개 API와 디자인 세대,플랫폼별 별칭·변형·상태·접근성의 전체 대응이 남았다. 분모를 억지로325로 확정하거나 공식 전체 제공을 완료했다고 보고하지 않는다. 실제 위젯·공식 화면·동작·UIA·성능 검증도 각각 남아 있다. 기존 최적화·Pi·비표준 비노출 목표와 승인된 크기 변경 구현을 유지한다.

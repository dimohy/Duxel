# Fluent 탭 콘텐츠와 원형 표현

Last synced: 2026-11-11

`./run-gallery.ps1`로 실행하고 **Fluent** 탭에서 **탭 표현과 아이콘**까지 스크롤합니다. 실제 탭 목록 네 개가 선택 값을 공유하며 페이지를 떠나도 유지합니다. 아이콘과 글자, 아이콘 전용, 비활성 항목, Transparent·Subtle·SubtleCircular·FilledCircular를 비교할 수 있습니다. 뒤의 두 표현은 캡슐 모양이며 밑줄을 그리지 않습니다. 방향키는 포커스를 옮기고 Enter·Space는 선택합니다. 선택하면 아이콘의 윤곽형을 채움형으로 바꾸며 마우스 올림이나 포커스만으로는 바꾸지 않습니다.

```csharp
var icon = UiTabIcon.Create<UiFluentAddIcon, UiFluentAddFilledIcon>();
var tabs = Fluent.TabList("탐색", selection.Binding,
    new UiTabOption[] { new("add", "추가", Icon : icon, IconOnly : true) },
    new(Appearance : UiTabListAppearance.FilledCircular));
```

아이콘 두 타입은 기존 `IUiButtonIcon`을 구현합니다. `UiTabIcon.Create<TRegular, TFilled>()`는 닫힌 타입 쌍별 정적 페인터를 재사용합니다. 사용자 벡터 페인터와 생성된 Fluent 글리프를 사용할 수 있으며 아이콘 선택과 입력 상태를 구분합니다. 글꼴을 자동 탐색하거나 다른 아이콘으로 대체하지 않습니다. 글꼴 아이콘에는 기존 DirectText 설정과 함께 배포하는 Fluent 글꼴이 필요합니다. 관찰 샘플은 생성된 홈·문서·설정의 윤곽형과 채움형을 사용합니다.

`UiTabOption`은 기존 키·접근성 라벨·활성 여부에 선택적인 `Icon`과 `IconOnly`를 추가합니다. 글자를 숨겨도 `Label`은 접근성 이름으로 유지합니다. 아이콘 전용에는 아이콘이 필요하며 모든 아이콘 탭의 라벨은 공백일 수 없습니다. 잘못된 항목은 교체 전에 거부하고 호출자의 선택 값은 유지합니다. 기존 글자 전용 호출과 표현 메서드는 계속 동작합니다. 사용자 `IUiTabListPresentation`은 항목을 받는 `MeasureTab`·`PaintTab` 오버로드로 아이콘을 지원합니다. 기본 오버로드는 아이콘을 버리지 않고 미지원 오류를 명시합니다.

Small·Medium 아이콘은20, Large는24 단위를 사용합니다. 원형 목록 간격은 Small6, 나머지8이며 아이콘과 글자 간격은 Small2, 나머지6입니다. 기존 글자 전용 크기는 유지하며 원형 테두리는 측정에 포함합니다. 세로 콘텐츠는 시작 가장자리에 정렬하고 RTL의 앞쪽 아이콘은 오른쪽에 배치합니다. 원형 탭 사이의 빈 간격은 클릭 대상이 아닙니다. 선택·네이티브 UIA 패턴·본문 관계·프레임 최종 포커스는 공통 TabList 경로를 사용하며 표현 변경은 선택 값과 항목 식별자를 유지합니다.

`UiFluentTabListPalette`는 배경·테두리·글자·아이콘·표시선 색상을 제공합니다. 기대값은 고정한 Fluent UI8830c10b의 스타일과 실제 brandWeb·grey 표현식에서 가져오며 별칭의 주석이나 제품 팔레트로 만들지 않습니다. `tests/Duxel.Graphics.Tests/Fixtures/TabAppearances.json`에 밝음·어두움, 표현 네 종류, 상태 네 종류, 선택 여부의64조합과 원본 토큰 이름·색상을 기록합니다.

검증은 코어197건, MSAA1·4와 두 색상 모드의 실제 GPU800건, 외부 네이티브 선택·제어 관계 클라이언트25건과 호스트3건입니다. 관찰 샘플은 CPU44·GPU40건으로 실제 네 목록까지 스크롤한 뒤 포인터로 선택하는 경로를 포함합니다. 기존 스케줄링35·접근성49·컨테이너179·기존 API10 검사도 통과했습니다. GPU 검사는 상태 색상, 밑줄 유무, 획득 이미지 재사용, 실제 글꼴의 표시와 윤곽형·채움형 기하 변화를 대조합니다. 공식 전체 이미지 일치, 강제 색상, 포커스 그림자, 표시선 전환, 넘침 메뉴, 화면 읽기 음성, NativeAOT의 완료를 뜻하지 않습니다. 전체 Fluent 범위는 아직 미완료입니다.

근거는 `artifacts/tab-representations-2026-11-11/verification.json`입니다. SDK11.0.100을 roll-forward 없이 고정했고 런타임은11.0.0이며 미실행 RC3 NativeAOT·런타임 이미지 비교와 구분합니다. 이번 팔레트·글꼴 GPU PPM 캡처는 `L:/Cache/Duxel/tab-representations-2026-11-11`의 바이트 검증 ZIP에 보관합니다. `gpu-archives.json`에 원래 파일 이름·보관 위치·해시 명세를 기록합니다. 선택한 PNG와 JSON 결과는 바로 확인할 수 있습니다. 관찰 GPU 캡처도 용량을 확인한 뒤 명시적인 L: 캐시에 저장합니다. W: 공간 부족과 복구 기록은 성공한 검사와 별도로 남깁니다.

공식 소스: [고정 Tab 스타일](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-tabs/library/src/components/Tab/useTabStyles.styles.ts), [고정 TabList 간격](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-tabs/library/src/components/TabList/useTabListStyles.styles.ts).

### 기존 BeginTabBar/BeginTabItem 의미 정보 (2026-11-11)

스트리밍 방식의 기존 API가 TabList·실제 Tab SelectionItem·활성 TabPanel만 게시하고 ControllerFor/LabeledBy 상호 관계를 제공합니다. 본문 컨트롤은 해당 패널에 속하며 중첩 탭은 바깥 부모를 복원합니다. TabItemButton은 탭 선택에 참여하지 않는 Invoke 버튼입니다. 필수 선택에서 선택된 머리글의 해제를 거부하며, 대기 중인 대상의 닫힘·삭제를 실제 게시되지 않은 선택의 성공으로 처리하지 않습니다.

포인터·접근성 선택 모두 기존의 다음 프레임 전환과 프레임당 본문 하나를 유지합니다. UiAccessibilityTree의 wait=true 선택 요청은 선택된 프레임 게시 후 완료되며 그 전에 대상이 닫히거나 삭제되면 InvalidOperation/Unavailable을 반환합니다. 기존 Windows SelectionItem 메서드는 비동기 접수 방식을 유지하므로 확정된 속성/선택 알림을 받은 뒤 새 상태를 읽습니다. 머리글/본문은 기존의 비포커스 계약을 유지하며 키보드/플래그 전체 지원·발화는 남아 있습니다. 전환 완료 후 반복 깨우기나 애니메이션 기한을 만들지 않습니다.

기존 탭 관리25개·공통 TabList197개·실제 관찰131개·외부 기존 탭 클라이언트15개/호스트3개·생성형 탭 회귀 클라이언트25개/호스트3개가 통과했습니다. 관찰 GPU47개가 통과했고 비교 가능한73개 본문 캡처는 이전 수치 접근성 기준과 같습니다(FPS 머리글 제외). 테스트는 실제 TabList 범위에서 같은 이름의 머리글/본문을 구분합니다. 생성형 본문 검사는 기본 포커스 가능 기대값을 유지하며 기존 본문은 명시적으로 false를 검사합니다. ./run-gallery.ps1 → 기존 API → Hello / Immediate에서 확인합니다. 근거: artifacts/legacy-tabs-2026-11-11/verification.json. NativeAOT와 전체 목표 검증은 별도입니다.
## 스크롤한 본문의 경계 (2026-11-17)

기존 BeginTabItem, Fluent.TabList.RenderPanel과 Nav.RenderPage는 화면 좌표를 기준으로 같은 클립 계산을 사용합니다. 자식 스크롤 영역에서 커서 좌표에는 이미 스크롤이 반영되어 있으므로, 남은 배치 높이를 클립 높이로 다시 사용하지 않습니다. 상위 클립, 콘텐츠 여백과 기존 열 너비 제한은 유지합니다. 본문이 화면보다 길어도 접근성 경계는 보이는 자식을 포함하고, 포커스 테두리는 화면에 보이는 경계 안에 그립니다.

검증: 실제 갤러리의 Scale을 깊은 위치까지 스크롤한 뒤 부모 경계를 확인했습니다. GPU 검사는 기존 탭·Fluent 탭·탐색 페이지를 MSAA 1·4에서 비교하고, 포커스 가능한 두 경로의 테두리 네 면을 검사합니다. 이전 Core만 사용한 대조 실행은 같은 부모 경계 검사에서 실패했습니다. 근거: `artifacts/tab-panel-scroll-clip-2026-11-17/result.json`.

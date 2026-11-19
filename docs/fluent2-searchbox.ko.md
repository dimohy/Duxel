# SearchBox 검색 입력

```csharp
var query = new UiState<string>("정세일");
var search = Fluent.SearchBox("Search", query.Binding,
    options: new(Width: 360, Placeholder: "Search people", ClearLabel: "Clear search"));
search.Options = search.Options with { Size = UiSearchBoxSize.Large, ReadOnly = true };
```

단일 행 편집·문자소 선택·클립보드·되돌리기·IME는 기존 입력 경로를 사용합니다. 검색 아이콘은 장식이며, 포커스가 있는 동안 지우기 버튼을 표시합니다. 읽기 전용에서는 지우기를 숨깁니다. 지우기에는 실제 Invoke 접근성 동작을 제공하며 Tab 정지점을 추가하지 않고 편집기로 포커스를 돌려줍니다. 고정 소스의 기본 슬롯처럼 값이 비어 있어도 포커스가 있으면 지우기를 표시합니다. 바인딩을 단일 권위로 사용하므로 변경 거부·정규화 결과를 같은 프레임에 다시 읽은 후 그리기와 접근성 게시를 수행합니다.

세 크기의 높이는24/32/40px, 아이콘은16/20/24px, 루트 여백은6/8/10px이며 최대 너비는468입니다. 기존 네 입력 외관과 RTL 아이콘 배치를 지원합니다. 실제 텍스트는 기존 편집기의 커서·양방향 처리 범위를 유지하며 Unicode bidi 전체 지원을 뜻하지 않습니다. 좁은 부모 영역에서도 안전하게 자릅니다. 읽기 전용은 포커스·선택을 허용하며 변경을 막고, 비활성화는 공통 컨트롤 경로를 사용합니다. Invalid와 Field/Label 연결도 공유합니다. MaxLength·null 금지·단일 행 계약을 검사하며 잘못된 권위 getter는 명시적으로 실패합니다.

Windows UIA에는 Edit 컨트롤·`Duxel.SearchBox` 클래스·`searchbox` ARIA 역할·Value/읽기 전용·수락된 Value 변경 이벤트와 하위 지우기 동작을 게시합니다. 표시 문자열·편집 버퍼·접근성 값은 편집 프레임에 일치시킵니다. 사용자 정의 표현은 `IUiSearchBoxPresentation`의 타이포그래피·측정·배치·그리기 계약을 사용합니다. 새 편집기·XML 파서·렌더러·네이티브 import·주기 애니메이션을 추가하지 않습니다. 검색·지우기 그림은 생성된 Fluent **폰트 아이콘**입니다. SVG 이미지·전용 아이콘 위젯은 별도의 기존 지원 경로입니다. 검색 실행·필터링은 호출자가 바인딩으로 구현합니다. 초기 API에는 임의 자식 슬롯과 제출 콜백이 없습니다.

`./run-gallery.ps1` → **검색 입력**에서 한글 입력·지우기·크기·외관·방향·읽기 전용·변경 거부를 확인하세요. 숨겨진 탭은 상단 더 보기에서 선택합니다. 다른 탭을 왕복해도 상태는 유지됩니다. 근거는 `artifacts/searchbox-2026-11-13/`, 큰 GPU 결과는 `L:/Cache/Duxel/searchbox-2026-11-13-evidence/`에 있습니다. 관리형 검증은 고정 RC3 NativeAOT·런타임 이미지·실기기·성능·Fluent 전체 적합성 완료가 아닙니다.

고정 공식 소스: [동작](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-search/library/src/components/SearchBox/useSearchBox.tsx), [스타일](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-search/library/src/components/SearchBox/useSearchBoxStyles.styles.ts), [렌더링](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-search/library/src/components/SearchBox/renderSearchBox.tsx).

지우기 버튼을 누른 채 여러 프레임 유지해도 편집기의 포커스와 Tab 등록을 유지합니다. 버튼 안에서 놓으면 수락된 값을 한 번 지우고, 밖에서 놓으면 취소합니다. 지우기는 되돌리기·다시 실행이 가능한 한 번의 편집이며 바로 한글을 다시 입력할 수 있습니다. 마우스 캡처를 유지하면서 공통 편집기 탐색 경로를 사용합니다. 변경 거부·읽기 전용의 기존 계약도 유지합니다. 회귀 근거: artifacts/searchbox-clear-2026-11-13/verification.json.

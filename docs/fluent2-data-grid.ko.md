# Fluent DataGrid

갤러리에서 **Fluent 컨트롤 → 콘텐츠와 목록 → DataGrid**를 열면 디자인 파일 2,048개가 표시됩니다. 행을 스크롤해도 열 제목은 위에 고정됩니다. 정렬할 수 있는 열 제목을 누르면 오름차순으로 정렬되고 한 번 더 누르면 내림차순으로 바뀝니다.

`Fluent.DataGrid<T>(rows, getKey, columns, options)`는 전달한 행을 복사해 사용합니다. 행마다 고유한 문자열 키를 지정하고, 열마다 화면에 표시할 문자열을 반환하는 함수를 전달합니다. 정렬할 열에는 비교 함수를 지정합니다. 열 너비는 가중치에 따라 나뉘며, 숫자는 오른쪽에 맞추고 실제 값으로 정렬할 수 있습니다. `AccessibleName`에는 표의 내용을 설명하는 이름을 지정합니다.

`Selection`을 `Single` 또는 `Multiple`로 지정하면 행을 선택할 수 있습니다. 다중 선택에서는 전체 선택도 가능합니다. 방향키, Home, End, Page Up, Page Down으로 셀 사이를 이동하고 Space로 행을 선택하거나 해제합니다. `SetRows`로 행을 바꾸면 키가 같은 행의 선택이 유지됩니다.

편집할 열에 `Edit` 함수를 전달합니다. 이 함수는 입력을 검사하고 키가 같은 새 행을 반환합니다. 화면에 단위를 붙여 표시하는 등 편집할 때 다른 문자열이 필요하면 `EditText`를 지정합니다. 셀을 두 번 누르거나 포커스한 뒤 F2 또는 Enter를 누르면 편집을 시작합니다. Enter로 확정하고 Esc로 취소합니다. 포커스가 편집기에서 벗어나거나 편집 중인 행이 화면 밖으로 나가면 입력 중인 값은 취소됩니다. 확정한 행은 `CellEdited`에서 받을 수 있습니다. 정렬 중인 열의 값을 바꾸면 현재 정렬 기준을 다시 적용합니다.

DataGrid는 화면에 보이는 행만 그립니다. 접근성 트리에서 전체 행과 열의 수, 화면에 보이는 셀을 확인할 수 있습니다. [Microsoft Fluent UI React DataGrid 예제](https://storybooks.fluentui.dev/react/iframe.html?id=components-datagrid--docs)

Microsoft는 Fluent UI React의 `react-table` 패키지에서 `DataGrid`를 제공합니다. 아래 커밋의 구현에서 행 선택, 정렬, 키보드로 셀에 초점을 옮기는 동작을 확인할 수 있습니다. Duxel은 앞에서 설명한 열 제목 고정, 행 가상화, 열별 편집을 제공합니다. 갤러리에서는 DataGrid를 추가 구성 요소로 제공하며, 공식 구성 요소의 구현 범위는 Fluent 2 디자인 목록을 기준으로 집계합니다. [Microsoft 패키지 공개 항목 (`c78d223`)](https://github.com/microsoft/fluentui/blob/c78d22353ba03b9eb744f577332aa400daf10934/packages/react-components/react-table/library/src/index.ts#L114-L153) · [DataGrid 동작 (`c78d223`)](https://github.com/microsoft/fluentui/blob/c78d22353ba03b9eb744f577332aa400daf10934/packages/react-components/react-table/library/src/components/DataGrid/useDataGrid.ts#L24-L78) · [Fluent 2 React 디자인 목록](https://fluent2.microsoft.design/components/web/react)

갤러리를 열면 한국 시간 기준 메뉴 등록일부터 7일 동안 `NEW` 배지가 표시됩니다.

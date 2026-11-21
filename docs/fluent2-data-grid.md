# Fluent DataGrid

In Gallery, open **Fluent controls → Content and lists → DataGrid**. The example shows 2,048 design files. Column headers stay visible while rows scroll. Activate a sortable header to start in ascending order and activate it again to reverse the order.

`Fluent.DataGrid<T>(rows, getKey, columns, options)` snapshots the rows. Supply a unique string key for each row, a display function for each column, and an optional typed comparison. Column weights share the available width. Numeric columns can align right and sort by their underlying values. Set `AccessibleName` to describe the grid's contents.

Set `Selection` to `Single` or `Multiple` to enable row selection. Multiple selection includes a select-all control. Arrow keys, Home, End, Page Up and Page Down move focus within the grid. Space toggles row selection. `SetRows` retains selection for surviving keys.

To edit a column, supply an `Edit` delegate that validates the draft and returns a replacement row with the same key. Set `EditText` when the display value includes units or other formatting the editor should omit. Double-click an editable cell or press F2 or Enter while focused. Enter validates and commits, and Esc cancels. When focus leaves the editor or its row scrolls out of view, the draft is discarded. `CellEdited` publishes the committed item. Editing a sorted column reapplies the current sort.

The control paints the visible rows using a pinned header and a vertical viewport. The accessibility tree reports the grid dimensions and the cells currently in view. The design follows Fluent UI React's DataGrid patterns for sorting, row selection and cell navigation. [Microsoft Fluent UI React DataGrid](https://storybooks.fluentui.dev/react/iframe.html?id=components-datagrid--docs)

Gallery shows `NEW` during the seven calendar days starting with menu registration, using Korean standard time. It calculates the badge when the gallery opens.

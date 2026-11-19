# Shared scrollbar motion

Every scrollbar uses the same `UiScrollbarMotion` entry/hold/exit policy. Consumers retain input and geometry; motion returns opacity and a finite deadline; one painter draws tracks and thumbs.

| Consumer | State lifetime |
| --- | --- |
| Window, child, lists, input, ScrollView, menus and VirtualList | Existing UiState scrollbar identity; removed with its owned axis |
| Tooltip | Weak tooltip-owner state within its context |
| Tab overlay | Directly owned by the tab instance |

Defaults:120ms entry,400ms stationary hold,160ms exit. Standard bars reveal on movement, drag or track hover; tabs retain the requested movement/drag-only trigger. A stationary held drag stays visible without periodic frames after entry. Late frames catch up from the actual hide deadline. Reentry reverses from the current opacity.

Configure the shared policy through `ControlSkins.Scrollbar.Motion` on a compiled design. Per-tab overrides use `UiTabListScrollOptions.Motion`. `ReducedMotion` resolves transitions immediately while retaining the hold deadline. `AlwaysShowThumb` remains an explicit permanent-visibility policy. Clipped axes and released owners do not renew animation deadlines. No Timer, thread or unbounded render flag is introduced. Existing viewport allocation, pointer, wheel and accessibility contracts remain; only the tab thumb is a layout-free bottom overlay.

See the [finite transition design](finite-transitions.md). NativeAOT, physical assistive technology and complete Fluent conformance remain separate validation scopes.


View scrollbars settle to a faint motion opacity of `0.28`, configured by `UiScrollbarMotionOptions.IdleOpacity`. The tab overlay retains the requested idle value `0`. Both use the same transitions and scheduling module.

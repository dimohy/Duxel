# Source text elements in single-line editing

[Single-line drag](input-drag.md): an owned press continues selection outside the field with horizontal autoscroll; release,hidden/clipped/disabled owners and higher popups end capture. CPU336/input-kernel400 pass, with all 357 prior input and 3457 multiline images unchanged. Actual OS capture and complete text/native fidelity remain open.

[Shared keyboard commands](text-commands.md): Ctrl+Backspace/Delete and held keys remove complete word ranges; multiline Left/Right collapse selections. CPU301, input-kernel304, multiline2336 pass. Full native/Unicode/OS conformance remains open.

The shared single-line kernel now moves and deletes at .NET `StringInfo` text-element boundaries. It keeps supplementary characters, combining sequences and joined emoji whole. A synchronized caret inside an element snaps to its leading boundary. Selection endpoints expand to complete elements while preserving direction; ordinary Left/Right collapse a selection to its leading/trailing endpoint. Pointer hits inside an element choose between its measured leading and trailing edges.

The edit buffer lazily caches source boundaries, invalidating them on Sync/Replace. ASCII without CR uses index arithmetic without a boundary array, and endpoint-only operations can avoid building the cache. Cache ownership follows the existing edit buffer. This is an implementation path, not a measured zero-allocation or whole-app performance claim.

Keyboard events, held repeats, clipboard and readonly selection use the same boundaries. Normalization between edits in one frame also protects a cluster formed with an existing suffix. Word movement recognizes Unicode BMP letters/digits and combining marks, then aligns its result to source elements. The word grouping remains a heuristic rather than locale-specific/UAX29 segmentation. Logical source navigation does not establish visual bidi navigation, shaping, ligatures or actual OS IME behavior.

Pinned .NET 11 RC3 reproduces 20 initial CPU failures and two further selection-collapse failures. The combined editing CPU suite passes 231 checks, including 27 new single-line cases. Input-kernel validation passes 240 combined checks: the previous 144 plus 96 new GPU comparisons with actual key events, literal display text, selection rectangles and caret references across raster/vector, Light/Dark, MSAA1/4 and four acquired-image reuses. All 117 prior input images and 3,265 prior multiline images remain identical. Detailed evidence: `artifacts/single-line-elements-2026-11-11/verification.json`.

The vector fixture initially rejected U+1F600 because the selected fonts did not contain it. The new fixture explicitly selects installed Segoe UI Emoji as a secondary regular font and records font hashes. The unsupported-font error is preserved. A compatibility overload also preserves the linked legacy-reference caller of the private repeat helper. Neither correction weakens the comparison.

`fluent_input_elements_fba.cs` demonstrates editable/readonly Unicode text, caller replacement and an explicitly selected emoji font. Full Unicode/browser/font fidelity, external Windows UIA, actual OS input, current Native/runtime comparison and integrated performance remain open.

using Duxel.Core;

internal static class AccessibilityTextTests
{
    internal static void Run(Action<bool, string> check)
    {
        var content = new UiAccessibilityTextContent("A정😀e\u0301\r\nZ");
        var textScroll = new UiAccessibilityTextScroll(content, 0, 1, new UiVector2(10, 20));
        check(ReferenceEquals(textScroll.Content, content) && textScroll.StartUtf16 == 0 && textScroll.EndUtf16 == 1
            && textScroll.Point == new UiVector2(10, 20), "Source range scroll retains its exact immutable content and declared client point");
        foreach (var invalid in new Action[]
        {
            () => new UiAccessibilityTextScroll(content, 2, 3),
            () => new UiAccessibilityTextScroll(content, 1, 0),
            () => new UiAccessibilityTextScroll(content, 0, 1, (UiAccessibilityTextScrollPlacement)7),
            () => new UiAccessibilityTextScroll(content, 0, 1, new UiVector2(float.NaN, 0)),
            () => new UiAccessibilityTextScroll(content, 0, 1, new UiVector2(0, float.PositiveInfinity))
        })
        {
            var refused = false;
            try { invalid(); }
            catch (ArgumentException) { refused = true; }
            check(refused, "Source range scroll contracts reject split scalars, reversed ranges, unknown placement and nonfinite points");
        }
        var nominal = new UiAccessibilityTextSize(36, 2);
        check(nominal.LogicalPixels == 18 && nominal.Points == 13.5f,
            "Nominal physical font pixels convert through content scale to96-DIP logical pixels and72-point units");
        foreach (var invalid in new[] { new UiAccessibilityTextSize(0, 1), new UiAccessibilityTextSize(18, 0),
            new UiAccessibilityTextSize(float.NaN, 1), new UiAccessibilityTextSize(18, float.PositiveInfinity) })
        {
            var rejected = false;
            try
            {
                new UiAccessibilityTextAppearance(content, default, [], invalid);
            }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "Unknown or invalid nominal size cannot be published as zero, NaN or infinity");
        }
        var font = new PlatformFontMetadata("Fixture", "Regular", 400, false, false, "en") { Oblique = false };
        var paintRuns = new[] { new UiAccessibilityTextSourcePaintRun(0, 6, new(1, 2, 3), nominal, font),
            new UiAccessibilityTextSourcePaintRun(8, 9, new(4, 5, 6), null, null) };
        var styledPaint = new UiAccessibilityTextAppearance(content, paintRuns);
        paintRuns[0] = paintRuns[0] with { Foreground = new(9, 9, 9) };
        check(!styledPaint.HasUniformPaint && styledPaint.GetPaintRun(0).Foreground == new UiColor(1, 2, 3)
            && styledPaint.GetPaintRangeAt(0).Size?.Points == 13.5f,
            "Source paint maps copy input runs and retain known nominal size without a fake uniform foreground");
        check(styledPaint.GetPaintRangeAt(5) == (5, 7, null, null)
            && styledPaint.GetFontRangeAt(7).Font is null && styledPaint.GetPaintRangeAt(8).Foreground == new UiColor(4, 5, 6),
            "Source paint gaps, unknown fonts and end-of-document lookups preserve independent sparse attributes");
        var uniformRefused = false;
        try { _ = styledPaint.Foreground; }
        catch (InvalidOperationException) { uniformRefused = true; }
        check(uniformRefused, "A styled appearance refuses an invented uniform foreground value");
        foreach (var invalidRun in new[] { new UiAccessibilityTextSourcePaintRun(0, 3, default, null, null),
            new UiAccessibilityTextSourcePaintRun(2, 1, default, null, null),
            new UiAccessibilityTextSourcePaintRun(0, 1, default, new(0, 1), null) })
        {
            var refused = false;
            try { _ = new UiAccessibilityTextAppearance(content, new[] { invalidRun }); }
            catch (ArgumentException) { refused = true; }
            check(refused, "Source paint contracts reject split scalars, reversed ranges and invalid nominal sizes");
        }
        var painted = new UiAccessibilityTextAppearance(content, new UiColor(1, 2, 3), new[]
        {
            new PlatformTextFontRun(0, 6, font), new PlatformTextFontRun(8, 9, font)
        });
        check(painted.FontRunCount == 2 && painted.GetFontRun(0).End == 5 && painted.GetFontRun(1).Start == 7,
            "Font source ranges convert supplementary UTF16 positions to immutable scalar intervals once");
        check(painted.GetFontRangeAt(5) == (5, 7, null) && painted.GetFontRangeAt(8) == (7, 8, font),
            "Unpainted CRLF gaps retain no invented font while document end selects the final actual font run");
        foreach (var invalid in new Action[]
        {
            () => new UiAccessibilityTextAppearance(content, default, [new(0, 3, font)]),
            () => new UiAccessibilityTextAppearance(content, default, [new(2, 4, font), new(1, 2, font)]),
            () => new UiAccessibilityTextAppearance(content, default, [new(0, 1, font with { Weight = 0 })]),
            () => new UiAccessibilityTextAppearance(content, default, [new(0, 1, font with { FamilyName = "bad\0name" })]),
            () => painted.GetFontRun(-1), () => painted.GetFontRangeAt(9)
        })
        {
            var rejected = false;
            try
            {
                invalid();
            }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "Accessible font ranges reject split scalars, unordered source and invalid lookups");
        }
        int[] offsets = [0, 1, 2, 4, 5, 6, 7, 8, 9];
        check(content.CharacterCount == 8 && content.Text.Length == 9,
            "Accessible character counts use Unicode scalars rather than UTF16 units or graphemes");
        check(offsets.Select((utf16, character) => content.GetUtf16Offset(character) == utf16
            && content.GetCharacterOffset(utf16) == character).All(value => value),
            "Accessible text converts independently declared Korean, supplementary, combining and CRLF offsets exactly");
        check(content.GetUnicodeScalar(2) == 0x1f600 && content.GetText(2, 5) == "😀e\u0301",
            "Native character ranges preserve supplementary characters and separate combining scalars");
        var selected = new UiAccessibilityTextInfo(content, 6, 2);
        check(selected.CaretCharacterOffset == 5 && selected.AnchorCharacterOffset == 2 && selected.HasSelection
            && selected.SelectionStartUtf16 == 2 && selected.SelectionEndUtf16 == 6,
            "Published text selection preserves its anchor direction and independent character offsets");
        var reversed = new UiAccessibilityTextInfo(content, 2, 6);
        check(reversed.SelectionStartUtf16 == 2 && reversed.SelectionEndUtf16 == 6 && reversed.CaretCharacterOffset == 2,
            "Reversed accessible selections retain caret direction while normalizing the selected range");
        foreach (var invalid in new[] { "\ud800", "\udc00", "A\ud800B" })
        {
            var rejected = false;
            try
            {
                _ = new UiAccessibilityTextContent(invalid);
            }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "Malformed UTF16 is rejected rather than substituted in accessible text");
        }
        foreach (var invalid in new Action[]
        {
            () => content.GetCharacterOffset(3),
            () => content.GetCharacterOffset(-1),
            () => content.GetUtf16Offset(9),
            () => content.GetText(5, 2),
            () => new UiAccessibilityTextInfo(content, 3, 0)
        })
        {
            var rejected = false;
            try
            {
                invalid();
            }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "Invalid accessible offsets and split surrogate pairs are rejected");
        }
        var empty = new UiAccessibilityTextContent("");
        check(empty.CharacterCount == 0 && empty.GetText(0, 0) == "" && empty.GetCharacterOffset(0) == 0,
            "Empty accessible text retains its valid insertion boundary");
        var bmp = new UiAccessibilityTextContent("정세일ABC");
        check(Enumerable.Range(0, bmp.Text.Length + 1).All(offset => bmp.GetUtf16Offset(offset) == offset),
            "BMP accessible text preserves identity offsets without supplementary character mapping");
        LiveInputs(check);
    }

    private static void LiveInputs(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var value = "가e\u0301Z";
        var memo = "첫 줄\n둘째 줄";
        var disabled = false;
        UiAccessibilityTextInfo? renderedText = null;
        void Inputs(UiImmediateContext ui)
        {
            ui.SetCursorScreenPos(new(5, 5));
            ui.BeginDisabled(disabled);
            ui.InputText("Input", ref value, 100, UiInputTextFlags.ReadOnly);
            renderedText = ui.GetAccessibilityTextInfo("Input");
            ui.EndDisabled();
            ui.SetCursorScreenPos(new(5, 60));
            ui.InputTextMultiline<UiFluentMultilineInputBehavior, UiFluentTextareaPresentation<UiFluentSmallTextareaSize, UiFluentOutlineInput>>("Memo", ref memo, 200, 280, 100);
        }
        void Frame(Action<UiImmediateContext>? render = null, UiInputState? pointer = null)
        {
            context.NewFrame(new(.1f, new(400, 260), new(1, 1)), pointer ?? idle, new(0, 0, 400, 260), UiTextSettings.Default);
            context.Render(new Screen(render ?? Inputs));
            context.GetDrawData().ReleasePooled();
        }
        UiAccessibilityElement Named(string name) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name);
        Frame();
        var input = Named("Input");
        var initial = input.Node.TextInfo ?? throw new InvalidOperationException("The actual input did not publish its text state.");
        var geometry = input.Node.TextLayout ?? throw new InvalidOperationException("The actual input did not publish its painted geometry.");
        var appearance = input.Node.TextAppearance ?? throw new InvalidOperationException("The actual input did not publish text paint.");
        check(ReferenceEquals(appearance.Content, initial.Content) && appearance.Foreground == context.GetStyle().InputText,
            "Actual plain input appearance uses its exact immutable content and supplied painter foreground");
        check(ReferenceEquals(geometry.Content, initial.Content), "Published character geometry uses the exact current immutable content");
        var firstBox = geometry.GetCharacterExtents(0);
        var cluster = geometry.GetCharacterExtents(1);
        check(firstBox.Width > 0 && firstBox.Height > 0 && firstBox.X >= input.Node.Bounds.X && firstBox.Y >= input.Node.Bounds.Y,
            "Actual input character boxes occupy the painted field rather than its label");
        check(cluster == geometry.GetCharacterExtents(2) && cluster.X >= firstBox.X + firstBox.Width - .01f,
            "Combining Unicode scalars share the actual grapheme's logical box");
        check(geometry.GetOffsetAtPoint(cluster.X + cluster.Width / 2, cluster.Y + cluster.Height / 2) == 1
            && geometry.GetOffsetAtPoint(-100, -100) == -1, "Character point lookup returns the containing grapheme's first scalar and rejects clipped exterior points");
        check(geometry.GetRangeExtents(1, 3) == cluster && geometry.GetRangeExtents(4, 4).Width == 0,
            "Range extents union logical boxes and empty ranges expose an insertion rectangle");
        check(initial.Content.Text == value && initial.CaretUtf16 == value.Length && !initial.HasSelection && input.Node.ReadOnly,
            "Actual readonly input publishes its committed text and real caret state");
        check(ReferenceEquals(renderedText, initial), "The render-frame text query returns the same immutable state later published by the tree");
        var multi = Named("Memo");
        check(ReferenceEquals(multi.Node.TextAppearance?.Content, multi.Node.TextInfo?.Content)
            && multi.Node.TextAppearance?.Foreground is not null, "Actual multiline paint publishes source-bound uniform foreground");
        var multiGeometry = multi.Node.TextLayout ?? throw new InvalidOperationException("The actual multiline input did not publish geometry.");
        check(multiGeometry.LineCount == 2 && multiGeometry.GetLine(0) == new UiAccessibilityTextLine(0, 3, 4)
            && multiGeometry.GetLine(1) == new UiAccessibilityTextLine(4, 8, 8),
            "Actual painted line ends exclude separators while next starts preserve their source extent");
        foreach (var lineIndex in new[] { -1, multiGeometry.LineCount })
        {
            var rejected = false;
            try
            {
                multiGeometry.GetLine(lineIndex);
            }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "Visual line lookup rejects indices outside its immutable snapshot");
        }
        check(multiGeometry.GetLineRangeAt(0) == (0, 4) && multiGeometry.GetLineRangeAt(4) == (4, 8),
            "Published actual visual lines include the hard newline in their source interval");
        check(multiGeometry.GetCharacterExtents(4).Y > multiGeometry.GetCharacterExtents(0).Y
            && multiGeometry.GetRangeExtents(0, 8).Height > multiGeometry.GetCharacterExtents(0).Height,
            "Multiline character and range boxes follow separate painted rows");
        check(multi.Node.Multiline && multi.Node.TextInfo?.Content.Text == memo,
            "Actual multiline input publishes the same committed multiline text as its value projection");
        var revision = tree.Revision;
        Frame();
        check(ReferenceEquals(Named("Input").Node.TextAppearance, appearance),
            "Unchanged actual text paint reuses its immutable appearance without republishing");
        check(ReferenceEquals(Named("Input").Node.TextLayout, geometry) && ReferenceEquals(Named("Memo").Node.TextLayout, multiGeometry),
            "Unchanged actual input frames reuse immutable character and visual-line geometry");
        check(ReferenceEquals(Named("Input").Node.TextInfo, initial) && tree.Revision == revision,
            "Unchanged input frames reuse the immutable text state without republishing or rebuilding its offset map");
        check(tree.RequestAction(input.Node.Id, input.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success,
            "Readonly text can receive accessibility focus before caret observation");
        Frame();
        context.State.SetCursor(input.Node.Id, 1);
        context.State.SetSelection(input.Node.Id, new(0, 1));
        revision = tree.Revision;
        Frame();
        var moved = Named("Input").Node.TextInfo!;
        check(moved.CaretUtf16 == 1 && moved.AnchorUtf16 == 0 && moved.HasSelection && tree.Revision > revision,
            "Actual caret and selection movement changes the published text state");
        check(ReferenceEquals(moved.Content, initial.Content), "Caret-only changes retain the text's one immutable Unicode offset map");
        revision = tree.Revision;
        Frame();
        check(ReferenceEquals(Named("Input").Node.TextInfo, moved) && tree.Revision == revision,
            "A stationary caret does not cause repeated text-state publications");
        var click = idle with
        {
            MousePosition = new(firstBox.X + firstBox.Width / 4, firstBox.Y + firstBox.Height / 2),
            LeftMouseDown = true,
            LeftMousePressed = true
        };
        Frame(pointer: click);
        Frame(pointer: click with
        {
            LeftMouseDown = false,
            LeftMousePressed = false,
            LeftMouseReleased = true
        });
        Frame();
        check(Named("Input").Node.Focused && Named("Input").Node.TextInfo?.CaretUtf16 == 0,
            "Clicking the actual first character after native focus preserves accessible keyboard focus and maps the matching caret");
        UiAccessibilityResult ApplySelection(UiAccessibilityElement target, UiAccessibilityTextInfo desired, Action? beforeFrame = null)
        {
            using var wake = new ManualResetEventSlim();
            tree.SetRequestFrameCallback(wake.Set);
            var operation = Task.Run(() => tree.RequestTextSelection(target.Node.Id, target.RuntimeId, desired));
            try
            {
                if (!wake.Wait(TimeSpan.FromSeconds(1)))
                    throw new TimeoutException("The external text selection request did not reach the frame queue.");
                beforeFrame?.Invoke();
                Frame();
                return operation.GetAwaiter().GetResult();
            }
            finally { context.SetAccessibilityTree(tree); }
        }
        check(moved.CanSetSelection && ApplySelection(input, new(moved.Content, 3, 1)) == UiAccessibilityResult.Success
            && Named("Input").Node.TextInfo is { CaretUtf16: 3, AnchorUtf16: 1 } && value == "가e\u0301Z",
            "An external readonly text selection commits real grapheme boundaries without editing text");
        check(ApplySelection(input, new(moved.Content, 2, 2)) == UiAccessibilityResult.InvalidArgument
            && Named("Input").Node.TextInfo is { CaretUtf16: 3, AnchorUtf16: 1 },
            "A native-style scalar offset inside a grapheme is rejected without moving the caret");
        var multilineInfo = Named("Memo");
        var focusedBefore = context.State.FocusedId;
        check(ApplySelection(multilineInfo, new(multilineInfo.Node.TextInfo!.Content, 2, 0)) == UiAccessibilityResult.Success
            && Named("Memo").Node.TextInfo is { CaretUtf16: 2, AnchorUtf16: 0 } && context.State.FocusedId == focusedBefore,
            "Multiline text selection changes real positions without stealing keyboard focus");
        check(ApplySelection(input, new(moved.Content, 0, 0), () => disabled = true) == UiAccessibilityResult.Disabled,
            "A control disabled after request admission rejects the pending text selection");
        check(tree.RequestTextSelection(input.Node.Id, input.RuntimeId, new(moved.Content, 0, 0), wait: false) == UiAccessibilityResult.Disabled,
            "Disabled text controls reject selection requests before queueing");
        disabled = false;
        Frame();
        check(ApplySelection(input, new(moved.Content, 0, 0), () => value = "다른 값") == UiAccessibilityResult.InvalidArgument
            && Named("Input").Node.TextInfo?.Content.Text == "다른 값",
            "Text changed after admission rejects old-content offsets while publishing the current model");
        value = "새 값";
        Frame();
        var edited = Named("Input").Node.TextInfo!;
        check(edited.Content.Text == value && !ReferenceEquals(edited.Content, initial.Content) && initial.Content.Text == "가e\u0301Z",
            "A committed text change creates a new map while older published text stays immutable");
        check(tree.RequestTextSelection(input.Node.Id, input.RuntimeId, new(initial.Content, 0, 0), wait: false) == UiAccessibilityResult.InvalidArgument,
            "Stale published content cannot authorize a new selection after the text changes");
        check(tree.RequestTextSelection(input.Node.Id, input.RuntimeId, new(new UiAccessibilityTextContent(value), 0, 0), wait: false)
            == UiAccessibilityResult.InvalidArgument, "Equivalent independently created content cannot bypass the published content identity");
        Frame(ui => renderedText = ui.GetAccessibilityTextInfo("Input"));
        check(renderedText is null, "The render-frame text query never returns stale data for a control omitted from this frame");
        var scopedText = "scoped";
        var wrongScope = false;
        Frame(ui =>
        {
            ui.PushID("query-scope");
            ui.InputText("Scoped", ref scopedText, 100);
            renderedText = ui.GetAccessibilityTextInfo("Scoped");
            ui.PopID();
            wrongScope = ui.GetAccessibilityTextInfo("Scoped") is null;
        });
        check(renderedText?.Content.Text == scopedText && wrongScope,
            "The render-frame text query resolves the exact current identifier scope");
        var query = new UiState<string>("정세일");
        using var search = Fluent.SearchBox("Search", query.Binding, options: new(Width: 280, ReadOnly: true));
        Frame(search.Render);
        check(Named("Search").Node.TextInfo?.Content.Text == query.Value,
            "SearchBox shares the authoritative text publication contract");
        query.Value = "서울";
        Frame(search.Render);
        check(Named("Search").Node.TextInfo?.Content.Text == query.Value,
            "An externally reconciled SearchBox publishes its accepted text and metadata together");
        check(ReferenceEquals(Named("Search").Node.TextAppearance?.Content, Named("Search").Node.TextInfo?.Content),
            "SearchBox reconciles actual painter appearance with its final source content");
        search.Options = search.Options with
        {
            ReadOnly = false
        };
        Frame(search.Render);
        var searchRoot = Named("Search");
        check(tree.RequestAction(searchRoot.Node.Id, searchRoot.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success,
            "SearchBox accepts native focus before its auxiliary clear action");
        Frame(search.Render);
        var clear = tree.CaptureSnapshot().Elements.Single(element => element.Node.ParentId == searchRoot.Node.Id && element.Node.Role == UiAccessibilityRole.Button);
        check(tree.RequestAction(clear.Node.Id, clear.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
            "SearchBox auxiliary clearing uses its actual native action path");
        Frame(search.Render);
        check(query.Value == "" && Named("Search").Node.TextInfo is { CaretUtf16: 0, AnchorUtf16: 0, Content.Text: "" },
            "Accepted SearchBox clearing publishes empty text and reset positions in the editing frame");
        check(Named("Search").Node.TextLayout is { Content.CharacterCount: 0 } emptyGeometry && emptyGeometry.GetRangeExtents(0, 0).Width == 0,
            "SearchBox clearing publishes current empty geometry instead of retaining old characters");
        var comboText = new UiState<string>("서울");
        var city = new UiState<string?>("seoul");
        using var combo = Fluent.Combobox("City", comboText.Binding, city.Binding, [new("seoul", "서울"), new("busan", "부산")]);
        Frame(combo.Render);
        var editor = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Edit);
        check(editor.Node.TextInfo?.Content.Text == editor.Node.Value,
            "Combobox editor publishes its reconciled text state on the actual editor child");
        var busan = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Option && element.Node.Name == "부산");
        check(tree.RequestAction(busan.Node.Id, busan.RuntimeId, UiAccessibilityAction.Select, wait: false) == UiAccessibilityResult.Success,
            "Closed Combobox options accept the existing native selection action");
        Frame(combo.Render);
        editor = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Edit);
        check(comboText.Value == "부산" && city.Value == "busan" && editor.Node.TextInfo is { Content.Text: "부산", CaretUtf16: 2 },
            "Native option selection reconciles accepted editor text, caret and published content together");
        check(ReferenceEquals(editor.Node.TextLayout?.Content, editor.Node.TextInfo?.Content),
            "Accepted Combobox text and character geometry share the final content instance");
        check(ReferenceEquals(editor.Node.TextAppearance?.Content, editor.Node.TextInfo?.Content),
            "Combobox child editor retains actual paint attributes only for its final source content");
        var number = new UiState<double?>(3);
        using var spin = Fluent.SpinButton("Quantity", number.Binding, new(Minimum: 0, Maximum: 100));
        Frame(spin.Render);
        var numeric = Named("Quantity");
        check(numeric.Node.TextInfo?.Content.Text == numeric.Node.Value && numeric.Node.Range?.Value == 3,
            "SpinButton retains separate coherent draft-text and numeric range projections");
        var increment = tree.CaptureSnapshot().Elements.Single(element => element.Node.ParentId == numeric.Node.Id && element.Node.Name == "Increment value");
        check(tree.RequestAction(increment.Node.Id, increment.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
            "SpinButton text reconciliation uses the actual native increment action");
        Frame(spin.Render);
        numeric = Named("Quantity");
        check(numeric.Node.TextLayout is { } spinGeometry && ReferenceEquals(spinGeometry.Content, numeric.Node.TextInfo?.Content),
            "Accepted SpinButton draft geometry follows the reconciled increment result");
        check(number.Value == 4 && numeric.Node.Range?.Value == 4 && numeric.Node.TextInfo?.Content.Text == numeric.Node.Value,
            "Accepted numeric increments keep draft text, immutable content and range in the same publication");
        Frame(ui =>
        {
            spin.Render(ui);
            ui.ReconcileNumericFocusCommit(numeric.Node.Id, "4", "4.00", numeric.Node.Range, false);
        });
        var committed = Named("Quantity").Node;
        check(committed.Value == "4.00" && committed.TextInfo is { Content.Text: "4.00", CaretUtf16: 4 } && committed.TextLayout is null,
            "A late numeric focus commit updates committed text and explicitly removes geometry drawn for the previous draft");
        check(committed.TextAppearance is null, "Late numeric reconciliation also removes appearance belonging to the preceding painted source");
        var originalInput = value;
        value = new string('W', 80);
        void NarrowInputFrame() => Frame(ui =>
        {
            ui.SetNextItemWidth(40);
            Inputs(ui);
        });
        NarrowInputFrame();
        var beforeScroll = Named("Input").Node.TextLayout!;
        context.State.SetScrollX(Named("Input").Node.Id, 9);
        NarrowInputFrame();
        var afterScroll = Named("Input").Node.TextLayout!;
        check(!ReferenceEquals(beforeScroll, afterScroll) && afterScroll.GetCharacterExtents(0).X == beforeScroll.GetCharacterExtents(0).X - 9
            && ReferenceEquals(beforeScroll.Content, afterScroll.Content),
            "Actual editor scrolling translates character geometry while reusing its immutable text content");
        value = originalInput;
        NarrowInputFrame();
        check(context.State.GetScrollX(Named("Input").Node.Id) == 0
            && Named("Input").Node.TextLayout!.GetCharacterExtents(0).X >= Named("Input").Node.Bounds.X,
            "Shortening an inactive input clamps a stale scroll offset before painting its source geometry");
        Frame();
        var memoBox = Named("Memo").Node.TextLayout!.GetCharacterExtents(0);
        var memoClick = idle with
        {
            MousePosition = new(memoBox.X + memoBox.Width / 4, memoBox.Y + memoBox.Height / 2),
            LeftMouseDown = true,
            LeftMousePressed = true
        };
        Frame(pointer: memoClick);
        Frame(pointer: memoClick with
        {
            LeftMouseDown = false,
            LeftMousePressed = false,
            LeftMouseReleased = true
        });
        Frame();
        check(Named("Memo").Node.Focused && Named("Memo").Node.TextInfo?.CaretUtf16 == 0,
            "Clicking the multiline character box establishes and retains accessible keyboard focus");
        var wrapped = "one two three four five six seven eight";
        Frame(ui => ui.InputTextMultiline<UiFluentMultilineInputBehavior, UiFluentTextareaPresentation<UiFluentSmallTextareaSize, UiFluentOutlineInput>>("Wrapped", ref wrapped, 100, 100, 100));
        var wrapLayout = Named("Wrapped").Node.TextLayout!;
        var wrapEnd = wrapLayout.GetLineRangeAt(0).End;
        check(wrapLayout.LineCount > 1 && wrapLayout.GetLine(0).End == wrapEnd
            && wrapLayout.GetLine(0).NextStart == wrapEnd && wrapLayout.GetLine(1).Start == wrapEnd,
            "Soft wraps share the exact source boundary without inventing a newline character");
        check(wrapEnd > 0 && wrapEnd < wrapped.Length && wrapLayout.GetLineRangeAt(wrapEnd).Start == wrapEnd
            && wrapLayout.GetCharacterExtents(wrapEnd).Y > wrapLayout.GetCharacterExtents(0).Y,
            "Actual soft wrapping publishes source line boundaries and the next painted row rather than only hard newlines");
        var theme = context.GetStyle();
        var oldForeground = appearance.Foreground;
        theme.InputText = new(17, 83, 201);
        context.SetTheme(theme);
        Frame();
        check(Named("Input").Node.TextAppearance?.Foreground == new UiColor(17, 83, 201)
            && appearance.Foreground == oldForeground,
            "Changing actual painter color publishes a new appearance while preserving old immutable snapshots");
        Frame(ui => ui.InputText<UiStandardTextInputBehavior, NoPaintPresentation>("Input", ref value, 100));
        check(Named("Input").Node.TextInfo is not null && Named("Input").Node.TextAppearance is null,
            "A custom presentation without source painting cannot reuse the preceding frame's appearance");
        context.SetAccessibilityTree(null);
        var plainText = "plain";
        Frame(ui =>
        {
            ui.InputText<ProbeBehavior, UiContextInputPresentation>("Plain", ref plainText, 100);
            renderedText = ui.GetAccessibilityTextInfo("Plain");
        });
        check(ProbeBehavior.Last.AccessibilityText is null && renderedText is null, "Rendering without accessibility does not construct or expose text metadata");
        tree.Dispose();
        check(geometry.GetCharacterExtents(0) == firstBox && multiGeometry.GetLineRangeAt(0) == (0, 4),
            "Published immutable geometry survives later scroll, reflow and tree disposal");
        check(initial.Content.GetText(0, initial.Content.CharacterCount) == "가e\u0301Z",
            "Previously captured immutable text remains readable after its tree is disposed");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private readonly struct ProbeBehavior : IUiTextInputBehavior
    {
        internal static UiInputEditResult Last;
        public static bool AccessibilityValue => true;
        public static UiInputEditResult Process(UiControlContext context, string id, ref string value, int maximum, in UiInputLayout layout)
            => Last = context.EditSingleLine(id, ref value, maximum, layout, false);
    }
    private readonly struct NoPaintPresentation : IUiInputPresentation
    {
        public static UiInputLayout Measure(UiControlContext context, string label, float width)
            => UiContextInputPresentation.Measure(context, label, width);
        public static void Paint(UiControlContext context, string label, in UiInputLayout layout, in UiInputEditResult edit)
        {
        }
    }
}

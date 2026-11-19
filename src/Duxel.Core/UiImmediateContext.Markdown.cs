namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void RenderMarkdown(string id, string label, UiBinding<string> binding, UiMultilineInputOwner owner,
        UiMarkdownTextSource source, UiMarkdownOptions options, bool viewer, UiMarkdownLinkOwner links, Action<Uri> navigate)
    {
        var text = UiMarkdownWidget.ValidateSource(binding.Value, options.MaxLength);
        var initial = text;
        var context = new UiControlContext(this);
        using var typography = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        var size = context.ConstrainToLayout(new(options.Width > 0 ? options.Width : context.AvailableWidth, options.Height));
        var origin = AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var root = ResolveId(id);
        var readOnly = viewer || options.ReadOnly;
        owner.Bind(this, root);
        var layout = new UiMultilineInputLayout(bounds, typography.LineHeight)
        {
            Content = new(bounds.X + 12, bounds.Y + 10, MathF.Max(0, bounds.Width - 24), MathF.Max(0, bounds.Height - 20)),
            Options = new() { Flags = readOnly ? UiInputTextFlags.ReadOnly : 0, Wrap = options.Wrap },
            Wrap = options.Wrap,
            InputOwner = owner,
            VisualLines = owner.Lines,
            StyledSource = source,
            StyledEditorId = root
        };
        var parent = _accessibilityParent;
        using var clip = context.Clip(bounds);
        try
        {
            ClaimAccessibilityField(root);
            ClaimFieldFocus(root, bounds);
            _accessibilityParent = root;
            var edit = context.EditNavigableMultiline(root, ref text, options.MaxLength, layout);
            if (text != initial && !readOnly)
            {
                binding.Value = text;
                var accepted = UiMarkdownWidget.ValidateSource(binding.Value, options.MaxLength);
                if (accepted != text)
                    edit = ReconcileMarkdownEdit(root, accepted, layout, edit);
                // An earlier consumer may already have painted this binding in the current frame.
                if (accepted != initial)
                    RequestFrameAt(_state.TimeSeconds);
                text = accepted;
            }
            var content = edit.VisualContent!.Value;
            var contentSize = new UiVector2(edit.ContentWidth, edit.ContentHeight);
            edit = ApplyMultilineAccessibilityScroll(root, layout, edit);
            if (viewer)
                edit = PrepareMarkdownLinkNavigation(root, source.Document, owner.Lines, edit, links, options);
            var state = !context.IsEnabled ? UiButtonVisualState.Disabled : edit.Active ? UiButtonVisualState.Pressed
                : edit.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
            var palette = UiFluentInputPalette.Resolve(context, state, UiInputAppearance.Outline, false);
            if (options.ShowBorder)
                UiFluentOutlineInput.Paint(context, bounds, palette, edit.Active && context.IsEnabled);
            PaintMultilineInput(layout, edit, new(palette.Text, context.Theme.InputSelectionBg, palette.Text)
            {
                Typography = true
            });
            CompleteMultilineInput(root, layout, edit, owner.Scrollbars);
            var scroll = new UiAccessibilityScrollInfo(new(content.Width, content.Height), contentSize, new(edit.ScrollX, edit.ScrollY));
            ExposeAccessibility(root, parent, UiAccessibilityRole.Edit, label, bounds, text, readOnly, options.MaxLength,
                multiline: true, scroll: CanPublishScroll(IntersectRect(CurrentClipRect, content), scroll) ? scroll : null,
                textInfo: edit.AccessibilityText, textLayout: CaptureMultilineTextLayout(root, edit.AccessibilityText, layout, edit));
            _accessibility?.CaptureScrollViewport(root, content, CurrentClipRect, scroll);
            if (viewer)
                ProcessMarkdownLinks(root, source.Document, edit, links, navigate);
            CaptureMarkdownImages(root, edit, viewer ? links : null);
        }
        finally
        {
            _accessibilityParent = parent;
            FinishLayoutBox(origin, size);
        }
    }

    private UiMultilineEditResult ReconcileMarkdownEdit(string id, string accepted, UiMultilineInputLayout layout, UiMultilineEditResult edit)
    {
        var buffer = _state.GetEditBuffer(id, accepted);
        buffer.Sync(accepted);
        var caret = buffer.SnapTextElement(Math.Min(edit.CaretIndex, accepted.Length));
        var selection = new UiTextSelection(caret, caret);
        _state.SetCursor(id, caret);
        _state.SetSelection(id, selection);
        var visual = layout.VisualLines!;
        var content = PrepareMultilineVisual(layout, accepted, visual, caret, edit.CompositionText);
        var displayed = string.IsNullOrEmpty(edit.CompositionText) ? visual : visual.Composition;
        var maxY = MathF.Max(0, displayed.Height - content.Height);
        var maxX = layout.Wrap && !displayed.HasUnwrappedOverflow ? 0 : MathF.Max(0, displayed.Width - content.Width);
        return edit with
        {
            Text = accepted,
            CaretIndex = caret,
            Selection = selection,
            ScrollX = Math.Clamp(edit.ScrollX, 0, maxX),
            ScrollY = Math.Clamp(edit.ScrollY, 0, maxY),
            MaximumScrollX = maxX,
            MaximumScrollY = maxY,
            ContentWidth = displayed.Width,
            ContentHeight = displayed.Height,
            VisualContent = content,
            VisualVersion = visual.Version,
            CompositionVersion = edit.CompositionLines?.Version ?? 0,
            AccessibilityText = CaptureAccessibilityText(id, accepted, caret, caret)
        };
    }
}

namespace Duxel.Core;

public readonly record struct UiTagPickerOptions(UiDropdownOptions? Field = null, float MinimumInputWidth = 120,
    int MaxLength = 4096, string EmptyText = "No matches", string DismissLabel = "Remove")
{
    public UiTagPickerOptions() : this(null, 120, 4096, "No matches", "Remove") { }
    /// <summary>An owned control placed between the input and the expand button.</summary>
    public UiWidget? SecondaryAction { get; init; }
    internal UiDropdownOptions FieldOptions => Field ?? new();
    internal void Validate()
    {
        new UiComboboxOptions(Field, MaxLength, EmptyText: EmptyText).Validate();
        if (!float.IsFinite(MinimumInputWidth) || MinimumInputWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(MinimumInputWidth));
        new UiTagOptions(DismissLabel: DismissLabel).Validate();
    }
}

/// <summary>A wrapping selected-token input sharing canonical selection, IME and popup ownership with Combobox.</summary>
public class UiTagPicker : UiArrangedChildrenWidget, IUiLabelTarget
{
    private sealed class Content
    {
        internal readonly UiOptionSelection Selection;
        internal readonly UiTagGroupInputState Tags = new();
        internal readonly UiComboboxCompletion Completion;
        internal readonly UiCombobox<UiFluentTagPickerInputPresentation> Editor;
        internal readonly UiWidget[] Children;
        internal readonly string[] Names;
        internal int EditorIndex => Names.Length;
        internal Action? Changed;
        internal Content(string label, UiBinding<string> query, UiBinding<string[]> selected, UiSelectOption[] items, UiTagPickerOptions options)
        {
            Selection = new(items, selected) { PreserveSelectionOrder = true };
            Completion = new(Tags) { SecondaryAction = options.SecondaryAction };
            Editor = new(label, query, Selection, new(options.FieldOptions with { Width = options.MinimumInputWidth },
                options.MaxLength, EmptyText: options.EmptyText)) { Completion = Completion };
            var tagSize = options.FieldOptions.Size == UiDropdownSize.Large ? UiTagSize.Medium
                : options.FieldOptions.Size == UiDropdownSize.Small ? UiTagSize.ExtraSmall : UiTagSize.Small;
            Children = new UiWidget[items.Length + (options.SecondaryAction is null ? 1 : 2)];
            Names = new string[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                var index = i;
                var name = "##tag-" + items[i].Value;
                var tag = new UiTag<UiFluentTagPresentation>(new UiTagContent(items[i].Text, Icon: items[i].Icon), () =>
                {
                    Selection.Synchronize();
                    Selection.Deselect(index);
                    if (Selection.Count == 0) Completion.FocusRequested = true;
                    Changed?.Invoke();
                }, new(tagSize, RightToLeft: options.FieldOptions.RightToLeft, DismissLabel: options.DismissLabel), UiWidgetIdentity.Parent)
                {
                    ItemId = name,
                    ItemEnabled = items[i].Enabled
                };
                Children[i] = tag;
                Names[i] = i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/" + name;
            }
            Children[EditorIndex] = Editor;
            if (options.SecondaryAction is { } action)
                Children[^1] = action;
            Tags.OptionIndices = Enumerable.Range(0, items.Length).ToArray();
        }
    }
    private readonly Content _content;
    private readonly UiTagPickerOptions _options;
    private int[] _order;
    private readonly string[] _navigationNames;
    private readonly int[] _navigationOrder;
    public string Label { get; }
    public UiBinding<string> Query => _content.Editor.Text;
    public bool IsOpen => _content.Editor.IsOpen;
    public UiBinding<bool>? Open { get => _content.Editor.Open; set => _content.Editor.Open = value; }
    public Action<bool>? OpenChanged { get => _content.Editor.OpenChanged; set => _content.Editor.OpenChanged = value; }
    public UiWidget LabelControl => _content.Editor;
    public UiTagPicker(string label, UiBinding<string> query, UiBinding<string[]> selected, UiSelectOption[] items,
        UiTagPickerOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : this(label, Create(label, query, selected, items, options ?? new()), options ?? new(), identity) { }
    private static Content Create(string label, UiBinding<string> query, UiBinding<string[]> selected, UiSelectOption[] items, UiTagPickerOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(query);
        options.Validate();
        return new(label, query, selected, items, options);
    }
    private UiTagPicker(string label, Content content, UiTagPickerOptions options, UiWidgetIdentity identity)
        : base(content.Children, identity)
    {
        Label = label;
        _content = content;
        _options = options;
        _order = new int[content.Children.Length];
        _navigationNames = new string[content.Names.Length];
        _navigationOrder = new int[content.Names.Length];
        Array.Fill(_navigationOrder, -1);
        content.Changed = RequestRebuild;
    }
    protected override bool CanRetainArrangement => false;
    protected override void PrepareChildren(UiControlContext context) => _content.Selection.Synchronize();
    protected override UiVector2 ArrangeChildren(UiControlContext context, Span<UiRect> bounds)
    {
        var field = _options.FieldOptions.WithInheritedDirection(context.RightToLeft);
        var width = Math.Clamp(field.Width, context.LayoutConstraints.MinWidth, context.LayoutConstraints.MaxWidth);
        var inner = MathF.Max(0, width - 8);
        var sideWidth = 0f;
        var arrowWidth = 0f;
        var actionSize = default(UiVector2);
        if (_content.Completion.SecondaryAction is { } action)
        {
            var arrow = UiFluentTagPickerInputPresentation.ExpandBounds(new(0, 0, inner, 0), field);
            arrowWidth = Math.Clamp(field.RightToLeft ? arrow.X + arrow.Width : inner - arrow.X, 0, inner);
            var minimumInput = Math.Clamp(_options.MinimumInputWidth, arrowWidth, inner);
            actionSize = context.MeasureConstrained(action, new(0, MathF.Max(0, inner - minimumInput - 4), 0,
                MathF.Max(0, context.LayoutConstraints.MaxHeight - 8)));
            sideWidth = arrowWidth + (actionSize.X > 0 ? actionSize.X + 4 : 0);
        }
        var inputLane = inner - sideWidth;
        var count = 0;
        foreach (var value in _content.Selection.Values)
            _order[count++] = Array.FindIndex(_content.Selection.Options, item => item.Value == value);
        var navigationChanged = false;
        var nav = 0;
        for (; nav < count; nav++)
        {
            var index = _order[nav];
            navigationChanged |= _navigationOrder[nav] != index;
            _navigationOrder[nav] = index;
            _navigationNames[nav] = _content.Names[index];
        }
        for (var index = 0; index < _content.Selection.Options.Length; index++)
        {
            if (_content.Selection.Selected[index]) continue;
            navigationChanged |= _navigationOrder[nav] != index;
            _navigationOrder[nav] = index;
            _navigationNames[nav++] = _content.Names[index];
        }
        if (navigationChanged) _content.Tags.Root = null;
        _content.Tags.OptionIndices = _navigationOrder;
        _order[count++] = _content.EditorIndex;
        bounds.Clear();
        var x = 0f;
        var y = 4f;
        var rowHeight = 0f;
        var rowStart = 0;
        void AlignRow(Span<UiRect> rectangles, int end)
        {
            for (var at = rowStart; at < end; at++)
            {
                var index = _order[at];
                var rect = rectangles[index];
                rectangles[index] = rect with { Y = rect.Y + (rowHeight - rect.Height) / 2 };
            }
        }
        for (var at = 0; at < count; at++)
        {
            var index = _order[at];
            var editor = index == _content.EditorIndex;
            var editorArrow = editor ? arrowWidth : 0;
            var size = context.MeasureConstrained(Children[index], new(0, inputLane + editorArrow, 0, float.PositiveInfinity));
            if (x > 0 && size.X - editorArrow > inputLane - x)
            {
                AlignRow(bounds, at);
                y += rowHeight + 4;
                x = rowHeight = 0;
                rowStart = at;
            }
            var allocatedWidth = editor ? MathF.Max(0, inputLane - x) + editorArrow : size.X;
            bounds[index] = new(4 + x, y, allocatedWidth, size.Y);
            x += allocatedWidth + 4;
            rowHeight = MathF.Max(rowHeight, size.Y);
        }
        AlignRow(bounds, count);
        var height = Math.Clamp(MathF.Max(y + rowHeight + 4, actionSize.Y + 8),
            context.LayoutConstraints.MinHeight, context.LayoutConstraints.MaxHeight);
        if (_content.Completion.SecondaryAction is not null)
        {
            var actionX = 4 + inputLane;
            bounds[^1] = new(field.RightToLeft ? width - actionX - actionSize.X : actionX,
                (height - actionSize.Y) / 2, actionSize.X, actionSize.Y);
        }
        if (field.RightToLeft)
            for (var at = 0; at < count; at++)
            {
                var index = _order[at];
                bounds[index] = bounds[index] with { X = width - bounds[index].X - bounds[index].Width };
            }
        return new(width, height);
    }
    protected override void RenderArrangedChildren(UiImmediateContext ui, ReadOnlySpan<UiRect> layout, UiRect root)
    {
        var field = _options.FieldOptions.WithInheritedDirection(ui.RightToLeft);
        ui.PaintTagPickerFrame(root, field, _content.Completion);
        _content.Completion.Anchor = root;
        if (_content.Completion.SecondaryAction is not null)
        {
            var side = layout[^1];
            _content.Completion.SecondaryBounds = side with { X = root.X + side.X, Y = root.Y + side.Y };
        }
        var scope = ui.BeginTagGroup(Label, root, _content.Selection, _content.Tags, _navigationNames, field.RightToLeft, true);
        try
        {
            for (var at = 0; at < _content.Selection.Count; at++)
            {
                var index = _order[at];
                var bounds = layout[index] with { X = root.X + layout[index].X, Y = root.Y + layout[index].Y };
                ui.RenderParentDataChild(Children[index], null, bounds, root);
            }
        }
        finally { ui.EndTagGroup(scope, _content.Tags, _content.Selection, true); }
        var editor = layout[_content.EditorIndex];
        var editorBounds = editor with { X = root.X + editor.X, Y = root.Y + editor.Y };
        ui.RenderParentDataChild(_content.Editor, null, editorBounds, root,
            clipToAllocation : _content.Completion.SecondaryAction is null);
    }
    private protected override void ReleaseOwnedResources()
    {
        _content.Changed = null;
        _content.Tags.Ids = [];
        _content.Tags.OptionIndices = [];
        _order = [];
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiTagPicker))]
[UiComponent("TagPicker", UiComponentExposure.Fluent)]
public partial class UiFluentTagPicker;

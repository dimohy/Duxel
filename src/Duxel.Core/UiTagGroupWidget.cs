namespace Duxel.Core;

internal sealed class UiTagGroupInputState
{
    internal Action? Changed;
    internal string? Root;
    internal string[] Ids = [];
    internal int[] OptionIndices = [];
    internal int RememberedIndex = -1;
}

/// <summary>Projects one canonical multi-selection through the existing wrap layout without gaps for unselected options.</summary>
public class UiTagGroup<TPresentation> : UiWrapWidget<UiHorizontalAxis> where TPresentation : IUiTagPresentation
{
    private UiOptionSelection? _selection;
    private UiOptionSelection? _emphasis;
    private readonly UiTagGroupInputState _input;
    private string[] _names;
    private readonly bool _actionable;
    private UiTagOptions _tagOptions;
    public string Label
    {
        get;
    }
    public UiTagOptions TagOptions
    {
        get => _tagOptions;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_tagOptions == value)
                return;
            ThrowIfBuilding();
            _tagOptions = value;
            Options = new(Spacing: value.Spacing, RunSpacing: value.Spacing);
            foreach (var tag in Children)
                ((UiTag<TPresentation>)tag).Options = value;
            RequestRebuild();
        }
    }
    public UiTagGroup(string label, UiBinding<string[]> selection, UiSelectOption[] items, UiTagOptions? options = null,
        bool dismissible = true, UiWidgetIdentity identity = UiWidgetIdentity.Instance, Action<string>? activated = null,
        UiBinding<string[]>? emphasized = null, Func<UiSelectOption, UiTagMedia>? media = null)
        : this(label, new UiOptionSelection(items, selection), emphasized is null ? null : new UiOptionSelection(items, emphasized),
            options ?? new(), dismissible, activated, media, new(), identity)
    {
    }
    private UiTagGroup(string label, UiOptionSelection selection, UiOptionSelection? emphasis, UiTagOptions options, bool dismissible,
        Action<string>? activated, Func<UiSelectOption, UiTagMedia>? media, UiTagGroupInputState input, UiWidgetIdentity identity)
        : base(CreateTags(selection, emphasis, options, dismissible, activated, media, input), new(Spacing: options.Spacing, RunSpacing: options.Spacing), identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        options.Validate();
        Label = label;
        _selection = selection;
        _emphasis = emphasis;
        _tagOptions = options;
        _actionable = dismissible || activated is not null || emphasis is not null;
        _input = input;
        input.Changed = RequestRebuild;
        var names = new List<string>();
        var indices = new List<int>();
        for (var i = 0; i < Children.Length; i++)
        {
            var value = selection.Options[i].Value;
            var name = "##tag-" + value.Length + ":" + value;
            ((UiTag<TPresentation>)Children[i]).ItemId = name;
            var scoped = i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/" + name;
            names.Add(activated is null && emphasis is null ? scoped : scoped + "##primary");
            indices.Add(i);
            if ((activated is not null || emphasis is not null) && dismissible)
            {
                names.Add(scoped + "##dismiss");
                indices.Add(i);
            }
        }
        _names = names.ToArray();
        input.OptionIndices = indices.ToArray();
    }
    private static UiWidget[] CreateTags(UiOptionSelection selection, UiOptionSelection? emphasis, UiTagOptions options, bool dismissible,
        Action<string>? activated, Func<UiSelectOption, UiTagMedia>? media, UiTagGroupInputState input)
    {
        options.Validate();
        if ((activated is not null || emphasis is not null) && !TPresentation.SupportsInteraction)
            throw new NotSupportedException("The selected presentation does not support interactive tag groups.");
        var tags = new UiWidget[selection.Options.Length];
        for (var i = 0; i < tags.Length; i++)
        {
            var index = i;
            var item = selection.Options[index];
            Action? dismiss = dismissible ? () => { selection.Synchronize(); selection.Deselect(index); input.Changed?.Invoke(); }
            : null;
            var content = new UiTagContent(item.Text, item.Description, item.Icon, media?.Invoke(item) ?? default);
            UiTag<TPresentation> tag = activated is null && emphasis is null
                ? new UiTag<TPresentation>(content, dismiss, options, UiWidgetIdentity.Parent)
                : new UiInteractionTag<TPresentation>(content, () => { activated?.Invoke(item.Value); input.Changed?.Invoke(); }, dismiss, options, UiWidgetIdentity.Parent);
            if (emphasis is not null)
            {
                ((UiInteractionTag<TPresentation>)tag).Selected = new(
                    () => emphasis.Selected[index],
                    value =>
                    {
                        emphasis.Synchronize();
                        if (value)
                            emphasis.Select(index, add: true);
                        else
                            emphasis.Deselect(index);
                    });
            }
            tag.ItemEnabled = item.Enabled;
            tags[i] = tag;
        }
        return tags;
    }
    protected override bool CanRetainArrangement => false;
    protected override bool ShouldArrangeChild(int index) => _selection!.Selected[index];
    protected override bool ShouldRenderChild(int index) => _selection!.Selected[index];
    protected override UiVector2 ArrangeChildren(UiControlContext context, Span<UiRect> bounds)
    {
        _selection!.Synchronize();
        _emphasis?.Synchronize();
        var size = base.ArrangeChildren(context, bounds);
        // Wrap already mirrors its allocation under an RTL provider.
        if (_tagOptions.RightToLeft && !context.RightToLeft)
            for (var i = 0; i < bounds.Length; i++)
                if (_selection.Selected[i])
                    bounds[i] = bounds[i] with
                    {
                        X = size.X - bounds[i].X - bounds[i].Width
                    };
        return size;
    }
    protected override void RenderArrangedChildren(UiImmediateContext ui, ReadOnlySpan<UiRect> layout, UiRect root)
    {
        var scope = ui.BeginTagGroup(Label, root, _selection!, _input, _names, _tagOptions.RightToLeft || ui.RightToLeft, _actionable);
        try
        {
            base.RenderArrangedChildren(ui, layout, root);
        }
        finally { ui.EndTagGroup(scope, _input, _selection!, _actionable); }
    }
    private protected override void ReleaseOwnedResources()
    {
        _input.Changed = null;
        _input.Ids = [];
        _input.OptionIndices = [];
        _input.Root = null;
        _input.RememberedIndex = -1;
        _selection = null;
        _emphasis = null;
        _names = [];
        base.ReleaseOwnedResources();
    }
}

/// <summary>Selected values rendered as Fluent tags; TagGroup is composition under the official Tag component.</summary>
public sealed class UiFluentTagGroup(string label, UiBinding<string[]> selection, UiSelectOption[] items,
    UiTagOptions? options = null, bool dismissible = true, UiWidgetIdentity identity = UiWidgetIdentity.Instance, Action<string>? activated = null,
    UiBinding<string[]>? emphasized = null, Func<UiSelectOption, UiTagMedia>? media = null)
    : UiTagGroup<UiFluentTagPresentation>(label, selection, items, options, dismissible, identity, activated, emphasized, media);

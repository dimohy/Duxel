namespace Duxel.Core;

/// <summary>A top-level menu and the commands it presents.</summary>
public sealed class UiMenuBarEntry
{
    public UiMenuBarEntry(string label, ReadOnlySpan<UiMenuOption> items, Action<string> invoked, UiKey accessKey = UiKey.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(invoked);
        if (items.IsEmpty)
            throw new ArgumentException("A top-level menu requires at least one item.", nameof(items));
        if (accessKey != UiKey.None && accessKey is < UiKey.A or > UiKey.Z)
            throw new ArgumentOutOfRangeException(nameof(accessKey));
        Label = label;
        AccessKey = accessKey;
        Items = UiMenuItems.Validate(items);
        Invoked = invoked;
    }

    public string Label { get; }
    public UiKey AccessKey { get; }
    internal UiMenuOption[] Items { get; }
    internal Action<string> Invoked { get; }
}

/// <summary>A group of Fluent menus that share one keyboard tab stop.</summary>
public class UiMenuBar<TPresentation> : UiControlWidget where TPresentation : IUiMenuPresentation
{
    private readonly UiMenuBarEntry[] _entries;
    private readonly UiMenuInputState[] _menus;
    private readonly UiRect[] _bounds;
    private readonly UiButtonInteraction[] _interactions;
    private readonly string[] _ids;
    private int _selected;
    private UiVector2 _lastPointer;

    public UiMenuBar(string label, params UiMenuBarEntry[] entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Length == 0 || Array.Exists(entries, static entry => entry is null))
            throw new ArgumentException("A menu bar requires at least one top-level menu.", nameof(entries));

        var accessKeys = new HashSet<UiKey>();
        foreach (var entry in entries)
            if (entry.AccessKey != UiKey.None && !accessKeys.Add(entry.AccessKey))
                throw new ArgumentException("Top-level menu access keys must be unique.", nameof(entries));
        Label = label;
        _entries = (UiMenuBarEntry[])entries.Clone();
        _menus = Array.ConvertAll(_entries, static _ => new UiMenuInputState { Entrance = new() });
        _bounds = new UiRect[entries.Length];
        _interactions = new UiButtonInteraction[entries.Length];
        _ids = Enumerable.Range(0, entries.Length).Select(index => label + "##top-menu-" + index).ToArray();
    }

    public string Label { get; }

    internal static UiVector2 MeasureEntry(UiControlContext context, string label)
    {
        var text = context.MeasureText(label);
        return new(MathF.Max(40, text.X + 20), MathF.Max(32, text.Y + 10));
    }

    internal UiVector2 MeasureBar(UiControlContext context)
    {
        var size = new UiVector2(4, 32);
        foreach (var entry in _entries)
        {
            var item = MeasureEntry(context, entry.Label);
            size = new(size.X + item.X, MathF.Max(size.Y, item.Y));
        }
        return size;
    }

    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        using var typography = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        return MeasureBar(context);
    }

    protected override void RenderControl(UiImmediateContext ui) =>
        ui.RenderMenuBar(PrimaryInputId(Label), Label, _entries, _menus, _bounds, _interactions, _ids,
            ref _selected, ref _lastPointer, this);

    private protected override void ReleaseOwnedResources()
    {
        foreach (var root in _menus)
            for (UiMenuInputState? menu = root; menu is not null; menu = menu.Child)
            {
                menu.Open = false;
                menu.ScrollOwner.Release();
                menu.Entrance?.Reset();
            }
        base.ReleaseOwnedResources();
    }
}


[UiWidgetStyle(typeof(UiMenuBar<>), typeof(UiFluentMenuPresentation))]
[UiComponent("MenuBar", UiComponentExposure.Fluent)]
public partial class UiFluentMenuBar;

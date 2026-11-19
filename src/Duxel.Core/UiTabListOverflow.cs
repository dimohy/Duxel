namespace Duxel.Core;

public readonly record struct UiTabListOverflowOptions(int MinimumVisible = 2, string Label = "more tabs")
{
    public UiTabListOverflowOptions() : this(2, "more tabs") { }
    public UiTabListOverflowOptions Validate()
    {
        if (MinimumVisible < 1) throw new ArgumentOutOfRangeException(nameof(MinimumVisible));
        ArgumentException.ThrowIfNullOrWhiteSpace(Label);
        return this;
    }
}

[UiFluentIcon("more_horizontal", UiFluentIconStyle.Interactive, 20)]
internal readonly partial struct UiFluentMoreTabsIcon;

public readonly partial struct UiFluentTabListPresentation
{
    public static UiVector2 MeasureOverflow(UiControlContext context, UiTabListOptions options)
    {
        var edge = options.Size == UiTabListSize.Small ? 24 : options.Size == UiTabListSize.Large ? 40 : 32;
        return new(edge, edge);
    }
    public static void PaintOverflow(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, UiTabListOptions options)
    {
        using var clip = context.Clip(bounds);
        var color = UiFluentTransparentButtonAppearance.Paint(context, bounds, interaction);
        UiFluentMoreTabsIcon.Paint(context, new(bounds.X + (bounds.Width - 20) * .5f, bounds.Y + (bounds.Height - 20) * .5f, 20, 20), color, interaction);
    }
}

public partial class UiTabList<TBehavior, TPresentation> where TBehavior : IUiTabListBehavior where TPresentation : IUiTabListPresentation
{
    private UiOverflowItem[] _overflowItems = [];
    private bool[] _visibleTabs = [];
    private UiMenuOption[] _overflowChoices = [];
    private readonly UiMenuInputState _overflowMenu = new();
    private UiRect _overflowBounds;
    private UiRect _headerBounds;
    public UiOverflowResult? OverflowResult { get; private set; }

    private UiVector2 MeasureTabControl(UiControlContext context, string? selected)
    {
        var size = MeasureTabs(context, selected);
        if (Options.Overflow is null) return size;
        var indicator = TPresentation.MeasureOverflow(context, EffectiveOptions(context));
        return Options.Vertical ? size with { X = MathF.Max(size.X, indicator.X) } : size with { Y = MathF.Max(size.Y, indicator.Y) };
    }

    private void ArrangeOverflow(UiControlContext context, UiVector2 size, string? selected)
    {
        _headerBounds = new(0, 0, size.X, size.Y);
        if (Options.Overflow is not { } options)
        {
            _input.Visibility = null;
            _input.HeaderClip = null;
            _input.OverflowFocused = false;
            OverflowResult = null;
            _overflowBounds = default;
            return;
        }
        if (_overflowItems.Length != _items.Length)
        {
            _overflowItems = new UiOverflowItem[_items.Length];
            _visibleTabs = new bool[_items.Length];
        }
        var indicator = TPresentation.MeasureOverflow(context, EffectiveOptions(context));
        var main = Options.Vertical ? size.Y : size.X;
        var rightToLeft = EffectiveOptions(context).RightToLeft;
        var extent = Options.Vertical ? indicator.Y : indicator.X;
        for (var i = 0; i < _items.Length; i++)
            _overflowItems[i] = new(Options.Vertical ? _layouts[i].Height : _layouts[i].Width, _items[i].Value == selected ? 2 : 1);
        var result = UiOverflowLayout.Resolve(_overflowItems, main, extent, _visibleTabs,
            new(Spacing : Options.Gap, MinimumVisible : options.MinimumVisible));
        OverflowResult = result;
        _input.Visibility = _visibleTabs;
        double cursor = 0;
        var count = 0;
        for (var i = 0; i < _items.Length; i++)
        {
            if (!_visibleTabs[i])
            {
                _layouts[i] = default;
                continue;
            }
            if (count++ != 0) cursor += Options.Gap;
            var rect = _layouts[i];
            _layouts[i] = Options.Vertical ? new(0, (float)cursor, size.X, rect.Height)
                : new(rightToLeft ? main - (float)cursor - rect.Width : (float)cursor, 0, rect.Width, size.Y);
            cursor += Options.Vertical ? rect.Height : rect.Width;
        }
        if (result.IsOverflowing)
        {
            var start = MathF.Min((float)cursor + (count > 0 ? Options.Gap : 0), MathF.Max(0, main - result.IndicatorExtent));
            _overflowBounds = Options.Vertical
                ? new((size.X - indicator.X) * .5f, start, indicator.X, result.IndicatorExtent)
                : new(rightToLeft ? main - start - result.IndicatorExtent : start, (size.Y - indicator.Y) * .5f, result.IndicatorExtent, indicator.Y);
            var headerExtent = MathF.Max(0, start - Options.Gap);
            _headerBounds = Options.Vertical ? new(0, 0, size.X, headerExtent)
                : new(rightToLeft ? main - headerExtent : 0, 0, headerExtent, size.Y);
        }
        else
        {
            _overflowBounds = default;
            _input.OverflowFocused = false;
        }
        if (result.IsOverflowing && (_headerBounds.Width <= 0 || _headerBounds.Height <= 0)) _input.OverflowFocused = true;
        var matches = _overflowChoices.Length == result.HiddenCount;
        var choiceIndex = 0;
        for (var i = 0; i < _items.Length; i++)
            if (!_visibleTabs[i])
            {
                if (matches && _overflowChoices[choiceIndex] != new UiMenuOption(_items[i].Value, _items[i].Label, _items[i].Enabled)) matches = false;
                choiceIndex++;
            }
        if (!matches)
        {
            var focused = (uint)_overflowMenu.Focused < (uint)_overflowChoices.Length ? _overflowChoices[_overflowMenu.Focused].Value : null;
            _overflowChoices = new UiMenuOption[result.HiddenCount];
            choiceIndex = 0;
            for (var i = 0; i < _items.Length; i++)
                if (!_visibleTabs[i]) _overflowChoices[choiceIndex++] = new(_items[i].Value, _items[i].Label, _items[i].Enabled);
            _overflowMenu.Focused = Array.FindIndex(_overflowChoices, item => item.Enabled && item.Value == focused);
            _overflowMenu.PressedValue = null;
            _overflowMenu.KeyboardPressed = false;
        }
    }

    private void ReleaseOverflow()
    {
        _overflowItems = [];
        _visibleTabs = [];
        _overflowChoices = [];
        _overflowMenu.Open = false;
        _overflowMenu.ScrollOwner.Release();
        _overflowMenu.ItemIds = [];
        _overflowMenu.IdentityItems = null;
        _input.Visibility = null;
        OverflowResult = null;
    }
}

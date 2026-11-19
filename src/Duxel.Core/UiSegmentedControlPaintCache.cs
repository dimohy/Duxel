namespace Duxel.Core;

public partial class UiSegmentedControlWidget
{
    internal readonly UiPaintGroup PaintGroup = new();
    internal Action<UiControlContext> PaintContent { get; }
    private PaintKey? _paintKey;
    private ulong _paintVersion;
    private ulong[] _iconVersions = [];
    private UiRadioGroupInteraction _paintInteraction;
    internal int PaintCaptureCount { get; private set; }
    private readonly record struct PaintKey(int Layout, ulong Content, UiSegmentedControlOptions Options,
        UiFluentColorScheme Scheme, bool Enabled, UiColor Text, UiColor DisabledText, UiColor Background,
        UiColor Border, UiColor? Highlight, UiColor? HighlightText, UiRect Thumb, int Selected,
        int Focused, int HoveredIcon, int HeldIcon);

    internal ulong PreparePaint(UiControlContext context, UiRect thumb, int selected, UiRadioGroupInteraction interaction)
    {
        var theme = context.Theme;
        int IconIndex(int index) => index >= 0 && !_items[index].Icon.IsEmpty ? index : -1;
        var key = new PaintKey(LayoutBuildCount, ContentVersion, Options, context.FluentColorScheme, context.IsEnabled,
            theme.Text, theme.TextDisabled, theme.WindowBg, theme.Border, theme.ForcedColors?.Highlight,
            theme.ForcedColors?.HighlightText, thumb, selected, interaction.Control.FocusVisible ? interaction.FocusedIndex : -1,
            IconIndex(interaction.HoveredIndex), IconIndex(interaction.HeldIndex));
        var changed = _paintKey != key;
        if (_iconVersions.Length != _items.Length)
        {
            _iconVersions = new ulong[_items.Length];
            changed = true;
        }
        for (var i = 0; i < _items.Length; i++)
        {
            // Unversioned custom painters may depend on external state and cannot promise stable snapshots.
            if (!_items[i].Icon.TryGetContentVersion(out var version) || _iconVersions[i] != version) changed = true;
            _iconVersions[i] = version;
        }
        _paintKey = key;
        _paintInteraction = interaction;
        if (changed) _paintVersion++;
        return _paintVersion;
    }
    private void PaintCachedContent(UiControlContext context)
    {
        var key = _paintKey!.Value;
        PaintCaptureCount++;
        UiSegmentedControlPainter.Paint(context, this, new(0, 0, ContentWidth, key.Thumb.Height > 0 ? key.Thumb.Height : _size.Y),
            key.Thumb, key.Selected, _paintInteraction);
    }
}

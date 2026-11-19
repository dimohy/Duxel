namespace Duxel.Core;

/// <summary>Compiled menu icon pair. Enabled hover selects the filled glyph.</summary>
public readonly record struct UiMenuIcon
{
    internal float SizeHint
    {
        get; init;
    }
    private readonly UiCompiledIconPainter? _paint;
    private readonly Func<ulong>? _readContentVersion;
    private readonly ulong? _contentVersion;
    internal UiMenuIcon(UiCompiledIconPainter paint, Func<ulong>? readContentVersion = null)
        => (_paint, _readContentVersion) = (paint, readContentVersion);
    /// <summary>Promises stable paint content for this version. Supply a new icon/version and request a frame when external inputs change.</summary>
    public UiMenuIcon WithContentVersion(ulong version) => new(_paint, version) { SizeHint = SizeHint };
    private UiMenuIcon(UiCompiledIconPainter? paint, ulong version)
        => (_paint, _contentVersion) = (paint, version);
    internal bool TryGetContentVersion(out ulong version)
    {
        version = _readContentVersion?.Invoke() ?? _contentVersion ?? 0;
        return IsEmpty || _readContentVersion is not null || _contentVersion.HasValue;
    }
    public bool IsEmpty => _paint is null;
    public static UiMenuIcon Create<TRegular, TFilled>()
        where TRegular : IUiButtonIcon where TFilled : IUiButtonIcon => new(UiCompiledIconPair<TRegular, TFilled>.Cached);
    public void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction)
    {
        if (_paint is null)
            throw new InvalidOperationException("An empty menu icon cannot be painted.");
        using var clip = context.Clip(bounds);
        _paint(context, bounds, color, context.IsEnabled && interaction.Hovered, interaction);
    }
}

namespace Duxel.Core;

/// <summary>A tooltip target owned by a compound control's stable sub-control identity.</summary>
internal sealed class UiControlTooltip : IUiTooltipTarget, IDisposable
{
    private WeakReference<UiImmediateContext>? _context;
    public string Text { get; set; } = string.Empty;
    public UiTooltipOptions Options
    {
        get; set;
    }
    public bool? Visible => null;
    public Action<UiTooltipVisibilityChange>? VisibleChangeRequested => null;
    public bool IsAlive { get; private set; } = true;
    public bool IsOpen
    {
        get; set;
    }
    public UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport, UiTooltipOptions options)
        => UiFluentTooltipPresentation.Arrange(context, text, target, viewport, options);
    public void Paint(UiControlContext context, string text, UiPopupLayout layout)
        => UiFluentTooltipPresentation.Paint(context, text, layout);
    internal void Attach(UiImmediateContext ui)
    {
        if (_context is not null && _context.TryGetTarget(out var previous) && !ReferenceEquals(previous, ui))
            previous.ReleaseTooltip(this);
        _context ??= new(ui);
        _context.SetTarget(ui);
    }
    public void Dispose()
    {
        if (_context is not null && _context.TryGetTarget(out var ui))
            ui.ReleaseTooltip(this);
        IsAlive = IsOpen = false;
        Text = string.Empty;
        _context = null;
    }
}

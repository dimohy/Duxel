namespace Duxel.Core;

public sealed partial class UiState
{
    internal bool RepaintHighlights;
}

public sealed partial class UiImmediateContext
{
    /// <summary>Highlights renderer damage without inserting diagnostic geometry into the scene.</summary>
    public void SetRepaintHighlights(bool enabled) => _state.RepaintHighlights = enabled;
    internal bool RepaintHighlights => _state.RepaintHighlights;
}

public sealed partial record class UiDrawData
{
    public bool RepaintHighlights
    {
        get; init;
    }
    public bool RepaintDiagnosticCleanup
    {
        get; init;
    }
}

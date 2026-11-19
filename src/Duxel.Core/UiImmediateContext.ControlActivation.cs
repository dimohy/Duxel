namespace Duxel.Core;

// Opening a portal must remember its invoking control independently of keyboard
// focus. Accessibility activation can invoke a button while another control is
// focused, and ordinary invocation must not steal that focus as a side effect.
internal readonly record struct UiControlActivationOrigin(object ContextToken, string Id, int Tier, int Frame);

public sealed partial class UiImmediateContext
{
    // A pending, unmounted portal may retain this token, never the UI context.
    private readonly object _controlActivationToken = new();
    internal UiControlActivationOrigin? ControlActivationOrigin
    {
        get; private set;
    }

    internal ControlActivationScope BeginControlActivation(string id)
        => new(this, new(_controlActivationToken, ResolveId(id), _popupTierDepth, GetFrameCount()));

    internal readonly ref struct ControlActivationScope
    {
        private readonly UiImmediateContext _context;
        private readonly UiControlActivationOrigin? _previous;

        internal ControlActivationScope(UiImmediateContext context, UiControlActivationOrigin origin)
        {
            _context = context;
            _previous = context.ControlActivationOrigin;
            context.ControlActivationOrigin = origin;
        }

        public void Dispose() => _context.ControlActivationOrigin = _previous;
    }
}

namespace Duxel.Core;

/// <summary>Retains a layout identity for inherited inputs without retaining a UI context.</summary>
internal sealed class UiLayoutEnvironment<TInput> where TInput : struct, IEquatable<TInput>
{
    private object? _parent;
    private object? _resources;
    private TInput _input;

    internal object Resolve(object parent, TInput input)
    {
        if (!ReferenceEquals(_parent, parent) || !_input.Equals(input))
        {
            _parent = parent;
            _input = input;
            _resources = new object();
        }
        return _resources!;
    }
}

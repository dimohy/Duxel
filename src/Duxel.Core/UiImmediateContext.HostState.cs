namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    /// <summary>Native top-level host activation for the current captured frame.</summary>
    public bool IsHostWindowActive { get; internal set; } = true;
}

namespace Duxel.Core;

/// <summary>Automatic overlay snapshots submitted so far in the current UI frame.</summary>
public readonly record struct UiOverlayLayerMetrics(int ReusedLists, int RebuiltLists);

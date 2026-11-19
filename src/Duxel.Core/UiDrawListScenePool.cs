using System.Collections.Generic;

namespace Duxel.Core;

// Only frame-local wrappers are pooled. Geometry and cached templates remain
// immutable. UiDrawData owns leases; public draw-list record copies do not.
internal sealed class UiDrawListScenePool(UiDrawList template)
{
    internal sealed class Lease(UiDrawListScenePool owner, UiDrawList list)
    {
        public readonly UiDrawListScenePool Owner = owner;
        public readonly UiDrawList List = list;
        public bool InUse;
        internal void Return()
        {
            lock (Owner._gate)
            {
                if (!InUse) return;
                InUse = false;
                if (Owner._available.Count < 8) Owner._available.Push(this);
            }
        }
    }
    private readonly object _gate = new();
    private readonly Stack<Lease> _available = new(4);
    internal UiDrawList Template { get; } = template;

    internal Lease Rent(UiLayer3D? scene)
    {
        lock (_gate)
        {
            Lease lease;
            if (_available.Count > 0) lease = _available.Pop();
            else
            {
                var list = Template with { Scene3D = scene };
                lease = new(this, list);
            }
            lease.List.SetLeasedScene3D(scene);
            lease.InUse = true;
            return lease;
        }
    }
}

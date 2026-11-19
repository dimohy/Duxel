namespace Duxel.Core;

public sealed partial class UiToastController
{
    private HostLease? _hostLease;
    private bool _restoreHostCapacity;

    internal IDisposable AcquireHost()
    {
        BeginChange();
        if (_hostLease is not null)
            throw new InvalidOperationException("A toast controller can be attached to only one live toaster host.");
        return _hostLease = new(this);
    }

    private void ReleaseHost(HostLease lease)
    {
        if (!ReferenceEquals(_hostLease, lease))
            throw new InvalidOperationException("The toaster host does not own this controller lease.");
        _hostLease = null;
        if (_notifying)
        {
            // Disposal is a lifecycle operation, not a recursive controller mutation.
            // Finish the current notification before restoring its viewport capacity.
            _restoreHostCapacity = true;
            return;
        }
        SetViewportCapacity(Limit);
    }

    private void CompleteDeferredHostRelease()
    {
        if (!_restoreHostCapacity)
            return;
        _restoreHostCapacity = false;
        SetViewportCapacity(Limit);
    }

    private sealed class HostLease(UiToastController controller) : IDisposable
    {
        private UiToastController? _controller = controller;

        public void Dispose()
        {
            if (_controller is not { } owner)
                return;
            _controller = null;
            owner.ReleaseHost(this);
        }
    }
}

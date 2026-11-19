using System.Runtime.InteropServices;

namespace Duxel.Platform.Linux;

public sealed unsafe partial class LinuxX11Clipboard
{
    private readonly Dictionary<int, ReadOperation> _reads = [];
    private bool _wakeClosed;

    private sealed class ReadOperation : IDisposable
    {
        private readonly LinuxX11Clipboard _owner;
        internal int Descriptor
        {
            get;
        }
        internal TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal ReadOperation(LinuxX11Clipboard owner)
        {
            _owner = owner;
            lock (owner._gate)
            {
                owner.CheckState();
                if (owner._reads.Count >= owner._options.MaximumConcurrentTransfers)
                    throw new InvalidOperationException("The clipboard read limit was reached.");
                Descriptor = X11Native.eventfd(0, 0x800 | 0x80000);
                if (Descriptor < 0)
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Clipboard read eventfd failed.");
                owner._reads.Add(Descriptor, this);
            }
        }

        internal void Check(long deadline)
        {
            lock (_owner._gate)
                _owner.CheckState();
            Remaining(deadline);
        }

        public void Dispose()
        {
            lock (_owner._gate)
            {
                _owner._reads.Remove(Descriptor);
                X11Native.close(Descriptor);
            }
            Finished.SetResult();
        }
    }

    private static void Signal(int descriptor)
    {
        ulong signal = 1;
        if (X11Native.write(descriptor, &signal, 8) != 8 && Marshal.GetLastPInvokeError() != 11)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Clipboard wake failed.");
    }
}

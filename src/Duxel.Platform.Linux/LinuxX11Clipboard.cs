using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using Duxel.Core;
using static Duxel.Platform.Linux.XcbNative;

namespace Duxel.Platform.Linux;

public sealed record LinuxX11ClipboardOptions
{
    public string Selection { get; init; } = "CLIPBOARD";
    public string? DisplayName
    {
        get; init;
    }
    public TimeSpan TransferTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public int MaximumBytes { get; init; } = 16 * 1024 * 1024;
    public int ChunkBytes { get; init; } = 64 * 1024;
    public int MaximumConcurrentTransfers { get; init; } = 32;
}

/// <summary>Synchronous text API with an independent XCB selection-owner thread. Construction does not acquire or read the user's selection.</summary>
public sealed unsafe partial class LinuxX11Clipboard : IUiClipboard, IDisposable
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly LinuxX11ClipboardOptions _options;
    private readonly string _display;
    private readonly object _gate = new();
    private readonly Queue<WriteRequest> _writes = new();
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private readonly int _wake;
    private bool _disposed, _stopping;
    private ExceptionDispatchInfo? _failure;
    private Atoms _atoms = null!;
    private Payload? _payload;
    private uint _ownedAt;
    private readonly Dictionary<(uint Window, uint Property), Outgoing> _outgoing = [];

    private sealed record Atoms(uint Selection, uint Utf8, uint Mime, uint Targets, uint Timestamp, uint Multiple,
        uint Pairs, uint Text, uint Latin, uint Incr, uint Integer, uint Atom, uint Clock, uint Read)
    {
        internal static Atoms Create(XcbConnection c, string selection) => new(c.Atom(selection), c.Atom("UTF8_STRING"),
            c.Atom("text/plain;charset=utf-8"), c.Atom("TARGETS"), c.Atom("TIMESTAMP"), c.Atom("MULTIPLE"),
            c.Atom("ATOM_PAIR"), c.Atom("TEXT"), c.Atom("STRING"), c.Atom("INCR"), c.Atom("INTEGER"),
            c.Atom("ATOM"), c.Atom("_DUXEL_CLIPBOARD_CLOCK"), c.Atom("_DUXEL_CLIPBOARD_READ"));
    }
    private sealed record Payload(byte[] Utf8, byte[]? Latin);
    private sealed record WriteRequest(Payload Payload, long Deadline, TaskCompletionSource Completion);
    private sealed class Outgoing(byte[] bytes, uint type, long deadline)
    {
        internal readonly byte[] Bytes = bytes;
        internal readonly uint Type = type;
        internal readonly long Deadline = deadline;
        internal int Offset;
    }

    public LinuxX11Clipboard(LinuxX11ClipboardOptions? options = null)
    {
        if (!OperatingSystem.IsLinux() || !Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("The X11 clipboard requires 64-bit Linux.");
        _options = options ?? new();
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.Selection);
        if (_options.TransferTimeout.TotalMilliseconds is <= 0 or > int.MaxValue
            || _options.MaximumBytes is <= 0 or > int.MaxValue - 3 || _options.ChunkBytes <= 0
            || _options.MaximumConcurrentTransfers <= 0)
            throw new ArgumentOutOfRangeException(nameof(options));
        _display = _options.DisplayName ?? Environment.GetEnvironmentVariable("DISPLAY")
            ?? throw new InvalidOperationException("Configure an explicit X11 DISPLAY for the clipboard.");
        _wake = X11Native.eventfd(0, 0x800 | 0x80000);
        if (_wake < 0)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Clipboard eventfd failed.");
        _thread = new Thread(OwnerLoop) { IsBackground = true, Name = "DuxelX11Clipboard" };
        _thread.Start();
        try
        {
            _started.Task.GetAwaiter().GetResult();
        }
        catch { _thread.Join(); X11Native.close(_wake); throw; }
    }

    private long Deadline => checked(Environment.TickCount64 + (long)Math.Ceiling(_options.TransferTimeout.TotalMilliseconds));
    private static int Remaining(long deadline)
    {
        var left = deadline - Environment.TickCount64;
        if (left <= 0)
            throw new TimeoutException("X11 clipboard transfer timed out.");
        return (int)Math.Min(left, int.MaxValue);
    }
    private void CheckState()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _failure?.Throw();
    }
    private void Wake() => Signal(_wake);

    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (_gate)
            CheckState();
        if (Utf8.GetByteCount(text) > _options.MaximumBytes)
            throw new ArgumentException("Clipboard text exceeds MaximumBytes.", nameof(text));
        var bytes = Utf8.GetBytes(text);
        var latin = text.All(c => c <= 255) ? Encoding.Latin1.GetBytes(text) : null;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new WriteRequest(new(bytes, latin), Deadline, completion);
        lock (_gate)
        {
            CheckState();
            if (_writes.Count >= _options.MaximumConcurrentTransfers)
                throw new InvalidOperationException("The clipboard write queue is full.");
            _writes.Enqueue(request);
            Wake();
        }
        try
        {
            completion.Task.WaitAsync(_options.TransferTimeout).GetAwaiter().GetResult();
        }
        catch (TimeoutException error) { completion.TrySetException(error); throw; }
    }

    private void OwnerLoop()
    {
        try
        {
            using var connection = new XcbConnection(_display);
            _atoms = Atoms.Create(connection, _options.Selection);
            _started.SetResult();
            while (true)
            {
                WriteRequest? write = null;
                lock (_gate)
                {
                    if (_stopping)
                        break;
                    if (_writes.Count != 0)
                        write = _writes.Dequeue();
                }
                if (write is not null)
                {
                    if (write.Completion.Task.IsCompleted)
                        continue;
                    try
                    {
                        Remaining(write.Deadline);
                        var time = ServerTime(connection, write.Deadline, e => HandleOwnerEvent(connection, e));
                        Remaining(write.Deadline);
                        connection.SetOwner(_atoms.Selection, time);
                        if (connection.Owner(_atoms.Selection) != connection.Window)
                            throw new InvalidOperationException("X11 refused clipboard ownership.");
                        _payload = write.Payload;
                        _ownedAt = time;
                        write.Completion.TrySetResult();
                    }
                    catch (Exception error) { write.Completion.TrySetException(error); }
                }
                var events = 0;
                while (events < 64 && connection.TryEvent(out var value))
                {
                    HandleOwnerEvent(connection, value);
                    events++;
                }
                var now = Environment.TickCount64;
                foreach (var key in _outgoing.Where(p => p.Value.Deadline <= now).Select(p => p.Key).ToArray())
                    Retire(connection, key);
                lock (_gate)
                    if (_stopping || _writes.Count != 0)
                        continue;
                if (events == 64)
                    continue;
                connection.Wait(_outgoing.Count == 0 ? -1 : (int)Math.Max(1, _outgoing.Values.Min(v => v.Deadline) - now), _wake);
            }
        }
        catch (Exception error)
        {
            lock (_gate)
            {
                _failure = ExceptionDispatchInfo.Capture(error);
                foreach (var read in _reads.Values)
                    Signal(read.Descriptor);
            }
            _started.TrySetException(error);
        }
        finally
        {
            lock (_gate)
            {
                while (_writes.TryDequeue(out var write))
                    write.Completion.TrySetException(_failure?.SourceException ?? new ObjectDisposedException(nameof(LinuxX11Clipboard)));
                _payload = null;
                _outgoing.Clear();
            }
        }
    }

    private uint ServerTime(XcbConnection c, long deadline, Action<Event>? other = null, ReadOperation? read = null)
    {
        c.PutAtoms(c.Window, _atoms.Clock, _atoms.Integer, [0]);
        while (true)
        {
            if (read is not null)
                read.Check(deadline);
            else
                lock (_gate)
                    ObjectDisposedException.ThrowIf(_stopping, this);
            while (c.TryEvent(out var e))
            {
                Remaining(deadline);
                if (e.Type == 28 && e.A == c.Window && e.B == _atoms.Clock && e.State == 0)
                    return e.C;
                other?.Invoke(e);
            }
            c.Wait(Remaining(deadline), read?.Descriptor ?? _wake);
        }
    }

    private void HandleOwnerEvent(XcbConnection c, Event e)
    {
        if (e.Type == 29 && e.C == _atoms.Selection && unchecked((int)(e.A - _ownedAt)) >= 0)
        {
            _payload = null;
            return;
        }
        if (e.Type == 17)
        {
            foreach (var key in _outgoing.Keys.Where(k => k.Window == e.B).ToArray())
                _outgoing.Remove(key);
            return;
        }
        if (e.Type == 28 && e.State == 1 && _outgoing.TryGetValue((e.A, e.B), out var pending))
        {
            try
            {
                var count = (int)Math.Min(Math.Min(_options.ChunkBytes, (long)c.MaximumRequestBytes - 1024), pending.Bytes.Length - pending.Offset);
                c.Put(e.A, e.B, pending.Type, 8, pending.Bytes.AsSpan(pending.Offset, count));
                pending.Offset += count;
                if (count == 0)
                    Retire(c, (e.A, e.B));
            }
            catch (XcbProtocolException error) when (error.Error == 3) { _outgoing.Remove((e.A, e.B)); }
            return;
        }
        if (e.Type != 30)
            return;
        var property = e.F == 0 ? e.E : e.F;
        try
        {
            var accepted = e.D == _atoms.Selection && _payload is not null
                && (e.A == 0 || unchecked((int)(e.A - _ownedAt)) >= 0);
            if (accepted && e.E == _atoms.Multiple)
            {
                XcbConnection.Property pairs = default;
                if (e.F == 0)
                    accepted = false;
                else
                {
                    try
                    {
                        pairs = c.Read(e.C, property, 64 * 1024);
                    }
                    catch (XcbPropertyLimitException) { accepted = false; }
                }
                if (accepted && (pairs.Type != _atoms.Pairs || pairs.Format != 32 || pairs.Data.Length % 8 != 0))
                    accepted = false;
                if (accepted)
                {
                    var values = MemoryMarshal.Cast<byte, uint>(pairs.Data.AsSpan());
                    HashSet<uint> destinations = [property];
                    for (var i = 0; i < values.Length; i += 2)
                        if (values[i] == _atoms.Multiple || values[i + 1] == 0 || !destinations.Add(values[i + 1])
                            || !Serve(c, e.C, values[i], values[i + 1]))
                            values[i + 1] = 0;
                    c.Put(e.C, property, _atoms.Pairs, 32, pairs.Data);
                }
            }
            else if (accepted)
                accepted = Serve(c, e.C, e.E, property);
            c.Notify(e.C, e.D, e.E, accepted ? property : 0, e.A);
        }
        catch (XcbProtocolException error) when (error.Error == 3)
        {
            foreach (var key in _outgoing.Keys.Where(k => k.Window == e.C).ToArray())
                _outgoing.Remove(key);
        }
        catch (XcbProtocolException error) when (error.Error == 5)
        {
            // A foreign requestor supplied an invalid property atom.
            try
            {
                c.Notify(e.C, e.D, e.E, 0, e.A);
            }
            catch (XcbProtocolException cancelled) when (cancelled.Error == 3) { }
        }
    }

    private void Retire(XcbConnection c, (uint Window, uint Property) key)
    {
        _outgoing.Remove(key);
        if (_outgoing.Keys.Any(k => k.Window == key.Window))
            return;
        try
        {
            c.Unsubscribe(key.Window);
        }
        catch (XcbProtocolException error) when (error.Error == 3) { }
    }

    private bool Serve(XcbConnection c, uint window, uint target, uint property)
    {
        if (_payload is null)
            return false;
        if (target == _atoms.Targets)
        {
            List<uint> targets = [_atoms.Targets, _atoms.Timestamp, _atoms.Multiple, _atoms.Utf8, _atoms.Mime, _atoms.Text];
            if (_payload.Latin is not null)
                targets.Add(_atoms.Latin);
            c.PutAtoms(window, property, _atoms.Atom, CollectionsMarshal.AsSpan(targets));
            return true;
        }
        if (target == _atoms.Timestamp)
        {
            c.PutAtoms(window, property, _atoms.Integer, [_ownedAt]);
            return true;
        }
        var bytes = target == _atoms.Latin ? _payload.Latin
            : target == _atoms.Utf8 || target == _atoms.Mime || target == _atoms.Text ? _payload.Utf8 : null;
        if (bytes is null)
            return false;
        var type = target == _atoms.Text ? _atoms.Utf8 : target;
        var chunk = (int)Math.Min(_options.ChunkBytes, (long)c.MaximumRequestBytes - 1024);
        if (bytes.Length <= chunk)
            c.Put(window, property, type, 8, bytes);
        else
        {
            if (_outgoing.Count == _options.MaximumConcurrentTransfers || _outgoing.ContainsKey((window, property)))
                return false;
            c.Subscribe(window);
            c.PutAtoms(window, property, _atoms.Incr, [(uint)bytes.Length]);
            _outgoing.Add((window, property), new(bytes, type, Deadline));
        }
        return true;
    }

    public void Dispose()
    {
        ReadOperation[] reads;
        lock (_gate)
        {
            if (!_disposed)
            {
                _disposed = _stopping = true;
                Wake();
                foreach (var read in _reads.Values)
                    Signal(read.Descriptor);
            }
            reads = _reads.Values.ToArray();
        }
        _thread.Join();
        foreach (var read in reads)
            read.Finished.Task.GetAwaiter().GetResult();
        lock (_gate)
        {
            if (!_wakeClosed)
                X11Native.close(_wake);
            _wakeClosed = true;
        }
    }
}

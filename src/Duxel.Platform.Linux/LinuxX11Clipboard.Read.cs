using System.Runtime.InteropServices;
using System.Text;
using static Duxel.Platform.Linux.XcbNative;

namespace Duxel.Platform.Linux;

public sealed unsafe partial class LinuxX11Clipboard
{
    public string GetText()
    {
        using var operation = new ReadOperation(this);
        var deadline = Deadline;
        using var c = new XcbConnection(_display);
        operation.Check(deadline);
        if (c.Owner(_atoms.Selection) == 0)
            return string.Empty;
        var time = ServerTime(c, deadline, read: operation);
        var targets = Receive(c, _atoms.Targets, time, deadline, operation);
        if (targets.Type != _atoms.Atom || targets.Format != 32 || targets.Data.Length % 4 != 0)
            throw new InvalidOperationException("The clipboard owner returned invalid TARGETS.");
        var advertised = MemoryMarshal.Cast<byte, uint>(targets.Data);
        var target = advertised.Contains(_atoms.Utf8) ? _atoms.Utf8
            : advertised.Contains(_atoms.Mime) ? _atoms.Mime
            : advertised.Contains(_atoms.Latin) ? _atoms.Latin
            : advertised.Contains(_atoms.Text) ? _atoms.Text : 0;
        if (target == 0)
            return string.Empty;
        var value = Receive(c, target, time, deadline, operation);
        if (value.Format != 8)
            throw new InvalidOperationException("Clipboard text must contain 8-bit encoded data.");
        if (value.Type == _atoms.Utf8 || value.Type == _atoms.Mime)
            return Utf8.GetString(value.Data);
        if (value.Type == _atoms.Latin)
            return Encoding.Latin1.GetString(value.Data);
        throw new NotSupportedException("The clipboard owner returned an unsupported text encoding.");
    }

    private XcbConnection.Property Receive(XcbConnection c, uint target, uint time, long deadline, ReadOperation operation)
    {
        c.Convert(_atoms.Selection, target, _atoms.Read, time);
        while (true)
        {
            operation.Check(deadline);
            while (c.TryEvent(out var e))
            {
                operation.Check(deadline);
                if (e.Type != 31 || e.B != c.Window || e.C != _atoms.Selection || e.D != target)
                    continue;
                if (e.E == 0)
                    throw new NotSupportedException("The clipboard owner refused the advertised conversion.");
                var limit = target == _atoms.Targets ? 64 * 1024 : _options.MaximumBytes;
                var first = c.Read(c.Window, e.E, Math.Max(limit, 4));
                if (first.Type != _atoms.Incr)
                {
                    if (first.Data.Length > limit)
                        throw new XcbPropertyLimitException();
                    return first;
                }
                if (first.Format != 32 || first.Data.Length != 4)
                    throw new InvalidOperationException("Invalid clipboard INCR header.");
                var expected = MemoryMarshal.Read<uint>(first.Data);
                if (expected > limit)
                    throw new XcbPropertyLimitException();
                return ReceiveIncremental(c, e.E, expected, limit, deadline, operation);
            }
            c.Wait(Remaining(deadline), operation.Descriptor);
        }
    }

    private XcbConnection.Property ReceiveIncremental(XcbConnection c, uint property, uint expected, int limit, long deadline, ReadOperation operation)
    {
        using var bytes = new MemoryStream(checked((int)expected));
        uint type = 0;
        byte format = 0;
        while (true)
        {
            operation.Check(deadline);
            while (c.TryEvent(out var e))
            {
                operation.Check(deadline);
                if (e.Type != 28 || e.A != c.Window || e.B != property || e.State != 0)
                    continue;
                var chunk = c.Read(c.Window, property, limit);
                if (type == 0)
                {
                    type = chunk.Type;
                    format = chunk.Format;
                }
                if (type == 0 || chunk.Type != type || chunk.Format != format)
                    throw new InvalidOperationException("Clipboard INCR changed its data type.");
                if (chunk.Data.Length == 0)
                {
                    if (bytes.Length < expected)
                        throw new InvalidOperationException("Clipboard INCR terminated below its declared minimum length.");
                    return new(type, format, bytes.ToArray());
                }
                if (bytes.Length + chunk.Data.Length > limit)
                    throw new XcbPropertyLimitException();
                bytes.Write(chunk.Data);
            }
            c.Wait(Remaining(deadline), operation.Descriptor);
        }
    }
}

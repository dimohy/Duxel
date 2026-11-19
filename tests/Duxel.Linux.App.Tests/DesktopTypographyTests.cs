using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Platform.Linux;

internal static class DesktopTypographyTests
{
    internal static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
            checks.Add(name);
            Console.WriteLine("PASS " + name);
        }
        foreach (var little in new[] { true, false })
        {
            var bytes = Property(little);
            var settings = LinuxDesktopSettings.Parse(bytes);
            Check(settings == new LinuxDesktopSettings(2, 122880, 245760, "Sans 12"), "XSETTINGS mixed types and padding decode in " + (little ? "LE" : "BE"));
            Check(settings.Resolve(null, 12, false) == (2f, 10f / 7f), "Desktop point size and font DPI are independent of window scale");
            Check(settings.Resolve(null, 28, true) == (2f, 1f), "Absolute desktop font pixels do not apply window scale twice");
            foreach (var length in new[] { 0, 11, 13, bytes.Length - 1 })
            {
                var rejected = false;
                try
                {
                    LinuxDesktopSettings.Parse(bytes.AsSpan(0, length));
                }
                catch (InvalidDataException) { rejected = true; }
                Check(rejected, $"Truncated XSETTINGS is rejected : endian={little}, bytes={length}");
            }
        }
        Check(default(LinuxDesktopSettings).Resolve(null, null, false) == (1f, 1f), "An unconfigured X11 desktop has the declared 96 DPI default");
        Check(default(LinuxDesktopSettings).Resolve(144, null, false) == (1.5f, 1f), "X resources DPI scales the display once");
        Check(new LinuxDesktopSettings(2, null, null, null).Resolve(null, null, false) == (2f, 1f), "Window scale without a separate font DPI retains logical text size");
        Check(new LinuxDesktopSettings(1, 147456, 147456, null).Resolve(null, null, false) == (1f, 1.5f), "Desktop accessibility font DPI affects text without growing icons");
        Check(new LinuxDesktopSettings(null, null, -1, null).Resolve(120, null, false) == (1.25f, 1f), "GTK unset-DPI sentinel respects X resources");
        Check(default(LinuxDesktopSettings).Resolve(72, null, false) == (.75f, 1f), "Sub-100-percent desktop DPI remains supported");
        foreach (var invalid in new[] { new LinuxDesktopSettings(0, null, null, null), new(null, -2, null, null), new(null, null, 0, null) })
        {
            var rejected = false;
            try
            {
                invalid.Resolve(null, null, false);
            }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Malformed desktop scales are rejected : " + invalid);
        }
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new DesktopTypographyResult(checks.Count, checks.ToArray()), DesktopTypographyJson.Default.DesktopTypographyResult));
    }
    private static byte[] Property(bool little)
    {
        using var data = new MemoryStream();
        void U32(uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            if (little)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            else
                BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
            data.Write(bytes);
        }
        void Padded(byte[] bytes)
        {
            data.Write(bytes);
            while (data.Length % 4 != 0)
                data.WriteByte(0);
        }
        void Entry(string name, byte type, uint value = 0, string? text = null)
        {
            data.WriteByte(type);
            data.WriteByte(0);
            var nameBytes = Encoding.UTF8.GetBytes(name);
            Span<byte> length = stackalloc byte[2];
            if (little)
                BinaryPrimitives.WriteUInt16LittleEndian(length, (ushort)nameBytes.Length);
            else
                BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)nameBytes.Length);
            data.Write(length);
            Padded(nameBytes);
            U32(1);
            if (type == 0)
                U32(value);
            else if (type == 1)
            {
                var bytes = Encoding.UTF8.GetBytes(text!);
                U32((uint)bytes.Length);
                Padded(bytes);
            }
            else
            {
                U32(0);
                U32(uint.MaxValue);
            }
        }
        data.WriteByte(little ? (byte)0 : (byte)1);
        data.Write([0, 0, 0]);
        U32(1);
        U32(5);
        Entry("Gdk/WindowScalingFactor", 0, 2);
        Entry("Unrelated/Color", 2);
        Entry("Gdk/UnscaledDPI", 0, 122880);
        Entry("Gtk/FontName", 1, text: "Sans 12");
        Entry("Xft/DPI", 0, 245760);
        return data.ToArray();
    }
}
internal sealed record DesktopTypographyResult(int Passed, string[] Checks);
[JsonSerializable(typeof(DesktopTypographyResult))]
internal partial class DesktopTypographyJson : JsonSerializerContext;

using System.Buffers.Binary;
using System.IO.Compression;

internal static class BrowserPng
{
    internal static void Write(string file, int width, int height, byte[] rgba)
    {
        using var output = File.Create(file);
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        Span<byte> header = stackalloc byte[13];
        header.Clear();
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8;
        header[9] = 6;
        Chunk(output, "IHDR"u8, header);
        using var compressed = new MemoryStream();
        using (var zip = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            for (var y = 0; y < height; y++)
            {
                zip.WriteByte(0);
                zip.Write(rgba.AsSpan(y * width * 4, width * 4));
            }
        Chunk(output, "IDAT"u8, compressed.ToArray());
        Chunk(output, "IEND"u8, []);
    }

    private static void Chunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> value = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(value, data.Length);
        output.Write(value);
        output.Write(type);
        output.Write(data);
        var crc = uint.MaxValue;
        foreach (var item in type) Add(item);
        foreach (var item in data) Add(item);
        BinaryPrimitives.WriteUInt32BigEndian(value, ~crc);
        output.Write(value);
        void Add(byte item)
        {
            crc ^= item;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
        }
    }
}

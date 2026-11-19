using Duxel.Svg;

/// <summary>Offline asset export; normal Gallery startup only loads the generated ICO.</summary>
internal static class GalleryIconAssets
{
    internal static void Export(string destination)
    {
        if (Directory.Exists(destination)) throw new IOException("Use a fresh icon export directory.");
        Directory.CreateDirectory(destination);
        int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
        using var store = new SvgStore();
        var asset = store.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "duxel-mark.svg"));
        var frames = new List<byte[]>();
        foreach (var size in sizes)
        {
            var raster = asset.Rasterize(size, size);
            if (raster.Pixels.Span[3] != 0) throw new InvalidOperationException("Icon corner must remain transparent.");
            var file = Path.Combine(destination, $"duxel-{size}.png");
            BrowserPng.Write(file, size, size, raster.Pixels.ToArray());
            frames.Add(File.ReadAllBytes(file));
        }
        if (store.Statistics.XmlParses != 1) throw new InvalidOperationException("Icon sizes must share one SVG compilation.");
        using var output = new BinaryWriter(File.Create(Path.Combine(destination, "duxel.ico")));
        output.Write((ushort)0);
        output.Write((ushort)1);
        output.Write((ushort)sizes.Length);
        var offset = 6 + sizes.Length * 16;
        for (var index = 0; index < sizes.Length; index++)
        {
            output.Write((byte)(sizes[index] == 256 ? 0 : sizes[index]));
            output.Write((byte)(sizes[index] == 256 ? 0 : sizes[index]));
            output.Write((byte)0);
            output.Write((byte)0);
            output.Write((ushort)1);
            output.Write((ushort)32);
            output.Write(frames[index].Length);
            output.Write(offset);
            offset += frames[index].Length;
        }
        foreach (var frame in frames) output.Write(frame);
        Console.WriteLine("Exported nine transparent32-bit icon sizes from one compiled SVG.");
    }
}

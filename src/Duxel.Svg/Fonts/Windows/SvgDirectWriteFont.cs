using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Duxel.Svg.Managed;

namespace Duxel.Svg;

internal sealed unsafe partial class SvgDirectWriteFont : ISvgNativeFont
{
    private nint _factory, _loader, _file, _face, _analyzer;
    private bool _registered;
    private readonly Dictionary<ushort, SvgGeometry> _outlines = [];
    public int UnitsPerEm { get; }
    public int Weight { get; }
    public int Width { get; }
    public bool Italic { get; }
    public bool Oblique { get; }
    public bool IsDisposed => _factory == 0 && _loader == 0 && _file == 0 && _face == 0 && _analyzer == 0;
    internal SvgDirectWriteFont(byte[] bytes, int collectionIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(collectionIndex);
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393))
            throw new PlatformNotSupportedException("SVG font snapshots require Windows 10 build 14393 or later.");
        try
        {
            var iid = new Guid("958DB99A-BE2A-4F09-AF7D-65189803D1D3");
            nint factory = 0;
            Check(DWriteCreateFactory(1, &iid, &factory), "Create isolated DirectWrite factory");
            _factory = factory;
            nint loader = 0;
            Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Method(_factory, 44))(_factory, &loader), "Create in-memory font loader");
            _loader = loader;
            Check(((delegate* unmanaged[Stdcall]<nint, nint, int>)Method(_factory, 13))(_factory, _loader), "Register in-memory font loader");
            _registered = true;
            nint file = 0;
            fixed (byte* data = bytes)
                Check(((delegate* unmanaged[Stdcall]<nint, nint, void*, uint, nint, nint*, int>)Method(_loader, 4))
                    (_loader, _factory, data, checked((uint)bytes.Length), 0, &file), "Copy explicit font bytes");
            _file = file;
            int supported = 0, fileType = 0, faceType = 0;
            uint faceCount = 0;
            var analysis = ((delegate* unmanaged[Stdcall]<nint, int*, int*, int*, uint*, int>)Method(_file, 5))
                (_file, &supported, &fileType, &faceType, &faceCount);
            if (analysis < 0 || supported == 0 || collectionIndex >= faceCount)
                throw new InvalidDataException("Invalid explicit SVG font bytes or collection index.");
            nint face = 0;
            Check(((delegate* unmanaged[Stdcall]<nint, int, uint, nint*, uint, int, nint*, int>)Method(_factory, 9))
                (_factory, faceType, 1, &file, (uint)collectionIndex, 0, &face), "Create explicit font face");
            _face = face;
            byte* metrics = stackalloc byte[20];
            ((delegate* unmanaged[Stdcall]<nint, void*, void>)Method(_face, 8))(_face, metrics);
            UnitsPerEm = *(ushort*)metrics;
            if (UnitsPerEm == 0) throw new InvalidDataException("SVG font units must be positive.");
            void* table = null;
            uint size = 0;
            nint context = 0;
            int exists = 0;
            Check(((delegate* unmanaged[Stdcall]<nint, uint, void**, uint*, nint*, int*, int>)Method(_face, 12))
                (_face, 0x322F534F, &table, &size, &context, &exists), "Read OpenType style metadata");
            try
            {
                if (exists == 0 || size < 64) throw new InvalidDataException("SVG fonts require explicit OS/2 style metadata.");
                var metadata = new ReadOnlySpan<byte>(table, checked((int)size));
                Weight = BinaryPrimitives.ReadUInt16BigEndian(metadata[4..]);
                Width = BinaryPrimitives.ReadUInt16BigEndian(metadata[6..]);
                var selection = BinaryPrimitives.ReadUInt16BigEndian(metadata[62..]);
                Italic = (selection & 1) != 0;
                Oblique = (selection & 512) != 0;
            }
            finally { if (exists != 0) ((delegate* unmanaged[Stdcall]<nint, nint, void>)Method(_face, 13))(_face, context); }
            nint analyzer = 0;
            Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Method(_factory, 21))(_factory, &analyzer), "Create text analyzer");
            _analyzer = analyzer;
        }
        catch { Dispose(); throw; }
    }
    public bool Contains(uint codepoint)
    {
        ObjectDisposedException.ThrowIf(_face == 0, this);
        ushort glyph = 0;
        Check(((delegate* unmanaged[Stdcall]<nint, uint*, uint, ushort*, int>)Method(_face, 11))(_face, &codepoint, 1, &glyph), "Read explicit glyph coverage");
        return glyph != 0;
    }
    public void Dispose()
    {
        Release(ref _analyzer);
        Release(ref _face);
        Release(ref _file);
        var status = 0;
        if (_registered)
        {
            status = ((delegate* unmanaged[Stdcall]<nint, nint, int>)Method(_factory, 14))(_factory, _loader);
            _registered = false;
        }
        Release(ref _loader);
        Release(ref _factory);
        _outlines.Clear();
        Check(status, "Unregister in-memory font loader");
    }
    private static void* Method(nint instance, int slot) => (*(void***)instance)[slot];
    private static void Release(ref nint instance)
    {
        var value = instance;
        instance = 0;
        if (value != 0) ((delegate* unmanaged[Stdcall]<nint, uint>)Method(value, 2))(value);
    }
    private static void Check(int result, string operation)
    {
        if (result < 0) throw new COMException(operation, result);
    }
    [LibraryImport("dwrite.dll")]
    private static partial int DWriteCreateFactory(int type, Guid* iid, nint* factory);
}

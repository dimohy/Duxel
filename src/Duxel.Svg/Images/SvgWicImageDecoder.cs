using System.Runtime.InteropServices;
using Duxel.Svg.Managed;

namespace Duxel.Svg;

internal static unsafe partial class SvgWicImageDecoder
{
    internal static SvgDecodedImage Decode(byte[] bytes)
    {
        var initialized = CoInitializeEx(0, 0);
        if (initialized < 0 && initialized != unchecked((int)0x80010106)) Check(initialized, "Initialize Windows imaging COM");
        nint factory = 0, stream = 0, decoder = 0, frame = 0, converter = 0;
        try
        {
            var clsid = new Guid("cacaf262-9370-4615-a13b-9f5539da4c0a");
            var iid = new Guid("ec5ec8a9-c395-4314-9c77-54d7a935ff70");
            Check(CoCreateInstance(&clsid, 0, 1, &iid, &factory), "Create Windows imaging factory");
            fixed (byte* data = bytes) stream = SHCreateMemStream(data, checked((uint)bytes.Length));
            if (stream == 0) throw new OutOfMemoryException("Windows could not snapshot the embedded image stream.");
            Check(((delegate* unmanaged[Stdcall]<nint, nint, Guid*, int, nint*, int>)Method(factory, 4))
                (factory, stream, null, 0, &decoder), "Decode embedded bitmap stream", input: true);
            Check(((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)Method(decoder, 13))
                (decoder, 0, &frame), "Read first embedded bitmap frame", input: true);
            uint width = 0, height = 0;
            Check(((delegate* unmanaged[Stdcall]<nint, uint*, uint*, int>)Method(frame, 3))
                (frame, &width, &height), "Read embedded bitmap dimensions", input: true);
            if (width == 0 || height == 0 || (ulong)width * height > 16 * 1024 * 1024)
                throw new InvalidDataException("Embedded SVG bitmap exceeds the 64 MiB pixel budget.");
            Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Method(factory, 10))
                (factory, &converter), "Create straight-RGBA bitmap converter");
            var rgba = new Guid("f5c7ad2d-6a8d-43dd-a7a8-a29935261ae9");
            Check(((delegate* unmanaged[Stdcall]<nint, nint, Guid*, int, nint, double, int, int>)Method(converter, 8))
                (converter, frame, &rgba, 0, 0, 0, 0), "Convert embedded bitmap to straight RGBA");
            var pixels = new byte[checked((int)(width * height * 4))];
            fixed (byte* destination = pixels)
                Check(((delegate* unmanaged[Stdcall]<nint, void*, uint, uint, byte*, int>)Method(converter, 7))
                    (converter, null, width * 4, checked((uint)pixels.Length), destination), "Copy embedded bitmap pixels", input: true);
            return new((int)width, (int)height, pixels);
        }
        finally
        {
            Release(converter); Release(frame); Release(decoder); Release(stream); Release(factory);
            if (initialized >= 0) CoUninitialize();
        }
    }
    private static void* Method(nint instance, int slot) => (*(void***)instance)[slot];
    private static void Release(nint value)
    {
        if (value != 0) ((delegate* unmanaged[Stdcall]<nint, uint>)Method(value, 2))(value);
    }
    private static void Check(int result, string operation, bool input = false)
    {
        if (result >= 0) return;
        var error = new COMException(operation, result);
        if (input) throw new InvalidDataException(operation, error);
        throw error;
    }
    [LibraryImport("ole32.dll")] private static partial int CoInitializeEx(nint reserved, uint flags);
    [LibraryImport("ole32.dll")] private static partial void CoUninitialize();
    [LibraryImport("ole32.dll")] private static partial int CoCreateInstance(Guid* clsid, nint outer, uint context, Guid* iid, nint* result);
    [LibraryImport("shlwapi.dll")] private static partial nint SHCreateMemStream(byte* bytes, uint length);
}

using System.Runtime.InteropServices;

namespace Duxel.Interop.Windows;

internal static unsafe partial class DirectWriteLongTextShaper
{
    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct TextRange(uint Start, uint Length);

    /// <summary>A registered, single-file collection whose lifetime encloses its layout.</summary>
    private sealed class ExplicitFontScope : IDisposable
    {
        private readonly nint _factory;
        private nint _file, _collection, _font, _family, _names;
        private Callback* _loader;
        private bool _registered;
        internal FontIdentity Identity
        {
            get;
        }
        private readonly char[] _name;
        private readonly int _weight, _stretch, _style;

        internal ExplicitFontScope(nint factory, nint face)
        {
            _factory = factory;
            Identity = FontIdentity.Read(face);
            _name = [];
            try
            {
                _file = GetFile(face);
                var state = new FileState(_file, face);
                _loader = CreateCallback(0, state);
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, Callback*, int>)Method(factory, 5))(factory, _loader));
                _registered = true;
                var key = face;
                nint collection = 0;
                var status = ((delegate* unmanaged[Stdcall]<nint, Callback*, void*, uint, nint*, int>)Method(factory, 4))
                    (factory, _loader, &key, (uint)sizeof(nint), &collection);
                _collection = collection;
                state.Failure?.Throw();
                Check(status, "CreateCustomFontCollection");
                nint font = 0, family = 0, names = 0;
                Check(((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)Method(_collection, 6))(_collection, face, &font), "GetFontFromFontFace");
                _font = font;
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Method(font, 3))(font, &family));
                _family = family;
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Method(family, 6))(family, &names));
                _names = names;
                uint count = 0;
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, uint*, int>)Method(names, 7))(names, 0, &count));
                _name = new char[checked((int)count + 1)];
                fixed (char* name = _name)
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, char*, uint, int>)Method(names, 8))(names, 0, name, (uint)_name.Length));
                _weight = ((delegate* unmanaged[Stdcall]<nint, int>)Method(font, 4))(font);
                _stretch = ((delegate* unmanaged[Stdcall]<nint, int>)Method(font, 5))(font);
                _style = (Identity.Simulations & 2) != 0 ? 1 : ((delegate* unmanaged[Stdcall]<nint, int>)Method(font, 6))(font);
            }
            catch { Dispose(); throw; }
        }

        internal nint CreateFormat(float size, bool rtl, bool wrap = false)
        {
            nint format = 0;
            try
            {
                fixed (char* name = _name)
                fixed (char* locale = "und")
                    Check(((delegate* unmanaged[Stdcall]<nint, char*, nint, int, int, int, float, char*, nint*, int>)Method(_factory, 15))
                        (_factory, name, _collection, _weight, _style, _stretch, size, locale, &format), "CreateTextFormat");
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, int>)Method(format, 6))(format, rtl ? 1 : 0));
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, int>)Method(format, 3))(format, rtl ? 1 : 0));
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, int>)Method(format, 5))(format, wrap ? 0 : 1));
                return format;
            }
            catch { Release(ref format); throw; }
        }

        internal void Apply(nint layout, FontSpan span)
        {
            var range = new TextRange((uint)span.Start, (uint)span.Length);
            // IDWriteTextLayout methods follow the public dwrite.h vtable after IDWriteTextFormat.
            Check(((delegate* unmanaged[Stdcall]<nint, nint, TextRange, int>)Method(layout, 30))(layout, _collection, range), "SetFontCollection");
            fixed (char* name = _name)
                Check(((delegate* unmanaged[Stdcall]<nint, char*, TextRange, int>)Method(layout, 31))(layout, name, range), "SetFontFamilyName");
            Check(((delegate* unmanaged[Stdcall]<nint, int, TextRange, int>)Method(layout, 32))(layout, _weight, range), "SetFontWeight");
            Check(((delegate* unmanaged[Stdcall]<nint, int, TextRange, int>)Method(layout, 33))(layout, _style, range), "SetFontStyle");
            Check(((delegate* unmanaged[Stdcall]<nint, int, TextRange, int>)Method(layout, 34))(layout, _stretch, range), "SetFontStretch");
            Check(((delegate* unmanaged[Stdcall]<nint, float, TextRange, int>)Method(layout, 35))(layout, span.Size, range), "SetFontSize");
        }

        public void Dispose()
        {
            Release(ref _names);
            Release(ref _family);
            Release(ref _font);
            Release(ref _collection);
            var status = _registered ? ((delegate* unmanaged[Stdcall]<nint, Callback*, int>)Method(_factory, 6))(_factory, _loader) : 0;
            _registered = false;
            var references = _loader == null ? 0 : ReleaseOwned(_loader);
            _loader = null;
            Release(ref _file);
            Identity.Dispose();
            Marshal.ThrowExceptionForHR(status);
            if (references != 0)
                throw new InvalidOperationException("DirectWrite retained an explicit-font collection callback after disposal.");
        }
    }
}

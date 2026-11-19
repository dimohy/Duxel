using System.Runtime.InteropServices;

namespace Duxel.Platform.Linux;

internal static unsafe partial class LinuxParagraphNative
{
    [LibraryImport(Pango)]
    internal static partial Attribute* pango_attr_shape_new(Rectangle* ink, Rectangle* logical);
}

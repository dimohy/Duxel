using static Duxel.Platform.Linux.LinuxParagraphNative;

namespace Duxel.Platform.Linux;

internal sealed unsafe partial class LinuxParagraphFontSet
{
    internal readonly record struct Shape(int Start, int End, Rectangle Logical);
    internal static Shape ObjectShape(int start, int end, float width, float height, float baseline)
        => new(start, end, new()
        {
            Y = checked(-(int)MathF.Round(baseline * Scale)),
            Width = checked((int)MathF.Round(width * Scale)),
            Height = checked((int)MathF.Round(height * Scale))
        });
}

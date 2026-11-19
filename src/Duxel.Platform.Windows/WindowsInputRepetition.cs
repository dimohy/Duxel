namespace Duxel.Platform.Windows;

internal static class WindowsInputRepetition
{
    internal static int Count(nuint flags)
    {
        var count = (int)(flags & 0xFFFF);
        // Existing directly sent synthetic messages use zero flags for one event.
        return count == 0 ? 1 : count;
    }

    internal static string Repeat(string text, int count)
    {
        if (count == 1)
            return text;
        return string.Create(checked(text.Length * count), (Text: text, Count: count), static (destination, source) =>
        {
            for (var i = 0; i < source.Count; i++)
                source.Text.AsSpan().CopyTo(destination.Slice(i * source.Text.Length));
        });
    }

    internal static bool Matches(string? value, string unit, int count)
    {
        if (value is null || value.Length != checked(unit.Length * count))
            return false;
        for (var i = 0; i < count; i++)
            if (!value.AsSpan(i * unit.Length, unit.Length).SequenceEqual(unit))
                return false;
        return true;
    }
}

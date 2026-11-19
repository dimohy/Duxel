using System.Text.Json;
using Duxel.Core;

internal readonly record struct TabAppearanceCase(UiFluentColorScheme Scheme, UiTabListAppearance Appearance,
    UiButtonVisualState State, bool Selected, UiTabPaint Paint);

internal static class TabAppearanceReference
{
    internal static TabAppearanceCase[] Load()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "TabAppearances.json")));
        return json.RootElement.EnumerateArray().Select(row =>
        {
            var colors = row.GetProperty("Colors");
            UiColor Color(string name)
            {
                var hex = colors.GetProperty(name).GetString()!.AsSpan(1);
                var bytes = Convert.FromHexString(hex);
                return new(bytes[0], bytes[1], bytes[2], bytes.Length == 4 ? bytes[3] : (byte)255);
            }
            return new TabAppearanceCase(Enum.Parse<UiFluentColorScheme>(row.GetProperty("Scheme").GetString()!),
                Enum.Parse<UiTabListAppearance>(row.GetProperty("Appearance").GetString()!),
                Enum.Parse<UiButtonVisualState>(row.GetProperty("State").GetString()!), row.GetProperty("Selected").GetBoolean(),
                new(Color("Background"), Color("Border"), Color("Text"), Color("Icon"), Color("Indicator")));
        }).ToArray();
    }

    internal static void Run(Action<bool, string> check)
    {
        foreach (var item in Load())
            check(UiFluentTabListPalette.Resolve(item.Scheme, item.State, item.Appearance, item.Selected) == item.Paint,
                $"independently resolved official tab tokens : {item.Scheme} / {item.Appearance} / {item.State} / selected {item.Selected}");
    }
}

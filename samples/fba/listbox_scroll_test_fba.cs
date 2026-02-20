// FBA: 대용량 ListBox 스크롤 테스트 샘플
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using Duxel.App;
using Duxel.Core;

var itemCount = ReadItemCount();

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel ListBox Scroll Test (FBA)",
        VSync = false,
        Width = 1280,
        Height = 780
    },
    Font = new DuxelFontOptions
    {
        InitialGlyphs = ListBoxScrollTestScreen.GlyphStrings
    },
    Screen = new ListBoxScrollTestScreen(itemCount)
});

static int ReadItemCount()
{
    var raw = Environment.GetEnvironmentVariable("DUXEL_LISTBOX_ITEMS");
    if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var parsed) && parsed >= 1000)
    {
        return parsed;
    }

    return 50000;
}

public sealed class ListBoxScrollTestScreen : UiScreen
{
    public static readonly string[] GlyphStrings =
    [
        "Duxel ListBox Scroll Test (FBA)",
        "ListBox Scroll Test",
        "Total Items",
        "Visible Rows",
        "Selected",
        "Jump Top",
        "Jump Middle",
        "Jump Bottom",
        "Huge ListBox",
        "Use mouse wheel / scrollbar to verify smooth scrolling"
    ];

    private readonly string[] _items;
    private int _selectedIndex;
    private int _visibleRows = 16;

    public ListBoxScrollTestScreen(int itemCount)
    {
        _items = new string[itemCount];
        for (var i = 0; i < _items.Length; i++)
        {
            _items[i] = $"Item {i:000000}";
        }

        _selectedIndex = 0;
    }

    public override void Render(UiImmediateContext ui)
    {
        var viewport = ui.GetMainViewport();
        var margin = 16f;
        ui.SetNextWindowPos(new UiVector2(viewport.Pos.X + margin, viewport.Pos.Y + margin));
        ui.SetNextWindowSize(new UiVector2(viewport.Size.X - margin * 2f, viewport.Size.Y - margin * 2f));
        ui.BeginWindow("ListBox Scroll Test");

        ui.TextV("Total Items: {0}", _items.Length);
        ui.TextV("Selected: {0}", _selectedIndex);
        ui.Text("Use mouse wheel / scrollbar to verify smooth scrolling");

        ui.SliderInt("Visible Rows", ref _visibleRows, 6, 40);

        if (ui.Button("Jump Top"))
        {
            _selectedIndex = 0;
        }

        ui.SameLine();
        if (ui.Button("Jump Middle"))
        {
            _selectedIndex = _items.Length / 2;
        }

        ui.SameLine();
        if (ui.Button("Jump Bottom"))
        {
            _selectedIndex = _items.Length - 1;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, _items.Length - 1);
        ui.Text("Huge ListBox");
        ui.ListBox(ref _selectedIndex, _items, _visibleRows, "Huge ListBox");

        ui.EndWindow();
    }
}

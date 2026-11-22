using System.Globalization;
using System.Text.Json;
using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class IconCatalogPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private static readonly Lazy<Entry[]> Catalog = new(LoadCatalog);
    private static readonly Lazy<Dictionary<string, Entry>> CatalogByName = new(() =>
        Catalog.Value.ToDictionary(entry => entry.Name, StringComparer.Ordinal));
    private const float IconColumnWidth = 96;
    private Content? _content;
    private Content PageContent => _content ??= new();

    public string Title => "Icon";
    public string Description => "Search the bundled Fluent icons and select a row to preview it.";
    public IReadOnlyList<IGallerySample> Samples => [PageContent.Sample];
    UiWidget IObserverNaturalPage.MeasureRoot => PageContent.Root;
    public void Render(UiImmediateContext ui) => PageContent.Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();

    private static readonly UiColor IconColor = new(66, 149, 237);
    private sealed record Glyph(string Text, int Size);
    private sealed record Entry(string Name, Glyph? Outline, Glyph? Filled)
    {
        public UiMenuIcon DefaultIcon => Outline is { } outline ? FontIcon(outline, false) : FontIcon(Filled!, true);
        public UiMenuIcon HoverIcon => Outline is not null && Filled is { } filled ? FontIcon(filled, true) : default;
    }
    private static UiMenuIcon FontIcon(Glyph glyph, bool filled) => filled
        ? UiMenuIcon.FromFontGlyph<UiFluentFilledIconFont>(glyph.Text)
        : UiMenuIcon.FromFontGlyph<UiFluentRegularIconFont>(glyph.Text);

    private static Entry[] LoadCatalog()
    {
        var outline = ReadFace("Regular");
        var filled = ReadFace("Filled");
        return outline.Keys.Union(filled.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(name => new Entry(name, outline.GetValueOrDefault(name).Glyph, filled.GetValueOrDefault(name).Glyph))
            .ToArray();
    }

    private static Dictionary<string, (Glyph Glyph, int Rank)> ReadFace(string face)
    {
        using var stream = typeof(IconCatalogPage).Assembly.GetManifestResourceStream($"GalleryIcons.FluentSystemIcons-{face}.json")
            ?? throw new InvalidOperationException("The Gallery icon catalog resource is missing.");
        using var document = JsonDocument.Parse(stream);
        var icons = new Dictionary<string, (Glyph Glyph, int Rank)>(StringComparer.Ordinal);
        const string prefix = "ic_fluent_";
        var suffix = "_" + face.ToLowerInvariant();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var key = property.Name.AsSpan();
            if (!key.StartsWith(prefix, StringComparison.Ordinal) || !key.EndsWith(suffix, StringComparison.Ordinal))
                continue;
            var sizedName = key[prefix.Length..^suffix.Length];
            var separator = sizedName.LastIndexOf('_');
            if (separator <= 0 || !int.TryParse(sizedName[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var size))
                throw new InvalidOperationException("Invalid Fluent icon name: " + property.Name);
            var name = sizedName[..separator].ToString();
            var rank = Math.Abs(size - 24) * 2 + (size > 24 ? 1 : 0);
            if (!icons.TryGetValue(name, out var current) || rank < current.Rank)
                icons[name] = (new(char.ConvertFromUtf32(property.Value.GetInt32()), size), rank);
        }
        return icons;
    }

    private sealed class Content
    {
        private const int RowHeight = 36;
        private const int MaximumTableHeight = 344;
        private string _query = string.Empty;
        private readonly Rows _rows;
        private readonly UiVirtualList<Rows> _list;
        private readonly UiIcon _preview;
        private Entry _selected;
        public UiWidget Root { get; }
        public IGallerySample Sample { get; }

        public Content()
        {
            _selected = Catalog.Value.Single(entry => entry.Name == "home");
            var sample = GallerySample.Create<UiIcon>(() =>
            {
                var icon = Fluent.Icon(_selected.DefaultIcon, new(48, 48), IconColor);
                icon.HoverIcon = _selected.HoverIcon;
                return icon;
            });
            sample.BindSource(() => _selected, FormatSource);
            Sample = sample;
            _preview = sample.Preview;
            _rows = new(Select, () => _selected);
            _list = Dux.VirtualList(_rows, _rows.Items.Length,
                new(RowHeight, Height : MaximumTableHeight, Overscan : 2, MaxRealizedItems : 24));
            _list.AccessibleName = GalleryText.Get("Fluent icon results");
            var search = Fluent.SearchBox(GalleryText.Get("Search icons"), new UiBinding<string>(() => _query, Filter),
                options : new(Placeholder : GalleryText.Get("Search by name, for example arrow left"), ClearLabel : GalleryText.Get("Clear search")));
            Root = Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min, CrossAxisAlignment : UiCrossAxisAlignment.Stretch),
                search,
                Fluent.Body1(() => _rows.Items.Length == 0
                    ? GalleryText.Get("No matching icons")
                    : GalleryText.Format("{0:N0} of {1:N0} icons", _rows.Items.Length, Catalog.Value.Length)),
                TableRow(Fluent.Body1Strong(GalleryText.Get("Icon name")), Fluent.Caption1(GalleryText.Get("Outline")),
                    Fluent.Caption1(GalleryText.Get("Filled"))),
                Fluent.Divider(), _list,
                Fluent.Subtitle2(() => GalleryText.Format("Selected icon: {0}", _selected.Name)),
                Fluent.Caption1(() => _selected is { Outline: not null, Filled: not null }
                    ? GalleryText.Get("Hover over the icon below to see the filled style.")
                    : GalleryText.Get("This icon has one style in the bundled font.")), sample);
        }

        private static string FormatSource(Entry item)
        {
            var defaultType = item.Outline is null ? "FilledIcon" : "OutlineIcon";
            var paired = item.Outline is not null && item.Filled is not null;
            return ($"var icon = Fluent.Icon(UiMenuIcon.Create<{defaultType}, {defaultType}>(),\n"
                + "    new(48, 48), new UiColor(66, 149, 237));\n"
                + (paired ? "icon.HoverIcon = UiMenuIcon.Create<FilledIcon, FilledIcon>();\n" : string.Empty)
                + "UiWidget example = icon;\n"
                + Declaration(item.Outline, "Regular", "OutlineIcon")
                + Declaration(item.Filled, "Filled", "FilledIcon")).TrimEnd();

            string Declaration(Glyph? glyph, string style, string type) => glyph is null ? string.Empty
                : $"\n[UiFluentIcon({GallerySample.String(item.Name)}, UiFluentIconStyle.{style}, {glyph.Size})]\n"
                    + $"readonly partial struct {type};\n";
        }

        private void Select(Entry selection)
        {
            if (_selected == selection) return;
            _selected = selection;
            _preview.Icon = selection.DefaultIcon;
            _preview.HoverIcon = selection.HoverIcon;
        }

        private void Filter(string query)
        {
            if (query == _query) return;
            _query = query;
            var terms = query.Split([' ', '\t', '\n', '\r', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
            _rows.Items = terms.Length == 0 ? Catalog.Value : Catalog.Value.Where(entry =>
                terms.All(term => entry.Name.Contains(term, StringComparison.OrdinalIgnoreCase))).ToArray();
            _list.ItemCount = _rows.Items.Length;
            _list.Options = _list.Options with { Height = Math.Min(MaximumTableHeight, _rows.Items.Length * RowHeight) };
            _list.Refresh();
            _list.Offset = 0;
        }
    }

    private static UiWidget TableRow(UiWidget name, UiWidget outline, UiWidget filled) =>
        Dux.Row(new(CrossAxisAlignment : UiCrossAxisAlignment.Center),
            Dux.Expanded(Dux.Padding(name, new(8, 0, 0, 0))),
            Dux.SizedBox(Dux.Align(outline, UiAlignment.Center), width : IconColumnWidth),
            Dux.SizedBox(Dux.Align(filled, UiAlignment.Center), width : IconColumnWidth));

    private sealed class Rows(Action<Entry> select, Func<Entry> selected) : IUiVirtualListSource
    {
        public Entry[] Items { get; set; } = Catalog.Value;
        public string GetKey(int index) => Items[index].Name;
        public void Bind(UiWidget widget, int index) { }
        public UiWidget Create(int index)
        {
            var item = Items[index];
            return new UiToggle<bool, UiBooleanToggleValue, UiReleaseButtonBehavior, RowPresentation>(
                item.Name, new UiBinding<bool>(() => selected() == item, _ => select(item)));
        }
    }

    private readonly struct RowPresentation : IUiTogglePresentation
    {
        public static UiAccessibilityRole AccessibilityRole => UiAccessibilityRole.Button;
        public static UiControlTypographyScope BeginText(UiControlContext context, bool hasText) =>
            context.BeginTypography<UiRegularTypeface, UiFluentType300>(hasText);
        public static UiToggleLayout Measure(UiControlContext context, UiVector2 textSize, bool hasLabel) =>
            new(new(textSize.X + IconColumnWidth * 2 + 16, 36), default, default);
        public static UiToggleLayout Arrange(UiControlContext context, UiToggleLayout layout, UiVector2 allocatedSize) =>
            layout.CenterContent(allocatedSize);

        public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds,
            UiToggleLayout layout, UiButtonInteraction interaction, bool? selected)
        {
            using var clip = context.Clip(bounds);
            var theme = context.Theme;
            if (selected == true || interaction.Hovered)
                context.Frame(bounds, selected == true ? theme.Header : theme.FrameBgHovered, default, 0, 0);
            if (interaction.FocusVisible) context.FocusOutline(bounds, 4);

            var columnWidth = MathF.Min(IconColumnWidth, bounds.Width / 3);
            var nameWidth = bounds.Width - columnWidth * 2;
            using (context.Clip(new(bounds.X, bounds.Y, nameWidth, bounds.Height)))
                context.TypographyButtonText(text, new(bounds.X + 8, bounds.Y, textSize.X, bounds.Height), textSize, theme.Text);

            var entry = CatalogByName.Value[text];
            var size = MathF.Min(22, columnWidth);
            if (size <= 0) return;
            var first = new UiRect(bounds.X + nameWidth + (columnWidth - size) / 2,
                bounds.Y + (bounds.Height - size) / 2, size, size);
            var second = first with { X = first.X + columnWidth };
            if (entry.Outline is { } outline) context.FontIcon<UiFluentRegularIconFont>(outline.Text, first, IconColor);
            if (entry.Filled is { } filled) context.FontIcon<UiFluentFilledIconFont>(filled.Text, second, IconColor);
        }
    }
}

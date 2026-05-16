// FBA: DSL 데이터 바인딩 샘플 — List<T> 바인딩, ForEach/ListBox/Combo/ListClipper with ItemsSource
#:property TargetFramework=net10.0
#:property OutputType=WinExe
#:property OptimizationPreference=Size
#:property InvariantGlobalization=true
#:property DebuggerSupport=false
#:property EventSourceSupport=false
#:property MetricsSupport=false
#:property MetadataUpdaterSupport=false
#:property StackTraceSupport=false
#:property UseSystemResourceKeys=true
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core;
using Duxel.Core.Dsl;
using Duxel.Windows.App;

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Data Binding Demo",
        Width = 1200,
        Height = 800,
        VSync = true
    },
    Screen = new DataBindingScreen()
});

// ── Model ────────────────────────────────────────────
// Note: In full projects with the Duxel analyzer, use [DslBindable] on classes
// to auto-generate BindObjectList(). FBA files use manual BindList with propertyAccessor.

public sealed class Product
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public double Price { get; set; }
    public int Stock { get; set; }
    public bool InStock { get; set; }
}

// ── Screen ───────────────────────────────────────────

public sealed class DataBindingScreen : UiScreen
{
    private readonly List<Product> _products;
    private readonly List<string> _categories;
    private readonly UiDslValueBinder _binder;
    private int _selectedProduct;
    private int _selectedCategory;

    public DataBindingScreen()
    {
        _categories = ["Electronics", "Books", "Clothing", "Food", "Sports"];

        _products =
        [
            new() { Name = "Laptop",       Category = "Electronics", Price = 999.99,  Stock = 15, InStock = true },
            new() { Name = "Headphones",   Category = "Electronics", Price = 149.50,  Stock = 42, InStock = true },
            new() { Name = "C# in Depth",  Category = "Books",       Price = 39.99,   Stock = 8,  InStock = true },
            new() { Name = "Winter Jacket", Category = "Clothing",   Price = 89.00,   Stock = 0,  InStock = false },
            new() { Name = "Running Shoes", Category = "Sports",     Price = 120.00,  Stock = 23, InStock = true },
            new() { Name = "Green Tea",     Category = "Food",       Price = 12.50,   Stock = 100, InStock = true },
            new() { Name = "Keyboard",      Category = "Electronics", Price = 79.99,  Stock = 30, InStock = true },
            new() { Name = "Design Patterns", Category = "Books",    Price = 45.00,   Stock = 5,  InStock = true },
            new() { Name = "T-Shirt",       Category = "Clothing",   Price = 25.00,   Stock = 0,  InStock = false },
            new() { Name = "Basketball",    Category = "Sports",     Price = 35.00,   Stock = 18, InStock = true },
        ];

        _binder = new UiDslValueBinder();

        // Phase B: simple string list binding
        _binder.BindList("categories", _categories);

        // Phase C: object list binding with manual property accessor
        // (In full projects, [DslBindable] on Product auto-generates BindObjectList())
        _binder.BindList("products", _products, p => p.Name, static (item, prop) => prop switch
        {
            "Name" => item.Name,
            "Category" => item.Category,
            "Price" => item.Price.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            "Stock" => item.Stock.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "InStock" => item.InStock ? "true" : "false",
            _ => null
        });
    }

    public override void Render(UiImmediateContext ui)
    {
        var viewport = ui.GetMainViewport();
        const float margin = 16f;

        ui.SetNextWindowPos(new UiVector2(viewport.Pos.X + margin, viewport.Pos.Y + margin));
        ui.SetNextWindowSize(new UiVector2(viewport.Size.X - margin * 2f, viewport.Size.Y - margin * 2f));
        ui.BeginWindow("Data Binding Demo");

        // ── Section 1: ListBox with ItemsSource (Phase B) ──
        ui.PushFontSize(20f);
        ui.Text("1. ListBox — String List Binding");
        ui.PopFontSize();
        ui.Separator();

        ui.Text("Categories (BindList<string>):");
        if (ui.ListBox(ref _selectedCategory, _categories, 5, "categoryList"))
        {
            // selection changed
        }
        ui.Text($"Selected: {_categories[_selectedCategory]}");
        ui.Spacing();

        // ── Section 2: ListBox with object binding (Phase C) ──
        ui.PushFontSize(20f);
        ui.Text("2. ListBox — Object Binding [DslBindable]");
        ui.PopFontSize();
        ui.Separator();

        ui.Text("Products (BindObjectList<Product>):");
        if (ui.ListBox(ref _selectedProduct, _products.Count, i => _products[i].Name, 6, "productList"))
        {
            // selection changed
        }

        var selected = _products[_selectedProduct];
        ui.Text($"Name: {selected.Name}");
        ui.Text($"Category: {selected.Category}");
        ui.Text($"Price: ${selected.Price:F2}");
        ui.Text($"Stock: {selected.Stock}");
        ui.Text($"In Stock: {selected.InStock}");
        ui.Spacing();

        // ── Section 3: ForEach with property template (Phase C) ──
        ui.PushFontSize(20f);
        ui.Text("3. ForEach — Property Template {product.Name}");
        ui.PopFontSize();
        ui.Separator();

        // Manual C# loop equivalent of DSL ForEach ItemsSource=products
        for (var i = 0; i < _products.Count; i++)
        {
            var p = _products[i];
            ui.BulletText($"{p.Name} — {p.Category} — ${p.Price:F2} (stock: {p.Stock})");
        }
        ui.Spacing();

        // ── Section 4: BeginListBox with Selectables (DSL: BeginListBox ItemsSource=) ──
        ui.PushFontSize(20f);
        ui.Text("4. BeginListBox — Selectable Items");
        ui.PopFontSize();
        ui.Separator();

        if (ui.BeginListBox(new UiVector2(0, 150), _products.Count, "productListBox"))
        {
            for (int i = 0; i < _products.Count; i++)
            {
                var isSelected = i == _selectedProduct;
                if (ui.Selectable(_products[i].Name, isSelected))
                    _selectedProduct = i;
            }
            ui.EndListBox();
        }
        ui.Spacing();

        // ── Section 5: BeginCombo with Selectables (DSL: BeginCombo ItemsSource=) ──
        ui.PushFontSize(20f);
        ui.Text("5. BeginCombo — Selectable Items");
        ui.PopFontSize();
        ui.Separator();

        var previewText = _categories[_selectedCategory];
        if (ui.BeginCombo(previewText, 8, "categoryCombo"))
        {
            for (int i = 0; i < _categories.Count; i++)
            {
                var isSelected = i == _selectedCategory;
                if (ui.Selectable(_categories[i], isSelected))
                    _selectedCategory = i;
            }
            ui.EndCombo();
        }
        ui.Spacing();

        // ── Section 6: Table from data (DSL: BeginTable ItemsSource= ColumnNames=) ──
        ui.PushFontSize(20f);
        ui.Text("6. Table — Product Data");
        ui.PopFontSize();
        ui.Separator();

        if (ui.BeginTable("productTable", 5, UiTableFlags.Borders | UiTableFlags.RowBg))
        {
            ui.TableSetupColumn("Name");
            ui.TableSetupColumn("Category");
            ui.TableSetupColumn("Price");
            ui.TableSetupColumn("Stock");
            ui.TableSetupColumn("InStock");
            ui.TableHeadersRow();

            for (int i = 0; i < _products.Count; i++)
            {
                var p = _products[i];
                ui.TableNextRow();
                ui.TableNextColumn(); ui.Text(p.Name);
                ui.TableNextColumn(); ui.Text(p.Category);
                ui.TableNextColumn(); ui.Text($"${p.Price:F2}");
                ui.TableNextColumn(); ui.Text(p.Stock.ToString());
                ui.TableNextColumn(); ui.Text(p.InStock ? "Yes" : "No");
            }
            ui.EndTable();
        }
        ui.Spacing();

        // ── Section 7: TabBar from data (DSL: BeginTabBar ItemsSource=) ──
        ui.PushFontSize(20f);
        ui.Text("7. TabBar — Category Tabs");
        ui.PopFontSize();
        ui.Separator();

        if (ui.BeginTabBar("categoryTabs"))
        {
            for (int i = 0; i < _categories.Count; i++)
            {
                if (ui.BeginTabItem(_categories[i]))
                {
                    ui.Text($"Items in {_categories[i]}:");
                    for (int j = 0; j < _products.Count; j++)
                    {
                        if (_products[j].Category == _categories[i])
                            ui.BulletText($"{_products[j].Name} — ${_products[j].Price:F2}");
                    }
                    ui.EndTabItem();
                }
            }
            ui.EndTabBar();
        }

        ui.EndWindow();
    }
}

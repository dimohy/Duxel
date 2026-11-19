using System;
using Duxel.Core;

public sealed class SampleScreen : UiScreen
{
    public bool UseEmbeddedMenuBar { get; init; }
    private int _count;
    private bool _enabled = true;
    private float _scale = 0.5f;
    private string _name = "Duxel";
    private string _notes = "Line 1\nLine 2";
    private int _flags = 0b101;
    private float _dragFloat = 0.25f;
    private float _dragX = 0.1f;
    private float _dragY = 0.2f;
    private float _dragZ = 0.3f;
    private int _dragInt = 5;
    private int _dragIntX = 1;
    private int _dragIntY = 2;
    private float _sliderX = 0.3f;
    private float _sliderY = 0.7f;
    private int _sliderIntX = 2;
    private int _sliderIntY = 8;
    private float _angle = 0.5f;
    private float _vSlider = 0.5f;
    private int _vSliderInt = 3;
    private int _comboIndex;
    private int _comboIndexGetter;
    private int _listIndex;
    private int _listIndexGetter;
    private bool _selectable;
    private bool _openHeader = true;
    private float _colorR = 0.3f;
    private float _colorG = 0.6f;
    private float _colorB = 0.9f;
    private float _colorA = 1f;
    private readonly float[] _plotValues = { 0.1f, 0.3f, 0.7f, 0.4f, 0.8f, 0.2f };
    private bool _openPopup;
    private int _tableRows = 6;
    private readonly string[] _items = { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta" };
    private readonly bool[] _tableEnabled = new bool[6];
    private readonly float[] _scalarFloats = { 0.1f, 0.2f, 0.3f, 0.4f };
    private readonly int[] _scalarInts = { 1, 2, 3 };
    private readonly double[] _scalarDoubles = { 0.5, 1.5, 2.5 };
    private readonly ImGuiExamples _imguiExamples = new();

    private readonly ImmediateSampleSection[] _sections;
    public Action<UiImmediateContext, ImmediateSampleSection>? ShowSource { get; set; }
    public SampleScreen()
    {
        _sections =
        [
            ImmediateSampleSection.Define("Menu", ui =>
            {
                if (UseEmbeddedMenuBar ? ui.BeginMenuBar() : ui.BeginMainMenuBar())
                {
                    if (ui.BeginMenu("File"))
                    {
                        ui.MenuItem("Open");
                        ui.MenuItem("Save");
                        ui.EndMenu();
                    }
                    if (ui.BeginMenu("Help"))
                    {
                        ui.MenuItem("About");
                        ui.EndMenu();
                    }
                    if (UseEmbeddedMenuBar)
                        ui.EndMenuBar();
                    else
                        ui.EndMainMenuBar();
                }
            }, () => UseEmbeddedMenuBar, () => string.Join("\n",
                [
                    "bool UseEmbeddedMenuBar = " + ImmediateSampleSection.Boolean(UseEmbeddedMenuBar) + ";"
                ])),
            ImmediateSampleSection.Define("Basics", ui =>
            {
                ui.Text("Immediate-mode UI in Render");
                ui.TextColored(new UiColor(0xFF66C2FF), "TextColored");
                ui.TextDisabled("TextDisabled");
                ui.TextWrapped("TextWrapped: The quick brown fox jumps over the lazy dog. This should wrap within the window content region.");
                ui.LabelText("LabelText", "Value");
                ui.TextUnformatted("TextUnformatted");

                ui.Bullet();
                ui.SameLine();
                ui.Text("Bullet");

                ui.BeginGroup();
                ui.Text("BeginGroup");
                ui.Button("Grouped Button");
                ui.EndGroup();

                if (ui.Button("Increment"))
                {
                    _count++;
                }
                ui.SameLine();
                ui.SmallButton("SmallButton");
                ui.SameLine();
                ui.ArrowButton("ArrowLeft", UiDir.Left);
                ui.SameLine();
                ui.InvisibleButton("InvisibleButton", new UiVector2(30, 20));

                ui.Text($"Count: {_count}");
                ui.Checkbox("Enable Feature", ref _enabled);
                ui.CheckboxFlags("Flags 0", ref _flags, 1 << 0);
                ui.CheckboxFlags("Flags 1", ref _flags, 1 << 1);
                ui.CheckboxFlags("Flags 2", ref _flags, 1 << 2);

                ui.RadioButton("Radio A", ref _count, 0);
                ui.RadioButton("Radio B", ref _count, 1);
                ui.ProgressBar(_scale, new UiVector2(220f, 0f), null);
            }, () => (_count, _enabled, _scale, _flags), () => string.Join("\n",
                [
                    "int _count = " + ImmediateSampleSection.Integer(_count) + ";",
                    "bool _enabled = " + ImmediateSampleSection.Boolean(_enabled) + ";",
                    "float _scale = " + ImmediateSampleSection.Number(_scale) + ";",
                    "int _flags = " + ImmediateSampleSection.Integer(_flags) + ";"
                ])),
            ImmediateSampleSection.Define("Sliders", ui =>
            {
                ui.SetNextItemWidth(260f);
                ui.SliderFloat("Scale", ref _scale, 0f, 1f, 0.05f, "0.00");
                ui.SliderFloat2("SliderFloat2", ref _sliderX, ref _sliderY, 0f, 1f);
                ui.SliderInt2("SliderInt2", ref _sliderIntX, ref _sliderIntY, 0, 10);
                ui.SliderAngle("Angle", ref _angle, -180f, 180f);
                ui.SliderScalar("SliderScalar", ref _dragFloat, 0f, 1f, 0f, "0.00");
                ui.SliderScalarN("SliderScalarN", _scalarFloats, 0f, 1f, 0f, "0.00");
                ui.VSliderFloat("VSliderFloat", new UiVector2(20f, 90f), ref _vSlider, 0f, 1f);
                ui.SameLine();
                ui.VSliderInt("VSliderInt", new UiVector2(20f, 90f), ref _vSliderInt, 0, 10);
            }, () => (_scale, _dragFloat, _sliderX, _sliderY, _sliderIntX, _sliderIntY, _angle, _vSlider, _vSliderInt, _scalarFloats[0], _scalarFloats[1], _scalarFloats[2], _scalarFloats[3]), () => string.Join("\n",
                [
                    "float _scale = " + ImmediateSampleSection.Number(_scale) + ";",
                    "float _dragFloat = " + ImmediateSampleSection.Number(_dragFloat) + ";",
                    "float _sliderX = " + ImmediateSampleSection.Number(_sliderX) + ";",
                    "float _sliderY = " + ImmediateSampleSection.Number(_sliderY) + ";",
                    "int _sliderIntX = " + ImmediateSampleSection.Integer(_sliderIntX) + ";",
                    "int _sliderIntY = " + ImmediateSampleSection.Integer(_sliderIntY) + ";",
                    "float _angle = " + ImmediateSampleSection.Number(_angle) + ";",
                    "float _vSlider = " + ImmediateSampleSection.Number(_vSlider) + ";",
                    "int _vSliderInt = " + ImmediateSampleSection.Integer(_vSliderInt) + ";",
                    "float[] _scalarFloats = " + ImmediateSampleSection.Array(_scalarFloats, ImmediateSampleSection.Number) + ";"
                ])),
            ImmediateSampleSection.Define("Inputs", ui =>
            {
                ui.PushItemWidth(240f);
                ui.InputText("Name", ref _name, 24, UiInputTextFlags.None, null);
                ui.InputTextWithHint("WithHint", "hint", ref _name, 24);
                ui.PopItemWidth();
                ui.InputTextMultiline("Notes", ref _notes, 256, 5);
                ui.InputScalar("InputScalarInt", ref _dragInt);
                ui.InputScalar("InputScalarFloat", ref _dragFloat, "0.00");
                var scalarDouble = _scalarDoubles[0];
                ui.InputScalar("InputScalarDouble", ref scalarDouble, "0.00");
                _scalarDoubles[0] = scalarDouble;
                ui.InputScalarN("InputScalarN Int", _scalarInts);
                ui.InputScalarN("InputScalarN Float", _scalarFloats, "0.00");
                ui.InputScalarN("InputScalarN Double", _scalarDoubles, "0.00");
            }, () => (_name, _notes, _dragFloat, _dragInt, _scalarFloats[0], _scalarFloats[1], _scalarFloats[2], _scalarFloats[3], _scalarInts[0], _scalarInts[1], _scalarInts[2], _scalarDoubles[0], _scalarDoubles[1], _scalarDoubles[2]), () => string.Join("\n",
                [
                    "string _name = " + ImmediateSampleSection.Text(_name) + ";",
                    "string _notes = " + ImmediateSampleSection.Text(_notes) + ";",
                    "float _dragFloat = " + ImmediateSampleSection.Number(_dragFloat) + ";",
                    "int _dragInt = " + ImmediateSampleSection.Integer(_dragInt) + ";",
                    "float[] _scalarFloats = " + ImmediateSampleSection.Array(_scalarFloats, ImmediateSampleSection.Number) + ";",
                    "int[] _scalarInts = " + ImmediateSampleSection.Array(_scalarInts, ImmediateSampleSection.Integer) + ";",
                    "double[] _scalarDoubles = " + ImmediateSampleSection.Array(_scalarDoubles, ImmediateSampleSection.Number) + ";"
                ])),
            ImmediateSampleSection.Define("Drag", ui =>
            {
                ui.DragFloat("DragFloat", ref _dragFloat, 0.01f, 0f, 1f, "0.00");
                ui.DragFloat3("DragFloat3", ref _dragX, ref _dragY, ref _dragZ, 0.01f, 0f, 1f, "0.00");
                ui.DragInt("DragInt", ref _dragInt, 0.1f, 0, 10);
                ui.DragInt2("DragInt2", ref _dragIntX, ref _dragIntY, 0.1f, 0, 10);
                ui.DragScalar("DragScalar", ref _dragFloat, 0.01f, 0f, 1f, "0.00");
                ui.DragScalarN("DragScalarN", _scalarFloats, 0.01f, 0f, 1f, "0.00");
            }, () => (_dragFloat, _dragX, _dragY, _dragZ, _dragInt, _dragIntX, _dragIntY, _scalarFloats[0], _scalarFloats[1], _scalarFloats[2], _scalarFloats[3]), () => string.Join("\n",
                [
                    "float _dragFloat = " + ImmediateSampleSection.Number(_dragFloat) + ";",
                    "float _dragX = " + ImmediateSampleSection.Number(_dragX) + ";",
                    "float _dragY = " + ImmediateSampleSection.Number(_dragY) + ";",
                    "float _dragZ = " + ImmediateSampleSection.Number(_dragZ) + ";",
                    "int _dragInt = " + ImmediateSampleSection.Integer(_dragInt) + ";",
                    "int _dragIntX = " + ImmediateSampleSection.Integer(_dragIntX) + ";",
                    "int _dragIntY = " + ImmediateSampleSection.Integer(_dragIntY) + ";",
                    "float[] _scalarFloats = " + ImmediateSampleSection.Array(_scalarFloats, ImmediateSampleSection.Number) + ";"
                ])),
            ImmediateSampleSection.Define("Color", ui =>
            {
                ui.ColorEdit3("ColorEdit3", ref _colorR, ref _colorG, ref _colorB);
                ui.ColorEdit4("ColorEdit4", ref _colorR, ref _colorG, ref _colorB, ref _colorA);
                var color = new UiColor(
                    ((uint)(_colorB * 255f) & 0xFF) |
                    (((uint)(_colorG * 255f) & 0xFF) << 8) |
                    (((uint)(_colorR * 255f) & 0xFF) << 16) |
                    (((uint)(_colorA * 255f) & 0xFF) << 24)
                );
                ui.ColorButton("ColorButton", color, new UiVector2(24f, 24f));
                ui.ColorPicker3("ColorPicker3", ref _colorR, ref _colorG, ref _colorB);
                ui.ColorPicker4("ColorPicker4", ref _colorR, ref _colorG, ref _colorB, ref _colorA);
            }, () => (_colorR, _colorG, _colorB, _colorA), () => string.Join("\n",
                [
                    "float _colorR = " + ImmediateSampleSection.Number(_colorR) + ";",
                    "float _colorG = " + ImmediateSampleSection.Number(_colorG) + ";",
                    "float _colorB = " + ImmediateSampleSection.Number(_colorB) + ";",
                    "float _colorA = " + ImmediateSampleSection.Number(_colorA) + ";"
                ])),
            ImmediateSampleSection.Define("Lists/Combo", ui =>
            {
                ui.Text("Combo");
                ui.Combo(ref _comboIndex, _items, 5, "Combo");
                ui.Text("ComboGetter");
                ui.Combo(ref _comboIndexGetter, _items.Length, i => _items[i], 5, "ComboGetter");

                ui.Text("ListBox");
                ui.ListBox(ref _listIndex, _items, 4, "ListBox");
                ui.Text("ListBoxGetter");
                ui.ListBox(ref _listIndexGetter, _items.Length, i => _items[i], 4, "ListBoxGetter");

                if (_items.Length > 0)
                {
                    _comboIndex = Math.Clamp(_comboIndex, 0, _items.Length - 1);
                }
                ui.Text("BeginCombo");
                if (ui.BeginCombo(_items[_comboIndex], 5, "BeginCombo"))
                {
                    for (var i = 0; i < _items.Length; i++)
                    {
                        var selected = _comboIndex == i;
                        if (ui.Selectable(_items[i], selected))
                        {
                            _comboIndex = i;
                        }
                    }
                    ui.EndCombo();
                }

                ui.Text("BeginListBox");
                if (ui.BeginListBox(new UiVector2(0f, 0f), 4, "BeginListBox"))
                {
                    for (var i = 0; i < _items.Length; i++)
                    {
                        var selected = _listIndex == i;
                        if (ui.Selectable(_items[i], selected))
                        {
                            _listIndex = i;
                        }
                    }
                    ui.EndListBox();
                }
            }, () => (_comboIndex, _comboIndexGetter, _listIndex, _listIndexGetter, _items[0], _items[1], _items[2], _items[3], _items[4], _items[5], _items[6], _items[7]), () => string.Join("\n",
                [
                    "int _comboIndex = " + ImmediateSampleSection.Integer(_comboIndex) + ";",
                    "int _comboIndexGetter = " + ImmediateSampleSection.Integer(_comboIndexGetter) + ";",
                    "int _listIndex = " + ImmediateSampleSection.Integer(_listIndex) + ";",
                    "int _listIndexGetter = " + ImmediateSampleSection.Integer(_listIndexGetter) + ";",
                    "string[] _items = " + ImmediateSampleSection.Array(_items, ImmediateSampleSection.Text) + ";"
                ])),
            ImmediateSampleSection.Define("Selection/Tree/Tab", ui =>
            {
                ui.Selectable("Selectable", ref _selectable, UiSelectableFlags.None, new UiVector2(140f, 0f));

                if (ui.TreeNode("TreeNode", true))
                {
                    ui.Text("Tree child");
                    ui.TreePop();
                }
                if (ui.TreeNodeEx("TreeNodeEx", UiTreeNodeFlags.DefaultOpen))
                {
                    ui.Text("TreeNodeEx content");
                    ui.TreePop();
                }

                if (ui.CollapsingHeader("CollapsingHeader", ref _openHeader, UiTreeNodeFlags.DefaultOpen))
                {
                    ui.Text("Header content");
                }

                if (ui.BeginTabBar("Tabs", UiTabBarFlags.None))
                {
                    if (ui.SmallButton("Close Tab 2"))
                    {
                        ui.SetTabItemClosed("Tab 2");
                    }
                    ui.SameLine();
                    if (ui.BeginTabItem("Tab 1", UiTabItemFlags.None))
                    {
                        ui.Text("Tab 1 content");
                        ui.EndTabItem();
                    }
                    if (ui.BeginTabItem("Tab 2", UiTabItemFlags.None))
                    {
                        ui.Text("Tab 2 content");
                        ui.EndTabItem();
                    }
                    ui.TabItemButton("TabItemButton");
                    ui.EndTabBar();
                }
            }, () => (_selectable, _openHeader), () => string.Join("\n",
                [
                    "bool _selectable = " + ImmediateSampleSection.Boolean(_selectable) + ";",
                    "bool _openHeader = " + ImmediateSampleSection.Boolean(_openHeader) + ";"
                ])),
            ImmediateSampleSection.Define("Plots", ui =>
            {
                ui.PlotLines("PlotLines", _plotValues, _plotValues.Length, 0, null, float.NaN, float.NaN, new UiVector2(240f, 60f));
                ui.PlotHistogram("PlotHistogram", _plotValues, _plotValues.Length, 0, null, float.NaN, float.NaN, new UiVector2(240f, 60f));
            }, () => (_plotValues[0], _plotValues[1], _plotValues[2], _plotValues[3], _plotValues[4], _plotValues[5]), () => string.Join("\n",
                [
                    "float[] _plotValues = " + ImmediateSampleSection.Array(_plotValues, ImmediateSampleSection.Number) + ";"
                ])),
            ImmediateSampleSection.Define("Table", ui =>
            {
                if (ui.BeginTable("Table", 3, UiTableFlags.Borders | UiTableFlags.RowBg | UiTableFlags.Sortable))
                {
                    ui.TableSetupScrollFreeze(0, 1);
                    ui.TableSetupColumn("Name", 120f, 0f, UiTableColumnFlags.None);
                    ui.TableSetupColumn("Value", 100f, 1f, UiTableColumnFlags.None);
                    ui.TableSetupColumn("Enabled", 80f, 0f, UiTableColumnFlags.None);
                    ui.TableSetColumnEnabled(2, true);
                    ui.TableHeadersRowSortable();

                    ui.TableNextRow(UiTableRowFlags.None, 32f);
                    ui.Text("Header Row");
                    ui.TableNextColumn();
                    ui.Text("Pinned");
                    ui.TableNextColumn();
                    ui.TableSetBgColor(UiTableBgTarget.CellBg, new UiColor(0xFF2A4A68));
                    ui.Text("Bg");

                    for (var i = 0; i < _tableRows; i++)
                    {
                        ui.TableNextRow();
                        ui.Text($"Row {i}");
                        ui.TableNextColumn();
                        ui.Text($"{i * 10}");
                        ui.TableNextColumn();
                        if (i < _tableEnabled.Length)
                        {
                            ui.Checkbox($"##row{i}", ref _tableEnabled[i]);
                        }
                    }

                    ui.EndTable();
                }
            }, () => (_tableRows, _tableEnabled[0], _tableEnabled[1], _tableEnabled[2], _tableEnabled[3], _tableEnabled[4], _tableEnabled[5]), () => string.Join("\n",
                [
                    "int _tableRows = " + ImmediateSampleSection.Integer(_tableRows) + ";",
                    "bool[] _tableEnabled = " + ImmediateSampleSection.Array(_tableEnabled, ImmediateSampleSection.Boolean) + ";"
                ])),
            ImmediateSampleSection.Define("Popups/Tooltips", ui =>
            {
                if (ui.Button("Open Popup"))
                {
                    _openPopup = true;
                    ui.OpenPopup("Popup");
                }

                if (ui.BeginPopup("Popup"))
                {
                    ui.Text("Popup content");
                    if (ui.Button("Close"))
                    {
                        _openPopup = false;
                        ui.CloseCurrentPopup();
                    }
                    ui.EndPopup();
                }

                if (ui.BeginPopupContextItem("ContextItem"))
                {
                    ui.Text("Context Item Popup");
                    ui.EndPopup();
                }

                if (ui.BeginPopupContextWindow("ContextWindow"))
                {
                    ui.Text("Context Window Popup");
                    ui.EndPopup();
                }

                if (ui.BeginPopupContextVoid("ContextVoid"))
                {
                    ui.Text("Context Void Popup");
                    ui.EndPopup();
                }

                if (ui.BeginPopupModal("Modal", ref _openPopup))
                {
                    ui.Text("Modal content");
                    if (ui.Button("OK"))
                    {
                        _openPopup = false;
                    }
                    ui.EndPopupModal(ref _openPopup);
                }

                if (ui.BeginTooltip())
                {
                    ui.Text("Tooltip via BeginTooltip");
                    ui.EndTooltip();
                }

                ui.SetTooltip("SetTooltip demo");
            }, () => _openPopup, () => string.Join("\n",
                [
                    "bool _openPopup = " + ImmediateSampleSection.Boolean(_openPopup) + ";"
                ]))
        ];
    }
    private void RenderSection(UiImmediateContext ui, int index)
    {
        var section = _sections[index];
        section.Render(ui);
        ShowSource?.Invoke(ui, section);
    }

    public override void Render(UiImmediateContext ui)
    {
        if (!ui.BeginTabBar("sample-pages")) return;
        if (ui.BeginTabItem("Hello", UiTabItemFlags.None))
        {
            _imguiExamples.Render(ui);
            ShowSource?.Invoke(ui, _imguiExamples.Section);
            ui.EndTabItem();
        }
        if (ui.BeginTabItem("Immediate", UiTabItemFlags.None))
        {
            RenderSection(ui, 0);
            if (ui.CollapsingHeader("Basics", UiTreeNodeFlags.DefaultOpen))
            {
                RenderSection(ui, 1);
            }
            if (ui.CollapsingHeader("Sliders", UiTreeNodeFlags.DefaultOpen))
            {
                RenderSection(ui, 2);
            }
            if (ui.CollapsingHeader("Inputs", UiTreeNodeFlags.DefaultOpen))
            {
                RenderSection(ui, 3);
            }
            if (ui.CollapsingHeader("Drag", UiTreeNodeFlags.DefaultOpen))
            {
                RenderSection(ui, 4);
            }
            if (ui.CollapsingHeader("Color", UiTreeNodeFlags.DefaultOpen))
            {
                RenderSection(ui, 5);
            }
            if (ui.CollapsingHeader("Lists/Combo", UiTreeNodeFlags.DefaultOpen))
            {
                RenderSection(ui, 6);
            }
            if (ui.CollapsingHeader("Selection/Tree/Tab", UiTreeNodeFlags.DefaultOpen))
            {
                RenderSection(ui, 7);
            }
            ui.EndTabItem();
        }
        if (ui.BeginTabItem("Widget Gallery", UiTabItemFlags.None))
        {
            if (ui.CollapsingHeader("Plots", UiTreeNodeFlags.DefaultOpen))
            {
                RenderSection(ui, 8);
            }
            if (ui.CollapsingHeader("Table", UiTreeNodeFlags.DefaultOpen))
            {
                RenderSection(ui, 9);
            }
            if (ui.CollapsingHeader("Popups/Tooltips", UiTreeNodeFlags.DefaultOpen))
            {
                RenderSection(ui, 10);
            }
            ui.EndTabItem();
        }
        ui.EndTabBar();
    }
}

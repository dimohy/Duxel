using System;
using System.Collections.Generic;
using Duxel.Core;

public sealed class SampleScreen : UiScreen
{
    public static readonly IReadOnlyList<string> GlyphStrings = new[]
    {
        "Duxel Immediate Sample",
        "Immediate-mode UI in Render",
        "Increment",
        "Count:",
        "Scale",
        "Name",
        "Notes",
        "Enable Feature",
        "Enabled",
        "Disabled",
        "DSL Demo 데모",
        "Play 재생",
        "Options 옵션",
        "Exit 종료",
        "Status 상태: Ready 준비됨",
        "Refresh 새로고침",
        "Enable VSync 수직동기화",
        "Fullscreen 전체화면"
    };

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

    public override void Render(UiImmediateContext ui)
    {
        _imguiExamples.Render(ui);

        ui.BeginWindow("Duxel Immediate Sample");

        if (ui.BeginMainMenuBar())
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
            ui.EndMainMenuBar();
        }

        if (ui.CollapsingHeader("Basics", UiTreeNodeFlags.DefaultOpen))
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
        }

        if (ui.CollapsingHeader("Sliders", UiTreeNodeFlags.DefaultOpen))
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
        }

        if (ui.CollapsingHeader("Inputs", UiTreeNodeFlags.DefaultOpen))
        {
            ui.PushItemWidth(240f);
            ui.InputText("Name", ref _name, 24, UiInputTextFlags.None, null);
            ui.InputTextWithHint("WithHint", "hint", ref _name, 24);
            ui.PopItemWidth();
            ui.InputTextMultiline("Notes", ref _notes, 256, 120f);
            ui.InputScalar("InputScalarInt", ref _dragInt);
            ui.InputScalar("InputScalarFloat", ref _dragFloat, "0.00");
            var scalarDouble = _scalarDoubles[0];
            ui.InputScalar("InputScalarDouble", ref scalarDouble, "0.00");
            _scalarDoubles[0] = scalarDouble;
            ui.InputScalarN("InputScalarN Int", _scalarInts);
            ui.InputScalarN("InputScalarN Float", _scalarFloats, "0.00");
            ui.InputScalarN("InputScalarN Double", _scalarDoubles, "0.00");
        }

        if (ui.CollapsingHeader("Drag", UiTreeNodeFlags.DefaultOpen))
        {
            ui.DragFloat("DragFloat", ref _dragFloat, 0.01f, 0f, 1f, "0.00");
            ui.DragFloat3("DragFloat3", ref _dragX, ref _dragY, ref _dragZ, 0.01f, 0f, 1f, "0.00");
            ui.DragInt("DragInt", ref _dragInt, 0.1f, 0, 10);
            ui.DragInt2("DragInt2", ref _dragIntX, ref _dragIntY, 0.1f, 0, 10);
            ui.DragScalar("DragScalar", ref _dragFloat, 0.01f, 0f, 1f, "0.00");
            ui.DragScalarN("DragScalarN", _scalarFloats, 0.01f, 0f, 1f, "0.00");
        }

        if (ui.CollapsingHeader("Color", UiTreeNodeFlags.DefaultOpen))
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
        }

        if (ui.CollapsingHeader("Lists/Combo", UiTreeNodeFlags.DefaultOpen))
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
        }

        if (ui.CollapsingHeader("Selection/Tree/Tab", UiTreeNodeFlags.DefaultOpen))
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
        }

        ui.EndWindow();

        ui.BeginWindow("Duxel Widget Gallery");

        if (ui.CollapsingHeader("Plots", UiTreeNodeFlags.DefaultOpen))
        {
            ui.PlotLines("PlotLines", _plotValues, _plotValues.Length, 0, null, float.NaN, float.NaN, new UiVector2(240f, 60f));
            ui.PlotHistogram("PlotHistogram", _plotValues, _plotValues.Length, 0, null, float.NaN, float.NaN, new UiVector2(240f, 60f));
        }

        if (ui.CollapsingHeader("Table", UiTreeNodeFlags.DefaultOpen))
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
        }

        if (ui.CollapsingHeader("Popups/Tooltips", UiTreeNodeFlags.DefaultOpen))
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
        }

        ui.EndWindow();
    }
}


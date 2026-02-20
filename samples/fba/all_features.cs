// FBA: Duxel 전체 위젯 종합 시연 — Immediate Mode API의 거의 모든 기능을 하나의 앱에서 데모
#:property TargetFramework=net10.0
#:property platform=windows
// run-fba 사용 시: --platform windows
// dotnet run 직접 사용 시: -p:platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using System.Collections.Generic;
using System.Text;
using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel All Features Showcase",
        Width = 1600,
        Height = 1000,
        VSync = false
    },
    Screen = new AllFeaturesScreen()
});

public sealed class AllFeaturesScreen : UiScreen
{
    // ── Basic ──
    private int _clickCount;
    private bool _checkboxA = true;
    private bool _checkboxB;
    private int _flagsBitmask = 0b101;
    private int _radioValue;
    private float _progress;
    private bool _progressDir = true;

    // ── Sliders ──
    private float _sliderFloat = 0.5f;
    private int _sliderInt = 5;
    private float _sl2x = 0.3f, _sl2y = 0.7f;
    private float _sl3x = 0.1f, _sl3y = 0.5f, _sl3z = 0.9f;
    private float _sl4x = 0.2f, _sl4y = 0.4f, _sl4z = 0.6f, _sl4w = 0.8f;
    private int _slI2x = 2, _slI2y = 8;
    private int _slI3x = 1, _slI3y = 5, _slI3z = 9;
    private int _slI4x = 0, _slI4y = 3, _slI4z = 6, _slI4w = 9;
    private float _angle = 1.57f;
    private float _vSliderF = 0.5f;
    private int _vSliderI = 3;
    private readonly float[] _scalarFloats = [0.1f, 0.2f, 0.3f, 0.4f];
    private readonly int[] _scalarInts = [1, 2, 3];
    private readonly double[] _scalarDoubles = [0.5, 1.5, 2.5];

    // ── Drag ──
    private float _dragF = 0.5f;
    private int _dragI = 5;
    private float _drag2x = 0.1f, _drag2y = 0.2f;
    private float _drag3x = 0.1f, _drag3y = 0.2f, _drag3z = 0.3f;
    private float _drag4x, _drag4y, _drag4z, _drag4w;
    private int _dragI2x = 1, _dragI2y = 2;
    private int _dragI3x = 1, _dragI3y = 2, _dragI3z = 3;
    private int _dragI4x, _dragI4y, _dragI4z, _dragI4w;
    private float _dragRangeMin = 0.2f, _dragRangeMax = 0.8f;
    private int _dragIRangeMin = 2, _dragIRangeMax = 8;

    // ── Input ──
    private string _inputText = "Hello";
    private string _inputHint = "";
    private string _inputMultiline = "Line 1\nLine 2\nLine 3";
    private int _inputInt = 42;
    private int _inputI2x = 1, _inputI2y = 2;
    private int _inputI3x = 1, _inputI3y = 2, _inputI3z = 3;
    private int _inputI4x = 1, _inputI4y = 2, _inputI4z = 3, _inputI4w = 4;
    private float _inputFloat = 3.14f;
    private float _inputF2x = 1f, _inputF2y = 2f;
    private float _inputF3x = 1f, _inputF3y = 2f, _inputF3z = 3f;
    private float _inputF4x = 1f, _inputF4y = 2f, _inputF4z = 3f, _inputF4w = 4f;
    private double _inputDouble = 2.718;
    private double _inputD2x = 1.0, _inputD2y = 2.0;

    // ── Color ──
    private float _colR = 0.4f, _colG = 0.7f, _colB = 1.0f, _colA = 1.0f;
    private float _pickR = 0.8f, _pickG = 0.2f, _pickB = 0.5f, _pickA = 1.0f;

    // ── Combo/ListBox ──
    private int _comboIdx;
    private int _comboGetterIdx;
    private int _listBoxIdx;
    private int _listBoxGetterIdx;
    private int _beginComboIdx;
    private int _beginListBoxIdx;
    private readonly string[] _items = ["Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta"];

    // ── Selectable ──
    private readonly bool[] _selectables = [false, true, false, false];

    // ── Tree ──
    private bool _openHeaderRef = true;

    // ── Tab ──
    private bool _tabClosed;

    // ── Table ──
    private readonly bool[] _tableChecks = new bool[5];

    // ── Popup ──
    private bool _modalOpen;

    // ── Child ──
    private float _childScroll;

    // ── Columns ──

    // ── Disabled ──
    private bool _disableSection;
    private float _disabledFloat = 0.5f;

    // ── Style ──
    private bool _customStyle;

    // ── Plot data ──
    private readonly float[] _plotSin = new float[64];
    private readonly float[] _plotHist = [0.2f, 0.8f, 0.4f, 0.9f, 0.1f, 0.6f, 0.3f, 0.7f];

    // ── Drawing ──
    private float _drawThickness = 2f;
    private int _drawSegments = 24;

    // ── DragDrop ──
    private string _dragDropSource = "Drag me!";
    private string _dragDropTarget = "[Drop here]";

    // ── ListClipper ──
    private int _clipperItemCount = 10000;

    // ── Demo/Debug ──
    private bool _showDemo;
    private bool _showMetrics;
    private bool _showDebugLog;
    private bool _showIdStack;
    private bool _showAbout;

    // ── Misc ──
    private int _frameCounter;
    private readonly UiFpsCounter _fpsCounter = new(0.5d);
    private float _fps;

    public AllFeaturesScreen()
    {
        for (var i = 0; i < _plotSin.Length; i++)
        {
            _plotSin[i] = MathF.Sin(i * 0.2f) * 0.5f + 0.5f;
        }
    }

    public override void Render(UiImmediateContext ui)
    {
        _frameCounter++;
        UpdateFps();

        // Animate progress bar
        if (_progressDir) _progress += 0.005f;
        else _progress -= 0.005f;
        if (_progress >= 1f) { _progress = 1f; _progressDir = false; }
        if (_progress <= 0f) { _progress = 0f; _progressDir = true; }

        RenderMainMenuBar(ui);
        RenderBasicWidgets(ui);
        RenderSliders(ui);
        RenderDragWidgets(ui);
        RenderInputWidgets(ui);
        RenderColorWidgets(ui);
        RenderComboListBox(ui);
        RenderSelectableTreeTab(ui);
        RenderTableWidget(ui);
        RenderPopupTooltip(ui);
        RenderChildColumnsLayout(ui);
        RenderDrawingPrimitives(ui);
        RenderDragDrop(ui);
        RenderListClipper(ui);
        RenderStyleDisabledMisc(ui);
        RenderDemoDebugWindows(ui);
        RenderFpsOverlay(ui);
    }

    // ─────────────────────────── Main Menu Bar ───────────────────────────
    private void RenderMainMenuBar(UiImmediateContext ui)
    {
        if (ui.BeginMainMenuBar())
        {
            if (ui.BeginMenu("File"))
            {
                ui.MenuItem("New");
                ui.MenuItem("Open");
                if (ui.BeginMenu("Recent"))
                {
                    ui.MenuItem("Project_A.dux");
                    ui.MenuItem("Project_B.dux");
                    if (ui.BeginMenu("Archived"))
                    {
                        ui.MenuItem("Old_1.dux");
                        ui.MenuItem("Old_2.dux");
                        ui.EndMenu();
                    }
                    ui.EndMenu();
                }
                ui.Separator();
                ui.MenuItem("Save");
                ui.MenuItem("Exit");
                ui.EndMenu();
            }
            if (ui.BeginMenu("Edit"))
            {
                ui.MenuItem("Undo");
                ui.MenuItem("Redo");
                ui.Separator();
                ui.MenuItem("Cut");
                ui.MenuItem("Copy");
                ui.MenuItem("Paste");
                ui.EndMenu();
            }
            if (ui.BeginMenu("View"))
            {
                if (ui.MenuItem("Demo Window")) _showDemo = !_showDemo;
                if (ui.MenuItem("Metrics")) _showMetrics = !_showMetrics;
                if (ui.MenuItem("Debug Log")) _showDebugLog = !_showDebugLog;
                if (ui.MenuItem("ID Stack")) _showIdStack = !_showIdStack;
                if (ui.MenuItem("About")) _showAbout = !_showAbout;
                ui.EndMenu();
            }
            if (ui.BeginMenu("Help"))
            {
                ui.MenuItem("User Guide");
                ui.MenuItem("About Duxel");
                ui.EndMenu();
            }
            ui.EndMainMenuBar();
        }
    }

    // ─────────────────────────── Basic Widgets ───────────────────────────
    private void RenderBasicWidgets(UiImmediateContext ui)
    {
        if (_frameCounter == 1) { ui.SetNextWindowSize(new UiVector2(380f, 480f)); ui.SetNextWindowCollapsed(true); }
        ui.BeginWindow("Basic Widgets");

        ui.SeparatorText("Text Variants");
        ui.Text("Text: Normal");
        ui.TextV("TextV: frame={0}", _frameCounter);
        ui.TextColored(new UiColor(0xFF44BBFF), "TextColored: Blue");
        ui.TextColoredV(new UiColor(0xFF44FF88), "TextColoredV: {0}", "Green");
        ui.TextDisabled("TextDisabled");
        ui.TextDisabledV("TextDisabledV: {0}", "dimmed");
        ui.TextWrapped("TextWrapped: The quick brown fox jumps over the lazy dog. This text will wrap within the window.");
        ui.TextWrappedV("TextWrappedV: val={0:0.0}", _progress);
        ui.TextUnformatted("TextUnformatted");
        ui.LabelText("LabelText", "value");
        ui.LabelTextV("LabelTextV", "{0}", _clickCount);
        ui.BulletText("BulletText");
        ui.BulletTextV("BulletTextV: {0}", "item");

        ui.SeparatorText("Buttons");
        if (ui.Button("Button")) _clickCount++;
        ui.SameLine();
        if (ui.Button("Sized Button", new UiVector2(120f, 30f))) _clickCount += 10;
        if (ui.SmallButton("SmallButton")) _clickCount--;
        ui.SameLine();
        ui.ArrowButton("arr_l", UiDir.Left);
        ui.SameLine();
        ui.ArrowButton("arr_r", UiDir.Right);
        ui.SameLine();
        ui.ArrowButton("arr_u", UiDir.Up);
        ui.SameLine();
        ui.ArrowButton("arr_d", UiDir.Down);
        ui.InvisibleButton("inv_btn", new UiVector2(60f, 20f));
        ui.SameLine();
        ui.Text("(InvisibleButton left)");
        ui.Text($"Click count: {_clickCount}");

        ui.SeparatorText("Toggles");
        ui.Checkbox("Checkbox A", ref _checkboxA);
        ui.SameLine();
        ui.Checkbox("Checkbox B", ref _checkboxB);
        ui.CheckboxFlags("Flag 0 (1<<0)", ref _flagsBitmask, 1 << 0);
        ui.CheckboxFlags("Flag 1 (1<<1)", ref _flagsBitmask, 1 << 1);
        ui.CheckboxFlags("Flag 2 (1<<2)", ref _flagsBitmask, 1 << 2);
        ui.Text($"Flags bitmask: 0b{Convert.ToString(_flagsBitmask, 2).PadLeft(3, '0')}");
        ui.RadioButton("Radio A", ref _radioValue, 0);
        ui.SameLine();
        ui.RadioButton("Radio B", ref _radioValue, 1);
        ui.SameLine();
        ui.RadioButton("Radio C", ref _radioValue, 2);

        ui.SeparatorText("Progress & Values");
        ui.ProgressBar(_progress, new UiVector2(200f, 16f), $"{_progress * 100f:0}%");
        ui.Bullet();
        ui.SameLine();
        ui.Text("Bullet + SameLine");
        ui.Value("Bool", _checkboxA);
        ui.Value("Int", _clickCount);
        ui.Value("Float", _progress);

        ui.EndWindow();
    }

    // ─────────────────────────── Sliders ───────────────────────────
    private void RenderSliders(UiImmediateContext ui)
    {
        if (_frameCounter == 1) { ui.SetNextWindowSize(new UiVector2(380f, 300f)); ui.SetNextWindowCollapsed(true); }
        ui.BeginWindow("Sliders");

        ui.SeparatorText("Float/Int Sliders");
        ui.SliderFloat("SliderFloat", ref _sliderFloat, 0f, 1f, 0.01f, "0.00");
        ui.SliderInt("SliderInt", ref _sliderInt, 0, 10);
        ui.SliderFloat2("SliderFloat2", ref _sl2x, ref _sl2y, 0f, 1f);
        ui.SliderFloat3("SliderFloat3", ref _sl3x, ref _sl3y, ref _sl3z, 0f, 1f);
        ui.SliderFloat4("SliderFloat4", ref _sl4x, ref _sl4y, ref _sl4z, ref _sl4w, 0f, 1f);
        ui.SliderInt2("SliderInt2", ref _slI2x, ref _slI2y, 0, 10);
        ui.SliderInt3("SliderInt3", ref _slI3x, ref _slI3y, ref _slI3z, 0, 10);
        ui.SliderInt4("SliderInt4", ref _slI4x, ref _slI4y, ref _slI4z, ref _slI4w, 0, 10);

        ui.SeparatorText("Angle & Scalar");
        ui.SliderAngle("SliderAngle", ref _angle, -180f, 180f);
        ui.SliderScalar("SliderScalar(f)", ref _sliderFloat, 0f, 1f, 0f, "0.00");
        ui.SliderScalarN("SliderScalarN(f[])", _scalarFloats, 0f, 1f, 0f, "0.00");
        ui.SliderScalarN("SliderScalarN(i[])", _scalarInts, 0, 10);
        ui.SliderScalarN("SliderScalarN(d[])", _scalarDoubles, 0.0, 3.0, 0.0, "0.00");

        ui.SeparatorText("Vertical Sliders");
        ui.VSliderFloat("VSliderFloat", new UiVector2(24f, 100f), ref _vSliderF, 0f, 1f);
        ui.SameLine();
        ui.VSliderInt("VSliderInt", new UiVector2(24f, 100f), ref _vSliderI, 0, 10);

        ui.EndWindow();
    }

    // ─────────────────────────── Drag Widgets ───────────────────────────
    private void RenderDragWidgets(UiImmediateContext ui)
    {
        if (_frameCounter == 1) { ui.SetNextWindowSize(new UiVector2(380f, 260f)); ui.SetNextWindowCollapsed(true); }
        ui.BeginWindow("Drag Widgets");

        ui.SeparatorText("DragFloat/Int");
        ui.DragFloat("DragFloat", ref _dragF, 0.01f, 0f, 1f, "0.00");
        ui.DragInt("DragInt", ref _dragI, 0.2f, 0, 20);
        ui.DragFloat2("DragFloat2", ref _drag2x, ref _drag2y, 0.01f, 0f, 1f, "0.00");
        ui.DragFloat3("DragFloat3", ref _drag3x, ref _drag3y, ref _drag3z, 0.01f, 0f, 1f, "0.00");
        ui.DragFloat4("DragFloat4", ref _drag4x, ref _drag4y, ref _drag4z, ref _drag4w, 0.01f, 0f, 1f, "0.00");
        ui.DragInt2("DragInt2", ref _dragI2x, ref _dragI2y, 0.2f, 0, 10);
        ui.DragInt3("DragInt3", ref _dragI3x, ref _dragI3y, ref _dragI3z, 0.2f, 0, 10);
        ui.DragInt4("DragInt4", ref _dragI4x, ref _dragI4y, ref _dragI4z, ref _dragI4w, 0.2f, 0, 10);

        ui.SeparatorText("DragRange");
        ui.DragFloatRange2("DragFloatRange2", ref _dragRangeMin, ref _dragRangeMax, 0.005f, 0f, 1f, "Min: 0.00", "Max: 0.00");
        ui.DragIntRange2("DragIntRange2", ref _dragIRangeMin, ref _dragIRangeMax, 0.2f, 0, 10);

        ui.SeparatorText("DragScalar");
        ui.DragScalar("DragScalar(f)", ref _dragF, 0.01f, 0f, 1f, "0.00");
        ui.DragScalarN("DragScalarN(f[])", _scalarFloats, 0.01f, 0f, 1f, "0.00");
        ui.DragScalarN("DragScalarN(i[])", _scalarInts, 0.2f, 0, 10);
        ui.DragScalarN("DragScalarN(d[])", _scalarDoubles, 0.01f, 0.0, 3.0, "0.00");

        ui.EndWindow();
    }

    // ─────────────────────────── Input Widgets ───────────────────────────
    private void RenderInputWidgets(UiImmediateContext ui)
    {
        if (_frameCounter == 1) { ui.SetNextWindowSize(new UiVector2(400f, 340f)); ui.SetNextWindowCollapsed(true); }
        ui.BeginWindow("Input Widgets");

        ui.SeparatorText("Text Input");
        ui.InputText("InputText", ref _inputText, 64);
        ui.InputTextWithHint("WithHint", "type here...", ref _inputHint, 64);
        ui.InputTextMultiline("Multiline", ref _inputMultiline, 512, 80f);

        ui.SeparatorText("Numeric Input");
        ui.InputInt("InputInt", ref _inputInt);
        ui.InputInt2("InputInt2", ref _inputI2x, ref _inputI2y);
        ui.InputInt3("InputInt3", ref _inputI3x, ref _inputI3y, ref _inputI3z);
        ui.InputInt4("InputInt4", ref _inputI4x, ref _inputI4y, ref _inputI4z, ref _inputI4w);
        ui.InputFloat("InputFloat", ref _inputFloat, "0.00");
        ui.InputFloat2("InputFloat2", ref _inputF2x, ref _inputF2y, "0.00");
        ui.InputFloat3("InputFloat3", ref _inputF3x, ref _inputF3y, ref _inputF3z, "0.00");
        ui.InputFloat4("InputFloat4", ref _inputF4x, ref _inputF4y, ref _inputF4z, ref _inputF4w, "0.00");
        ui.InputDouble("InputDouble", ref _inputDouble, "0.000");
        ui.InputDouble2("InputDouble2", ref _inputD2x, ref _inputD2y, "0.00");

        ui.SeparatorText("InputScalar");
        ui.InputScalar("InputScalar(i)", ref _inputInt);
        ui.InputScalar("InputScalar(f)", ref _inputFloat, "0.00");
        ui.InputScalar("InputScalar(d)", ref _inputDouble, "0.000");
        ui.InputScalarN("InputScalarN(i[])", _scalarInts);
        ui.InputScalarN("InputScalarN(f[])", _scalarFloats, "0.00");
        ui.InputScalarN("InputScalarN(d[])", _scalarDoubles, "0.00");

        ui.EndWindow();
    }

    // ─────────────────────────── Color Widgets ───────────────────────────
    private void RenderColorWidgets(UiImmediateContext ui)
    {
        if (_frameCounter == 1) { ui.SetNextWindowSize(new UiVector2(380f, 280f)); ui.SetNextWindowCollapsed(true); }
        ui.BeginWindow("Color Widgets");

        ui.SeparatorText("ColorEdit");
        ui.ColorEdit3("ColorEdit3", ref _colR, ref _colG, ref _colB);
        ui.ColorEdit4("ColorEdit4", ref _colR, ref _colG, ref _colB, ref _colA);

        ui.SeparatorText("ColorPicker");
        ui.ColorPicker3("ColorPicker3", ref _pickR, ref _pickG, ref _pickB);
        ui.ColorPicker4("ColorPicker4", ref _pickR, ref _pickG, ref _pickB, ref _pickA);

        ui.SeparatorText("ColorButton");
        var btnColor = new UiColor(
            ((uint)(_colB * 255f) & 0xFF) |
            (((uint)(_colG * 255f) & 0xFF) << 8) |
            (((uint)(_colR * 255f) & 0xFF) << 16) |
            (((uint)(_colA * 255f) & 0xFF) << 24));
        ui.ColorButton("color_btn", btnColor, new UiVector2(32f, 32f));
        ui.SameLine();
        ui.Text("ColorButton");

        ui.EndWindow();
    }

    // ─────────────────────────── Combo/ListBox ───────────────────────────
    private void RenderComboListBox(UiImmediateContext ui)
    {
        if (_frameCounter == 1) { ui.SetNextWindowSize(new UiVector2(380f, 320f)); ui.SetNextWindowCollapsed(true); }
        ui.BeginWindow("Combo & ListBox");

        ui.SeparatorText("Combo");
        ui.Text("Combo");
        ui.Combo(ref _comboIdx, _items, 5, "Combo");
        ui.Text("Combo(getter)");
        ui.Combo(ref _comboGetterIdx, _items.Length, i => _items[i], 5, "Combo(getter)");

        _beginComboIdx = Math.Clamp(_beginComboIdx, 0, _items.Length - 1);
        ui.Text("BeginCombo");
        if (ui.BeginCombo(_items[_beginComboIdx], 5, "BeginCombo"))
        {
            for (var i = 0; i < _items.Length; i++)
            {
                if (ui.Selectable(_items[i], _beginComboIdx == i))
                    _beginComboIdx = i;
            }
            ui.EndCombo();
        }

        ui.SeparatorText("ListBox");
        ui.Text("ListBox");
        ui.ListBox(ref _listBoxIdx, _items, 4, "ListBox");
        ui.Text("ListBox(getter)");
        ui.ListBox(ref _listBoxGetterIdx, _items.Length, i => _items[i], 4, "ListBox(getter)");

        ui.Text("BeginListBox");
        if (ui.BeginListBox(new UiVector2(0f, 0f), 4, "BeginListBox"))
        {
            for (var i = 0; i < _items.Length; i++)
            {
                if (ui.Selectable(_items[i], _beginListBoxIdx == i))
                    _beginListBoxIdx = i;
            }
            ui.EndListBox();
        }

        ui.EndWindow();
    }

    // ─────────────────────────── Selectable/Tree/Tab ───────────────────────────
    private void RenderSelectableTreeTab(UiImmediateContext ui)
    {
        if (_frameCounter == 1) { ui.SetNextWindowSize(new UiVector2(400f, 380f)); ui.SetNextWindowCollapsed(true); }
        ui.BeginWindow("Selectable / Tree / Tab");

        ui.SeparatorText("Selectable");
        for (var i = 0; i < _selectables.Length; i++)
        {
            ui.Selectable($"Selectable {i}", ref _selectables[i]);
        }
        ui.Selectable("Sized Selectable", false, UiSelectableFlags.None, new UiVector2(180f, 24f));

        ui.SeparatorText("Tree Nodes");
        if (ui.TreeNode("TreeNode A", true))
        {
            ui.Text("Child of A");
            if (ui.TreeNode("TreeNode A.1", false))
            {
                ui.Text("Leaf A.1");
                ui.TreePop();
            }
            ui.TreePop();
        }
        if (ui.TreeNodeEx("TreeNodeEx B", UiTreeNodeFlags.DefaultOpen))
        {
            ui.Text("Child of B");
            ui.TreePop();
        }
        if (ui.TreeNodeEx("TreeNodeEx Leaf", UiTreeNodeFlags.DefaultOpen))
        {
            ui.BulletText("Bullet tree node leaf");
            ui.TreePop();
        }
        if (ui.CollapsingHeader("CollapsingHeader"))
        {
            ui.Text("Collapsible content");
        }
        if (ui.CollapsingHeader("CollapsingHeader(ref)", ref _openHeaderRef, UiTreeNodeFlags.DefaultOpen))
        {
            ui.Text("Closeable header content");
        }

        ui.SeparatorText("Tab Bar");
        if (ui.BeginTabBar("MainTabs"))
        {
            if (ui.BeginTabItem("Tab A"))
            {
                ui.Text("Content of Tab A");
                ui.EndTabItem();
            }
            if (ui.BeginTabItem("Tab B"))
            {
                ui.Text("Content of Tab B");
                ui.EndTabItem();
            }
            if (!_tabClosed && ui.BeginTabItem("Closeable", UiTabItemFlags.None))
            {
                ui.Text("This tab can be closed");
                if (ui.SmallButton("Close This Tab"))
                {
                    _tabClosed = true;
                    ui.SetTabItemClosed("Closeable");
                }
                ui.EndTabItem();
            }
            ui.TabItemButton("+");
            ui.EndTabBar();
        }

        ui.EndWindow();
    }

    // ─────────────────────────── Table ───────────────────────────
    private void RenderTableWidget(UiImmediateContext ui)
    {
        if (_frameCounter == 1) { ui.SetNextWindowSize(new UiVector2(450f, 320f)); ui.SetNextWindowCollapsed(true); }
        ui.BeginWindow("Table");

        if (ui.BeginTable("AllFeaturesTable", 4, UiTableFlags.Borders | UiTableFlags.RowBg | UiTableFlags.Sortable))
        {
            ui.TableSetupScrollFreeze(0, 1);
            ui.TableSetupColumn("ID", 50f, 0f, UiTableColumnFlags.None);
            ui.TableSetupColumn("Name", 100f, 0f, UiTableColumnFlags.None);
            ui.TableSetupColumn("Value", 80f, 1f, UiTableColumnFlags.None);
            ui.TableSetupColumn("Active", 60f, 0f, UiTableColumnFlags.None);
            ui.TableHeadersRowSortable();

            for (var i = 0; i < 5; i++)
            {
                ui.TableNextRow();
                ui.Text($"{i}");
                ui.TableNextColumn();
                ui.Text(i < _items.Length ? _items[i] : $"Item {i}");
                ui.TableNextColumn();
                ui.Text($"{i * 11}");
                ui.TableNextColumn();
                ui.Checkbox($"##tbl{i}", ref _tableChecks[i]);
            }

            // Demonstrate bg colors
            ui.TableNextRow();
            ui.TableSetBgColor(UiTableBgTarget.RowBg0, new UiColor(0x40335588));
            ui.Text("5");
            ui.TableNextColumn();
            ui.Text("Custom BG");
            ui.TableNextColumn();
            ui.TableSetBgColor(UiTableBgTarget.CellBg, new UiColor(0x40883322));
            ui.Text("55");
            ui.TableNextColumn();
            ui.Text("-");

            ui.EndTable();
        }

        ui.SeparatorText("Table Sort Info");
        ui.Text("(Sort by clicking column headers)");

        ui.EndWindow();
    }

    // ─────────────────────────── Popup/Tooltip ───────────────────────────
    private void RenderPopupTooltip(UiImmediateContext ui)
    {
        if (_frameCounter == 1) { ui.SetNextWindowSize(new UiVector2(380f, 300f)); ui.SetNextWindowCollapsed(true); }
        ui.BeginWindow("Popups & Tooltips");

        ui.SeparatorText("Popups");
        if (ui.Button("Open Popup"))
        {
            ui.OpenPopup("TestPopup");
        }
        if (ui.BeginPopup("TestPopup"))
        {
            ui.Text("Popup content!");
            if (ui.Button("Close")) ui.CloseCurrentPopup();
            ui.EndPopup();
        }

        ui.SameLine();
        if (ui.Button("Open Modal"))
        {
            _modalOpen = true;
            ui.OpenPopup("TestModal");
        }
        if (ui.BeginPopupModal("TestModal", ref _modalOpen))
        {
            ui.Text("This is a modal dialog.");
            ui.Text("You must close it to continue.");
            if (ui.Button("OK")) _modalOpen = false;
            ui.EndPopupModal(ref _modalOpen);
        }

        ui.Text("Right-click below for context menus:");
        ui.Button("ContextItem Target");
        if (ui.BeginPopupContextItem("ctx_item"))
        {
            ui.MenuItem("Action 1");
            ui.MenuItem("Action 2");
            ui.EndPopup();
        }

        if (ui.BeginPopupContextWindow("ctx_window"))
        {
            ui.MenuItem("Window Action 1");
            ui.MenuItem("Window Action 2");
            ui.EndPopup();
        }

        ui.SeparatorText("Tooltips");
        ui.Button("Hover me (SetTooltip)");
        if (ui.IsItemHovered())
        {
            ui.SetTooltip("This is a SetTooltip!");
        }
        ui.Button("Hover me (BeginTooltip)");
        if (ui.IsItemHovered())
        {
            if (ui.BeginTooltip())
            {
                ui.Text("Rich tooltip with:");
                ui.BulletText("Multiple lines");
                ui.BulletText("Bullet points");
                ui.ProgressBar(0.6f, new UiVector2(150f, 12f), null);
                ui.EndTooltip();
            }
        }
        ui.Button("Hover me (SetItemTooltip)");
        if (ui.IsItemHovered())
        {
            ui.SetItemTooltip("SetItemTooltip example");
        }

        ui.EndWindow();
    }

    // ─────────────────────────── Child/Columns/Layout ───────────────────────────
    private void RenderChildColumnsLayout(UiImmediateContext ui)
    {
        if (_frameCounter == 1) { ui.SetNextWindowSize(new UiVector2(400f, 360f)); ui.SetNextWindowCollapsed(true); }
        ui.BeginWindow("Child / Columns / Layout");

        ui.SeparatorText("Child Window");
        if (ui.BeginChild("child1", new UiVector2(350f, 80f), true))
        {
            ui.Text("Inside child window");
            ui.SliderFloat("ChildSlider", ref _childScroll, 0f, 1f, 0f, "0.00");
            ui.ProgressBar(_childScroll, new UiVector2(200f, 12f), null);
        }
        ui.EndChild();

        ui.SeparatorText("Layout Helpers");
        ui.BeginGroup();
        ui.Text("Group start");
        ui.Dummy(new UiVector2(60f, 10f));
        ui.Spacing();
        ui.NewLine();
        ui.Indent();
        ui.Text("Indented");
        ui.Unindent();
        ui.EndGroup();

        ui.EndWindow();
    }

    // ─────────────────────────── Drawing Primitives ───────────────────────────
    private void RenderDrawingPrimitives(UiImmediateContext ui)
    {
        if (_frameCounter == 1) { ui.SetNextWindowSize(new UiVector2(420f, 360f)); ui.SetNextWindowCollapsed(true); }
        ui.BeginWindow("Drawing Primitives");

        if (!ui.IsWindowCollapsed())
        {
            ui.SliderFloat("Thickness", ref _drawThickness, 1f, 5f, 0.5f, "0.0");
            ui.SliderInt("Segments", ref _drawSegments, 3, 48);

            var avail = ui.GetContentRegionAvail();
            var canvasW = MathF.Max(avail.X, 360f);
            var canvasH = 220f;

            if (ui.BeginChild("draw_canvas", new UiVector2(canvasW, canvasH), false))
            {
                var drawList = ui.GetWindowDrawList();
                var p = ui.GetCursorScreenPos();

                // Reserve space
                ui.Dummy(new UiVector2(canvasW, canvasH));

                drawList.PushTexture(ui.WhiteTextureId);
                var thick = _drawThickness;

                // Line
                drawList.AddLine(
                    new UiVector2(p.X + 10f, p.Y + 10f),
                    new UiVector2(p.X + 80f, p.Y + 10f),
                    new UiColor(0xFFFF4444), thick);

                // Rect outline
                drawList.AddRect(
                    new UiRect(p.X + 10f, p.Y + 20f, 60f, 30f),
                    new UiColor(0xFF44FF44), 4f, thick);

                // Rect filled
                drawList.AddRectFilled(
                    new UiRect(p.X + 80f, p.Y + 20f, 60f, 30f),
                    new UiColor(0xFF4444FF));

                // RectFilledMultiColor
                drawList.AddRectFilledMultiColor(
                    new UiRect(p.X + 150f, p.Y + 20f, 60f, 30f),
                    new UiColor(0xFFFF0000),
                    new UiColor(0xFF00FF00),
                    new UiColor(0xFF0000FF),
                    new UiColor(0xFFFFFF00));

                // Triangle
                drawList.AddTriangle(
                    new UiVector2(p.X + 230f, p.Y + 20f),
                    new UiVector2(p.X + 260f, p.Y + 50f),
                    new UiVector2(p.X + 200f, p.Y + 50f),
                    new UiColor(0xFFFF88FF), thick);

                // Triangle filled
                drawList.AddTriangleFilled(
                    new UiVector2(p.X + 290f, p.Y + 20f),
                    new UiVector2(p.X + 330f, p.Y + 50f),
                    new UiVector2(p.X + 260f, p.Y + 50f),
                    new UiColor(0xFFFFAA44));

                // Circle
                drawList.AddCircle(
                    new UiVector2(p.X + 40f, p.Y + 80f), 20f,
                    new UiColor(0xFFFFFFFF), _drawSegments, thick);

                // Circle filled
                drawList.AddCircleFilled(
                    new UiVector2(p.X + 100f, p.Y + 80f), 20f,
                    new UiColor(0xFF88CCFF), ui.WhiteTextureId, _drawSegments);

                // Ngon
                drawList.AddNgon(
                    new UiVector2(p.X + 160f, p.Y + 80f), 20f,
                    6, new UiColor(0xFFFFCC44), thick);

                // Ngon filled
                drawList.AddNgonFilled(
                    new UiVector2(p.X + 220f, p.Y + 80f), 20f,
                    5, new UiColor(0xFF44FFCC));

                // Ellipse
                drawList.AddEllipse(
                    new UiVector2(p.X + 300f, p.Y + 80f),
                    new UiVector2(30f, 15f),
                    new UiColor(0xFFFF66FF), 24, thick);

                // Ellipse filled
                drawList.AddEllipseFilled(
                    new UiVector2(p.X + 50f, p.Y + 130f),
                    new UiVector2(35f, 18f),
                    new UiColor(0xFF66FFAA), 24);

                // Bezier cubic
                drawList.AddBezierCubic(
                    new UiVector2(p.X + 100f, p.Y + 120f),
                    new UiVector2(p.X + 130f, p.Y + 100f),
                    new UiVector2(p.X + 170f, p.Y + 150f),
                    new UiVector2(p.X + 200f, p.Y + 120f),
                    new UiColor(0xFFFFFF00), thick, 20);

                // Bezier quadratic
                drawList.AddBezierQuadratic(
                    new UiVector2(p.X + 210f, p.Y + 120f),
                    new UiVector2(p.X + 250f, p.Y + 100f),
                    new UiVector2(p.X + 290f, p.Y + 140f),
                    new UiColor(0xFFFF8800), thick, 20);

                // Quad
                drawList.AddQuad(
                    new UiVector2(p.X + 10f, p.Y + 160f),
                    new UiVector2(p.X + 60f, p.Y + 155f),
                    new UiVector2(p.X + 65f, p.Y + 190f),
                    new UiVector2(p.X + 15f, p.Y + 195f),
                    new UiColor(0xFFCCCCFF), thick);

                // Quad filled
                drawList.AddQuadFilled(
                    new UiVector2(p.X + 80f, p.Y + 160f),
                    new UiVector2(p.X + 130f, p.Y + 155f),
                    new UiVector2(p.X + 135f, p.Y + 190f),
                    new UiVector2(p.X + 85f, p.Y + 195f),
                    new UiColor(0xFFFFCC88));

                // Text via draw list
                drawList.AddText(
                    new UiVector2(p.X + 150f, p.Y + 170f),
                    new UiColor(0xFFFFFFFF), "DrawList Text");

                drawList.PopTexture();
            }
            ui.EndChild();
        }

        ui.EndWindow();
    }

    // ─────────────────────────── Drag & Drop ───────────────────────────
    private void RenderDragDrop(UiImmediateContext ui)
    {
        if (_frameCounter == 1) { ui.SetNextWindowSize(new UiVector2(350f, 200f)); ui.SetNextWindowCollapsed(true); }
        ui.BeginWindow("Drag & Drop");

        ui.Button(_dragDropSource);
        if (ui.BeginDragDropSource(UiDragDropFlags.None))
        {
            var data = Encoding.UTF8.GetBytes(_dragDropSource);
            ui.SetDragDropPayload("TEXT", data);
            ui.Text($"Dragging: {_dragDropSource}");
            ui.EndDragDropSource();
        }

        ui.SameLine();
        ui.Button(_dragDropTarget);
        if (ui.BeginDragDropTarget())
        {
            var payload = ui.AcceptDragDropPayload("TEXT", UiDragDropFlags.None);
            if (payload is not null)
            {
                _dragDropTarget = Encoding.UTF8.GetString(payload.Data.Span);
            }
            ui.EndDragDropTarget();
        }

        ui.Text($"Source: {_dragDropSource}");
        ui.Text($"Target: {_dragDropTarget}");

        ui.EndWindow();
    }

    // ─────────────────────────── ListClipper ───────────────────────────
    private void RenderListClipper(UiImmediateContext ui)
    {
        if (_frameCounter == 1) { ui.SetNextWindowSize(new UiVector2(350f, 320f)); ui.SetNextWindowCollapsed(true); }
        ui.BeginWindow("ListClipper (10K items)");

        ui.Text($"Total items: {_clipperItemCount}");
        if (ui.BeginChild("clipper_child", new UiVector2(360f, 150f), true))
        {
            var clipper = new UiListClipper();
            clipper.Begin(ui, _clipperItemCount, ui.GetTextLineHeightWithSpacing());
            while (clipper.Step())
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    ui.Text($"Item #{i:D5}");
                }
            }
        }
        ui.EndChild();

        ui.EndWindow();
    }

    // ─────────────────────────── Style / Disabled / Misc ───────────────────────────
    private void RenderStyleDisabledMisc(UiImmediateContext ui)
    {
        if (_frameCounter == 1) { ui.SetNextWindowSize(new UiVector2(380f, 340f)); ui.SetNextWindowCollapsed(true); }
        ui.BeginWindow("Style / Disabled / Misc");

        ui.SeparatorText("Style Push/Pop");
        ui.Checkbox("Custom Style", ref _customStyle);
        if (_customStyle)
        {
            ui.PushStyleColor(UiStyleColor.Button, new UiColor(0xFF2266AA));
            ui.PushStyleColor(UiStyleColor.ButtonHovered, new UiColor(0xFF3388CC));
        }
        ui.Button("Styled Button");
        if (_customStyle)
        {
            ui.PopStyleColor(2);
        }

        ui.SeparatorText("Disabled");
        ui.Checkbox("Disable Below", ref _disableSection);
        ui.BeginDisabled(_disableSection);
        ui.SliderFloat("Disabled Slider", ref _disabledFloat, 0f, 1f, 0f, "0.00");
        ui.Button("Disabled Button");
        ui.InputText("Disabled Input", ref _inputText, 32);
        ui.EndDisabled();

        ui.SeparatorText("Plots");
        ui.PlotLines("PlotLines", _plotSin, _plotSin.Length, 0, "sin(x)", 0f, 1f, new UiVector2(350f, 40f));
        ui.PlotHistogram("PlotHistogram", _plotHist, _plotHist.Length, 0, null, 0f, 1f, new UiVector2(350f, 40f));

        ui.EndWindow();
    }

    // ─────────────────────────── Demo / Debug Windows ───────────────────────────
    private void RenderDemoDebugWindows(UiImmediateContext ui)
    {
        if (_showDemo) ui.ShowDemoWindow(ref _showDemo);
        if (_showMetrics) ui.ShowMetricsWindow(ref _showMetrics);
        if (_showDebugLog) ui.ShowDebugLogWindow(ref _showDebugLog);
        if (_showIdStack) ui.ShowIDStackToolWindow(ref _showIdStack);
        if (_showAbout) ui.ShowAboutWindow(ref _showAbout);
    }

    // ─────────────────────────── FPS ───────────────────────────
    private void UpdateFps()
    {
        _fps = _fpsCounter.Tick().Fps;
    }

    private void RenderFpsOverlay(UiImmediateContext ui)
    {
        var viewport = ui.GetMainViewport();
        var vsync = ui.GetVSync();
        var vsyncText = vsync ? "[VSync ON]" : "[VSync OFF]";
        var fpsText = $"FPS: {_fps:0}  {vsyncText}";
        var timingText = $"NF:{ui.GetNewFrameTimeMs():0.00} R:{ui.GetRenderTimeMs():0.00} S:{ui.GetSubmitTimeMs():0.00}ms";
        var textSize = ui.CalcTextSize(fpsText);
        var timingSize = ui.CalcTextSize(timingText);
        var maxWidth = MathF.Max(textSize.X, timingSize.X);
        var margin = 8f;
        var lineHeight = ui.GetTextLineHeight();
        var pos = new UiVector2(viewport.Size.X - maxWidth - margin, 4f);

        ui.DrawOverlayText(
            fpsText,
            new UiColor(200, 200, 200),
            UiItemHorizontalAlign.Right,
            UiItemVerticalAlign.Top,
            background: null,
            margin: new UiVector2(margin, 4f));

        ui.DrawOverlayText(
            timingText,
            new UiColor(160, 160, 160),
            UiItemHorizontalAlign.Right,
            UiItemVerticalAlign.Top,
            background: null,
            margin: new UiVector2(margin, 4f + lineHeight + 2f));

        // Click detection on vsync label
        var mouse = ui.GetMousePos();
        var vsyncSize = ui.CalcTextSize(vsyncText);
        var vsyncX = pos.X + textSize.X - vsyncSize.X;
        if (ui.IsMouseClicked(0) &&
            mouse.X >= vsyncX && mouse.X <= vsyncX + vsyncSize.X &&
            mouse.Y >= pos.Y && mouse.Y <= pos.Y + textSize.Y)
        {
            ui.SetVSync(!vsync);
        }
    }
}

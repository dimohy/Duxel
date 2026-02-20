using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Duxel.Core;

namespace Duxel.Core.Dsl;

internal enum UiDslBeginResult
{
    None,
    SkipChildren,
}

internal sealed class UiDslRuntimeState
{
    public Stack<string> PopupModalKeys { get; } = new();
    public Stack<bool> TreeOpenStack { get; } = new();
}

internal static class UiDslWidgetDispatcher
{
    public static bool IsContainer(string name)
    {
        return name switch
        {
            "Window" or "BeginWindow" or "Row" or "BeginRow" or "BeginGroup" or "BeginChild" or
            "BeginCombo" or "BeginListBox" or "BeginMenu" or "BeginMenuBar" or "BeginMainMenuBar" or
            "BeginPopup" or "BeginPopupModal" or "BeginPopupContextItem" or "BeginPopupContextWindow" or "BeginPopupContextVoid" or
            "BeginTabBar" or "BeginTabItem" or "BeginTable" or "BeginTooltip" or "BeginItemTooltip" or
            "BeginDragDropSource" or "BeginDragDropTarget" or "BeginDisabled" or "BeginMultiSelect" or
            "TreeNode" or "TreeNodeEx" or "TreeNodeV" or "TreeNodeExV" or "TreeNodePush" => true,
            _ => false,
        };
    }

    public static UiDslBeginResult BeginOrInvoke(
        UiImmediateContext ui,
        UiDslRenderContext ctx,
        UiDslRuntimeState runtimeState,
        string name,
        IReadOnlyList<string> args
    )
    {
        var reader = new UiDslArgReader(args);

        switch (name)
        {
            case "Window":
            case "BeginWindow":
            {
                var title = reader.ReadString("Title", "Window");
                ui.BeginWindow(title);
                return UiDslBeginResult.None;
            }
            case "Row":
            case "BeginRow":
                ui.BeginRow();
                return UiDslBeginResult.None;
            case "BeginGroup":
                ui.BeginGroup();
                return UiDslBeginResult.None;
            case "BeginChild":
            {
                var id = reader.ReadString("Id", "Child");
                var size = reader.ReadVector2("Size", default);
                var border = reader.ReadBool("Border", false);
                var open = ui.BeginChild(id, size, border);
                return open ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            }
            case "Button":
            {
                var (id, label) = ReadIdLabel(ref reader, "Button");
                var size = reader.ReadVector2("Size", default);
                var pressed = WithId(ui, id, label, () => size != default ? ui.Button(label, size) : ui.Button(label));
                if (pressed)
                {
                    ctx.EventSink?.OnButton(id);
                }
                return UiDslBeginResult.None;
            }
            case "SmallButton":
            {
                var (id, label) = ReadIdLabel(ref reader, "Button");
                var pressed = WithId(ui, id, label, () => ui.SmallButton(label));
                if (pressed)
                {
                    ctx.EventSink?.OnButton(id);
                }
                return UiDslBeginResult.None;
            }
            case "InvisibleButton":
            {
                var id = reader.ReadString("Id", "InvisibleButton");
                var size = reader.ReadVector2("Size", default);
                var pressed = ui.InvisibleButton(id, size);
                if (pressed)
                {
                    ctx.EventSink?.OnButton(id);
                }
                return UiDslBeginResult.None;
            }
            case "ArrowButton":
            {
                var id = reader.ReadString("Id", "Arrow");
                var dir = reader.ReadEnum("Dir", UiDir.Right);
                var pressed = ui.ArrowButton(id, dir);
                if (pressed)
                {
                    ctx.EventSink?.OnButton(id);
                }
                return UiDslBeginResult.None;
            }
            case "Text":
                ui.Text(reader.ReadString("Text", string.Empty));
                return UiDslBeginResult.None;
            case "TextV":
            {
                var format = reader.ReadString("Format", string.Empty);
                ui.TextV(format, reader.ReadFormatArgs());
                return UiDslBeginResult.None;
            }
            case "TextColored":
            {
                var color = reader.ReadColor("Color", default);
                var text = reader.ReadString("Text", string.Empty);
                ui.TextColored(color, text);
                return UiDslBeginResult.None;
            }
            case "TextColoredV":
            {
                var color = reader.ReadColor("Color", default);
                var format = reader.ReadString("Format", string.Empty);
                ui.TextColoredV(color, format, reader.ReadFormatArgs());
                return UiDslBeginResult.None;
            }
            case "TextDisabled":
                ui.TextDisabled(reader.ReadString("Text", string.Empty));
                return UiDslBeginResult.None;
            case "TextDisabledV":
            {
                var format = reader.ReadString("Format", string.Empty);
                ui.TextDisabledV(format, reader.ReadFormatArgs());
                return UiDslBeginResult.None;
            }
            case "TextWrapped":
                ui.TextWrapped(reader.ReadString("Text", string.Empty));
                return UiDslBeginResult.None;
            case "TextWrappedV":
            {
                var format = reader.ReadString("Format", string.Empty);
                ui.TextWrappedV(format, reader.ReadFormatArgs());
                return UiDslBeginResult.None;
            }
            case "TextUnformatted":
                ui.TextUnformatted(reader.ReadString("Text", string.Empty));
                return UiDslBeginResult.None;
            case "TextLink":
            {
                var (id, label) = ReadIdLabel(ref reader, "Link");
                var pressed = WithId(ui, id, label, () => ui.TextLink(label));
                if (pressed)
                {
                    ctx.EventSink?.OnButton(id);
                }
                return UiDslBeginResult.None;
            }
            case "TextLinkOpenURL":
            {
                var (id, label) = ReadIdLabel(ref reader, "Link");
                var url = reader.ReadString("Url", string.Empty);
                var pressed = WithId(ui, id, label, () => ui.TextLinkOpenURL(label, url));
                if (pressed)
                {
                    ctx.EventSink?.OnButton(id);
                }
                return UiDslBeginResult.None;
            }
            case "LabelText":
            {
                var label = reader.ReadString("Label", string.Empty);
                var text = reader.ReadString("Text", string.Empty);
                ui.LabelText(label, text);
                return UiDslBeginResult.None;
            }
            case "LabelTextV":
            {
                var label = reader.ReadString("Label", string.Empty);
                var format = reader.ReadString("Format", string.Empty);
                ui.LabelTextV(label, format, reader.ReadFormatArgs());
                return UiDslBeginResult.None;
            }
            case "Bullet":
                ui.Bullet();
                return UiDslBeginResult.None;
            case "BulletText":
                ui.BulletText(reader.ReadString("Text", string.Empty));
                return UiDslBeginResult.None;
            case "BulletTextV":
            {
                var format = reader.ReadString("Format", string.Empty);
                ui.BulletTextV(format, reader.ReadFormatArgs());
                return UiDslBeginResult.None;
            }
            case "Value":
            {
                var prefix = reader.ReadString("Prefix", string.Empty);
                if (!reader.TryRead(out var rawValue))
                {
                    ui.Text(prefix);
                    return UiDslBeginResult.None;
                }

                if (bool.TryParse(rawValue, out var boolValue))
                {
                    ui.Value(prefix, boolValue);
                }
                else if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    ui.Value(prefix, intValue);
                }
                else if (uint.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uintValue))
                {
                    ui.Value(prefix, uintValue);
                }
                else if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
                {
                    var format = reader.ReadOptionalString("Format");
                    ui.Value(prefix, floatValue, format);
                }
                else
                {
                    ui.Text(FormattableString.Invariant($"{prefix}: {rawValue}"));
                }

                return UiDslBeginResult.None;
            }
            case "SeparatorText":
                ui.SeparatorText(reader.ReadString("Text", string.Empty));
                return UiDslBeginResult.None;
            case "Separator":
                ui.Separator();
                return UiDslBeginResult.None;
            case "SameLine":
                ui.SameLine(reader.ReadFloat("Spacing", -1f));
                return UiDslBeginResult.None;
            case "NewLine":
                ui.NewLine();
                return UiDslBeginResult.None;
            case "Spacing":
                ui.Spacing();
                return UiDslBeginResult.None;
            case "Dummy":
                ui.Dummy(reader.ReadVector2("Size", default));
                return UiDslBeginResult.None;
            case "Indent":
                if (reader.TryReadFloat("Width", out var indent))
                {
                    ui.Indent(indent);
                }
                else
                {
                    ui.Indent();
                }
                return UiDslBeginResult.None;
            case "Unindent":
                if (reader.TryReadFloat("Width", out var unindent))
                {
                    ui.Unindent(unindent);
                }
                else
                {
                    ui.Unindent();
                }
                return UiDslBeginResult.None;
            case "AlignTextToFramePadding":
                ui.AlignTextToFramePadding();
                return UiDslBeginResult.None;
            case "PushID":
            {
                var id = reader.ReadString("Id", string.Empty);
                ui.PushID(id);
                return UiDslBeginResult.None;
            }
            case "PopID":
                ui.PopID();
                return UiDslBeginResult.None;
            case "PushItemWidth":
                ui.PushItemWidth(reader.ReadFloat("Width", 0f));
                return UiDslBeginResult.None;
            case "PopItemWidth":
                ui.PopItemWidth();
                return UiDslBeginResult.None;
            case "SetNextItemWidth":
                ui.SetNextItemWidth(reader.ReadFloat("Width", 0f));
                return UiDslBeginResult.None;
            case "Checkbox":
            {
                var (id, label) = ReadIdLabel(ref reader, "Checkbox");
                var defaultValue = reader.ReadBool("Default", false);
                var value = UiDslValues.GetBool(ctx, id, defaultValue);
                var changed = WithId(ui, id, label, () => ui.Checkbox(label, ref value));
                if (changed)
                {
                    UiDslValues.SetBool(ctx, id, value);
                    ctx.EventSink?.OnCheckbox(id, value);
                }
                return UiDslBeginResult.None;
            }
            case "CheckboxFlags":
            {
                var (id, label) = ReadIdLabel(ref reader, "Flags");
                var flagsValue = reader.ReadInt("FlagsValue", 0);
                var defaultFlags = reader.ReadInt("Default", 0);
                var flags = UiDslValues.GetInt(ctx, id, defaultFlags);
                var changed = WithId(ui, id, label, () => ui.CheckboxFlags(label, ref flags, flagsValue));
                if (changed)
                {
                    UiDslValues.SetInt(ctx, id, flags);
                }
                return UiDslBeginResult.None;
            }
            case "RadioButton":
            {
                var (id, label) = ReadIdLabel(ref reader, "Radio");
                if (reader.TryReadInt("ButtonValue", out var buttonValue))
                {
                    var defaultValue = reader.ReadInt("Default", 0);
                    var value = UiDslValues.GetInt(ctx, id, defaultValue);
                    var changed = WithId(ui, id, label, () => ui.RadioButton(label, ref value, buttonValue));
                    if (changed)
                    {
                        UiDslValues.SetInt(ctx, id, value);
                    }
                }
                else
                {
                    var active = reader.ReadBool("Active", false);
                    WithId(ui, id, label, () => ui.RadioButton(label, active));
                }
                return UiDslBeginResult.None;
            }
            case "ProgressBar":
            {
                var fraction = reader.ReadFloat("Fraction", 0f);
                var size = reader.ReadVector2("Size", default);
                var overlay = reader.ReadOptionalString("Overlay");
                ui.ProgressBar(fraction, size, overlay);
                return UiDslBeginResult.None;
            }
            case "PlotLines":
            {
                var label = reader.ReadString("Label", "PlotLines");
                var values = reader.ReadFloatArray("Values");
                ui.PlotLines(label, values);
                return UiDslBeginResult.None;
            }
            case "PlotHistogram":
            {
                var label = reader.ReadString("Label", "PlotHistogram");
                var values = reader.ReadFloatArray("Values");
                ui.PlotHistogram(label, values);
                return UiDslBeginResult.None;
            }
            case "BeginCombo":
            {
                var (id, label) = ReadIdLabel(ref reader, "Combo");
                var preview = reader.ReadString("Preview", string.Empty);
                var maxItems = reader.ReadInt("MaxItems", 8);
                var open = WithId(ui, id, label, () => ui.BeginCombo(preview, maxItems, label));
                return open ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            }
            case "Combo":
            {
                var (id, label) = ReadIdLabel(ref reader, "Combo");
                var items = reader.ReadStringList("Items");
                var maxItems = reader.ReadInt("MaxItems", 8);
                var value = UiDslValues.GetInt(ctx, id, 0);
                var changed = WithId(ui, id, label, () => ui.Combo(ref value, items, maxItems, label));
                if (changed)
                {
                    UiDslValues.SetInt(ctx, id, value);
                }
                return UiDslBeginResult.None;
            }
            case "BeginListBox":
            {
                var label = reader.ReadString("Label", "ListBox");
                var size = reader.ReadVector2("Size", default);
                var itemsCount = reader.ReadInt("ItemsCount", -1);
                var open = ui.BeginListBox(size, itemsCount, label);
                return open ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            }
            case "ListBox":
            {
                var (id, label) = ReadIdLabel(ref reader, "ListBox");
                var items = reader.ReadStringList("Items");
                var heightItems = reader.ReadInt("HeightItems", -1);
                var value = UiDslValues.GetInt(ctx, id, 0);
                var changed = WithId(ui, id, label, () => ui.ListBox(ref value, items, heightItems, label));
                if (changed)
                {
                    UiDslValues.SetInt(ctx, id, value);
                }
                return UiDslBeginResult.None;
            }
            case "ListBoxHeader":
            {
                var label = reader.ReadString("Label", "ListBox");
                var size = reader.ReadVector2("Size", default);
                var itemsCount = reader.ReadInt("ItemsCount", -1);
                var open = ui.ListBoxHeader(size, itemsCount, label);
                return open ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            }
            case "ListBoxFooter":
                ui.ListBoxFooter();
                return UiDslBeginResult.None;
            case "Selectable":
            {
                var (id, label) = ReadIdLabel(ref reader, "Selectable");
                var size = reader.ReadVector2("Size", default);
                var selected = UiDslValues.GetBool(ctx, id, false);
                var changed = WithId(ui, id, label, () =>
                    size != default
                        ? ui.Selectable(label, ref selected, size)
                        : ui.Selectable(label, ref selected));
                if (changed)
                {
                    UiDslValues.SetBool(ctx, id, selected);
                }
                return UiDslBeginResult.None;
            }
            case "ListBoxRow":
            {
                var (id, label) = ReadIdLabel(ref reader, "ListBoxRow");
                var key = reader.ReadString("Key", label);
                var size = reader.ReadVector2("Size", default);
                var selected = UiDslValues.GetBool(ctx, id, false);

                var pressed = WithId(ui, id, label, () =>
                    size != default
                        ? ui.ListBoxRow(key, selected, size, out _)
                        : ui.ListBoxRow(key, selected, out _));

                if (pressed)
                {
                    selected = !selected;
                    UiDslValues.SetBool(ctx, id, selected);
                    ctx.EventSink?.OnButton(id);
                }

                return UiDslBeginResult.None;
            }
            case "Image":
            {
                var textureId = reader.ReadTextureId("TextureId");
                var size = reader.ReadVector2("Size", default);
                var tint = reader.TryReadColor("Tint", out var color) ? color : (UiColor?)null;
                ui.Image(textureId, size, tint);
                return UiDslBeginResult.None;
            }
            case "ImageWithBg":
            {
                var textureId = reader.ReadTextureId("TextureId");
                var size = reader.ReadVector2("Size", default);
                var bg = reader.TryReadColor("Bg", out var bgColor) ? bgColor : default;
                var tint = reader.TryReadColor("Tint", out var tintColor) ? tintColor : (UiColor?)null;
                ui.ImageWithBg(textureId, size, bg, tint);
                return UiDslBeginResult.None;
            }
            case "ImageButton":
            {
                var id = reader.ReadString("Id", "ImageButton");
                var textureId = reader.ReadTextureId("TextureId");
                var size = reader.ReadVector2("Size", default);
                var pressed = ui.ImageButton(id, textureId, size);
                if (pressed)
                {
                    ctx.EventSink?.OnButton(id);
                }
                return UiDslBeginResult.None;
            }
            case "InputText":
            {
                var (id, label) = ReadIdLabel(ref reader, "Input");
                var maxLength = reader.ReadInt("MaxLength", 256);
                var flags = reader.TryReadEnum("Flags", out UiInputTextFlags parsed) ? parsed : UiInputTextFlags.None;
                var value = UiDslValues.GetString(ctx, id, string.Empty);
                var changed = WithId(ui, id, label, () =>
                    flags == UiInputTextFlags.None
                        ? ui.InputText(label, ref value, maxLength)
                        : ui.InputText(label, ref value, maxLength, flags));
                if (changed)
                {
                    UiDslValues.SetString(ctx, id, value);
                }
                return UiDslBeginResult.None;
            }
            case "InputTextWithHint":
            {
                var (id, label) = ReadIdLabel(ref reader, "Input");
                var hint = reader.ReadString("Hint", string.Empty);
                var maxLength = reader.ReadInt("MaxLength", 256);
                var value = UiDslValues.GetString(ctx, id, string.Empty);
                var changed = WithId(ui, id, label, () => ui.InputTextWithHint(label, hint, ref value, maxLength));
                if (changed)
                {
                    UiDslValues.SetString(ctx, id, value);
                }
                return UiDslBeginResult.None;
            }
            case "InputTextMultiline":
            {
                var (id, label) = ReadIdLabel(ref reader, "Input");
                var maxLength = reader.ReadInt("MaxLength", 1024);
                var height = reader.ReadFloat("Height", 120f);
                var value = UiDslValues.GetString(ctx, id, string.Empty);
                var changed = WithId(ui, id, label, () => ui.InputTextMultiline(label, ref value, maxLength, height));
                if (changed)
                {
                    UiDslValues.SetString(ctx, id, value);
                }
                return UiDslBeginResult.None;
            }
            case "InputInt":
            {
                var (id, label) = ReadIdLabel(ref reader, "InputInt");
                var value = UiDslValues.GetInt(ctx, id, reader.ReadInt("Value", 0));
                var changed = WithId(ui, id, label, () => ui.InputInt(label, ref value));
                if (changed)
                {
                    UiDslValues.SetInt(ctx, id, value);
                }
                return UiDslBeginResult.None;
            }
            case "InputFloat":
            {
                var (id, label) = ReadIdLabel(ref reader, "InputFloat");
                var format = reader.ReadString("Format", "0.###");
                var value = UiDslValues.GetFloat(ctx, id, reader.ReadFloat("Value", 0f));
                var changed = WithId(ui, id, label, () => ui.InputFloat(label, ref value, format));
                if (changed)
                {
                    UiDslValues.SetFloat(ctx, id, value);
                }
                return UiDslBeginResult.None;
            }
            case "InputDouble":
            {
                var (id, label) = ReadIdLabel(ref reader, "InputDouble");
                var format = reader.ReadString("Format", "0.###");
                var value = UiDslValues.GetDouble(ctx, id, reader.ReadDouble("Value", 0d));
                var changed = WithId(ui, id, label, () => ui.InputDouble(label, ref value, format));
                if (changed)
                {
                    UiDslValues.SetDouble(ctx, id, value);
                }
                return UiDslBeginResult.None;
            }
            case "InputScalar":
            {
                var (id, label) = ReadIdLabel(ref reader, "InputScalar");
                var type = reader.ReadString("Type", "float");
                var format = reader.ReadString("Format", "0.###");
                if (type.Equals("int", StringComparison.OrdinalIgnoreCase))
                {
                    var value = UiDslValues.GetInt(ctx, id, reader.ReadInt("Value", 0));
                    var changed = WithId(ui, id, label, () => ui.InputScalar(label, ref value));
                    if (changed)
                    {
                        UiDslValues.SetInt(ctx, id, value);
                    }
                }
                else if (type.Equals("double", StringComparison.OrdinalIgnoreCase))
                {
                    var value = UiDslValues.GetDouble(ctx, id, reader.ReadDouble("Value", 0d));
                    var changed = WithId(ui, id, label, () => ui.InputScalar(label, ref value, format));
                    if (changed)
                    {
                        UiDslValues.SetDouble(ctx, id, value);
                    }
                }
                else
                {
                    var value = UiDslValues.GetFloat(ctx, id, reader.ReadFloat("Value", 0f));
                    var changed = WithId(ui, id, label, () => ui.InputScalar(label, ref value, format));
                    if (changed)
                    {
                        UiDslValues.SetFloat(ctx, id, value);
                    }
                }
                return UiDslBeginResult.None;
            }
            case "InputFloat2":
            {
                var (id, label) = ReadIdLabel(ref reader, "InputFloat2");
                var format = reader.ReadString("Format", "0.###");
                var vec = UiDslValues.GetVector2(ctx, id, default);
                var x = vec.X;
                var y = vec.Y;
                var changed = WithId(ui, id, label, () => ui.InputFloat2(label, ref x, ref y, format));
                if (changed)
                {
                    UiDslValues.SetVector2(ctx, id, new UiVector2(x, y));
                }
                return UiDslBeginResult.None;
            }
            case "InputFloat3":
            {
                var (id, label) = ReadIdLabel(ref reader, "InputFloat3");
                var format = reader.ReadString("Format", "0.###");
                var vec = UiDslValues.GetVector4(ctx, id, default);
                var x = vec.X;
                var y = vec.Y;
                var z = vec.Z;
                var changed = WithId(ui, id, label, () => ui.InputFloat3(label, ref x, ref y, ref z, format));
                if (changed)
                {
                    UiDslValues.SetVector4(ctx, id, new UiVector4(x, y, z, vec.W));
                }
                return UiDslBeginResult.None;
            }
            case "InputFloat4":
            {
                var (id, label) = ReadIdLabel(ref reader, "InputFloat4");
                var format = reader.ReadString("Format", "0.###");
                var vec = UiDslValues.GetVector4(ctx, id, default);
                var x = vec.X;
                var y = vec.Y;
                var z = vec.Z;
                var w = vec.W;
                var changed = WithId(ui, id, label, () => ui.InputFloat4(label, ref x, ref y, ref z, ref w, format));
                if (changed)
                {
                    UiDslValues.SetVector4(ctx, id, new UiVector4(x, y, z, w));
                }
                return UiDslBeginResult.None;
            }
            case "InputInt2":
            {
                var (id, label) = ReadIdLabel(ref reader, "InputInt2");
                var vec = UiDslValues.GetVector2(ctx, id, default);
                var x = (int)vec.X;
                var y = (int)vec.Y;
                var changed = WithId(ui, id, label, () => ui.InputInt2(label, ref x, ref y));
                if (changed)
                {
                    UiDslValues.SetVector2(ctx, id, new UiVector2(x, y));
                }
                return UiDslBeginResult.None;
            }
            case "InputInt3":
            {
                var (id, label) = ReadIdLabel(ref reader, "InputInt3");
                var vec = UiDslValues.GetVector4(ctx, id, default);
                var x = (int)vec.X;
                var y = (int)vec.Y;
                var z = (int)vec.Z;
                var changed = WithId(ui, id, label, () => ui.InputInt3(label, ref x, ref y, ref z));
                if (changed)
                {
                    UiDslValues.SetVector4(ctx, id, new UiVector4(x, y, z, vec.W));
                }
                return UiDslBeginResult.None;
            }
            case "InputInt4":
            {
                var (id, label) = ReadIdLabel(ref reader, "InputInt4");
                var vec = UiDslValues.GetVector4(ctx, id, default);
                var x = (int)vec.X;
                var y = (int)vec.Y;
                var z = (int)vec.Z;
                var w = (int)vec.W;
                var changed = WithId(ui, id, label, () => ui.InputInt4(label, ref x, ref y, ref z, ref w));
                if (changed)
                {
                    UiDslValues.SetVector4(ctx, id, new UiVector4(x, y, z, w));
                }
                return UiDslBeginResult.None;
            }
            case "InputDouble2":
            {
                var (id, label) = ReadIdLabel(ref reader, "InputDouble2");
                var format = reader.ReadString("Format", "0.###");
                var values = UiDslValues.GetDoubleArray(ctx, id, reader.ReadDoubleArray("Values"));
                EnsureDoubleArray(ref values, 2);
                var x = values[0];
                var y = values[1];
                var changed = WithId(ui, id, label, () => ui.InputDouble2(label, ref x, ref y, format));
                if (changed)
                {
                    values[0] = x;
                    values[1] = y;
                    UiDslValues.SetDoubleArray(ctx, id, values);
                }
                return UiDslBeginResult.None;
            }
            case "InputDouble3":
            {
                var (id, label) = ReadIdLabel(ref reader, "InputDouble3");
                var format = reader.ReadString("Format", "0.###");
                var values = UiDslValues.GetDoubleArray(ctx, id, reader.ReadDoubleArray("Values"));
                EnsureDoubleArray(ref values, 3);
                var x = values[0];
                var y = values[1];
                var z = values[2];
                var changed = WithId(ui, id, label, () => ui.InputDouble3(label, ref x, ref y, ref z, format));
                if (changed)
                {
                    values[0] = x;
                    values[1] = y;
                    values[2] = z;
                    UiDslValues.SetDoubleArray(ctx, id, values);
                }
                return UiDslBeginResult.None;
            }
            case "InputDouble4":
            {
                var (id, label) = ReadIdLabel(ref reader, "InputDouble4");
                var format = reader.ReadString("Format", "0.###");
                var values = UiDslValues.GetDoubleArray(ctx, id, reader.ReadDoubleArray("Values"));
                EnsureDoubleArray(ref values, 4);
                var x = values[0];
                var y = values[1];
                var z = values[2];
                var w = values[3];
                var changed = WithId(ui, id, label, () => ui.InputDouble4(label, ref x, ref y, ref z, ref w, format));
                if (changed)
                {
                    values[0] = x;
                    values[1] = y;
                    values[2] = z;
                    values[3] = w;
                    UiDslValues.SetDoubleArray(ctx, id, values);
                }
                return UiDslBeginResult.None;
            }
            case "InputScalarN":
            {
                var (id, label) = ReadIdLabel(ref reader, "InputScalarN");
                var type = reader.ReadString("Type", "float");
                var format = reader.ReadString("Format", "0.###");
                if (type.Equals("int", StringComparison.OrdinalIgnoreCase))
                {
                    var values = UiDslValues.GetIntArray(ctx, id, reader.ReadIntArray("Values"));
                    var changed = WithId(ui, id, label, () => ui.InputScalarN(label, values));
                    if (changed)
                    {
                        UiDslValues.SetIntArray(ctx, id, values);
                    }
                }
                else if (type.Equals("double", StringComparison.OrdinalIgnoreCase))
                {
                    var values = UiDslValues.GetDoubleArray(ctx, id, reader.ReadDoubleArray("Values"));
                    var changed = WithId(ui, id, label, () => ui.InputScalarN(label, values, format));
                    if (changed)
                    {
                        UiDslValues.SetDoubleArray(ctx, id, values);
                    }
                }
                else
                {
                    var values = UiDslValues.GetFloatArray(ctx, id, reader.ReadFloatArray("Values"));
                    var changed = WithId(ui, id, label, () => ui.InputScalarN(label, values, format));
                    if (changed)
                    {
                        UiDslValues.SetFloatArray(ctx, id, values);
                    }
                }
                return UiDslBeginResult.None;
            }
            case "DragFloat":
            {
                var (id, label) = ReadIdLabel(ref reader, "DragFloat");
                var value = UiDslValues.GetFloat(ctx, id, reader.ReadFloat("Value", 0f));
                var speed = reader.ReadFloat("Speed", 0.01f);
                var min = reader.ReadFloat("Min", float.MinValue);
                var max = reader.ReadFloat("Max", float.MaxValue);
                var changed = WithId(ui, id, label, () => ui.DragFloat(label, ref value, speed, min, max));
                if (changed)
                {
                    UiDslValues.SetFloat(ctx, id, value);
                }
                return UiDslBeginResult.None;
            }
            case "DragInt":
            {
                var (id, label) = ReadIdLabel(ref reader, "DragInt");
                var value = UiDslValues.GetInt(ctx, id, reader.ReadInt("Value", 0));
                var speed = reader.ReadFloat("Speed", 0.1f);
                var min = reader.ReadInt("Min", int.MinValue);
                var max = reader.ReadInt("Max", int.MaxValue);
                var changed = WithId(ui, id, label, () => ui.DragInt(label, ref value, speed, min, max));
                if (changed)
                {
                    UiDslValues.SetInt(ctx, id, value);
                }
                return UiDslBeginResult.None;
            }
            case "DragFloat2":
            {
                var (id, label) = ReadIdLabel(ref reader, "DragFloat2");
                var vec = UiDslValues.GetVector2(ctx, id, default);
                var x = vec.X;
                var y = vec.Y;
                var speed = reader.ReadFloat("Speed", 0.01f);
                var min = reader.ReadFloat("Min", float.MinValue);
                var max = reader.ReadFloat("Max", float.MaxValue);
                var changed = WithId(ui, id, label, () => ui.DragFloat2(label, ref x, ref y, speed, min, max));
                if (changed)
                {
                    UiDslValues.SetVector2(ctx, id, new UiVector2(x, y));
                }
                return UiDslBeginResult.None;
            }
            case "DragFloat3":
            {
                var (id, label) = ReadIdLabel(ref reader, "DragFloat3");
                var vec = UiDslValues.GetVector4(ctx, id, default);
                var x = vec.X;
                var y = vec.Y;
                var z = vec.Z;
                var speed = reader.ReadFloat("Speed", 0.01f);
                var min = reader.ReadFloat("Min", float.MinValue);
                var max = reader.ReadFloat("Max", float.MaxValue);
                var changed = WithId(ui, id, label, () => ui.DragFloat3(label, ref x, ref y, ref z, speed, min, max));
                if (changed)
                {
                    UiDslValues.SetVector4(ctx, id, new UiVector4(x, y, z, vec.W));
                }
                return UiDslBeginResult.None;
            }
            case "DragFloat4":
            {
                var (id, label) = ReadIdLabel(ref reader, "DragFloat4");
                var vec = UiDslValues.GetVector4(ctx, id, default);
                var x = vec.X;
                var y = vec.Y;
                var z = vec.Z;
                var w = vec.W;
                var speed = reader.ReadFloat("Speed", 0.01f);
                var min = reader.ReadFloat("Min", float.MinValue);
                var max = reader.ReadFloat("Max", float.MaxValue);
                var changed = WithId(ui, id, label, () => ui.DragFloat4(label, ref x, ref y, ref z, ref w, speed, min, max));
                if (changed)
                {
                    UiDslValues.SetVector4(ctx, id, new UiVector4(x, y, z, w));
                }
                return UiDslBeginResult.None;
            }
            case "DragInt2":
            {
                var (id, label) = ReadIdLabel(ref reader, "DragInt2");
                var vec = UiDslValues.GetVector2(ctx, id, default);
                var x = (int)vec.X;
                var y = (int)vec.Y;
                var speed = reader.ReadFloat("Speed", 0.1f);
                var min = reader.ReadInt("Min", int.MinValue);
                var max = reader.ReadInt("Max", int.MaxValue);
                var changed = WithId(ui, id, label, () => ui.DragInt2(label, ref x, ref y, speed, min, max));
                if (changed)
                {
                    UiDslValues.SetVector2(ctx, id, new UiVector2(x, y));
                }
                return UiDslBeginResult.None;
            }
            case "DragInt3":
            {
                var (id, label) = ReadIdLabel(ref reader, "DragInt3");
                var vec = UiDslValues.GetVector4(ctx, id, default);
                var x = (int)vec.X;
                var y = (int)vec.Y;
                var z = (int)vec.Z;
                var speed = reader.ReadFloat("Speed", 0.1f);
                var min = reader.ReadInt("Min", int.MinValue);
                var max = reader.ReadInt("Max", int.MaxValue);
                var changed = WithId(ui, id, label, () => ui.DragInt3(label, ref x, ref y, ref z, speed, min, max));
                if (changed)
                {
                    UiDslValues.SetVector4(ctx, id, new UiVector4(x, y, z, vec.W));
                }
                return UiDslBeginResult.None;
            }
            case "DragInt4":
            {
                var (id, label) = ReadIdLabel(ref reader, "DragInt4");
                var vec = UiDslValues.GetVector4(ctx, id, default);
                var x = (int)vec.X;
                var y = (int)vec.Y;
                var z = (int)vec.Z;
                var w = (int)vec.W;
                var speed = reader.ReadFloat("Speed", 0.1f);
                var min = reader.ReadInt("Min", int.MinValue);
                var max = reader.ReadInt("Max", int.MaxValue);
                var changed = WithId(ui, id, label, () => ui.DragInt4(label, ref x, ref y, ref z, ref w, speed, min, max));
                if (changed)
                {
                    UiDslValues.SetVector4(ctx, id, new UiVector4(x, y, z, w));
                }
                return UiDslBeginResult.None;
            }
            case "DragFloatRange2":
            {
                var (id, label) = ReadIdLabel(ref reader, "DragFloatRange2");
                var minValue = UiDslValues.GetFloat(ctx, id + ".min", reader.ReadFloat("MinValue", 0f));
                var maxValue = UiDslValues.GetFloat(ctx, id + ".max", reader.ReadFloat("MaxValue", 1f));
                var speed = reader.ReadFloat("Speed", 0.01f);
                var min = reader.ReadFloat("Min", float.MinValue);
                var max = reader.ReadFloat("Max", float.MaxValue);
                var changed = WithId(ui, id, label, () => ui.DragFloatRange2(label, ref minValue, ref maxValue, speed, min, max));
                if (changed)
                {
                    UiDslValues.SetFloat(ctx, id + ".min", minValue);
                    UiDslValues.SetFloat(ctx, id + ".max", maxValue);
                }
                return UiDslBeginResult.None;
            }
            case "DragIntRange2":
            {
                var (id, label) = ReadIdLabel(ref reader, "DragIntRange2");
                var minValue = UiDslValues.GetInt(ctx, id + ".min", reader.ReadInt("MinValue", 0));
                var maxValue = UiDslValues.GetInt(ctx, id + ".max", reader.ReadInt("MaxValue", 100));
                var speed = reader.ReadFloat("Speed", 0.1f);
                var min = reader.ReadInt("Min", int.MinValue);
                var max = reader.ReadInt("Max", int.MaxValue);
                var changed = WithId(ui, id, label, () => ui.DragIntRange2(label, ref minValue, ref maxValue, speed, min, max));
                if (changed)
                {
                    UiDslValues.SetInt(ctx, id + ".min", minValue);
                    UiDslValues.SetInt(ctx, id + ".max", maxValue);
                }
                return UiDslBeginResult.None;
            }
            case "DragScalar":
            {
                var (id, label) = ReadIdLabel(ref reader, "DragScalar");
                var type = reader.ReadString("Type", "float");
                var speed = reader.ReadFloat("Speed", 0.01f);
                if (type.Equals("int", StringComparison.OrdinalIgnoreCase))
                {
                    var value = UiDslValues.GetInt(ctx, id, reader.ReadInt("Value", 0));
                    var min = reader.ReadInt("Min", int.MinValue);
                    var max = reader.ReadInt("Max", int.MaxValue);
                    var changed = WithId(ui, id, label, () => ui.DragScalar(label, ref value, speed, min, max));
                    if (changed)
                    {
                        UiDslValues.SetInt(ctx, id, value);
                    }
                }
                else if (type.Equals("double", StringComparison.OrdinalIgnoreCase))
                {
                    var value = UiDslValues.GetDouble(ctx, id, reader.ReadDouble("Value", 0d));
                    var min = reader.ReadDouble("Min", double.MinValue);
                    var max = reader.ReadDouble("Max", double.MaxValue);
                    var changed = WithId(ui, id, label, () => ui.DragScalar(label, ref value, speed, min, max));
                    if (changed)
                    {
                        UiDslValues.SetDouble(ctx, id, value);
                    }
                }
                else
                {
                    var value = UiDslValues.GetFloat(ctx, id, reader.ReadFloat("Value", 0f));
                    var min = reader.ReadFloat("Min", float.MinValue);
                    var max = reader.ReadFloat("Max", float.MaxValue);
                    var changed = WithId(ui, id, label, () => ui.DragScalar(label, ref value, speed, min, max));
                    if (changed)
                    {
                        UiDslValues.SetFloat(ctx, id, value);
                    }
                }
                return UiDslBeginResult.None;
            }
            case "DragScalarN":
            {
                var (id, label) = ReadIdLabel(ref reader, "DragScalarN");
                var type = reader.ReadString("Type", "float");
                var speed = reader.ReadFloat("Speed", 0.01f);
                if (type.Equals("int", StringComparison.OrdinalIgnoreCase))
                {
                    var values = UiDslValues.GetIntArray(ctx, id, reader.ReadIntArray("Values"));
                    var min = reader.ReadInt("Min", int.MinValue);
                    var max = reader.ReadInt("Max", int.MaxValue);
                    var changed = WithId(ui, id, label, () => ui.DragScalarN(label, values, speed, min, max));
                    if (changed)
                    {
                        UiDslValues.SetIntArray(ctx, id, values);
                    }
                }
                else if (type.Equals("double", StringComparison.OrdinalIgnoreCase))
                {
                    var values = UiDslValues.GetDoubleArray(ctx, id, reader.ReadDoubleArray("Values"));
                    var min = reader.ReadDouble("Min", double.MinValue);
                    var max = reader.ReadDouble("Max", double.MaxValue);
                    var changed = WithId(ui, id, label, () => ui.DragScalarN(label, values, speed, min, max));
                    if (changed)
                    {
                        UiDslValues.SetDoubleArray(ctx, id, values);
                    }
                }
                else
                {
                    var values = UiDslValues.GetFloatArray(ctx, id, reader.ReadFloatArray("Values"));
                    var min = reader.ReadFloat("Min", float.MinValue);
                    var max = reader.ReadFloat("Max", float.MaxValue);
                    var changed = WithId(ui, id, label, () => ui.DragScalarN(label, values, speed, min, max));
                    if (changed)
                    {
                        UiDslValues.SetFloatArray(ctx, id, values);
                    }
                }
                return UiDslBeginResult.None;
            }
            case "SliderFloat":
            {
                var (id, label) = ReadIdLabel(ref reader, "SliderFloat");
                var min = reader.ReadFloat("Min", 0f);
                var max = reader.ReadFloat("Max", 1f);
                var value = UiDslValues.GetFloat(ctx, id, min);
                var changed = WithId(ui, id, label, () => ui.SliderFloat(label, ref value, min, max));
                if (changed)
                {
                    UiDslValues.SetFloat(ctx, id, value);
                }
                return UiDslBeginResult.None;
            }
            case "SliderInt":
            {
                var (id, label) = ReadIdLabel(ref reader, "SliderInt");
                var min = reader.ReadInt("Min", 0);
                var max = reader.ReadInt("Max", 100);
                var value = UiDslValues.GetInt(ctx, id, min);
                var changed = WithId(ui, id, label, () => ui.SliderInt(label, ref value, min, max));
                if (changed)
                {
                    UiDslValues.SetInt(ctx, id, value);
                }
                return UiDslBeginResult.None;
            }
            case "SliderFloat2":
            {
                var (id, label) = ReadIdLabel(ref reader, "SliderFloat2");
                var min = reader.ReadFloat("Min", 0f);
                var max = reader.ReadFloat("Max", 1f);
                var vec = UiDslValues.GetVector2(ctx, id, default);
                var x = vec.X;
                var y = vec.Y;
                var changed = WithId(ui, id, label, () => ui.SliderFloat2(label, ref x, ref y, min, max));
                if (changed)
                {
                    UiDslValues.SetVector2(ctx, id, new UiVector2(x, y));
                }
                return UiDslBeginResult.None;
            }
            case "SliderFloat3":
            {
                var (id, label) = ReadIdLabel(ref reader, "SliderFloat3");
                var min = reader.ReadFloat("Min", 0f);
                var max = reader.ReadFloat("Max", 1f);
                var vec = UiDslValues.GetVector4(ctx, id, default);
                var x = vec.X;
                var y = vec.Y;
                var z = vec.Z;
                var changed = WithId(ui, id, label, () => ui.SliderFloat3(label, ref x, ref y, ref z, min, max));
                if (changed)
                {
                    UiDslValues.SetVector4(ctx, id, new UiVector4(x, y, z, vec.W));
                }
                return UiDslBeginResult.None;
            }
            case "SliderFloat4":
            {
                var (id, label) = ReadIdLabel(ref reader, "SliderFloat4");
                var min = reader.ReadFloat("Min", 0f);
                var max = reader.ReadFloat("Max", 1f);
                var vec = UiDslValues.GetVector4(ctx, id, default);
                var x = vec.X;
                var y = vec.Y;
                var z = vec.Z;
                var w = vec.W;
                var changed = WithId(ui, id, label, () => ui.SliderFloat4(label, ref x, ref y, ref z, ref w, min, max));
                if (changed)
                {
                    UiDslValues.SetVector4(ctx, id, new UiVector4(x, y, z, w));
                }
                return UiDslBeginResult.None;
            }
            case "SliderInt2":
            {
                var (id, label) = ReadIdLabel(ref reader, "SliderInt2");
                var min = reader.ReadInt("Min", 0);
                var max = reader.ReadInt("Max", 100);
                var vec = UiDslValues.GetVector2(ctx, id, default);
                var x = (int)vec.X;
                var y = (int)vec.Y;
                var changed = WithId(ui, id, label, () => ui.SliderInt2(label, ref x, ref y, min, max));
                if (changed)
                {
                    UiDslValues.SetVector2(ctx, id, new UiVector2(x, y));
                }
                return UiDslBeginResult.None;
            }
            case "SliderInt3":
            {
                var (id, label) = ReadIdLabel(ref reader, "SliderInt3");
                var min = reader.ReadInt("Min", 0);
                var max = reader.ReadInt("Max", 100);
                var vec = UiDslValues.GetVector4(ctx, id, default);
                var x = (int)vec.X;
                var y = (int)vec.Y;
                var z = (int)vec.Z;
                var changed = WithId(ui, id, label, () => ui.SliderInt3(label, ref x, ref y, ref z, min, max));
                if (changed)
                {
                    UiDslValues.SetVector4(ctx, id, new UiVector4(x, y, z, vec.W));
                }
                return UiDslBeginResult.None;
            }
            case "SliderInt4":
            {
                var (id, label) = ReadIdLabel(ref reader, "SliderInt4");
                var min = reader.ReadInt("Min", 0);
                var max = reader.ReadInt("Max", 100);
                var vec = UiDslValues.GetVector4(ctx, id, default);
                var x = (int)vec.X;
                var y = (int)vec.Y;
                var z = (int)vec.Z;
                var w = (int)vec.W;
                var changed = WithId(ui, id, label, () => ui.SliderInt4(label, ref x, ref y, ref z, ref w, min, max));
                if (changed)
                {
                    UiDslValues.SetVector4(ctx, id, new UiVector4(x, y, z, w));
                }
                return UiDslBeginResult.None;
            }
            case "SliderScalar":
            {
                var (id, label) = ReadIdLabel(ref reader, "SliderScalar");
                var type = reader.ReadString("Type", "float");
                if (type.Equals("int", StringComparison.OrdinalIgnoreCase))
                {
                    var min = reader.ReadInt("Min", 0);
                    var max = reader.ReadInt("Max", 100);
                    var value = UiDslValues.GetInt(ctx, id, min);
                    var changed = WithId(ui, id, label, () => ui.SliderScalar(label, ref value, min, max));
                    if (changed)
                    {
                        UiDslValues.SetInt(ctx, id, value);
                    }
                }
                else if (type.Equals("double", StringComparison.OrdinalIgnoreCase))
                {
                    var min = reader.ReadDouble("Min", 0d);
                    var max = reader.ReadDouble("Max", 1d);
                    var value = UiDslValues.GetDouble(ctx, id, min);
                    var changed = WithId(ui, id, label, () => ui.SliderScalar(label, ref value, min, max));
                    if (changed)
                    {
                        UiDslValues.SetDouble(ctx, id, value);
                    }
                }
                else
                {
                    var min = reader.ReadFloat("Min", 0f);
                    var max = reader.ReadFloat("Max", 1f);
                    var value = UiDslValues.GetFloat(ctx, id, min);
                    var changed = WithId(ui, id, label, () => ui.SliderScalar(label, ref value, min, max));
                    if (changed)
                    {
                        UiDslValues.SetFloat(ctx, id, value);
                    }
                }
                return UiDslBeginResult.None;
            }
            case "SliderScalarN":
            {
                var (id, label) = ReadIdLabel(ref reader, "SliderScalarN");
                var type = reader.ReadString("Type", "float");
                if (type.Equals("int", StringComparison.OrdinalIgnoreCase))
                {
                    var min = reader.ReadInt("Min", 0);
                    var max = reader.ReadInt("Max", 100);
                    var values = UiDslValues.GetIntArray(ctx, id, reader.ReadIntArray("Values"));
                    var changed = WithId(ui, id, label, () => ui.SliderScalarN(label, values, min, max));
                    if (changed)
                    {
                        UiDslValues.SetIntArray(ctx, id, values);
                    }
                }
                else if (type.Equals("double", StringComparison.OrdinalIgnoreCase))
                {
                    var min = reader.ReadDouble("Min", 0d);
                    var max = reader.ReadDouble("Max", 1d);
                    var values = UiDslValues.GetDoubleArray(ctx, id, reader.ReadDoubleArray("Values"));
                    var changed = WithId(ui, id, label, () => ui.SliderScalarN(label, values, min, max));
                    if (changed)
                    {
                        UiDslValues.SetDoubleArray(ctx, id, values);
                    }
                }
                else
                {
                    var min = reader.ReadFloat("Min", 0f);
                    var max = reader.ReadFloat("Max", 1f);
                    var values = UiDslValues.GetFloatArray(ctx, id, reader.ReadFloatArray("Values"));
                    var changed = WithId(ui, id, label, () => ui.SliderScalarN(label, values, min, max));
                    if (changed)
                    {
                        UiDslValues.SetFloatArray(ctx, id, values);
                    }
                }
                return UiDslBeginResult.None;
            }
            case "SliderAngle":
            {
                var (id, label) = ReadIdLabel(ref reader, "SliderAngle");
                var min = reader.ReadFloat("Min", -360f);
                var max = reader.ReadFloat("Max", 360f);
                var value = UiDslValues.GetFloat(ctx, id, 0f);
                var changed = WithId(ui, id, label, () => ui.SliderAngle(label, ref value, min, max));
                if (changed)
                {
                    UiDslValues.SetFloat(ctx, id, value);
                }
                return UiDslBeginResult.None;
            }
            case "VSliderFloat":
            {
                var (id, label) = ReadIdLabel(ref reader, "VSliderFloat");
                var size = reader.ReadVector2("Size", default);
                var min = reader.ReadFloat("Min", 0f);
                var max = reader.ReadFloat("Max", 1f);
                var value = UiDslValues.GetFloat(ctx, id, min);
                var changed = WithId(ui, id, label, () => ui.VSliderFloat(label, size, ref value, min, max));
                if (changed)
                {
                    UiDslValues.SetFloat(ctx, id, value);
                }
                return UiDslBeginResult.None;
            }
            case "VSliderInt":
            {
                var (id, label) = ReadIdLabel(ref reader, "VSliderInt");
                var size = reader.ReadVector2("Size", default);
                var min = reader.ReadInt("Min", 0);
                var max = reader.ReadInt("Max", 100);
                var value = UiDslValues.GetInt(ctx, id, min);
                var changed = WithId(ui, id, label, () => ui.VSliderInt(label, size, ref value, min, max));
                if (changed)
                {
                    UiDslValues.SetInt(ctx, id, value);
                }
                return UiDslBeginResult.None;
            }
            case "VSliderScalar":
            {
                var (id, label) = ReadIdLabel(ref reader, "VSliderScalar");
                var size = reader.ReadVector2("Size", default);
                var type = reader.ReadString("Type", "float");
                if (type.Equals("int", StringComparison.OrdinalIgnoreCase))
                {
                    var min = reader.ReadInt("Min", 0);
                    var max = reader.ReadInt("Max", 100);
                    var value = UiDslValues.GetInt(ctx, id, min);
                    var changed = WithId(ui, id, label, () => ui.VSliderScalar(label, size, ref value, min, max));
                    if (changed)
                    {
                        UiDslValues.SetInt(ctx, id, value);
                    }
                }
                else
                {
                    var min = reader.ReadFloat("Min", 0f);
                    var max = reader.ReadFloat("Max", 1f);
                    var value = UiDslValues.GetFloat(ctx, id, min);
                    var changed = WithId(ui, id, label, () => ui.VSliderScalar(label, size, ref value, min, max));
                    if (changed)
                    {
                        UiDslValues.SetFloat(ctx, id, value);
                    }
                }
                return UiDslBeginResult.None;
            }
            case "ColorButton":
            {
                var id = reader.ReadString("Id", "Color");
                var color = reader.ReadColor("Color", default);
                var size = reader.ReadVector2("Size", default);
                var pressed = ui.ColorButton(id, color, size);
                if (pressed)
                {
                    ctx.EventSink?.OnButton(id);
                }
                return UiDslBeginResult.None;
            }
            case "ColorEdit3":
            {
                var (id, label) = ReadIdLabel(ref reader, "ColorEdit3");
                var vec = UiDslValues.GetVector4(ctx, id, default);
                var r = vec.X;
                var g = vec.Y;
                var b = vec.Z;
                var changed = WithId(ui, id, label, () => ui.ColorEdit3(label, ref r, ref g, ref b));
                if (changed)
                {
                    UiDslValues.SetVector4(ctx, id, new UiVector4(r, g, b, vec.W));
                }
                return UiDslBeginResult.None;
            }
            case "ColorEdit4":
            {
                var (id, label) = ReadIdLabel(ref reader, "ColorEdit4");
                var vec = UiDslValues.GetVector4(ctx, id, default);
                var r = vec.X;
                var g = vec.Y;
                var b = vec.Z;
                var a = vec.W;
                var changed = WithId(ui, id, label, () => ui.ColorEdit4(label, ref r, ref g, ref b, ref a));
                if (changed)
                {
                    UiDslValues.SetVector4(ctx, id, new UiVector4(r, g, b, a));
                }
                return UiDslBeginResult.None;
            }
            case "ColorPicker3":
            {
                var (id, label) = ReadIdLabel(ref reader, "ColorPicker3");
                var vec = UiDslValues.GetVector4(ctx, id, default);
                var r = vec.X;
                var g = vec.Y;
                var b = vec.Z;
                var changed = WithId(ui, id, label, () => ui.ColorPicker3(label, ref r, ref g, ref b));
                if (changed)
                {
                    UiDslValues.SetVector4(ctx, id, new UiVector4(r, g, b, vec.W));
                }
                return UiDslBeginResult.None;
            }
            case "ColorPicker4":
            {
                var (id, label) = ReadIdLabel(ref reader, "ColorPicker4");
                var vec = UiDslValues.GetVector4(ctx, id, default);
                var r = vec.X;
                var g = vec.Y;
                var b = vec.Z;
                var a = vec.W;
                var changed = WithId(ui, id, label, () => ui.ColorPicker4(label, ref r, ref g, ref b, ref a));
                if (changed)
                {
                    UiDslValues.SetVector4(ctx, id, new UiVector4(r, g, b, a));
                }
                return UiDslBeginResult.None;
            }
            case "SetColorEditOptions":
            {
                var flags = reader.ReadUInt("Flags", 0);
                ui.SetColorEditOptions(flags);
                return UiDslBeginResult.None;
            }
            case "Columns":
            {
                var count = reader.ReadInt("Count", 1);
                var border = reader.ReadBool("Border", false);
                ui.Columns(count, border);
                return UiDslBeginResult.None;
            }
            case "NextColumn":
                ui.NextColumn();
                return UiDslBeginResult.None;
            case "GetColumnIndex":
            {
                var outKey = reader.ReadString("OutKey", string.Empty);
                var value = ui.GetColumnIndex();
                if (!string.IsNullOrWhiteSpace(outKey))
                {
                    UiDslValues.SetInt(ctx, outKey, value);
                }
                return UiDslBeginResult.None;
            }
            case "GetColumnWidth":
            {
                var index = reader.ReadInt("Index", 0);
                var outKey = reader.ReadString("OutKey", string.Empty);
                var value = ui.GetColumnWidth(index);
                if (!string.IsNullOrWhiteSpace(outKey))
                {
                    UiDslValues.SetFloat(ctx, outKey, value);
                }
                return UiDslBeginResult.None;
            }
            case "SetColumnWidth":
                ui.SetColumnWidth(reader.ReadInt("Index", 0), reader.ReadFloat("Width", 0f));
                return UiDslBeginResult.None;
            case "GetColumnOffset":
            {
                var index = reader.ReadInt("Index", 0);
                var outKey = reader.ReadString("OutKey", string.Empty);
                var value = ui.GetColumnOffset(index);
                if (!string.IsNullOrWhiteSpace(outKey))
                {
                    UiDslValues.SetFloat(ctx, outKey, value);
                }
                return UiDslBeginResult.None;
            }
            case "SetColumnOffset":
                ui.SetColumnOffset(reader.ReadInt("Index", 0), reader.ReadFloat("Offset", 0f));
                return UiDslBeginResult.None;
            case "GetColumnsCount":
            {
                var outKey = reader.ReadString("OutKey", string.Empty);
                var value = ui.GetColumnsCount();
                if (!string.IsNullOrWhiteSpace(outKey))
                {
                    UiDslValues.SetInt(ctx, outKey, value);
                }
                return UiDslBeginResult.None;
            }
            case "BeginMenuBar":
                return ui.BeginMenuBar() ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            case "BeginMainMenuBar":
                return ui.BeginMainMenuBar() ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            case "BeginMenu":
            {
                var label = reader.ReadString("Label", "Menu");
                return ui.BeginMenu(label) ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            }
            case "MenuItem":
            {
                var (id, label) = ReadIdLabel(ref reader, "Item");
                var selected = reader.ReadBool("Selected", false);
                var enabled = reader.ReadBool("Enabled", true);
                var pressed = WithId(ui, id, label, () => ui.MenuItem(label, selected, enabled));
                if (pressed)
                {
                    ctx.EventSink?.OnButton(id);
                }
                return UiDslBeginResult.None;
            }
            case "OpenPopupOnItemClick":
                ui.OpenPopupOnItemClick(reader.ReadString("Id", "Popup"));
                return UiDslBeginResult.None;
            case "OpenPopup":
                ui.OpenPopup(reader.ReadString("Id", "Popup"));
                return UiDslBeginResult.None;
            case "BeginPopupContextItem":
                return ui.BeginPopupContextItem(reader.ReadString("Id", "ContextItem")) ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            case "BeginPopupContextWindow":
                return ui.BeginPopupContextWindow(reader.ReadString("Id", "ContextWindow")) ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            case "BeginPopupContextVoid":
                return ui.BeginPopupContextVoid(reader.ReadString("Id", "ContextVoid")) ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            case "BeginPopup":
                return ui.BeginPopup(reader.ReadString("Id", "Popup")) ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            case "BeginPopupModal":
            {
                var id = reader.ReadString("Id", "Modal");
                var openKey = reader.ReadString("OpenKey", id);
                var open = UiDslValues.GetBool(ctx, openKey, true);
                var openResult = ui.BeginPopupModal(id, ref open);
                UiDslValues.SetBool(ctx, openKey, open);
                if (openResult)
                {
                    runtimeState.PopupModalKeys.Push(openKey);
                }
                return openResult ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            }
            case "CloseCurrentPopup":
                ui.CloseCurrentPopup();
                return UiDslBeginResult.None;
            case "IsPopupOpen":
            {
                var id = reader.ReadString("Id", "Popup");
                var outKey = reader.ReadString("OutKey", string.Empty);
                var isOpen = ui.IsPopupOpen(id);
                if (!string.IsNullOrWhiteSpace(outKey))
                {
                    UiDslValues.SetBool(ctx, outKey, isOpen);
                }
                return UiDslBeginResult.None;
            }
            case "BeginTabBar":
            {
                var id = reader.ReadString("Id", "TabBar");
                if (reader.TryReadEnum("Flags", out UiTabBarFlags flags))
                {
                    var open = ui.BeginTabBar(id, flags);
                    return open ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
                }

                return ui.BeginTabBar(id) ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            }
            case "BeginTabItem":
            {
                var label = reader.ReadString("Label", "Tab");
                if (reader.TryReadEnum("Flags", out UiTabItemFlags flags))
                {
                    return ui.BeginTabItem(label, flags) ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
                }

                return ui.BeginTabItem(label) ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            }
            case "TabItemButton":
            {
                var (id, label) = ReadIdLabel(ref reader, "Tab");
                var pressed = WithId(ui, id, label, () => ui.TabItemButton(label));
                if (pressed)
                {
                    ctx.EventSink?.OnButton(id);
                }
                return UiDslBeginResult.None;
            }
            case "SetTabItemClosed":
                ui.SetTabItemClosed(reader.ReadString("Label", string.Empty));
                return UiDslBeginResult.None;
            case "BeginTable":
            {
                var id = reader.ReadString("Id", "Table");
                var columns = reader.ReadInt("Columns", 1);
                if (reader.TryReadEnum("Flags", out UiTableFlags flags))
                {
                    return ui.BeginTable(id, columns, flags) ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
                }

                return ui.BeginTable(id, columns) ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            }
            case "TableSetupColumn":
            {
                var label = reader.ReadString("Label", string.Empty);
                var width = reader.ReadFloat("Width", 0f);
                var alignX = reader.ReadFloat("AlignX", 0f);
                if (reader.TryReadEnum("Flags", out UiTableColumnFlags flags))
                {
                    ui.TableSetupColumn(label, width, alignX, flags);
                }
                else if (width > 0f || alignX > 0f)
                {
                    ui.TableSetupColumn(label, width, alignX);
                }
                else
                {
                    ui.TableSetupColumn(label);
                }
                return UiDslBeginResult.None;
            }
            case "TableSetupScrollFreeze":
                ui.TableSetupScrollFreeze(reader.ReadInt("Cols", 0), reader.ReadInt("Rows", 0));
                return UiDslBeginResult.None;
            case "TableHeadersRow":
                ui.TableHeadersRow();
                return UiDslBeginResult.None;
            case "TableHeader":
                ui.TableHeader(reader.ReadString("Label", string.Empty));
                return UiDslBeginResult.None;
            case "TableAngledHeadersRow":
                ui.TableAngledHeadersRow();
                return UiDslBeginResult.None;
            case "TableHeadersRowSortable":
                ui.TableHeadersRowSortable();
                return UiDslBeginResult.None;
            case "TableNextRow":
            {
                if (reader.TryReadEnum("Flags", out UiTableRowFlags flags))
                {
                    var height = reader.ReadFloat("MinRowHeight", 0f);
                    ui.TableNextRow(flags, height);
                }
                else
                {
                    ui.TableNextRow();
                }
                return UiDslBeginResult.None;
            }
            case "TableNextColumn":
                ui.TableNextColumn();
                return UiDslBeginResult.None;
            case "TableSetColumnIndex":
                ui.TableSetColumnIndex(reader.ReadInt("Index", 0));
                return UiDslBeginResult.None;
            case "TableSetColumnWidth":
                ui.TableSetColumnWidth(reader.ReadInt("Index", 0), reader.ReadFloat("Width", 0f));
                return UiDslBeginResult.None;
            case "TableSetColumnAlign":
                ui.TableSetColumnAlign(reader.ReadInt("Index", 0), reader.ReadFloat("AlignX", 0f));
                return UiDslBeginResult.None;
            case "TableGetColumnIndex":
            {
                var outKey = reader.ReadString("OutKey", string.Empty);
                var value = ui.TableGetColumnIndex();
                if (!string.IsNullOrWhiteSpace(outKey))
                {
                    UiDslValues.SetInt(ctx, outKey, value);
                }
                return UiDslBeginResult.None;
            }
            case "TableGetColumnName":
            {
                var index = reader.ReadInt("Index", 0);
                var outKey = reader.ReadString("OutKey", string.Empty);
                var value = ui.TableGetColumnName(index);
                if (!string.IsNullOrWhiteSpace(outKey))
                {
                    UiDslValues.SetString(ctx, outKey, value);
                }
                return UiDslBeginResult.None;
            }
            case "TableGetColumnWidth":
            {
                var index = reader.ReadInt("Index", 0);
                var outKey = reader.ReadString("OutKey", string.Empty);
                var value = ui.TableGetColumnWidth(index);
                if (!string.IsNullOrWhiteSpace(outKey))
                {
                    UiDslValues.SetFloat(ctx, outKey, value);
                }
                return UiDslBeginResult.None;
            }
            case "TableGetColumnCount":
            {
                var outKey = reader.ReadString("OutKey", string.Empty);
                var value = ui.TableGetColumnCount();
                if (!string.IsNullOrWhiteSpace(outKey))
                {
                    UiDslValues.SetInt(ctx, outKey, value);
                }
                return UiDslBeginResult.None;
            }
            case "TableGetColumnFlags":
            {
                var index = reader.ReadInt("Index", 0);
                var outKey = reader.ReadString("OutKey", string.Empty);
                var value = ui.TableGetColumnFlags(index);
                if (!string.IsNullOrWhiteSpace(outKey))
                {
                    UiDslValues.SetUInt(ctx, outKey, (uint)value);
                }
                return UiDslBeginResult.None;
            }
            case "TableGetRowIndex":
            {
                var outKey = reader.ReadString("OutKey", string.Empty);
                var value = ui.TableGetRowIndex();
                if (!string.IsNullOrWhiteSpace(outKey))
                {
                    UiDslValues.SetInt(ctx, outKey, value);
                }
                return UiDslBeginResult.None;
            }
            case "TableGetHoveredColumn":
            {
                var outKey = reader.ReadString("OutKey", string.Empty);
                var value = ui.TableGetHoveredColumn();
                if (!string.IsNullOrWhiteSpace(outKey))
                {
                    UiDslValues.SetInt(ctx, outKey, value);
                }
                return UiDslBeginResult.None;
            }
            case "TableColumnIsHovered":
            {
                var index = reader.ReadInt("Index", 0);
                var outKey = reader.ReadString("OutKey", string.Empty);
                var value = ui.TableColumnIsHovered(index);
                if (!string.IsNullOrWhiteSpace(outKey))
                {
                    UiDslValues.SetBool(ctx, outKey, value);
                }
                return UiDslBeginResult.None;
            }
            case "TableSetColumnEnabled":
                ui.TableSetColumnEnabled(reader.ReadInt("Index", 0), reader.ReadBool("Enabled", true));
                return UiDslBeginResult.None;
            case "TableRowBg":
                ui.TableRowBg(reader.ReadColor("Color", default));
                return UiDslBeginResult.None;
            case "TableRowBgAlternating":
                ui.TableRowBgAlternating(reader.ReadColor("EvenColor", default), reader.ReadColor("OddColor", default));
                return UiDslBeginResult.None;
            case "TableRowSeparator":
                ui.TableRowSeparator(reader.ReadColor("Color", default));
                return UiDslBeginResult.None;
            case "TableCellBg":
                ui.TableCellBg(reader.ReadColor("Color", default));
                return UiDslBeginResult.None;
            case "TableSetBgColor":
            {
                var target = reader.ReadEnum("Target", UiTableBgTarget.RowBg0);
                var color = reader.ReadColor("Color", default);
                var column = reader.ReadInt("Column", -1);
                ui.TableSetBgColor(target, color, column);
                return UiDslBeginResult.None;
            }
            case "TableGetSortSpecs":
            {
                var columnKey = reader.ReadString("ColumnKey", string.Empty);
                var ascendingKey = reader.ReadString("AscendingKey", string.Empty);
                var changedKey = reader.ReadString("ChangedKey", string.Empty);
                if (ui.TableGetSortSpecs(out var column, out var ascending, out var changed))
                {
                    if (!string.IsNullOrWhiteSpace(columnKey)) UiDslValues.SetInt(ctx, columnKey, column);
                    if (!string.IsNullOrWhiteSpace(ascendingKey)) UiDslValues.SetBool(ctx, ascendingKey, ascending);
                    if (!string.IsNullOrWhiteSpace(changedKey)) UiDslValues.SetBool(ctx, changedKey, changed);
                }
                return UiDslBeginResult.None;
            }
            case "BeginTooltip":
                return ui.BeginTooltip() ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            case "BeginItemTooltip":
                return ui.BeginItemTooltip() ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            case "SetTooltip":
                ui.SetTooltip(reader.ReadString("Text", string.Empty));
                return UiDslBeginResult.None;
            case "SetTooltipV":
            {
                var format = reader.ReadString("Format", string.Empty);
                ui.SetTooltipV(format, reader.ReadFormatArgs());
                return UiDslBeginResult.None;
            }
            case "SetItemTooltip":
                ui.SetItemTooltip(reader.ReadString("Text", string.Empty));
                return UiDslBeginResult.None;
            case "SetItemTooltipV":
            {
                var format = reader.ReadString("Format", string.Empty);
                ui.SetItemTooltipV(format, reader.ReadFormatArgs());
                return UiDslBeginResult.None;
            }
            case "TreeNode":
            {
                var label = reader.ReadString("Label", "TreeNode");
                var defaultOpen = reader.ReadBool("DefaultOpen", false);
                var open = ui.TreeNode(label, defaultOpen);
                runtimeState.TreeOpenStack.Push(open);
                return open ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            }
            case "TreeNodeEx":
            {
                var label = reader.ReadString("Label", "TreeNode");
                var flags = reader.ReadEnum("Flags", UiTreeNodeFlags.None);
                var open = ui.TreeNodeEx(label, flags);
                runtimeState.TreeOpenStack.Push(open);
                return open ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            }
            case "TreeNodeV":
            {
                var format = reader.ReadString("Format", "TreeNode");
                var open = ui.TreeNodeV(format, reader.ReadFormatArgs());
                runtimeState.TreeOpenStack.Push(open);
                return open ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            }
            case "TreeNodeExV":
            {
                var format = reader.ReadString("Format", "TreeNode");
                var flags = reader.ReadEnum("Flags", UiTreeNodeFlags.None);
                var open = ui.TreeNodeExV(format, flags, reader.ReadFormatArgs());
                runtimeState.TreeOpenStack.Push(open);
                return open ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            }
            case "TreeNodePush":
            {
                var label = reader.ReadString("Label", "TreeNode");
                var open = ui.TreeNodePush(label);
                runtimeState.TreeOpenStack.Push(open);
                return open ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            }
            case "TreePush":
                ui.TreePush(reader.ReadString("Id", "TreePush"));
                return UiDslBeginResult.None;
            case "SetNextItemOpen":
                ui.SetNextItemOpen(reader.ReadBool("IsOpen", true));
                return UiDslBeginResult.None;
            case "SetNextItemStorageID":
                ui.SetNextItemStorageID(reader.ReadString("Id", string.Empty));
                return UiDslBeginResult.None;
            case "CollapsingHeader":
            {
                var (id, label) = ReadIdLabel(ref reader, "Header");
                var defaultOpen = reader.ReadBool("DefaultOpen", false);
                if (reader.TryReadEnum("Flags", out UiTreeNodeFlags flags))
                {
                    if (reader.TryReadBool("Open", out var openValue))
                    {
                        var open = UiDslValues.GetBool(ctx, id, openValue);
                        var changed = WithId(ui, id, label, () => ui.CollapsingHeader(label, ref open, flags));
                        if (changed)
                        {
                            UiDslValues.SetBool(ctx, id, open);
                        }
                    }
                    else
                    {
                        WithId(ui, id, label, () => ui.CollapsingHeader(label, flags));
                    }
                }
                else
                {
                    WithId(ui, id, label, () => ui.CollapsingHeader(label, defaultOpen));
                }
                return UiDslBeginResult.None;
            }
            case "BeginDragDropSource":
            {
                if (reader.TryReadEnum("Flags", out UiDragDropFlags flags))
                {
                    return ui.BeginDragDropSource(flags) ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
                }

                return ui.BeginDragDropSource() ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            }
            case "SetDragDropPayload":
            {
                var type = reader.ReadString("Type", "Payload");
                var payload = reader.ReadString("Data", string.Empty);
                var data = System.Text.Encoding.UTF8.GetBytes(payload);
                ui.SetDragDropPayload(type, data);
                return UiDslBeginResult.None;
            }
            case "BeginDragDropTarget":
                return ui.BeginDragDropTarget() ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            case "AcceptDragDropPayload":
            {
                var type = reader.ReadString("Type", "Payload");
                if (reader.TryReadEnum("Flags", out UiDragDropFlags flags))
                {
                    ui.AcceptDragDropPayload(type, flags);
                }
                else
                {
                    ui.AcceptDragDropPayload(type);
                }
                return UiDslBeginResult.None;
            }
            case "BeginDisabled":
                ui.BeginDisabled(reader.ReadBool("Disabled", true));
                return UiDslBeginResult.None;
            case "BeginMultiSelect":
            {
                if (reader.TryReadEnum("Flags", out UiMultiSelectFlags flags))
                {
                    return ui.BeginMultiSelect(flags) ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
                }

                return ui.BeginMultiSelect() ? UiDslBeginResult.None : UiDslBeginResult.SkipChildren;
            }
            default:
                throw new InvalidOperationException($"Unknown DSL node: {name}.");
        }
    }

    public static void End(UiImmediateContext ui, UiDslRenderContext ctx, UiDslRuntimeState runtimeState, string name)
    {
        switch (name)
        {
            case "Window":
            case "BeginWindow":
                ui.EndWindow();
                return;
            case "Row":
            case "BeginRow":
                ui.EndRow();
                return;
            case "BeginGroup":
                ui.EndGroup();
                return;
            case "BeginChild":
                ui.EndChild();
                return;
            case "BeginCombo":
                ui.EndCombo();
                return;
            case "BeginListBox":
            case "ListBoxHeader":
                ui.EndListBox();
                return;
            case "BeginMenu":
                ui.EndMenu();
                return;
            case "BeginMenuBar":
                ui.EndMenuBar();
                return;
            case "BeginMainMenuBar":
                ui.EndMainMenuBar();
                return;
            case "BeginPopup":
            case "BeginPopupContextItem":
            case "BeginPopupContextWindow":
            case "BeginPopupContextVoid":
                ui.EndPopup();
                return;
            case "BeginPopupModal":
            {
                if (runtimeState.PopupModalKeys.Count == 0)
                {
                    return;
                }

                var openKey = runtimeState.PopupModalKeys.Pop();
                var open = UiDslValues.GetBool(ctx, openKey, true);
                ui.EndPopupModal(ref open);
                UiDslValues.SetBool(ctx, openKey, open);
                return;
            }
            case "BeginTabBar":
                ui.EndTabBar();
                return;
            case "BeginTabItem":
                ui.EndTabItem();
                return;
            case "BeginTable":
                ui.EndTable();
                return;
            case "BeginTooltip":
            case "BeginItemTooltip":
                ui.EndTooltip();
                return;
            case "TreeNode":
            case "TreeNodeEx":
            case "TreeNodeV":
            case "TreeNodeExV":
            case "TreeNodePush":
            {
                if (runtimeState.TreeOpenStack.Count > 0 && runtimeState.TreeOpenStack.Pop())
                {
                    ui.TreePop();
                }
                return;
            }
            case "TreePush":
                ui.TreePop();
                return;
            case "BeginDragDropSource":
                ui.EndDragDropSource();
                return;
            case "BeginDragDropTarget":
                ui.EndDragDropTarget();
                return;
            case "BeginDisabled":
                ui.EndDisabled();
                return;
            case "BeginMultiSelect":
                ui.EndMultiSelect();
                return;
        }
    }

    private static (string Id, string Label) ReadIdLabel(ref UiDslArgReader reader, string defaultLabel)
    {
        var id = reader.ReadNamedString("Id", null);
        var label = reader.ReadNamedString("Text", null)
            ?? reader.ReadNamedString("Label", null)
            ?? reader.ReadNamedString("Name", null);

        if (id is null && label is null)
        {
            if (!reader.TryRead(out var first))
            {
                return (defaultLabel, defaultLabel);
            }

            if (!reader.TryRead(out var second))
            {
                return (first, first);
            }

            return (first, second);
        }

        if (id is null)
        {
            id = label ?? defaultLabel;
        }

        if (label is null)
        {
            label = id;
        }

        return (id, label);
    }

    private static T WithId<T>(UiImmediateContext ui, string id, string label, Func<T> action)
    {
        if (string.IsNullOrWhiteSpace(id) || string.Equals(id, label, StringComparison.Ordinal))
        {
            return action();
        }

        ui.PushID(id);
        try
        {
            return action();
        }
        finally
        {
            ui.PopID();
        }
    }

    private static void WithId(UiImmediateContext ui, string id, string label, Action action)
    {
        if (string.IsNullOrWhiteSpace(id) || string.Equals(id, label, StringComparison.Ordinal))
        {
            action();
            return;
        }

        ui.PushID(id);
        try
        {
            action();
        }
        finally
        {
            ui.PopID();
        }
    }

    private static void EnsureDoubleArray(ref double[] values, int length)
    {
        if (values.Length >= length)
        {
            return;
        }

        var next = new double[length];
        Array.Copy(values, next, values.Length);
        values = next;
    }
}

internal static class UiDslValues
{
    public static bool GetBool(UiDslRenderContext ctx, string id, bool defaultValue)
    {
        return ctx.ValueSource?.TryGetBool(id, out var value) == true ? value : ctx.State.GetBool(id, defaultValue);
    }

    public static void SetBool(UiDslRenderContext ctx, string id, bool value)
    {
        if (ctx.ValueSource is not null)
        {
            ctx.ValueSource.SetBool(id, value);
            return;
        }

        ctx.State.SetBool(id, value);
    }

    public static int GetInt(UiDslRenderContext ctx, string id, int defaultValue)
    {
        return ctx.ValueSource?.TryGetInt(id, out var value) == true ? value : ctx.State.GetInt(id, defaultValue);
    }

    public static void SetInt(UiDslRenderContext ctx, string id, int value)
    {
        if (ctx.ValueSource is not null)
        {
            ctx.ValueSource.SetInt(id, value);
            return;
        }

        ctx.State.SetInt(id, value);
    }

    public static uint GetUInt(UiDslRenderContext ctx, string id, uint defaultValue)
    {
        return ctx.ValueSource?.TryGetUInt(id, out var value) == true ? value : ctx.State.GetUInt(id, defaultValue);
    }

    public static void SetUInt(UiDslRenderContext ctx, string id, uint value)
    {
        if (ctx.ValueSource is not null)
        {
            ctx.ValueSource.SetUInt(id, value);
            return;
        }

        ctx.State.SetUInt(id, value);
    }

    public static float GetFloat(UiDslRenderContext ctx, string id, float defaultValue)
    {
        return ctx.ValueSource?.TryGetFloat(id, out var value) == true ? value : ctx.State.GetFloat(id, defaultValue);
    }

    public static void SetFloat(UiDslRenderContext ctx, string id, float value)
    {
        if (ctx.ValueSource is not null)
        {
            ctx.ValueSource.SetFloat(id, value);
            return;
        }

        ctx.State.SetFloat(id, value);
    }

    public static double GetDouble(UiDslRenderContext ctx, string id, double defaultValue)
    {
        return ctx.ValueSource?.TryGetDouble(id, out var value) == true ? value : ctx.State.GetDouble(id, defaultValue);
    }

    public static void SetDouble(UiDslRenderContext ctx, string id, double value)
    {
        if (ctx.ValueSource is not null)
        {
            ctx.ValueSource.SetDouble(id, value);
            return;
        }

        ctx.State.SetDouble(id, value);
    }

    public static string GetString(UiDslRenderContext ctx, string id, string defaultValue)
    {
        return ctx.ValueSource?.TryGetString(id, out var value) == true ? value : ctx.State.GetString(id, defaultValue);
    }

    public static void SetString(UiDslRenderContext ctx, string id, string value)
    {
        if (ctx.ValueSource is not null)
        {
            ctx.ValueSource.SetString(id, value);
            return;
        }

        ctx.State.SetString(id, value);
    }

    public static UiVector2 GetVector2(UiDslRenderContext ctx, string id, UiVector2 defaultValue)
    {
        return ctx.ValueSource?.TryGetVector2(id, out var value) == true ? value : ctx.State.GetVector2(id, defaultValue);
    }

    public static void SetVector2(UiDslRenderContext ctx, string id, UiVector2 value)
    {
        if (ctx.ValueSource is not null)
        {
            ctx.ValueSource.SetVector2(id, value);
            return;
        }

        ctx.State.SetVector2(id, value);
    }

    public static UiVector4 GetVector4(UiDslRenderContext ctx, string id, UiVector4 defaultValue)
    {
        return ctx.ValueSource?.TryGetVector4(id, out var value) == true ? value : ctx.State.GetVector4(id, defaultValue);
    }

    public static void SetVector4(UiDslRenderContext ctx, string id, UiVector4 value)
    {
        if (ctx.ValueSource is not null)
        {
            ctx.ValueSource.SetVector4(id, value);
            return;
        }

        ctx.State.SetVector4(id, value);
    }

    public static UiColor GetColor(UiDslRenderContext ctx, string id, UiColor defaultValue)
    {
        return ctx.ValueSource?.TryGetColor(id, out var value) == true ? value : ctx.State.GetColor(id, defaultValue);
    }

    public static void SetColor(UiDslRenderContext ctx, string id, UiColor value)
    {
        if (ctx.ValueSource is not null)
        {
            ctx.ValueSource.SetColor(id, value);
            return;
        }

        ctx.State.SetColor(id, value);
    }

    public static float[] GetFloatArray(UiDslRenderContext ctx, string id, float[] defaultValue)
    {
        return ctx.State.GetFloatArray(id, defaultValue);
    }

    public static void SetFloatArray(UiDslRenderContext ctx, string id, float[] value)
    {
        ctx.State.SetFloatArray(id, value);
    }

    public static int[] GetIntArray(UiDslRenderContext ctx, string id, int[] defaultValue)
    {
        return ctx.State.GetIntArray(id, defaultValue);
    }

    public static void SetIntArray(UiDslRenderContext ctx, string id, int[] value)
    {
        ctx.State.SetIntArray(id, value);
    }

    public static double[] GetDoubleArray(UiDslRenderContext ctx, string id, double[] defaultValue)
    {
        return ctx.State.GetDoubleArray(id, defaultValue);
    }

    public static void SetDoubleArray(UiDslRenderContext ctx, string id, double[] value)
    {
        ctx.State.SetDoubleArray(id, value);
    }
}

internal ref struct UiDslArgReader
{
    private readonly IReadOnlyList<string> _args;
    private readonly List<string> _positional;
    private readonly Dictionary<string, string?> _named;
    private int _index;

    public UiDslArgReader(IReadOnlyList<string> args)
    {
        _args = args;
        _positional = new List<string>(args.Count);
        _named = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        _index = 0;

        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var equals = token.IndexOf('=');
            if (equals > 0 && equals < token.Length - 1)
            {
                var key = token[..equals].Trim();
                var value = token[(equals + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    _named[key] = value;
                    continue;
                }
            }

            _positional.Add(token);
        }
    }

    public bool TryRead(out string value)
    {
        if (_index < _positional.Count)
        {
            value = _positional[_index++];
            return true;
        }

        value = string.Empty;
        return false;
    }

    public string ReadString(string defaultValue)
    {
        return TryRead(out var value) ? value : defaultValue;
    }

    public string ReadString(string key, string defaultValue)
    {
        return TryReadNamed(key, out var value) ? value : ReadString(defaultValue);
    }

    public string? ReadOptionalString()
    {
        return TryRead(out var value) ? value : null;
    }

    public string? ReadOptionalString(string key)
    {
        return TryReadNamed(key, out var value) ? value : ReadOptionalString();
    }

    public bool TryReadNamed(string key, out string value)
    {
        if (_named.TryGetValue(key, out string? raw) && raw is not null)
        {
            value = raw;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public string? ReadNamedString(string key, string? defaultValue)
    {
        return _named.TryGetValue(key, out string? value) && value is not null ? value : defaultValue;
    }


    public bool ReadBool(bool defaultValue)
    {
        return TryReadBool(out var value) ? value : defaultValue;
    }

    public bool ReadBool(string key, bool defaultValue)
    {
        if (TryReadNamed(key, out var raw))
        {
            if (bool.TryParse(raw, out var value))
            {
                return value;
            }

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                return intValue != 0;
            }
        }

        return ReadBool(defaultValue);
    }

    public bool TryReadBool(string key, out bool value)
    {
        if (TryReadNamed(key, out var raw))
        {
            if (bool.TryParse(raw, out value))
            {
                return true;
            }

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                value = intValue != 0;
                return true;
            }
        }

        return TryReadBool(out value);
    }

    public bool TryReadBool(out bool value)
    {
        if (TryRead(out var raw))
        {
            if (bool.TryParse(raw, out value))
            {
                return true;
            }

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                value = intValue != 0;
                return true;
            }
        }

        value = default;
        return false;
    }

    public int ReadInt(int defaultValue)
    {
        return TryReadInt(out var value) ? value : defaultValue;
    }

    public int ReadInt(string key, int defaultValue)
    {
        return TryReadNamed(key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : ReadInt(defaultValue);
    }

    public bool TryReadInt(string key, out int value)
    {
        if (TryReadNamed(key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return TryReadInt(out value);
    }

    public bool TryReadInt(out int value)
    {
        if (TryRead(out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    public uint ReadUInt(uint defaultValue)
    {
        return TryReadUInt(out var value) ? value : defaultValue;
    }

    public uint ReadUInt(string key, uint defaultValue)
    {
        return TryReadNamed(key, out var raw) && uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : ReadUInt(defaultValue);
    }

    public bool TryReadUInt(out uint value)
    {
        if (TryRead(out var raw) && uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    public float ReadFloat(float defaultValue)
    {
        return TryReadFloat(out var value) ? value : defaultValue;
    }

    public float ReadFloat(string key, float defaultValue)
    {
        return TryReadNamed(key, out var raw) && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : ReadFloat(defaultValue);
    }

    public bool TryReadFloat(string key, out float value)
    {
        if (TryReadNamed(key, out var raw) && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return TryReadFloat(out value);
    }

    public bool TryReadFloat(out float value)
    {
        if (TryRead(out var raw) && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    public double ReadDouble(double defaultValue)
    {
        return TryReadDouble(out var value) ? value : defaultValue;
    }

    public double ReadDouble(string key, double defaultValue)
    {
        return TryReadNamed(key, out var raw) && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : ReadDouble(defaultValue);
    }

    public bool TryReadDouble(string key, out double value)
    {
        if (TryReadNamed(key, out var raw) && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return TryReadDouble(out value);
    }

    public bool TryReadDouble(out double value)
    {
        if (TryRead(out var raw) && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    public UiVector2 ReadVector2()
    {
        return TryReadVector2(out var value) ? value : default;
    }

    public UiVector2 ReadVector2(string key, UiVector2 defaultValue)
    {
        if (TryReadNamed(key, out var raw) && TryParseVector(raw, 2, out var parts))
        {
            return new UiVector2(parts[0], parts[1]);
        }

        return TryReadVector2(out var value) ? value : defaultValue;
    }

    public bool TryReadVector2(out UiVector2 value)
    {
        if (!TryRead(out var raw))
        {
            value = default;
            return false;
        }

        if (TryParseVector(raw, 2, out var parts))
        {
            value = new UiVector2(parts[0], parts[1]);
            return true;
        }

        if (TryReadFloat(out var y))
        {
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
            {
                value = new UiVector2(x, y);
                return true;
            }
        }

        value = default;
        return false;
    }

    public UiColor ReadColor()
    {
        return TryReadColor(out var value) ? value : default;
    }

    public UiColor ReadColor(string key, UiColor defaultValue)
    {
        if (TryReadNamed(key, out var raw))
        {
            if (TryParseHex(raw, out var rgba))
            {
                return new UiColor(rgba);
            }

            if (uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return new UiColor(parsed);
            }
        }

        return TryReadColor(out var value) ? value : defaultValue;
    }

    public bool TryReadColor(string key, out UiColor value)
    {
        if (TryReadNamed(key, out var raw))
        {
            if (TryParseHex(raw, out var rgba))
            {
                value = new UiColor(rgba);
                return true;
            }

            if (uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                value = new UiColor(parsed);
                return true;
            }
        }

        return TryReadColor(out value);
    }

    public bool TryReadColor(out UiColor value)
    {
        if (!TryRead(out var raw))
        {
            value = default;
            return false;
        }

        if (TryParseHex(raw, out var rgba))
        {
            value = new UiColor(rgba);
            return true;
        }

        if (uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            value = new UiColor(parsed);
            return true;
        }

        value = default;
        return false;
    }

    public UiTextureId ReadTextureId()
    {
        if (TryReadUInt(out var value))
        {
            return new UiTextureId((nuint)value);
        }

        return new UiTextureId(0);
    }

    public UiTextureId ReadTextureId(string key)
    {
        if (TryReadNamed(key, out var raw) && uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return new UiTextureId((nuint)value);
        }

        return ReadTextureId();
    }

    public bool TryReadEnum<T>(out T value) where T : struct, Enum
    {
        if (TryRead(out var raw) && Enum.TryParse(raw, true, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    public bool TryReadEnum<T>(string key, out T value) where T : struct, Enum
    {
        if (TryReadNamed(key, out var raw) && Enum.TryParse(raw, true, out value))
        {
            return true;
        }

        return TryReadEnum(out value);
    }

    public T ReadEnum<T>(T defaultValue) where T : struct, Enum
    {
        return TryReadEnum(out T value) ? value : defaultValue;
    }

    public T ReadEnum<T>(string key, T defaultValue) where T : struct, Enum
    {
        return TryReadNamed(key, out var raw) && Enum.TryParse(raw, true, out T value)
            ? value
            : ReadEnum(defaultValue);
    }

    public object[] ReadFormatArgs()
    {
        if (_index >= _args.Count)
        {
            return Array.Empty<object>();
        }

        var remaining = _args.Count - _index;
        var values = new object[remaining];
        for (var i = 0; i < remaining; i++)
        {
            var raw = _args[_index++];
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                values[i] = intValue;
            }
            else if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
            {
                values[i] = doubleValue;
            }
            else if (bool.TryParse(raw, out var boolValue))
            {
                values[i] = boolValue;
            }
            else
            {
                values[i] = raw;
            }
        }

        return values;
    }

    public string[] ReadStringList()
    {
        if (!TryRead(out var raw))
        {
            return Array.Empty<string>();
        }

        var parts = raw.Split(['|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? Array.Empty<string>() : parts;
    }

    public string[] ReadStringList(string key)
    {
        if (TryReadNamed(key, out var raw))
        {
            var parts = raw.Split(['|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length == 0 ? Array.Empty<string>() : parts;
        }

        return ReadStringList();
    }

    public float[] ReadFloatArray()
    {
        if (!TryRead(out var raw))
        {
            return Array.Empty<float>();
        }

        return ParseFloatList(raw);
    }

    public float[] ReadFloatArray(string key)
    {
        return TryReadNamed(key, out var raw) ? ParseFloatList(raw) : ReadFloatArray();
    }

    public int[] ReadIntArray()
    {
        if (!TryRead(out var raw))
        {
            return Array.Empty<int>();
        }

        return ParseIntList(raw);
    }

    public int[] ReadIntArray(string key)
    {
        return TryReadNamed(key, out var raw) ? ParseIntList(raw) : ReadIntArray();
    }

    public double[] ReadDoubleArray()
    {
        if (!TryRead(out var raw))
        {
            return Array.Empty<double>();
        }

        return ParseDoubleList(raw);
    }

    public double[] ReadDoubleArray(string key)
    {
        return TryReadNamed(key, out var raw) ? ParseDoubleList(raw) : ReadDoubleArray();
    }

    private static float[] ParseFloatList(string raw)
    {
        var parts = raw.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return Array.Empty<float>();
        }

        var list = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                list[i] = value;
            }
        }

        return list;
    }

    private static int[] ParseIntList(string raw)
    {
        var parts = raw.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return Array.Empty<int>();
        }

        var list = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                list[i] = value;
            }
        }

        return list;
    }

    private static double[] ParseDoubleList(string raw)
    {
        var parts = raw.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return Array.Empty<double>();
        }

        var list = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                list[i] = value;
            }
        }

        return list;
    }

    private static bool TryParseVector(string raw, int expectedCount, out float[] values)
    {
        values = Array.Empty<float>();
        var parts = raw.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != expectedCount)
        {
            return false;
        }

        var temp = new float[expectedCount];
        for (var i = 0; i < expectedCount; i++)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out temp[i]))
            {
                return false;
            }
        }

        values = temp;
        return true;
    }

    private static bool TryParseHex(string raw, out uint value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        raw = raw.Trim();
        if (raw.StartsWith("#", StringComparison.Ordinal))
        {
            raw = raw[1..];
        }
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw[2..];
        }

        if (raw.Length == 6)
        {
            return uint.TryParse(raw + "FF", NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        return uint.TryParse(raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

}


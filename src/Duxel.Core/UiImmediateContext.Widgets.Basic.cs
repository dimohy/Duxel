using System.Diagnostics;
using System.Globalization;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool Button(string label)
    {
        return Button(label, default);
    }

    public bool Button(string label, UiVector2 size)
    {
        label ??= "Button";

        var displayLabel = GetDisplayLabel(label);
        var textSize = UiTextBuilder.MeasureText(_fontAtlas, displayLabel, _textSettings, _lineHeight);
        var width = size.X > 0f ? size.X : textSize.X + ButtonPaddingX * 2f;
        var height = size.Y > 0f ? size.Y : textSize.Y + ButtonPaddingY * 2f;
        var cursor = AdvanceCursor(new UiVector2(width, height));
        var rect = new UiRect(cursor.X, cursor.Y, width, height);

        var pressed = ButtonBehavior(label, rect, out var hovered, out var held);
        var color = held ? _theme.ButtonActive : hovered ? _theme.ButtonHovered : _theme.Button;

        AddRectFilled(rect, color, _whiteTexture);

        var textPos = new UiVector2(
            rect.X + (rect.Width - textSize.X) * 0.5f,
            rect.Y + (rect.Height - textSize.Y) * 0.5f
        );
        if (!string.IsNullOrEmpty(displayLabel))
        {
            _builder.AddText(
                _fontAtlas,
                displayLabel,
                textPos,
                _theme.Text,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        return pressed;
    }

    public bool SmallButton(string label)
    {
        label ??= "Button";

        const float smallPadX = 4f;
        const float smallPadY = 2f;
        var displayLabel = GetDisplayLabel(label);
        var textSize = UiTextBuilder.MeasureText(_fontAtlas, displayLabel, _textSettings, _lineHeight);
        var size = new UiVector2(textSize.X + smallPadX * 2f, textSize.Y + smallPadY * 2f);
        var cursor = AdvanceCursor(size);
        var rect = new UiRect(cursor.X, cursor.Y, size.X, size.Y);

        var pressed = ButtonBehavior(label, rect, out var hovered, out var held);
        var color = held ? _theme.ButtonActive : hovered ? _theme.ButtonHovered : _theme.Button;
        AddRectFilled(rect, color, _whiteTexture);

        var textPos = new UiVector2(
            rect.X + (rect.Width - textSize.X) * 0.5f,
            rect.Y + (rect.Height - textSize.Y) * 0.5f
        );
        if (!string.IsNullOrEmpty(displayLabel))
        {
            _builder.AddText(
                _fontAtlas,
                displayLabel,
                textPos,
                _theme.Text,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        return pressed;
    }

    public bool InvisibleButton(string id, UiVector2 size)
    {
        id ??= "InvisibleButton";

        var width = MathF.Max(1f, size.X);
        var height = MathF.Max(1f, size.Y);
        var cursor = AdvanceCursor(new UiVector2(width, height));
        var rect = new UiRect(cursor.X, cursor.Y, width, height);
        return ButtonBehavior(id, rect, out _, out _);
    }

    public bool ArrowButton(string id, UiDir dir)
    {
        id ??= "Arrow";

        var size = GetFrameHeight();
        var cursor = AdvanceCursor(new UiVector2(size, size));
        var rect = new UiRect(cursor.X, cursor.Y, size, size);

        var pressed = ButtonBehavior(id, rect, out var hovered, out var held);
        var color = held ? _theme.ButtonActive : hovered ? _theme.ButtonHovered : _theme.Button;
        AddRectFilled(rect, color, _whiteTexture);

        var arrow = dir switch
        {
            UiDir.Left => "<",
            UiDir.Right => ">",
            UiDir.Up => "^",
            UiDir.Down => "v",
            _ => ">",
        };
        var textSize = UiTextBuilder.MeasureText(_fontAtlas, arrow, _textSettings, _lineHeight);
        var textPos = new UiVector2(
            rect.X + (rect.Width - textSize.X) * 0.5f,
            rect.Y + (rect.Height - textSize.Y) * 0.5f
        );
        _builder.AddText(
            _fontAtlas,
            arrow,
            textPos,
            _theme.Text,
            _fontTexture,
            CurrentClipRect,
            _textSettings,
            _lineHeight
        );

        return pressed;
    }

    public void Text(string text)
    {
        RenderText(text, _theme.Text);
    }

    public void TextV(string format, params object[] args)
    {
        Text(FormatInvariant(format, args));
    }

    public void TextV<T0>(string format, T0 arg0)
    {
        Text(FormatInvariant(format, arg0));
    }

    public void TextV<T0, T1>(string format, T0 arg0, T1 arg1)
    {
        Text(FormatInvariant(format, arg0, arg1));
    }

    public void TextColored(UiColor color, string text)
    {
        RenderText(text, color);
    }

    public void TextColoredV(UiColor color, string format, params object[] args)
    {
        TextColored(color, FormatInvariant(format, args));
    }

    public void TextColoredV<T0>(UiColor color, string format, T0 arg0)
    {
        TextColored(color, FormatInvariant(format, arg0));
    }

    public void TextColoredV<T0, T1>(UiColor color, string format, T0 arg0, T1 arg1)
    {
        TextColored(color, FormatInvariant(format, arg0, arg1));
    }

    public void TextDisabled(string text)
    {
        RenderText(text, _theme.TextDisabled);
    }

    public void TextDisabledV(string format, params object[] args)
    {
        TextDisabled(FormatInvariant(format, args));
    }

    public void TextDisabledV<T0>(string format, T0 arg0)
    {
        TextDisabled(FormatInvariant(format, arg0));
    }

    public void TextDisabledV<T0, T1>(string format, T0 arg0, T1 arg1)
    {
        TextDisabled(FormatInvariant(format, arg0, arg1));
    }

    public void TextWrapped(string text)
    {
        text ??= string.Empty;

        var current = _layouts.Peek();
        var availableWidth = GetWrapWidth(current.Cursor);
        if (availableWidth <= 0f && _hasWindowRect)
        {
            availableWidth = MathF.Max(0f, (_windowRect.X + _windowRect.Width - WindowPadding) - current.Cursor.X);
        }

        var wrapped = availableWidth > 0f ? WrapText(text, availableWidth) : text;
        RenderText(wrapped, _theme.Text);
    }

    public void TextWrappedV(string format, params object[] args)
    {
        TextWrapped(FormatInvariant(format, args));
    }

    public void TextWrappedV<T0>(string format, T0 arg0)
    {
        TextWrapped(FormatInvariant(format, arg0));
    }

    public void TextWrappedV<T0, T1>(string format, T0 arg0, T1 arg1)
    {
        TextWrapped(FormatInvariant(format, arg0, arg1));
    }

    public void TextUnformatted(string text)
    {
        RenderText(text, _theme.Text);
    }

    public void DrawTextAligned(
        UiRect containerRect,
        string text,
        UiColor color,
        UiItemHorizontalAlign horizontalAlign = UiItemHorizontalAlign.Left,
        UiItemVerticalAlign verticalAlign = UiItemVerticalAlign.Top,
        float? fontSize = null,
        bool clipToContainer = true)
    {
        text ??= string.Empty;
        if (string.IsNullOrEmpty(text) || containerRect.Width <= 0f || containerRect.Height <= 0f)
        {
            return;
        }

        var pushedFont = false;
        if (fontSize is float targetSize && targetSize > 0f)
        {
            PushFontSize(targetSize);
            pushedFont = true;
        }

        try
        {
            var textSize = UiTextBuilder.MeasureText(_fontAtlas, text, _textSettings, _lineHeight);
            var position = AlignRect(containerRect, textSize, horizontalAlign, verticalAlign);

            var itemClip = ResolveItemClipRect();
            var clipRect = clipToContainer ? IntersectRect(itemClip, containerRect) : itemClip;
            var clipped = IntersectRect(clipRect, new UiRect(position.X, position.Y, textSize.X, textSize.Y));
            if (clipped.Width <= 0f || clipped.Height <= 0f)
            {
                return;
            }

            _builder.AddText(
                _fontAtlas,
                text,
                position,
                color,
                _fontTexture,
                clipRect,
                _textSettings,
                _lineHeight
            );
        }
        finally
        {
            if (pushedFont)
            {
                PopFontSize();
            }
        }
    }

    public UiRect BeginWindowCanvas(UiColor? background = null, UiTextureId? textureId = null, bool clipToCanvas = true)
    {
        var origin = GetCursorScreenPos();
        var avail = GetContentRegionAvail();
        var canvas = new UiRect(origin.X, origin.Y, MathF.Max(1f, avail.X), MathF.Max(1f, avail.Y));
        var drawList = GetWindowDrawList();
        var texture = textureId ?? WhiteTextureId;

        drawList.PushTexture(texture);
        if (clipToCanvas)
        {
            drawList.PushClipRect(canvas);
        }

        if (background is UiColor backgroundColor)
        {
            var clipRect = clipToCanvas ? canvas : CurrentClipRect;
            drawList.AddRectFilled(canvas, backgroundColor, texture, clipRect);
        }

        _windowCanvasStack.Push(new UiWindowCanvasState(drawList, origin, canvas, clipToCanvas));
        return canvas;
    }

    public UiRect EndWindowCanvas()
    {
        if (_windowCanvasStack.Count == 0)
        {
            return default;
        }

        var state = _windowCanvasStack.Pop();
        if (state.HasClipRect)
        {
            state.DrawList.PopClipRect();
        }

        state.DrawList.PopTexture();
        SetCursorScreenPos(state.Origin);
        Dummy(new UiVector2(state.Rect.Width, state.Rect.Height));
        return state.Rect;
    }

    public void DrawOverlayText(
        string text,
        UiColor color,
        UiItemHorizontalAlign horizontalAlign = UiItemHorizontalAlign.Right,
        UiItemVerticalAlign verticalAlign = UiItemVerticalAlign.Top,
        UiColor? background = null,
        UiVector2? margin = null,
        UiVector2? padding = null,
        float? fontSize = null)
    {
        text ??= string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var viewport = GetMainViewport();
        var marginValue = margin ?? new UiVector2(8f, 8f);
        var paddingValue = padding ?? new UiVector2(6f, 4f);
        var viewportRect = new UiRect(viewport.WorkPos.X, viewport.WorkPos.Y, viewport.WorkSize.X, viewport.WorkSize.Y);
        var containerRect = new UiRect(
            viewportRect.X + marginValue.X,
            viewportRect.Y + marginValue.Y,
            MathF.Max(0f, viewportRect.Width - marginValue.X * 2f),
            MathF.Max(0f, viewportRect.Height - marginValue.Y * 2f)
        );

        var pushedFont = false;
        if (fontSize is float targetSize && targetSize > 0f)
        {
            PushFontSize(targetSize);
            pushedFont = true;
        }

        try
        {
            var textSize = UiTextBuilder.MeasureText(_fontAtlas, text, _textSettings, _lineHeight);
            var textPos = AlignRect(containerRect, textSize, horizontalAlign, verticalAlign);
            var drawList = GetForegroundDrawList();

            if (background is UiColor backgroundColor)
            {
                var bgRect = new UiRect(
                    textPos.X - paddingValue.X,
                    textPos.Y - paddingValue.Y,
                    textSize.X + paddingValue.X * 2f,
                    textSize.Y + paddingValue.Y * 2f
                );
                var clippedBg = IntersectRect(viewportRect, bgRect);
                if (clippedBg.Width > 0f && clippedBg.Height > 0f)
                {
                    drawList.AddRectFilled(clippedBg, backgroundColor, WhiteTextureId, viewportRect);
                }
            }

            drawList.AddText(
                _fontAtlas,
                text,
                textPos,
                color,
                _fontTexture,
                viewportRect,
                _textSettings,
                _lineHeight
            );
        }
        finally
        {
            if (pushedFont)
            {
                PopFontSize();
            }
        }
    }

    public void DrawKeyValueRow(
        UiRect rowRect,
        string key,
        string value,
        bool selected = false,
        UiColor? keyColor = null,
        UiColor? valueColor = null,
        UiColor? accent = null,
        float keyWidth = 42f,
        float horizontalPadding = 12f,
        float verticalPadding = 4f,
        bool clipToRow = true)
    {
        if (rowRect.Width <= 0f || rowRect.Height <= 0f)
        {
            return;
        }

        key ??= string.Empty;
        value ??= string.Empty;

        if (selected)
        {
            var accentColor = accent ?? new UiColor(0xFF39AFFF);
            var indicatorRect = new UiRect(rowRect.X + 2f, rowRect.Y + 3f, 3f, MathF.Max(6f, rowRect.Height - 6f));
            GetWindowDrawList().AddRectFilled(indicatorRect, accentColor, WhiteTextureId, clipToRow ? rowRect : CurrentClipRect);
        }

        var insetX = MathF.Max(0f, horizontalPadding);
        var insetY = MathF.Max(0f, verticalPadding);
        var contentRect = new UiRect(
            rowRect.X + insetX,
            rowRect.Y + insetY,
            MathF.Max(0f, rowRect.Width - insetX * 2f),
            MathF.Max(0f, rowRect.Height - insetY * 2f));

        if (contentRect.Width <= 0f || contentRect.Height <= 0f)
        {
            return;
        }

        var keyAreaWidth = MathF.Min(MathF.Max(0f, keyWidth), contentRect.Width);
        var keyRect = new UiRect(contentRect.X, contentRect.Y, keyAreaWidth, contentRect.Height);
        var valueRect = new UiRect(
            contentRect.X + keyAreaWidth,
            contentRect.Y,
            MathF.Max(0f, contentRect.Width - keyAreaWidth),
            contentRect.Height);

        DrawTextAligned(
            keyRect,
            key,
            keyColor ?? _theme.Text,
            UiItemHorizontalAlign.Left,
            UiItemVerticalAlign.Center,
            fontSize: null,
            clipToContainer: clipToRow);

        DrawTextAligned(
            valueRect,
            value,
            valueColor ?? _theme.TextDisabled,
            UiItemHorizontalAlign.Left,
            UiItemVerticalAlign.Center,
            fontSize: null,
            clipToContainer: clipToRow);
    }

    public UiRect DrawLayerCardSkeleton(
        UiRect canvas,
        UiVector2 position,
        UiVector2 size,
        UiColor headerColor,
        out UiRect headerRect,
        out UiRect bodyRect,
        UiColor? bodyBackground = null,
        UiColor? borderColor = null,
        float headerHeight = 24f,
        float borderThickness = 1f)
    {
        var layerRect = new UiRect(
            canvas.X + position.X,
            canvas.Y + position.Y,
            MathF.Max(1f, size.X),
            MathF.Max(1f, size.Y));

        var clampedHeaderHeight = Math.Clamp(headerHeight, 1f, layerRect.Height);
        headerRect = new UiRect(layerRect.X, layerRect.Y, layerRect.Width, clampedHeaderHeight);
        bodyRect = new UiRect(layerRect.X, layerRect.Y + clampedHeaderHeight, layerRect.Width, layerRect.Height - clampedHeaderHeight);

        var drawList = GetWindowDrawList();
        drawList.AddRectFilled(layerRect, bodyBackground ?? new UiColor(0xCC202020), WhiteTextureId, canvas);
        drawList.AddRectFilled(headerRect, headerColor, WhiteTextureId, canvas);

        var thickness = MathF.Max(0f, borderThickness);
        if (thickness > 0f)
        {
            drawList.AddRect(layerRect, borderColor ?? new UiColor(0xFFA0A0A0), 0f, thickness);
        }

        return layerRect;
    }

    public UiRect DrawLayerCard(
        UiRect canvas,
        UiVector2 position,
        UiVector2 size,
        UiColor headerColor,
        string? headerText,
        out UiRect headerRect,
        out UiRect bodyRect,
        out bool hitClicked,
        UiColor? bodyBackground = null,
        UiColor? borderColor = null,
        UiColor? headerTextColor = null,
        float headerHeight = 24f,
        float borderThickness = 1f,
        float headerTextInsetX = 8f,
        float headerTextInsetY = 5f,
        float? headerMarkerCenterX = null,
        float headerMarkerRadius = 0f,
        UiColor? headerMarkerColor = null,
        string? hitTestId = null)
    {
        var cursorBackup = GetCursorScreenPos();

        var layerRect = DrawLayerCardSkeleton(
            canvas,
            position,
            size,
            headerColor,
            out headerRect,
            out bodyRect,
            bodyBackground,
            borderColor,
            headerHeight,
            borderThickness);

        if (headerMarkerCenterX is float markerX && headerMarkerRadius > 0f)
        {
            var markerCenter = new UiVector2(markerX, headerRect.Y + headerRect.Height * 0.5f);
            GetWindowDrawList().AddCircleFilled(
                markerCenter,
                headerMarkerRadius,
                headerMarkerColor ?? new UiColor(0xFFFCF18B),
                WhiteTextureId,
                canvas,
                12);
        }

        if (!string.IsNullOrEmpty(headerText))
        {
            SetCursorScreenPos(new UiVector2(layerRect.X + headerTextInsetX, layerRect.Y + headerTextInsetY));
            TextColored(headerTextColor ?? _theme.Text, headerText);
        }

        hitClicked = false;
        if (!string.IsNullOrWhiteSpace(hitTestId))
        {
            SetCursorScreenPos(new UiVector2(layerRect.X, layerRect.Y));
            InvisibleButton(hitTestId, new UiVector2(layerRect.Width, layerRect.Height));
            hitClicked = IsItemClicked();
        }

        SetCursorScreenPos(cursorBackup);
        return layerRect;
    }

    public UiRect DrawLayerCardInteractive(
        UiRect canvas,
        UiVector2 position,
        UiVector2 size,
        UiColor headerColor,
        string? headerText,
        out UiRect headerRect,
        out UiRect bodyRect,
        out UiLayerCardInteraction interaction,
        UiColor? bodyBackground = null,
        UiColor? borderColor = null,
        UiColor? headerTextColor = null,
        float headerHeight = 24f,
        float borderThickness = 1f,
        float headerTextInsetX = 8f,
        float headerTextInsetY = 5f,
        float? headerMarkerCenterX = null,
        float headerMarkerRadius = 0f,
        UiColor? headerMarkerColor = null,
        string? hitTestId = null)
    {
        var layerRect = DrawLayerCard(
            canvas,
            position,
            size,
            headerColor,
            headerText,
            out headerRect,
            out bodyRect,
            out var hitClicked,
            bodyBackground,
            borderColor,
            headerTextColor,
            headerHeight,
            borderThickness,
            headerTextInsetX,
            headerTextInsetY,
            headerMarkerCenterX,
            headerMarkerRadius,
            headerMarkerColor,
            hitTestId);

        if (string.IsNullOrWhiteSpace(hitTestId))
        {
            interaction = default;
            return layerRect;
        }

        interaction = new UiLayerCardInteraction(
            Clicked: hitClicked,
            Held: IsItemActive() && IsMouseDown((int)UiMouseButton.Left),
            Released: IsItemDeactivated(),
            Hovered: IsItemHovered(),
            MousePosition: GetMousePos());

        return layerRect;
    }

    public bool TextLink(string label)
    {
        label ??= "Link";

        var displayLabel = GetDisplayLabel(label);
        var textSize = UiTextBuilder.MeasureText(_fontAtlas, displayLabel, _textSettings, _lineHeight);
        var size = new UiVector2(textSize.X, textSize.Y);
        var cursor = AdvanceCursor(size);
        var rect = new UiRect(cursor.X, cursor.Y, size.X, size.Y);

        var pressed = ButtonBehavior(label, rect, out var hovered, out _);
        var color = hovered ? _theme.SliderGrabActive : _theme.SliderGrab;
        if (!string.IsNullOrEmpty(displayLabel))
        {
            _builder.AddText(
                _fontAtlas,
                displayLabel,
                cursor,
                color,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        return pressed;
    }

    public bool TextLinkOpenURL(string label, string url)
    {
        var pressed = TextLink(label);
        if (pressed && !string.IsNullOrWhiteSpace(url))
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // Ignore failures to open URLs.
            }
        }

        return pressed;
    }

    public void LabelText(string label, string text)
    {
        label ??= string.Empty;
        text ??= string.Empty;
        var displayLabel = GetDisplayLabel(label);

        var labelText = string.IsNullOrEmpty(displayLabel) ? text : FormattableString.Invariant($"{displayLabel}: ");
        var valueText = string.IsNullOrEmpty(displayLabel) ? string.Empty : text;

        var labelSize = UiTextBuilder.MeasureText(_fontAtlas, labelText, _textSettings, _lineHeight);
        var valueSize = UiTextBuilder.MeasureText(_fontAtlas, valueText, _textSettings, _lineHeight);
        var totalSize = new UiVector2(labelSize.X + valueSize.X, MathF.Max(labelSize.Y, valueSize.Y));
        var cursor = AdvanceCursor(totalSize);

        if (!string.IsNullOrEmpty(labelText))
        {
            _builder.AddText(
                _fontAtlas,
                labelText,
                cursor,
                _theme.TextDisabled,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        if (!string.IsNullOrEmpty(valueText))
        {
            var valuePos = new UiVector2(cursor.X + labelSize.X, cursor.Y);
            _builder.AddText(
                _fontAtlas,
                valueText,
                valuePos,
                _theme.Text,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }
    }

    public void LabelTextV(string label, string format, params object[] args)
    {
        LabelText(label, FormatInvariant(format, args));
    }

    public void LabelTextV<T0>(string label, string format, T0 arg0)
    {
        LabelText(label, FormatInvariant(format, arg0));
    }

    public void LabelTextV<T0, T1>(string label, string format, T0 arg0, T1 arg1)
    {
        LabelText(label, FormatInvariant(format, arg0, arg1));
    }

    public void Bullet()
    {
        var size = new UiVector2(_lineHeight, _lineHeight);
        var cursor = AdvanceCursor(size);
        var radius = MathF.Max(2f, _lineHeight * 0.15f);
        var center = new UiVector2(cursor.X + radius + 2f, cursor.Y + (_lineHeight * 0.5f));
        AddCircleFilled(center, radius, _theme.Text, _whiteTexture, 12);
    }

    public void BulletText(string text)
    {
        Bullet();
        SameLine();
        Text(text);
    }

    public void BulletTextV(string format, params object[] args)
    {
        Bullet();
        SameLine();
        Text(FormatInvariant(format, args));
    }

    public void BulletTextV<T0>(string format, T0 arg0)
    {
        Bullet();
        SameLine();
        Text(FormatInvariant(format, arg0));
    }

    public void BulletTextV<T0, T1>(string format, T0 arg0, T1 arg1)
    {
        Bullet();
        SameLine();
        Text(FormatInvariant(format, arg0, arg1));
    }

    public void Value(string prefix, bool value)
    {
        var display = value ? "true" : "false";
        Text(FormatValue(prefix, display));
    }

    public void Value(string prefix, int value)
    {
        Text(FormatValue(prefix, value.ToString(CultureInfo.InvariantCulture)));
    }

    public void Value(string prefix, uint value)
    {
        Text(FormatValue(prefix, value.ToString(CultureInfo.InvariantCulture)));
    }

    public void Value(string prefix, float value, string? format = null)
    {
        format = string.IsNullOrWhiteSpace(format) ? "0.000" : format;
        Text(FormatValue(prefix, value.ToString(format, CultureInfo.InvariantCulture)));
    }

    private void RenderText(string? text, UiColor color)
    {
        text ??= string.Empty;

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, text, _textSettings, _lineHeight);
        var cursor = AdvanceCursor(new UiVector2(textSize.X, textSize.Y));
        if (_tableActive && _tableColumns > 0)
        {
            var columnWidth = GetTableColumnWidth(_tableColumn);
            var align = _tableColumnAlign.Length > _tableColumn ? _tableColumnAlign[_tableColumn] : 0f;
            var padding = ButtonPaddingX;
            var available = MathF.Max(0f, columnWidth - padding * 2f);
            var alignedX = cursor.X + padding + MathF.Max(0f, available - textSize.X) * align;
            cursor = new UiVector2(alignedX, cursor.Y);
        }

        var clipRect = ResolveItemClipRect();
        var clipped = IntersectRect(clipRect, new UiRect(cursor.X, cursor.Y, textSize.X, textSize.Y));
        if (clipped.Width <= 0f || clipped.Height <= 0f)
        {
            return;
        }

        _builder.AddText(
            _fontAtlas,
            text,
            cursor,
            color,
            _fontTexture,
            clipRect,
            _textSettings,
            _lineHeight
        );
    }

    private static string FormatInvariant(string? format, object[] args)
    {
        var safeFormat = format ?? string.Empty;
        if (args.Length == 0)
        {
            return safeFormat;
        }

        return string.Format(CultureInfo.InvariantCulture, safeFormat, args);
    }

    private static string FormatInvariant<T0>(string? format, T0 arg0)
    {
        var safeFormat = format ?? string.Empty;
        return string.Format(CultureInfo.InvariantCulture, safeFormat, arg0);
    }

    private static string FormatInvariant<T0, T1>(string? format, T0 arg0, T1 arg1)
    {
        var safeFormat = format ?? string.Empty;
        return string.Format(CultureInfo.InvariantCulture, safeFormat, arg0, arg1);
    }

    private static string FormatValue(string? prefix, string value)
    {
        var label = prefix ?? string.Empty;
        return string.IsNullOrWhiteSpace(label) ? value : string.Format(CultureInfo.InvariantCulture, "{0}: {1}", label, value);
    }

    private string WrapText(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0f)
        {
            return text;
        }

        var textSpan = text.AsSpan();
        var spaceWidth = MeasureTextWidth(" ", 1);
        var builder = new System.Text.StringBuilder(text.Length + 16);
        var lineWidth = 0f;
        var first = true;

        var index = 0;
        while (index < textSpan.Length)
        {
            while (index < textSpan.Length && textSpan[index] == ' ')
            {
                index++;
            }

            if (index >= textSpan.Length)
            {
                break;
            }

            var wordStart = index;
            while (index < textSpan.Length && textSpan[index] != ' ')
            {
                index++;
            }

            var wordLength = index - wordStart;
            var wordWidth = MeasureTextWidthSpan(textSpan.Slice(wordStart, wordLength));
            if (!first && lineWidth + spaceWidth + wordWidth > maxWidth)
            {
                builder.Append('\n');
                lineWidth = 0f;
            }

            if (lineWidth > 0f)
            {
                builder.Append(' ');
                lineWidth += spaceWidth;
            }

            builder.Append(text, wordStart, wordLength);
            lineWidth += wordWidth;
            first = false;
        }

        return builder.ToString();
    }

    public bool Checkbox(string label, bool defaultValue = false)
    {
        label ??= "Checkbox";

        var id = ResolveId(label);
        var value = _state.GetBool(id, defaultValue);
        _ = Checkbox(label, ref value);
        _state.SetBool(id, value);
        return value;
    }

    public bool Checkbox(string label, ref bool value)
    {
        label ??= "Checkbox";

        var displayLabel = GetDisplayLabel(label);
        var textSize = UiTextBuilder.MeasureText(_fontAtlas, displayLabel, _textSettings, _lineHeight);
        var glyphHeight = (_fontAtlas.Ascent - _fontAtlas.Descent) * _textSettings.Scale;
        var frameHeight = GetFrameHeight();
        var checkboxSize = frameHeight;
        var height = MathF.Max(checkboxSize, glyphHeight);
        var totalSize = new UiVector2(checkboxSize + (string.IsNullOrEmpty(displayLabel) ? 0f : CheckboxSpacing + textSize.X), height);
        var cursor = AdvanceCursor(totalSize);

        var totalRect = new UiRect(cursor.X, cursor.Y, totalSize.X, totalSize.Y);
        var textTop = cursor.Y + (height - glyphHeight) * 0.5f;
        var boxY = cursor.Y + (height - checkboxSize) * 0.5f;
        if (_textSettings.PixelSnap)
        {
            boxY = MathF.Round(boxY);
        }
        var boxRect = new UiRect(cursor.X, boxY, checkboxSize, checkboxSize);
        var pressed = ButtonBehavior(label, totalRect, out var hovered, out _);
        if (pressed)
        {
            value = !value;
        }

        var boxColor = hovered ? _theme.FrameBgHovered : _theme.FrameBg;
        AddRectFilled(boxRect, boxColor, _whiteTexture);

        if (value)
        {
            var inset = 4f;
            var checkRect = new UiRect(boxRect.X + inset, boxRect.Y + inset, boxRect.Width - inset * 2f, boxRect.Height - inset * 2f);
            AddRectFilled(checkRect, _theme.CheckMark, _whiteTexture);
        }

        if (!string.IsNullOrEmpty(displayLabel))
        {
            var textPos = new UiVector2(cursor.X + checkboxSize + CheckboxSpacing, textTop);
            _builder.AddText(
                _fontAtlas,
                displayLabel,
                textPos,
                _theme.Text,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        return pressed;
    }

    public bool CheckboxFlags(string label, ref int flags, int flagsValue)
    {
        var value = (flags & flagsValue) != 0;
        var pressed = Checkbox(label, ref value);
        if (pressed)
        {
            if (value)
            {
                flags |= flagsValue;
            }
            else
            {
                flags &= ~flagsValue;
            }
        }

        return pressed;
    }

    public bool RadioButton(string label, bool active)
    {
        label ??= "Radio";

        var displayLabel = GetDisplayLabel(label);
        var textSize = UiTextBuilder.MeasureText(_fontAtlas, displayLabel, _textSettings, _lineHeight);
        var glyphHeight = (_fontAtlas.Ascent - _fontAtlas.Descent) * _textSettings.Scale;
        var frameHeight = GetFrameHeight();
        var radioSize = frameHeight;
        var height = MathF.Max(radioSize, glyphHeight);
        var totalSize = new UiVector2(radioSize + (string.IsNullOrEmpty(displayLabel) ? 0f : CheckboxSpacing + textSize.X), height);
        var cursor = AdvanceCursor(totalSize);

        var totalRect = new UiRect(cursor.X, cursor.Y, totalSize.X, totalSize.Y);
        var textTop = cursor.Y + (height - glyphHeight) * 0.5f;
        var center = new UiVector2(cursor.X + (radioSize * 0.5f), cursor.Y + (height * 0.5f));

        var pressed = ButtonBehavior(label, totalRect, out var hovered, out _);

        var outerColor = hovered ? _theme.FrameBgHovered : _theme.FrameBg;
        AddCircleFilled(center, radioSize * 0.5f, outerColor, _whiteTexture, 16);

        if (active)
        {
            AddCircleFilled(center, radioSize * 0.25f, _theme.CheckMark, _whiteTexture, 12);
        }

        if (!string.IsNullOrEmpty(displayLabel))
        {
            var textPos = new UiVector2(cursor.X + radioSize + CheckboxSpacing, textTop);
            _builder.AddText(
                _fontAtlas,
                displayLabel,
                textPos,
                _theme.Text,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        return pressed;
    }

    public bool RadioButton(string label, ref int value, int buttonValue)
    {
        var pressed = RadioButton(label, value == buttonValue);
        if (pressed)
        {
            value = buttonValue;
        }

        return pressed;
    }

    public void ProgressBar(float fraction, UiVector2 size, string? overlay = null)
    {
        fraction = Math.Clamp(fraction, 0f, 1f);

        var frameHeight = GetFrameHeight();
        var width = size.X > 0f ? size.X : 220f;
        var height = size.Y > 0f ? size.Y : frameHeight;

        var totalSize = new UiVector2(width, height);
        var cursor = AdvanceCursor(totalSize);
        var rect = new UiRect(cursor.X, cursor.Y, width, height);

        AddRectFilled(rect, _theme.FrameBg, _whiteTexture);

        var fillWidth = rect.Width * fraction;
        if (fillWidth > 0f)
        {
            var fillRect = new UiRect(rect.X, rect.Y, fillWidth, rect.Height);
            AddRectFilled(fillRect, _theme.SliderGrab, _whiteTexture);
        }

        var text = overlay ?? FormattableString.Invariant($"{fraction * 100f:0}%");
        if (!string.IsNullOrWhiteSpace(text))
        {
            var textSize = UiTextBuilder.MeasureText(_fontAtlas, text, _textSettings, _lineHeight);
            var textPos = new UiVector2(
                rect.X + (rect.Width - textSize.X) * 0.5f,
                rect.Y + (rect.Height - textSize.Y) * 0.5f
            );
            _builder.AddText(
                _fontAtlas,
                text,
                textPos,
                _theme.Text,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }
    }

    public void PlotLines(string label, float[] values, int valuesCount = -1, int valuesOffset = 0, string? overlayText = null, float scaleMin = float.NaN, float scaleMax = float.NaN, UiVector2 size = default)
    {
        PlotInternal(label, values, valuesCount, valuesOffset, overlayText, scaleMin, scaleMax, size, false);
    }

    public void PlotHistogram(string label, float[] values, int valuesCount = -1, int valuesOffset = 0, string? overlayText = null, float scaleMin = float.NaN, float scaleMax = float.NaN, UiVector2 size = default)
    {
        PlotInternal(label, values, valuesCount, valuesOffset, overlayText, scaleMin, scaleMax, size, true);
    }

    private void PlotInternal(string label, float[] values, int valuesCount, int valuesOffset, string? overlayText, float scaleMin, float scaleMax, UiVector2 size, bool histogram)
    {
        label ??= "Plot";
        ArgumentNullException.ThrowIfNull(values);

        var count = valuesCount > 0 ? Math.Min(valuesCount, values.Length) : values.Length;
        if (count <= 0)
        {
            return;
        }

        var width = size.X > 0f ? size.X : ResolveItemWidth(InputWidth);
        var height = size.Y > 0f ? size.Y : GetFrameHeight() * 2f;
        var totalSize = new UiVector2(width, height);
        var cursor = AdvanceCursor(totalSize);
        var rect = new UiRect(cursor.X, cursor.Y, width, height);

        AddRectFilled(rect, _theme.FrameBg, _whiteTexture);

        var min = float.IsNaN(scaleMin) ? float.MaxValue : scaleMin;
        var max = float.IsNaN(scaleMax) ? float.MinValue : scaleMax;
        if (float.IsNaN(scaleMin) || float.IsNaN(scaleMax))
        {
            for (var i = 0; i < count; i++)
            {
                var v = values[(valuesOffset + i) % values.Length];
                min = MathF.Min(min, v);
                max = MathF.Max(max, v);
            }
        }

        if (MathF.Abs(max - min) < float.Epsilon)
        {
            max = min + 1f;
        }

        var step = histogram ? rect.Width / count : rect.Width / Math.Max(1, count - 1);
        var prevX = rect.X;
        var prevY = rect.Y + rect.Height;

        for (var i = 0; i < count; i++)
        {
            var value = values[(valuesOffset + i) % values.Length];
            var t = Math.Clamp((value - min) / (max - min), 0f, 1f);
            var y = rect.Y + rect.Height - (t * rect.Height);
            var x = rect.X + (i * step);

            if (histogram)
            {
                var barRect = new UiRect(x, y, MathF.Max(1f, step - 1f), rect.Y + rect.Height - y);
                AddRectFilled(barRect, _theme.PlotHistogram, _whiteTexture);
            }
            else if (i > 0)
            {
                var lineMinY = MathF.Min(prevY, y);
                var lineHeight = MathF.Max(1f, MathF.Abs(prevY - y));
                var lineRect = new UiRect(prevX, lineMinY, MathF.Max(1f, step), lineHeight);
                AddRectFilled(lineRect, _theme.PlotLines, _whiteTexture);
            }

            prevX = x;
            prevY = y;
        }

        if (!string.IsNullOrWhiteSpace(label))
        {
            var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
            var textPos = new UiVector2(rect.X + 4f, rect.Y + 2f);
            _builder.AddText(
                _fontAtlas,
                label,
                textPos,
                _theme.TextDisabled,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        if (!string.IsNullOrWhiteSpace(overlayText))
        {
            var overlaySize = UiTextBuilder.MeasureText(_fontAtlas, overlayText, _textSettings, _lineHeight);
            var overlayPos = new UiVector2(rect.X + (rect.Width - overlaySize.X) * 0.5f, rect.Y + (rect.Height - overlaySize.Y) * 0.5f);
            _builder.AddText(
                _fontAtlas,
                overlayText,
                overlayPos,
                _theme.Text,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }
    }
}


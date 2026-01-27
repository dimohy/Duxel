namespace Dux.Core.Dsl;

public sealed class UiDslRenderContext
{
    public UiDslRenderContext(
        UiDslState state,
        UiFontAtlas fontAtlas,
        UiTextSettings textSettings,
        float lineHeight,
        UiTextureId fontTexture,
        UiTextureId whiteTexture,
        UiTheme theme,
        UiRect clipRect,
        UiVector2 mousePosition,
        bool leftMouseDown,
        bool leftMousePressed,
        UiVector2 displaySize,
        IUiDslEventSink? eventSink = null,
        IUiDslValueSource? valueSource = null
    )
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        FontAtlas = fontAtlas;
        TextSettings = textSettings;
        LineHeight = lineHeight;
        FontTexture = fontTexture;
        WhiteTexture = whiteTexture;
        Theme = theme;
        ClipRect = clipRect;
        MousePosition = mousePosition;
        LeftMouseDown = leftMouseDown;
        LeftMousePressed = leftMousePressed;
        DisplaySize = displaySize;
        EventSink = eventSink;
        ValueSource = valueSource;
    }

    public UiDslState State { get; }
    public UiFontAtlas FontAtlas { get; }
    public UiTextSettings TextSettings { get; }
    public float LineHeight { get; }
    public UiTextureId FontTexture { get; }
    public UiTextureId WhiteTexture { get; }
    public UiTheme Theme { get; }
    public UiRect ClipRect { get; }
    public UiVector2 MousePosition { get; }
    public bool LeftMouseDown { get; }
    public bool LeftMousePressed { get; }
    public UiVector2 DisplaySize { get; }
    public IUiDslEventSink? EventSink { get; }
    public IUiDslValueSource? ValueSource { get; }
}

public sealed class UiDslImmediateEmitter : IUiDslEmitter
{
    private readonly UiDslRenderContext _ctx;
    private readonly UiDrawListBuilder _builder;
    private readonly Stack<UiLayoutState> _layouts = new();

    private const float WindowPadding = 8f;
    private const float ItemSpacingX = 8f;
    private const float ItemSpacingY = 4f;
    private const float RowSpacing = ItemSpacingY;
    private const float ButtonPaddingX = 4f;
    private const float ButtonPaddingY = 3f;
    private const float CheckboxSpacing = 4f;

    public UiDslImmediateEmitter(UiDslRenderContext context)
    {
        _ctx = context;
        _builder = new UiDrawListBuilder(context.ClipRect);
        _layouts.Push(new UiLayoutState(new UiVector2(0, 0), false, 0f));
    }

    public UiPooledList<UiDrawList> BuildDrawLists() => _builder.Build();

    public void BeginNode(string name, IReadOnlyList<string> args)
    {
        switch (name)
        {
            case "Window":
                BeginWindow(args);
                break;
            case "Row":
                BeginRow();
                break;
            case "Button":
                EmitButton(args);
                break;
            case "Checkbox":
                EmitCheckbox(args);
                break;
            case "Text":
                EmitText(args);
                break;
            default:
                throw new InvalidOperationException($"Unknown DSL node: {name}.");
        }
    }

    public void EndNode(string name)
    {
        switch (name)
        {
            case "Window":
                EndWindow();
                break;
            case "Row":
                EndRow();
                break;
            case "Button":
            case "Checkbox":
            case "Text":
                break;
            default:
                throw new InvalidOperationException($"Unknown DSL node: {name}.");
        }
    }

    private void BeginWindow(IReadOnlyList<string> args)
    {
        var title = args.Count > 0 ? args[0] : "Window";
        var rect = new UiRect(20, 20, _ctx.DisplaySize.X - 40, _ctx.DisplaySize.Y - 40);
        _builder.AddRectFilled(rect, _ctx.Theme.WindowBg, _ctx.WhiteTexture);

        _builder.AddText(
            _ctx.FontAtlas,
            title,
            new UiVector2(rect.X + WindowPadding, rect.Y + WindowPadding),
            _ctx.Theme.Text,
            _ctx.FontTexture,
            rect,
            _ctx.TextSettings,
            _ctx.LineHeight
        );

        var cursor = new UiVector2(rect.X + WindowPadding, rect.Y + WindowPadding + _ctx.LineHeight + ItemSpacingY);
        _layouts.Push(new UiLayoutState(cursor, false, 0f));
    }

    private void EndWindow()
    {
        if (_layouts.Count <= 1)
        {
            throw new InvalidOperationException("Layout stack underflow.");
        }
        _layouts.Pop();
    }

    private void BeginRow()
    {
        var current = _layouts.Peek();
        _layouts.Push(new UiLayoutState(current.Cursor, true, 0f));
    }

    private void EndRow()
    {
        var row = _layouts.Pop();
        var parent = _layouts.Pop();
        var nextCursor = new UiVector2(parent.Cursor.X, row.Cursor.Y + row.RowMaxHeight + RowSpacing);
        parent = parent with { Cursor = nextCursor };
        _layouts.Push(parent);
    }

    private void EmitButton(IReadOnlyList<string> args)
    {
        if (args.Count < 2)
        {
            throw new InvalidOperationException("Button requires id and label.");
        }

        var id = args[0];
        var label = args[1];
        var textSize = UiTextBuilder.MeasureText(_ctx.FontAtlas, label, _ctx.TextSettings, _ctx.LineHeight);
        var size = new UiVector2(textSize.X + ButtonPaddingX * 2f, textSize.Y + ButtonPaddingY * 2f);
        var cursor = AdvanceCursor(size);
        var rect = new UiRect(cursor.X, cursor.Y, size.X, size.Y);

        var hovered = IsHovering(rect);
        var held = hovered && _ctx.LeftMouseDown;
        var pressed = hovered && _ctx.LeftMousePressed;
        var color = held ? _ctx.Theme.ButtonActive : hovered ? _ctx.Theme.ButtonHovered : _ctx.Theme.Button;

        _builder.AddRectFilled(rect, color, _ctx.WhiteTexture);

        if (pressed)
        {
            _ctx.EventSink?.OnButton(id);
        }

        var textPos = new UiVector2(rect.X + ButtonPaddingX, rect.Y + ButtonPaddingY);
        _builder.AddText(
            _ctx.FontAtlas,
            label,
            textPos,
            _ctx.Theme.Text,
            _ctx.FontTexture,
            _ctx.ClipRect,
            _ctx.TextSettings,
            _ctx.LineHeight
        );
    }

    private void EmitText(IReadOnlyList<string> args)
    {
        var text = args.Count > 0 ? args[0] : string.Empty;
        var textSize = UiTextBuilder.MeasureText(_ctx.FontAtlas, text, _ctx.TextSettings, _ctx.LineHeight);
        var cursor = AdvanceCursor(new UiVector2(textSize.X, textSize.Y));

        _builder.AddText(
            _ctx.FontAtlas,
            text,
            cursor,
            _ctx.Theme.Text,
            _ctx.FontTexture,
            _ctx.ClipRect,
            _ctx.TextSettings,
            _ctx.LineHeight
        );
    }

    private void EmitCheckbox(IReadOnlyList<string> args)
    {
        if (args.Count < 2)
        {
            throw new InvalidOperationException("Checkbox requires id and label.");
        }

        var id = args[0];
        var label = args[1];
        var defaultValue = args.Count > 2 && bool.TryParse(args[2], out var parsed) ? parsed : false;
        var value = _ctx.ValueSource?.TryGetBool(id, out var bound) == true ? bound : _ctx.State.GetBool(id, defaultValue);

        var textSize = UiTextBuilder.MeasureText(_ctx.FontAtlas, label, _ctx.TextSettings, _ctx.LineHeight);
        var glyphHeight = (_ctx.FontAtlas.Ascent - _ctx.FontAtlas.Descent) * _ctx.TextSettings.Scale;
        var frameHeight = _ctx.LineHeight + (ButtonPaddingY * 2f);
        var checkboxSize = frameHeight;
        var height = MathF.Max(checkboxSize, glyphHeight);
        var totalSize = new UiVector2(checkboxSize + CheckboxSpacing + textSize.X, height);
        var cursor = AdvanceCursor(totalSize);

        var totalRect = new UiRect(cursor.X, cursor.Y, totalSize.X, totalSize.Y);
        var textTop = cursor.Y + (height - glyphHeight) * 0.5f;
        var boxY = cursor.Y + (height - checkboxSize) * 0.5f;
        if (_ctx.TextSettings.PixelSnap)
        {
            boxY = MathF.Round(boxY);
        }
        var boxRect = new UiRect(cursor.X, boxY, checkboxSize, checkboxSize);
        var hovered = IsHovering(totalRect);
        var pressed = hovered && _ctx.LeftMousePressed;
        if (pressed)
        {
            value = !value;
            if (_ctx.ValueSource is not null)
            {
                _ctx.ValueSource.SetBool(id, value);
            }
            else
            {
                _ctx.State.SetBool(id, value);
            }
            _ctx.EventSink?.OnCheckbox(id, value);
        }

        var boxColor = hovered ? _ctx.Theme.FrameBgHovered : _ctx.Theme.FrameBg;
        _builder.AddRectFilled(boxRect, boxColor, _ctx.WhiteTexture);

        if (value)
        {
            var inset = 4f;
            var checkRect = new UiRect(boxRect.X + inset, boxRect.Y + inset, boxRect.Width - inset * 2f, boxRect.Height - inset * 2f);
            _builder.AddRectFilled(checkRect, _ctx.Theme.CheckMark, _ctx.WhiteTexture);
        }

        var textPos = new UiVector2(cursor.X + checkboxSize + CheckboxSpacing, textTop);
        _builder.AddText(
            _ctx.FontAtlas,
            label,
            textPos,
            _ctx.Theme.Text,
            _ctx.FontTexture,
            _ctx.ClipRect,
            _ctx.TextSettings,
            _ctx.LineHeight
        );
    }

    private UiVector2 AdvanceCursor(UiVector2 size)
    {
        var current = _layouts.Pop();
        var cursor = current.Cursor;
        UiVector2 next;
        if (current.IsRow)
        {
            next = new UiVector2(cursor.X + size.X + ItemSpacingX, cursor.Y);
            current = current with
            {
                Cursor = next,
                RowMaxHeight = MathF.Max(current.RowMaxHeight, size.Y)
            };
        }
        else
        {
            next = new UiVector2(cursor.X, cursor.Y + size.Y + ItemSpacingY);
            current = current with { Cursor = next };
        }

        _layouts.Push(current);
        return cursor;
    }

    private bool IsHovering(UiRect rect)
    {
        var pos = _ctx.MousePosition;
        return pos.X >= rect.X && pos.X <= rect.X + rect.Width && pos.Y >= rect.Y && pos.Y <= rect.Y + rect.Height;
    }

    private readonly record struct UiLayoutState(UiVector2 Cursor, bool IsRow, float RowMaxHeight);
}

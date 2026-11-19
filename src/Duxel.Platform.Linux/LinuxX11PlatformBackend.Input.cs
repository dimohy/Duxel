using Duxel.Core;

namespace Duxel.Platform.Linux;

public sealed unsafe partial class LinuxX11PlatformBackend
{
    private sealed class InputState : IInputBackend
    {
        internal readonly List<UiKeyEvent> Keys = [];
        internal readonly List<UiCharEvent> Characters = [];
        private readonly List<UiInputState> _orderedInputs = [];
        internal UiVector2 Position = new(-1, -1);
        internal KeyModifiers Modifiers;
        internal UiKeyRepeatSettings Repeat;
        private bool _left, _right, _middle, _leftPressed, _leftReleased, _rightPressed, _rightReleased, _middlePressed, _middleReleased;
        private float _wheel, _horizontal;
        private ulong _held;
        internal bool IsWindowInactive = true;
        internal bool HasPressedButton => _left || _right || _middle;

        public InputSnapshot Snapshot => new(Position, _left, _right, _middle, _leftPressed, _leftReleased,
            _rightPressed, _rightReleased, _middlePressed, _middleReleased, _wheel, _horizontal, Keys, Characters, Repeat, Modifiers)
            { IsWindowInactive = IsWindowInactive, OrderedInputs = _orderedInputs };

        internal void BeginFrame()
        {
            Keys.Clear();
            Characters.Clear();
            _orderedInputs.Clear();
            _leftPressed = _leftReleased = _rightPressed = _rightReleased = _middlePressed = _middleReleased = false;
            _wheel = _horizontal = 0;
        }

        internal readonly record struct EventStart(UiVector2 Position, KeyModifiers Modifiers, bool Left, bool Right,
            bool Middle, float Wheel, float Horizontal, int KeyCount, int CharacterCount);

        internal EventStart BeginEvent() =>
            new(Position, Modifiers, _left, _right, _middle, _wheel, _horizontal, Keys.Count, Characters.Count);

        internal void EndEvent(in EventStart before, float coordinateScale)
        {
            if (before.Position == Position && before.Modifiers == Modifiers && before.Left == _left
                && before.Right == _right && before.Middle == _middle && before.Wheel == _wheel
                && before.Horizontal == _horizontal && before.KeyCount == Keys.Count && before.CharacterCount == Characters.Count)
                return;

            _orderedInputs.Add(new(Position, _left, _right, !before.Left && _left, before.Left && !_left,
                !before.Right && _right, before.Right && !_right, _wheel - before.Wheel, _horizontal - before.Horizontal,
                CopyEventTail(Keys, before.KeyCount), CopyEventTail(Characters, before.CharacterCount), Repeat, Modifiers)
            {
                MiddleMouseDown = _middle,
                MiddleMousePressed = !before.Middle && _middle,
                MiddleMouseReleased = before.Middle && !_middle,
                CoordinateScale = coordinateScale
            });
        }

        private static T[] CopyEventTail<T>(List<T> events, int start)
        {
            if (start == events.Count) return [];
            var values = new T[events.Count - start];
            events.CopyTo(start, values, 0, values.Length);
            return values;
        }

        internal void Button(uint button, bool down)
        {
            static void Set(bool value, ref bool held, ref bool pressed, ref bool released)
            {
                if (value == held)
                    return;
                held = value;
                if (value)
                    pressed = true;
                else
                    released = true;
            }
            switch (button)
            {
                case 1:
                    Set(down, ref _left, ref _leftPressed, ref _leftReleased);
                    break;
                case 2:
                    Set(down, ref _middle, ref _middlePressed, ref _middleReleased);
                    break;
                case 3:
                    Set(down, ref _right, ref _rightPressed, ref _rightReleased);
                    break;
                case 4 when down:
                    _wheel++;
                    break;
                case 5 when down:
                    _wheel--;
                    break;
                case 6 when down:
                    _horizontal++;
                    break;
                case 7 when down:
                    _horizontal--;
                    break;
            }
        }

        internal static KeyModifiers ModifiersFrom(uint state) =>
            ((state & 1) != 0 ? KeyModifiers.Shift : 0)
            | ((state & 4) != 0 ? KeyModifiers.Ctrl : 0)
            | ((state & 8) != 0 ? KeyModifiers.Alt : 0)
            | ((state & 64) != 0 ? KeyModifiers.Super : 0);

        internal void Key(nuint symbol, bool down, uint state)
        {
            uint modifier = symbol switch
            {
                0xffe1 or 0xffe2 => 1,
                0xffe3 or 0xffe4 => 4,
                0xffe9 or 0xffea => 8,
                0xffeb or 0xffec => 64,
                _ => 0
            };
            Modifiers = ModifiersFrom(down ? state | modifier : state & ~modifier);
            var key = symbol switch
            {
                0xff09 or 0xfe20 => UiKey.Tab,
                0xff51 => UiKey.LeftArrow,
                0xff53 => UiKey.RightArrow,
                0xff52 => UiKey.UpArrow,
                0xff54 => UiKey.DownArrow,
                0xff55 => UiKey.PageUp,
                0xff56 => UiKey.PageDown,
                0xff50 => UiKey.Home,
                0xff57 => UiKey.End,
                0xff63 => UiKey.Insert,
                0xffff => UiKey.Delete,
                0xff08 => UiKey.Backspace,
                0x20 => UiKey.Space,
                0xff0d or 0xff8d => UiKey.Enter,
                0xff1b => UiKey.Escape,
                0xff67 => UiKey.ContextMenu,
                >= 0x61 and <= 0x7a => (UiKey)((int)UiKey.A + (int)(symbol - 0x61)),
                >= 0x41 and <= 0x5a => (UiKey)((int)UiKey.A + (int)(symbol - 0x41)),
                >= 0xffbe and <= 0xffc9 => (UiKey)((int)UiKey.F1 + (int)(symbol - 0xffbe)),
                _ => UiKey.None
            };
            if (key == UiKey.None)
                return;
            Keys.Add(new(key, down, Modifiers));
            var bit = 1UL << (int)key;
            _held = down ? _held | bit : _held & ~bit;
        }

        internal void LoseFocus()
        {
            Position = new(-1, -1);
            _left = _right = _middle = false;
            _leftPressed = _rightPressed = _middlePressed = false;
            Modifiers = default;
            for (var key = UiKey.Tab; key <= UiKey.ContextMenu; key++)
                if ((_held & (1UL << (int)key)) != 0)
                    Keys.Add(new(key, false, default));
            _held = 0;
        }
    }
}

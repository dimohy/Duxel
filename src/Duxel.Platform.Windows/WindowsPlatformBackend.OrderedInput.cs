using Duxel.Core;

namespace Duxel.Platform.Windows;

public sealed partial class WindowsPlatformBackend
{
    private float GetInputCoordinateScale()
    {
        var scale = ContentScale;
        OverrideValidationInputCoordinateScale(ref scale);
        return scale;
    }
    partial void OverrideValidationInputCoordinateScale(ref float scale);
    private sealed partial class WindowsInputBackend
    {
        private readonly List<UiInputState> _orderedInputs = [];
        private WindowsCharacterDecoder _imeGlyphDecoder;
        private IUiInputDispatch? _pendingCharacterDispatch;

        private static IReadOnlyList<T> CaptureEvents<T>(List<T> events, int start)
        {
            var count = events.Count - start;
            if (count == 0)
                return [];
            var result = new T[count];
            events.CopyTo(start, result, 0, count);
            return result;
        }

        private UiInputState CurrentInput() => new(_mousePosition, _leftDown, _rightDown, false, false, false, false,
            0, 0, [], [], keyRepeatSettings, GetModifiers())
        {
            MiddleMouseDown = _middleDown,
            CoordinateScale = coordinateScale(),
            EventTimeSeconds = EventTimeSeconds
        };

        internal void CaptureIme(uint message, uint character, IUiInputDispatch dispatch, string? text)
        {
            if (message == WmChar)
            {
                _pendingCharacterDispatch = dispatch;
                return;
            }
            IReadOnlyList<UiCharEvent> characters = [];
            if (message == 0x0286 && _imeGlyphDecoder.TryDecode(character, out var scalar))
                characters = [new(scalar)];
            else if (text is not null)
            {
                var decoder = new WindowsCharacterDecoder();
                var glyphs = new List<UiCharEvent>();
                foreach (var unit in text)
                    if (decoder.TryDecode(unit, out var codepoint))
                        glyphs.Add(new(codepoint));
                characters = glyphs;
            }
            _orderedInputs.Add(CurrentInput() with
            {
                Dispatch = dispatch,
                CharEvents = characters
            });
        }

        internal void CapturePointer(UiVector2 position, bool down, double? eventTime = null)
        {
            var previous = _leftDown;
            _mousePosition = position;
            _leftDown = down;
            _orderedInputs.Add(CurrentInput() with
            {
                LeftMousePressed = !previous && down,
                LeftMouseReleased = previous && !down,
                EventTimeSeconds = eventTime ?? EventTimeSeconds
            });
        }
    }
}

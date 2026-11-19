using System;

namespace Duxel.Core;

public sealed partial class UiState
{
    private UiKeyRepeatSource _keyRepeatSource;
    public UiKeyRepeatSource KeyRepeatSource => _keyRepeatSource;

    internal static void ValidateKeyRepeatSource(UiKeyRepeatSource source)
    {
        if (source is not (UiKeyRepeatSource.Engine or UiKeyRepeatSource.Platform))
            throw new ArgumentOutOfRangeException(nameof(source), "Key repetition must be owned by the engine or platform.");
    }

    internal void SetKeyRepeatSource(UiKeyRepeatSource source)
    {
        if (_keyRepeatSource == source)
            return;
        _keyRepeatSource = source;
        _repeatNext.Clear();
    }
}

namespace Duxel.Core;

public enum UiInlineTextWeight
{
    Regular, Semibold, Bold
}

/// <summary>Literal text with an explicit face; no Markdown or markup is interpreted.</summary>
public readonly record struct UiInlineTextRun(string Text, UiInlineTextWeight Weight = UiInlineTextWeight.Regular);

/// <summary>One Body1 text root with retained, mixed-weight wrapping and passive accessibility.</summary>
[UiComponent("InlineText", UiComponentExposure.Infrastructure)]
public class UiInlineText : UiControlWidget
{
    private readonly UiInlineTextSource _source;
    private readonly UiVisualTextVariants _layouts = new();
    private bool _wrap;
    private UiColor? _color;
    private UiItemHorizontalAlign _horizontalAlignment;
    private string? _accessibilityIdentity;

    public UiInlineText(IReadOnlyList<UiInlineTextRun> runs, bool wrap = true, UiColor? color = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Parent)
        : this(runs, wrap, color, identity, UiStyledTextFlags.None) { }

    internal UiInlineText(IReadOnlyList<UiInlineTextRun> runs, bool wrap, UiColor? color,
        UiWidgetIdentity identity, UiStyledTextFlags baseFlags) : base(identity)
    {
        _source = new(baseFlags);
        _source.Replace(runs);
        _wrap = wrap;
        _color = color;
    }

    /// <summary>The immutable snapshot owned by this widget. Replacing it invalidates layout.</summary>
    public IReadOnlyList<UiInlineTextRun> Runs
    {
        get
        {
            ThrowIfDisposed();
            return _source.InlineRuns;
        }
        set
        {
            ThrowIfDisposed();
            ThrowIfBuilding();
            if (_source.Replace(value))
                RequestRebuild();
        }
    }

    public string Text
    {
        get
        {
            ThrowIfDisposed();
            return _source.Source;
        }
    }
    public bool Wrap
    {
        get => _wrap;
        set
        {
            ThrowIfDisposed();
            if (_wrap == value)
                return;
            ThrowIfBuilding();
            _wrap = value;
            RequestRebuild();
        }
    }

    /// <summary>Sets paragraph bidi direction. HorizontalAlignment controls physical line alignment independently.</summary>
    public bool RightToLeft
    {
        get
        {
            ThrowIfDisposed();
            return _source.RightToLeft;
        }
        set
        {
            ThrowIfDisposed();
            if (_source.RightToLeft == value)
                return;
            ThrowIfBuilding();
            _source.SetRightToLeft(value);
            RequestRebuild();
        }
    }

    public UiItemHorizontalAlign HorizontalAlignment
    {
        get => _horizontalAlignment;
        set
        {
            ThrowIfDisposed();
            if ((uint)value > (uint)UiItemHorizontalAlign.Right)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_horizontalAlignment == value)
                return;
            ThrowIfBuilding();
            _horizontalAlignment = value;
            RequestRebuild();
        }
    }

    public UiColor? Color
    {
        get => _color;
        set
        {
            ThrowIfDisposed();
            if (_color == value)
                return;
            ThrowIfBuilding();
            _color = value;
            RequestRebuild();
        }
    }

    internal UiVisualTextLines VisualLines => _layouts.Latest;
    internal int LayoutBuilds => _layouts.Builds;
    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context)
        => context.MeasureInlineText(_source, _layouts.ForWidth(_wrap ? context.InlineTextLayoutWidth : 0), _wrap);
    protected override void RenderControl(UiImmediateContext ui)
        => ui.RenderInlineText(_source, _layouts.ForWidth(_wrap ? ui.InlineTextLayoutWidth : 0), _wrap, _color,
            ref _accessibilityIdentity, alignment : HorizontalAlignment);

    private protected override void ReleaseOwnedResources()
    {
        _layouts.Clear();
        _source.Clear();
        _accessibilityIdentity = null;
        base.ReleaseOwnedResources();
    }
}

internal sealed class UiInlineTextSource(UiStyledTextFlags baseFlags = UiStyledTextFlags.None) : UiStyledTextSource
{
    internal UiStyledTextStyle BaseStyle => new(Flags : baseFlags);
    internal bool RightToLeft
    {
        get; private set;
    }

    internal void SetRightToLeft(bool value)
    {
        RightToLeft = value;
        _revision++;
        ReleaseProjection();
    }
    private IReadOnlyList<UiInlineTextRun> _inlineRuns = Array.AsReadOnly(Array.Empty<UiInlineTextRun>());
    private UiStyledSourceRun[] _runs = [];
    private string _source = string.Empty;
    private long _revision;
    internal IReadOnlyList<UiInlineTextRun> InlineRuns => _inlineRuns;
    internal override long Revision => _revision;
    internal override string Source => _source;
    internal override IReadOnlyList<UiStyledSourceRun> Runs => _runs;
    internal override UiSourceTextRange? ActiveBlock => null;
    internal override UiStyledTextSource Composition
        => throw new NotSupportedException("Literal inline text is not an editable composition source.");

    internal bool Replace(IReadOnlyList<UiInlineTextRun> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var snapshot = new UiInlineTextRun[runs.Count];
        var length = 0;
        for (var i = 0; i < snapshot.Length; i++)
        {
            var run = runs[i];
            ArgumentNullException.ThrowIfNull(run.Text);
            if ((uint)run.Weight > (uint)UiInlineTextWeight.Bold)
                throw new ArgumentOutOfRangeException(nameof(runs), "Inline text requires a supported face.");
            snapshot[i] = run;
            length = checked(length + run.Text.Length);
        }
        if (_inlineRuns.SequenceEqual(snapshot))
            return false;
        var source = new System.Text.StringBuilder(length);
        var styled = new List<UiStyledSourceRun>(snapshot.Length);
        foreach (var run in snapshot)
        {
            if (run.Text.Length == 0)
                continue;
            var flags = run.Weight switch
            {
                UiInlineTextWeight.Regular => UiStyledTextFlags.None,
                UiInlineTextWeight.Semibold => UiStyledTextFlags.Semibold,
                UiInlineTextWeight.Bold => UiStyledTextFlags.Bold,
                _ => throw new InvalidOperationException("Inline face was not validated.")
            };
            var style = new UiStyledTextStyle(Flags : flags | baseFlags);
            if (styled.Count > 0 && styled[^1].Style == style)
            {
                var previous = styled[^1];
                styled[^1] = previous with
                {
                    Source = previous.Source with
                    {
                        Length = checked(previous.Source.Length + run.Text.Length)
                    }
                };
            }
            else
                styled.Add(new(new(source.Length, run.Text.Length), string.Empty, style, Syntax: false));
            source.Append(run.Text);
        }
        var literal = source.ToString();
        var boundaries = System.Globalization.StringInfo.ParseCombiningCharacters(literal);
        for (var i = 0; i < styled.Count; i++)
        {
            var run = styled[i];
            if (i > 0 && Array.BinarySearch(boundaries, run.Source.Start) < 0)
                throw new ArgumentException("An inline face change cannot split a Unicode text element.", nameof(runs));
            styled[i] = run with
            {
                Display = literal.Substring(run.Source.Start, run.Source.Length)
            };
        }
        _inlineRuns = Array.AsReadOnly(snapshot);
        _source = literal;
        _runs = styled.ToArray();
        _revision++;
        ReleaseProjection();
        return true;
    }

    internal override void Prepare(string source, int activeIndex, bool preserveActiveBlock = false)
    {
        if (!string.Equals(source, _source, StringComparison.Ordinal) || activeIndex >= 0 || preserveActiveBlock)
            throw new InvalidOperationException("Literal inline layout must use its owned source without an editing block.");
    }

    internal void Clear()
    {
        _inlineRuns = Array.AsReadOnly(Array.Empty<UiInlineTextRun>());
        _source = string.Empty;
        _runs = [];
        _revision++;
        ReleaseProjection();
    }
}

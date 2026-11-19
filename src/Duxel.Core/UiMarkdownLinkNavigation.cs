namespace Duxel.Core;

internal sealed class UiMarkdownLinkEntry(string id, UiMarkdownLink link, Uri? destination)
{
    internal readonly string Id = id;
    internal readonly UiMarkdownLink Link = link;
    internal readonly Uri? Destination = destination;
    internal readonly List<UiRect> Fragments = [];
    internal UiRect Bounds;
    internal UiButtonInteraction Interaction;
}

internal sealed partial class UiMarkdownLinkOwner
{
    private WeakReference<UiImmediateContext>? _navigationContext;
    private string? _navigationRoot;
    private long _navigationRevision = -1;
    private int _layoutVersion = -1;
    private Uri? _baseUri;
    private long _navigationGeneration;
    internal UiMarkdownLinkEntry[] Entries { get; private set; } = [];
    internal string? RevealedFocus;

    internal void BindNavigation(UiImmediateContext ui, string root, UiMarkdownDocument document, Uri? baseUri)
    {
        if (_navigationContext is not null && _navigationContext.TryGetTarget(out var previous)
            && ReferenceEquals(previous, ui) && _navigationRoot == root && _navigationRevision == document.Revision && _baseUri == baseUri)
            return;
        ReleaseNavigation();
        _pressed = null;
        _frame = -1;
        _navigationGeneration = checked(_navigationGeneration + 1);
        _navigationContext = new(ui);
        _navigationRoot = root;
        _navigationRevision = document.Revision;
        _baseUri = baseUri;
        Entries = new UiMarkdownLinkEntry[document.Links.Count];
        for (var i = 0; i < Entries.Length; i++)
        {
            var link = document.Links[i];
            Uri.TryCreate(link.Destination, UriKind.RelativeOrAbsolute, out var destination);
            if (destination is { IsAbsoluteUri: false } && baseUri is not null)
                destination = new(baseUri, destination);
            Entries[i] = new(root + "##markdown-link/" + _navigationGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "/" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), link, destination);
        }
    }

    internal bool PrepareLayout(UiVisualTextLines visual)
    {
        if (_layoutVersion == visual.Version)
            return false;
        var firstLine = 0;
        foreach (var entry in Entries)
        {
            entry.Fragments.Clear();
            entry.Bounds = default;
            while (firstLine < visual.Count - 1 && visual[firstLine].End <= entry.Link.Label.Start)
                firstLine++;
            for (var line = firstLine; line < visual.Count && visual[line].Start < entry.Link.Label.End; line++)
            {
                var start = Math.Max(visual[line].Start, entry.Link.Label.Start);
                var end = Math.Min(visual[line].End, entry.Link.Label.End);
                if (end <= start)
                    continue;
                var left = visual.X(start, line);
                var right = visual.X(end, line);
                if (right <= left)
                    continue;
                var rect = new UiRect(left, visual.Y(line), right - left, visual.LineHeight(line));
                entry.Bounds = entry.Fragments.Count == 0 ? rect : Union(entry.Bounds, rect);
                entry.Fragments.Add(rect);
            }
        }
        _layoutVersion = visual.Version;
        return true;
    }

    private static UiRect Union(UiRect left, UiRect right)
    {
        var x = MathF.Min(left.X, right.X);
        var y = MathF.Min(left.Y, right.Y);
        return new(x, y, MathF.Max(left.X + left.Width, right.X + right.Width) - x,
            MathF.Max(left.Y + left.Height, right.Y + right.Height) - y);
    }

    private void ReleaseNavigation()
    {
        if (_navigationContext is not null && _navigationContext.TryGetTarget(out var previous))
            previous.ReleaseMarkdownLinkFocus(Entries);
        Entries = [];
        RevealedFocus = null;
        _navigationContext = null;
        _navigationRoot = null;
        _navigationRevision = -1;
        _layoutVersion = -1;
        _baseUri = null;
    }
}

public sealed partial class UiImmediateContext
{
    internal void ReleaseMarkdownLinkFocus(UiMarkdownLinkEntry[] entries)
    {
        foreach (var entry in entries)
        {
            if (_state.FocusedId == entry.Id)
                _state.FocusedId = null;
            if (_ownedControlFocus == entry.Id)
                _ownedControlFocus = null;
            if (_spacePressOwner == entry.Id)
                CancelControlSpace();
        }
    }

    private UiMultilineEditResult PrepareMarkdownLinkNavigation(string root, UiMarkdownDocument document,
        UiVisualTextLines visual, UiMultilineEditResult edit, UiMarkdownLinkOwner owner, UiMarkdownOptions options)
    {
        owner.BindNavigation(this, root, document, options.BaseUri);
        var layoutChanged = owner.PrepareLayout(visual);
        var content = edit.VisualContent!.Value;
        var widget = _accessibilityWidget;
        _accessibilityWidget = null;
        try
        {
            foreach (var entry in owner.Entries)
            {
                BeginDisabled(entry.Destination is null || entry.Fragments.Count == 0);
                try
                {
                    entry.Interaction = ProcessControlKeyboard<LinkControlKeys>(entry.Id, content, default);
                }
                finally { EndDisabled(); }
            }
            if (IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier
                && IntersectRect(CurrentClipRect, content) is { Width: > 0, Height: > 0 }
                && (layoutChanged || owner.RevealedFocus != _state.FocusedId))
                foreach (var entry in owner.Entries)
                    if (_state.FocusedId == entry.Id && entry.Fragments.Count > 0)
                    {
                        var target = entry.Fragments[0];
                        static float Reveal(float offset, float start, float extent, float visible, float maximum) =>
                            Math.Clamp(extent > visible || start < offset ? start : start + extent > offset + visible
                                ? start + extent - visible : offset, 0, maximum);
                        edit = edit with
                        {
                            ScrollX = Reveal(edit.ScrollX, target.X, target.Width, content.Width, edit.MaximumScrollX),
                            ScrollY = Reveal(edit.ScrollY, target.Y, target.Height, content.Height, edit.MaximumScrollY)
                        };
                        RequestScrollReveal(MarkdownLinkBounds(target, edit));
                        break;
                    }
            owner.RevealedFocus = _state.FocusedId;
        }
        finally { _accessibilityWidget = widget; }
        return edit;
    }

    private static UiRect MarkdownLinkBounds(UiRect source, UiMultilineEditResult edit)
    {
        var content = edit.VisualContent!.Value;
        return source with
        {
            X = content.X + source.X - edit.ScrollX,
            Y = content.Y + source.Y - edit.ScrollY
        };
    }
}

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void RenderMessageBar<TPresentation>(string label, UiWidget body, UiWidget? actions,
        UiWidget? containerAction, UiMessageBarOptions options, UiMessageBarLayout layout)
        where TPresentation : IUiMessageBarPresentation
    {
        EnsureNotMeasuring();
        var context = new UiControlContext(this);
        var origin = AllocateLayoutBox(layout.Size);
        var root = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
        UiRect Absolute(UiRect rect) => rect with
        {
            X = origin.X + rect.X,
            Y = origin.Y + rect.Y
        };
        using var clip = context.Clip(root);
        TPresentation.Paint(context, root, Absolute(layout.Icon), options);
        var parent = _accessibilityParent;
        var id = ResolveId("##message-bar");
        var foreground = TPresentation.Foreground(context, options);
        _accessibilityParent = id;
        var bodyStart = _accessibility?.StagedCount ?? 0;
        var bodyEnd = bodyStart;
        if (foreground is { } color)
            PushStyleColor(UiStyleColor.Text, color);
        try
        {
            RenderLayoutChild(body, Absolute(layout.Body), root);
            bodyEnd = _accessibility?.StagedCount ?? bodyStart;
            if (actions is not null)
                RenderLayoutChild(actions, Absolute(layout.Actions), root);
            if (containerAction is not null)
                RenderLayoutChild(containerAction, Absolute(layout.ContainerAction), root);
        }
        finally
        {
            if (foreground.HasValue)
                PopStyleColor();
            _accessibilityParent = parent;
        }
        ExposeAccessibility(id, parent, UiAccessibilityRole.Group, label, root,
            readOnly: true, focusable: false, valuePattern: false, literalLabel: true);
        if (_accessibility is { } tree)
        {
            if (body is UiMessageBarBodyWidget { Title: { } title })
            {
                var titleId = title.LastAccessibilityId
                    ?? throw new InvalidOperationException("An owned MessageBar title must publish its actual label in the current frame.");
                tree.ConnectLabel(id, titleId);
            }
            var priority = options.AnnouncementPriority
                ?? (options.Intent == UiMessageBarIntent.Info ? UiAnnouncementPriority.Polite : UiAnnouncementPriority.Assertive);
            if (tree.PrepareAutomaticAnnouncement(id, options.DisableAnnouncement ? null : priority))
            {
                var message = options.AnnouncementText;
                if (message is null)
                {
                    var bodyText = tree.PreparedLeafText(bodyStart, bodyEnd);
                    // The last staged node is this aggregate root, not action content.
                    var actionsText = tree.PreparedLeafText(bodyEnd, tree.StagedCount - 1);
                    message = bodyText.Length == 0 ? actionsText : actionsText.Length == 0 ? bodyText : bodyText + "," + actionsText;
                }
                if (!string.IsNullOrWhiteSpace(message))
                    tree.QueueAnnouncement(new(message, id + "/mount", priority));
            }
        }
        FinishLayoutBox(origin, layout.Size);
    }
}

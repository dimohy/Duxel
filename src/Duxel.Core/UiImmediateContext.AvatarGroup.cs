namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal string? RenderAvatarGroup<TBehavior, TPresentation>(string id, string label, UiFluentAvatar[] avatars,
        UiAvatarGroupLayoutResult layout, UiMenuInputState menu)
        where TBehavior : IUiMenuBehavior
        where TPresentation : IUiAvatarGroupPresentation
    {
        EnsureNotMeasuring();
        var context = new UiControlContext(this);
        var origin = AllocateLayoutBox(layout.Size);
        var rootBounds = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
        var root = ResolveId(id);
        var overflowId = ResolveCachedIdentifier(id, "##avatar-overflow", true);
        var parent = _accessibilityParent;
        var ownerWidget = _accessibilityWidget;
        ExposeAccessibility(root, parent, UiAccessibilityRole.AvatarGroup, label, rootBounds, focusable: false, valuePattern: false);
        var pie = layout.Options.Layout == UiAvatarGroupLayout.Pie;
        var edge = new UiAvatarOptions(layout.Options.Size).PixelSize;
        var pieBody = new UiRect(origin.X, origin.Y, edge, edge);
        var trigger = layout.OverflowBounds with
        {
            X = origin.X + layout.OverflowBounds.X,
            Y = origin.Y + layout.OverflowBounds.Y
        };
        UiButtonInteraction interaction = default;
        try
        {
            _accessibilityWidget = null;
            _accessibilityParent = root;
            using (context.Clip(rootBounds))
            {
                for (var i = 0; i < layout.Inline.Length; i++)
                {
                    var index = layout.Inline[i];
                    var avatar = avatars[index];
                    var rect = layout.Bounds[i] with
                    {
                        X = origin.X + layout.Bounds[i].X,
                        Y = origin.Y + layout.Bounds[i].Y
                    };
                    var visible = rect;
                    if (pie)
                    {
                        var divider = TPresentation.Divider(layout.Options) * .5f;
                        var count = layout.Inline.Length;
                        var left = i == 0;
                        var half = count == 3 && !left;
                        var width = half ? edge * .5f : edge;
                        var x = count == 1 ? 0 : left ? -edge * .25f : count == 2 ? edge * .25f : edge * .5f;
                        var y = count == 3 && i == 2 ? edge * .5f : 0;
                        var clipX = count == 1 ? 0 : left ? 0 : edge * .5f + divider;
                        var clipY = count == 3 && i == 2 ? edge * .5f + divider : 0;
                        var clipWidth = count == 1 ? edge : edge * .5f - divider;
                        var clipHeight = half ? edge * .5f - divider : edge;
                        if (layout.Options.RightToLeft)
                        {
                            x = edge - x - width;
                            clipX = edge - clipX - clipWidth;
                        }
                        rect = new(pieBody.X + x, pieBody.Y + y, width, width);
                        visible = new(pieBody.X + clipX, pieBody.Y + clipY, clipWidth, clipHeight);
                        using var circle = context.RoundedClip(pieBody, edge * .5f);
                        using var slice = context.Clip(visible);
                        TPresentation.PaintAvatar(context, rect, avatar.PreparePaintContent(), avatar.Options, true);
                    }
                    else
                    {
                        if (layout.Options.Layout == UiAvatarGroupLayout.Stack)
                        {
                            var body = layout.Bodies[i] with
                            {
                                X = origin.X + layout.Bodies[i].X,
                                Y = origin.Y + layout.Bodies[i].Y
                            };
                            TPresentation.PaintSeparator(context, body, layout.Options);
                        }
                        TPresentation.PaintAvatar(context, rect, avatar.PreparePaintContent(), avatar.Options, false);
                    }
                    var clipped = IntersectRect(CurrentClipRect, visible);
                    _accessibility?.Add(new(ResolveCachedIdentifier(root, layout.Identifiers[index], false), root,
                        UiAccessibilityRole.Image, avatar.ResolvedAccessibilityName, clipped, IsControlEnabled,
                        clipped.Width <= 0 || clipped.Height <= 0, false, Focusable: false, ValuePattern: false), visible);
                }
                if (layout.HasOverflow)
                {
                    if (layout.Overflow.Length > 0)
                    {
                        using var pointerClip = new LayoutPointerBlockScope(this, !ContainsRoundedRect(trigger, trigger.Width * .5f, _mousePosition));
                        interaction = ProcessMenuTrigger(overflowId, layout.OverflowName, layout.Overflow, menu, trigger,
                            activation : TBehavior.TriggerActivation);
                    }
                    else
                    {
                        CancelMenuPopup(menu);
                        ExposeAccessibility(overflowId, root, UiAccessibilityRole.Label, layout.OverflowName,
                            trigger, focusable : false, valuePattern : false);
                    }
                    if (!pie && layout.Options.Layout == UiAvatarGroupLayout.Stack)
                        TPresentation.PaintSeparator(context, trigger, layout.Options);
                    var paintOptions = pie && layout.Inline.Length == 0
                        ? layout.Options with { Layout = UiAvatarGroupLayout.Spread }
                        : layout.Options;
                    TPresentation.PaintOverflow(context, trigger, layout.Count, interaction, paintOptions);
                }
                else
                    CancelMenuPopup(menu);
            }
            return layout.Overflow.Length == 0 ? null : RenderMenuPopup<UiAvatarGroupMenuPresentation>(overflowId,
                layout.Options.OverflowLabel, layout.Overflow, new UiMenuOptions(220, edge + 8, 8,
                    layout.Options.RightToLeft, MaximumWidth: 300)
                {
                    MaximumHeight = 220
                }, menu, trigger, interaction, optionActivation: TBehavior.OptionActivation);
        }
        finally
        {
            _accessibilityParent = parent;
            _accessibilityWidget = ownerWidget;
            FinishLayoutBox(origin, layout.Size);
        }
    }
}

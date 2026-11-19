using Duxel.Core;

internal static class PersonaRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, UiContext context)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var persona = Fluent.Persona(new UiPersonaContent("정세일", SecondaryText: "Developer", TertiaryText: "Duxel", QuaternaryText: "Seoul"));
        persona.Avatar.Options = new(Color: UiAvatarColor.Brand, Presence: new());
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(paint.ClipRect, new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(16, 16));
            persona.Render(ui);
        }
        UiAccessibilityNode Node()
        {
            var id = tree.Root.First ?? throw new InvalidOperationException("Persona provider missing");
            if (!tree.TryGet(id, out var node))
                throw new InvalidOperationException("Persona provider unavailable");
            return node.Node;
        }
        foreach (var size in Enum.GetValues<UiPersonaSize>())
        {
            foreach (var position in Enum.GetValues<UiPersonaTextPosition>())
            {
                foreach (var alignment in Enum.GetValues<UiPersonaTextAlignment>())
                {
                    foreach (var presence in new[] { false, true })
                    {
                        foreach (var rtl in new[] { false, true })
                        {
                            persona.Options = new(size, position, alignment, PresenceOnly: presence, RightToLeft: rtl);
                            var name = $"{size}-{position}-{alignment}-{presence}-{rtl}";
                            var pixels = capture(name, Draw);
                            check(pixels.AsSpan().SequenceEqual(capture(name + "-replay", Draw)), "GPU Persona unchanged media/text replays : " + name);
                            check(Node() is { Role: UiAccessibilityRole.Group, Focusable: false, SupportsInvoke: false }
                                && Node().Name == "정세일, Developer, Duxel, Seoul, available",
                                "GPU Persona publishes actual composite information without fake input");
                        }
                    }
                }
            }
        }
        persona.Options = new();
        var original = capture("source-initials", Draw);
        persona.Avatar.Source = new(new(1, 1, [230, 40, 60, 255]));
        var image = capture("source-image", Draw);
        check(!original.AsSpan().SequenceEqual(image), "GPU Persona source image replaces actual initials pixels");
        persona.Content = persona.Content with
        {
            SecondaryText = "Changed information"
        };
        var changedText = capture("updated-secondary-text", Draw);
        check(!image.AsSpan().SequenceEqual(changedText), "GPU Persona optional text changes actual pixels while its avatar image stays fixed");
        persona.Options = persona.Options with
        {
            PresenceOnly = true
        };
        var presenceOnly = capture("presence-only", Draw);
        persona.Avatar.Source = null;
        check(presenceOnly.AsSpan().SequenceEqual(capture("presence-only-image-ignored", Draw)), "GPU Persona presence-only mode does not paint hidden avatar content");
        persona.Options = new(UiPersonaSize.ExtraSmall, PresenceOnly: true);
        persona.Content = new("정세일");
        persona.Avatar.Options = persona.Avatar.Options with
        {
            Presence = new(Size: UiBadgeSize.ExtraLarge)
        };
        var oversizedPresence = capture("explicit-presence-size", Draw);
        check(Node().Bounds.Height >= 28 && oversizedPresence.AsSpan().SequenceEqual(capture("explicit-presence-size-replay", Draw)),
            "GPU Persona explicit presence size fits its primary row without clipped overflow");
        persona.Avatar.Options = persona.Avatar.Options with
        {
            Presence = new()
        };
        persona.Options = new(UiPersonaSize.Huge, TextAlignment: UiPersonaTextAlignment.Center);
        persona.Avatar.Options = persona.Avatar.Options with
        {
            IsInactive = true
        };
        var inactive = capture("inactive", Draw);
        check(inactive.AsSpan().SequenceEqual(capture("inactive-replay", Draw)), "GPU Persona reuses the existing inactive avatar composite");
        persona.Content = persona.Content with
        {
            Name = "김민수"
        };
        var changed = capture("updated-name", Draw);
        check(!changed.AsSpan().SequenceEqual(inactive) && Node().Name.StartsWith("김민수", StringComparison.Ordinal), "GPU Persona name update changes both actual pixels and native information");
        persona.Avatar.Options = persona.Avatar.Options with
        {
            IsInactive = false,
            ShowActivityRing = true,
            ShowActivityShadow = true
        };
        var shadow = capture("active-shadow", Draw);
        check(shadow.AsSpan().SequenceEqual(capture("active-shadow-replay", Draw)), "GPU Persona releases inactive cache and paints existing activity/shadow resources");
        persona.Options = persona.Options with
        {
            ShowMedia = false
        };
        var textOnly = capture("text-only", Draw);
        check(!textOnly.AsSpan().SequenceEqual(shadow) && context.State.NextScheduledFrameTimeSeconds is null, "GPU Persona text-only mode has real visible changes without periodic frames");
    }
}

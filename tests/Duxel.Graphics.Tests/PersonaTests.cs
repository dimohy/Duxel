using Duxel.Core;

internal static class PersonaTests
{
    internal static void Run(Action<bool, string> check)
    {
        check(Fluent.ComponentNames.Contains("Persona")
            && !Fluent.MissingComponentNames.Contains("Persona"), "Persona connects one new official top-level component");
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var persona = new UiPersona<TracePresentation, TraceAvatar>(new UiPersonaContent("정##세일", SecondaryText: "Developer", TertiaryText: "Duxel", QuaternaryText: "Seoul"));
        persona.Avatar.Options = new(Presence: new());
        UiWidget current = persona;
        void Frame()
        {
            context.NewFrame(new(0, new(640, 480), new(1, 1)), new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default), new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); current.Render(ui); }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement Node()
        {
            var id = tree.Root.First ?? throw new InvalidOperationException("Persona accessibility root missing");
            if (!tree.TryGet(id, out var e))
                throw new InvalidOperationException("Persona provider missing");
            return e;
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
                            Frame();
                            var layout = TracePresentation.Last;
                            var expected = size switch
                            {
                                UiPersonaSize.ExtraSmall => 20,
                                UiPersonaSize.Small => 28,
                                UiPersonaSize.Medium => 32,
                                UiPersonaSize.Large => 36,
                                UiPersonaSize.ExtraLarge => 40,
                                _ => 56
                            };
                            check(presence || TraceAvatar.Options.PixelSize == expected && TraceAvatar.Content.Initials == "정",
                                "Persona resolves pinned avatar metrics and Korean initials : " + persona.Options);
                            if (position == UiPersonaTextPosition.Below)
                                check(layout.TextBounds[0].Y >= layout.Media.Y + layout.Media.Height && layout.Media.X >= 0,
                                    "Persona below layout reserves media and text as separate rows");
                            else
                            {
                                var textAfter = (position == UiPersonaTextPosition.After) != rtl;
                                check(textAfter ? layout.TextBounds[0].X >= layout.Media.X + layout.Media.Width
                                    : layout.Media.X >= layout.TextBounds[0].X + layout.TextBounds[0].Width,
                                    "Persona logical text position mirrors actual allocations in RTL");
                            }
                            check(Node().Node is { Role: UiAccessibilityRole.Group, Focusable: false, SupportsInvoke: false }
                                && Node().Node.Name == "정##세일, Developer, Duxel, Seoul, available",
                                "Persona exposes complete literal text/status without invented actions or duplicate avatar providers");
                        }
                    }
                }
            }
        }
        persona.Options = new();
        Frame();
        var measured = TracePresentation.Measures;
        var runtime = Node().RuntimeId;
        Frame();
        check(TracePresentation.Measures == measured && Node().RuntimeId == runtime, "Persona reuses unchanged measured slots and stable provider identity");
        persona.Avatar.Options = persona.Avatar.Options with
        {
            ShowActivityRing = true
        };
        Frame();
        check(TracePresentation.Measures > measured, "Persona observes externally changed owned-avatar layout options");
        persona.Avatar.ActivityText = "활동 중";
        Frame();
        check(Node().Node.Name.EndsWith(", 활동 중", StringComparison.Ordinal), "Persona exposes localized visible avatar activity");
        persona.Avatar.Options = persona.Avatar.Options with
        {
            IsInactive = true
        };
        Frame();
        check(!Node().Node.Name.Contains("활동 중", StringComparison.Ordinal), "Persona inactive media does not announce hidden activity");
        persona.Avatar.Options = persona.Avatar.Options with
        {
            IsInactive = false
        };
        using var nested = new UiPersona<TracePresentation, TraceAvatar>("Nested person");
        using var parent = Dux.Column(new(Spacing: 8), nested);
        current = parent;
        Frame();
        var parentMeasures = TracePresentation.Measures;
        nested.Avatar.Options = nested.Avatar.Options with
        {
            ShowActivityRing = true
        };
        Frame();
        check(TracePresentation.Measures > parentMeasures, "Owned avatar changes invalidate the retained Persona ancestor chain");
        using var edited = new UiPersona<TracePresentation, TraceAvatar>("Old content");
        using var editButton = Fluent.Button("Edit Persona content", () => edited.Content = new("Updated during render"));
        using var editParent = Dux.Column(new(Spacing: 8), editButton, edited);
        current = editParent;
        Frame();
        var editId = tree.Root.First ?? throw new InvalidOperationException("Persona edit button missing");
        if (!tree.TryGet(editId, out var editEntry))
            throw new InvalidOperationException("Persona edit provider missing");
        check(tree.RequestAction(editEntry.Node.Id, editEntry.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
            "Persona content edit uses actual queued native button activation");
        Frame();
        check(TracePresentation.Painted.Text[0].GetLine(0).Text == "Updated during render",
            "Persona updates painted text in the same frame as a preceding button changes its measured content");
        current = persona;
        persona.Content = new("Ada Lovelace");
        persona.Options = new(UiPersonaSize.ExtraSmall, PresenceOnly: true);
        Frame();
        check(TracePresentation.Last.Text[0].Size.Y == 16 && TracePresentation.Last.Media.Width == 6,
            "Single-line extra-small presence-only Persona uses Caption1 and Tiny presence");
        persona.Avatar.Options = persona.Avatar.Options with
        {
            Presence = new(Size: UiBadgeSize.ExtraLarge)
        };
        Frame();
        check(TracePresentation.Last.Media.Width == 28 && TracePresentation.Last.Media.Y >= 0
            && TracePresentation.Last.Size.Y >= 28, "Persona explicit presence size overrides defaults and expands the primary row without clipping");
        persona.Avatar.Options = persona.Avatar.Options with
        {
            Presence = null
        };
        Frame();
        check(TracePresentation.Last.Media.Width == 0 && TracePresentation.Last.TextBounds[0].X == 0,
            "Presence-only Persona without presence has no phantom media gap");
        persona.Avatar.Options = new(Presence: new(UiPresenceStatus.Busy));
        persona.Avatar.PresenceText = "통화 중";
        Frame();
        check(Node().Node.Name == "Ada Lovelace, 통화 중", "Persona respects caller-localized presence text");
        persona.Content = persona.Content with
        {
            PrimaryText = "다른 표시 이름",
            SecondaryText = "Engineer"
        };
        persona.Options = new(ShowMedia: false);
        Frame();
        check(Node().Node.Name == "다른 표시 이름, Engineer" && TracePresentation.Last.Media.Width == 0,
            "Persona primary override and text-only mode follow actual displayed information");
        using var narrow = Dux.SizedBox(persona, 8);
        current = narrow;
        Frame();
        check(Node().Node.Bounds.Width == 8 && float.IsFinite(TracePresentation.Last.Size.X), "Persona accepts a narrow parent allocation without overflowing its published bounds");
        check(context.State.NextScheduledFrameTimeSeconds is null, "Static Persona schedules no periodic render");
        var rejected = false;
        try
        {
            using var invalid = Fluent.Persona(new UiPersonaContent("bad\nname"));
        }
        catch (ArgumentException) { rejected = true; }
        check(rejected, "Persona rejects multiline slot content before building resources");
        var avatar = persona.Avatar;
        persona.Dispose();
        var disposed = false;
        try
        {
            avatar.Name = "after dispose";
        }
        catch (ObjectDisposedException) { disposed = true; }
        check(disposed, "Persona releases its owned avatar on disposal");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
    private readonly struct TraceAvatar : IUiAvatarPresentation
    {
        internal static UiAvatarOptions Options;
        internal static UiAvatarContent Content;
        public static bool CanRetainMeasurement => true;
        public static UiVector2 Measure(UiControlContext context, UiAvatarOptions options) => new(options.PixelSize + (options.ShowActivityRing ? 8 : 0), options.PixelSize);
        public static void Paint(UiControlContext context, UiRect bounds, UiAvatarContent content, UiAvatarOptions options)
        {
            Content = content;
            Options = options;
        }
    }
    private readonly struct TracePresentation : IUiPersonaPresentation
    {
        internal static int Measures;
        internal static UiPersonaLayout Last;
        internal static UiPersonaLayout Painted;
        public static bool CanRetainMeasurement => true;
        public static UiAvatarSize AvatarSize(UiPersonaOptions options) => UiFluentPersonaPresentation.AvatarSize(options);
        public static UiPresenceBadgeOptions Presence(UiAvatarPresenceOptions presence, UiPersonaOptions options) => UiFluentPersonaPresentation.Presence(presence, options);
        public static UiVector2 MeasurePresence(UiPresenceBadgeOptions presence) => UiFluentPersonaPresentation.MeasurePresence(presence);
        public static UiPersonaLayout Measure(UiControlContext context, UiPersonaContent content, UiPersonaOptions options, UiVector2 media)
        {
            Measures++;
            return Last = UiFluentPersonaPresentation.Measure(context, content, options, media);
        }
        public static void PaintText(UiControlContext context, UiPersonaLayout layout, UiPersonaOptions options, UiVector2 origin)
        {
            Painted = layout;
            UiFluentPersonaPresentation.PaintText(context, layout, options, origin);
        }
        public static void PaintPresence(UiControlContext context, UiRect bounds, UiPresenceBadgeOptions presence) => UiFluentPersonaPresentation.PaintPresence(context, bounds, presence);
    }
}

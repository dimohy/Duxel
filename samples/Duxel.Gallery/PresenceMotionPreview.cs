using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class PresenceMotionPreview : UiWidget
{
    private int _nextItem = 3;

    protected override IUiView Build(UiBuildContext context) => context.Child("presence", () =>
    {
        var presence = new UiPresenceGroupWidget([Item(1), Item(2)], new(Gap : 8, AnimateEnter : true), keys : ["1", "2"]);
        var reducedMotion = new UiBinding<bool>(() => presence.Options.ReducedMotion, value =>
        {
            presence.Options = presence.Options with { ReducedMotion = value };
        });
        return Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Subtitle2(GalleryText.Get("Enter, exit, and reflow")),
            Dux.Wrap(new(Spacing : 8, RunSpacing : 6),
                Fluent.Button(GalleryText.Get("Add motion item"), () =>
                {
                    if (presence.Items.Length >= 5) return;
                    var id = _nextItem++;
                    presence.SetItems([..presence.Items, new(id.ToString(System.Globalization.CultureInfo.InvariantCulture), Item(id))]);
                }),
                Fluent.Button(GalleryText.Get("Remove motion item"), () =>
                {
                    if (!presence.Items.IsEmpty) presence.SetItems(presence.Items[..^1]);
                }),
                Fluent.Switch(GalleryText.Get("Reduced motion"), reducedMotion),
                Fluent.Button(GalleryText.Get("Reverse order"), () =>
                {
                    var items = presence.Items.ToArray();
                    Array.Reverse(items);
                    presence.SetItems(items);
                })),
            presence);
    });

    private static UiWidget Item(int index) => Fluent.Button(string.Format(GalleryText.Get("Motion item {0}"), index));
}

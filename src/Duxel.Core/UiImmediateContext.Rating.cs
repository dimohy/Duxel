namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    public UiRadioGroupInteraction RatingInput(string id, UiRect bounds, UiRatingOptions options,
        ReadOnlySpan<UiRadioOption> items, ReadOnlySpan<UiRadioOptionLayout> layouts,
        string? selected, UiRadioGroupInputState state)
        => _ui.ProcessRadioGroup(id, bounds, items, layouts, selected, state, UiButtonActivation.Release, options.RightToLeft);
}

public sealed partial class UiImmediateContext
{
    internal void Rating<TBehavior, TPresentation>(string id, string label, UiRatingContent content, UiRatingOptions options, float value)
        where TBehavior : IUiRatingBehavior where TPresentation : IUiRatingPresentation
    {
        var context = new UiControlContext(this);
        var size = context.ConstrainToLayout(TPresentation.Measure(context, options));
        var position = AdvanceCursor(size);
        var bounds = new UiRect(position.X, position.Y, size.X, size.Y);
        TPresentation.Arrange(context, bounds, options, content.Layouts);
        var interaction = TBehavior.Process(context, id, bounds, options, content.Items, content.Layouts,
            content.Selected(value, options.Step), content.Input);
        interaction = ApplyRadioAccessibility(id, label, bounds, content.Items, content.Layouts, content.Input, interaction, selectionRequired: false);
        if (!IsPresentationPass)
            return;
        var selected = content.Input.Value is { } token ? content.ValueOf(token, options.Step) : value;
        float? preview = interaction.HoveredIndex >= 0 ? (interaction.HoveredIndex + 1) * options.Step : null;
        using var clip = context.Clip(bounds);
        TPresentation.Paint(context, bounds, options, selected, preview, interaction);
    }
}

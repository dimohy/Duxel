using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ValidatedNameExample : UiWidget
{
    private readonly UiState<string> _name;
    private readonly UiFluentField _field;
    internal ValidatedNameExample(UiState<string> name)
    {
        _name = name;
        _field = Fluent.Field(Fluent.Input(GalleryText.Get("Name"), name.Binding, 256, 460));
        _field.Width = 460;
        OwnChild("field", _field);
    }
    protected override IUiView Build(UiBuildContext context)
    {
        var name = context.Watch(_name);
        _field.Options = new()
        {
            Label = GalleryText.Get("Name"),
            Required = true,
            Hint = GalleryText.Get("Select the label to focus its input."),
            ValidationMessage = name.Length < 2 ? GalleryText.Get("Enter at least two characters.") : GalleryText.Get("Name entered."),
            ValidationState = name.Length < 2 ? UiFieldValidationState.Error : UiFieldValidationState.Success
        };
        return context.Child("field", () => _field);
    }
}

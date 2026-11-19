using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ValidatedNameExample : UiWidget
{
    private readonly UiState<string> _name;
    private readonly UiFluentField _field;
    internal ValidatedNameExample(UiState<string> name)
    {
        _name = name;
        _field = Fluent.Field(Fluent.Input("이름", name.Binding, 256, 460));
        _field.Width = 460;
        OwnChild("field", _field);
    }
    protected override IUiView Build(UiBuildContext context)
    {
        var name = context.Watch(_name);
        _field.Options = new()
        {
            Label = "이름",
            Required = true,
            Hint = "라벨을 눌러 입력창에 포커스를 옮겨 보세요.",
            ValidationMessage = name.Length < 2 ? "두 글자 이상 입력하세요." : "입력되었습니다.",
            ValidationState = name.Length < 2 ? UiFieldValidationState.Error : UiFieldValidationState.Success
        };
        return context.Child("field", () => _field);
    }
}

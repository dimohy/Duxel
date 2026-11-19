internal static class LegacyCpuContractTests
{
    internal static void Run(Action<bool, string> check)
    {
        (string Name, Action<Action<bool, string>> Run)[] suites =
        [
            ("Text", TextWidgetTests.Run),
            ("Badge", BadgeWidgetTests.Run),
            ("Button lifetime", ButtonLifetimeTests.Run),
            ("Ownership", ControlOwnershipTests.Run),
            ("Typography", ButtonTypographyTests.Run),
            ("Button icons", ButtonIconTests.Run),
            ("Button appearance", ButtonAppearanceInputTests.Run),
            ("Divider", DividerWidgetTests.Run),
            ("Slider", SliderWidgetTests.Run),
            ("Internal windows", WindowInteractionTests.Run),
            ("Panel", PanelTests.Run),
            ("DirectText", DirectTextLayoutTests.Run),
            ("Checkbox", CheckboxWidgetTests.Run),
            ("RadioGroup", RadioGroupWidgetTests.Run),
            ("Switch", SwitchWidgetTests.Run),
            ("Link", LinkWidgetTests.Run),
            ("Field", FieldWidgetTests.Run),
            ("Measurement", WidgetMeasurementTests.Run),
            ("Input kernel", InputKernelTests.Run),
            ("Fluent input", FluentInputTests.Run),
            ("Input adornments", InputAdornmentTests.Run),
            ("Input children", InputChildrenTests.Run)
        ];
        foreach (var suite in suites)
            suite.Run((condition, name) => check(condition, suite.Name + " : " + name));
    }
}

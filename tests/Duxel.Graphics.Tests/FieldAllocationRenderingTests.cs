using System.Numerics;
using Duxel.Core;

internal static class FieldAllocationRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, bool vector)
    {
        Compare<UiFluentSmallFieldSize, UiVerticalFieldLayout>("small-vertical", capture, check, vector);
        Compare<UiFluentMediumFieldSize, UiVerticalFieldLayout>("medium-vertical", capture, check, vector);
        Compare<UiFluentLargeFieldSize, UiVerticalFieldLayout>("large-vertical", capture, check, vector);
        Compare<UiFluentSmallFieldSize, UiHorizontalFieldLayout>("small-horizontal", capture, check, vector);
        Compare<UiFluentMediumFieldSize, UiHorizontalFieldLayout>("medium-horizontal", capture, check, vector);
        Compare<UiFluentLargeFieldSize, UiHorizontalFieldLayout>("large-horizontal", capture, check, vector);
    }

    internal static void VerifyRetained(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, bool vector)
    {
        using var first = Fluent.Field(Fluent.Input("First", new UiState<string>("Value").Binding, 64));
        using var second = Fluent.Field(Fluent.Input("Second", new UiState<string>("Value").Binding, 64));
        first.Options = new() { Label = "FPS : 0" };
        second.Options = new() { Label = "Second", Hint = "NF : 0" };
        using var firstBox = Dux.SizedBox(first, 100, 120);
        using var secondBox = Dux.SizedBox(second, 100, 120);
        long version = 0;
        var builds = 0;
        var expectedBuilds = 0;
        for (var frame = 0; frame < 20; frame++)
        {
            var changed = false;
            if (frame % 16 == 0)
            {
                first.Options = first.Options with { Label = "FPS : " + frame / 16 };
                changed = true;
            }
            if (frame % 3 == 0)
            {
                second.Options = second.Options with { Hint = "NF : " + frame / 3 };
                changed = true;
            }
            if (frame == 8)
            {
                firstBox.Options = firstBox.Options with { Width = 24 };
                changed = true;
            }
            if (frame == 12)
            {
                firstBox.Options = firstBox.Options with { Height = 16 };
                changed = true;
            }
            if (changed)
            {
                version++;
                expectedBuilds++;
            }
            var options = new UiLayerOptions(true, 1, default) { ContentVersion = version };
            if (vector)
                options = options with { Scene3D = new(Matrix4x4.Identity, false) };
            var actual = capture($"field-retained-{vector}-{frame}", ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (ui.BeginLayer("allocated-field-retained-" + vector, options))
                {
                    builds++;
                    ui.SetCursorScreenPos(new(10, 10));
                    firstBox.Render(ui);
                    ui.SetCursorScreenPos(new(160, 10));
                    secondBox.Render(ui);
                }
                ui.EndLayer();
            });
            using var firstReference = Fluent.Input("First", new UiState<string>("Value").Binding, 64);
            using var secondReference = Fluent.Input("Second", new UiState<string>("Value").Binding, 64);
            var expected = capture($"field-retained-{vector}-{frame}-reference", ui => Scene(ui, vector, "retained-reference-" + frame, () =>
            {
                Reference<UiFluentMediumFieldSize, UiVerticalFieldLayout>(ui, firstReference, first.Options,
                    new(10, 10, firstBox.Options.Width!.Value, firstBox.Options.Height!.Value));
                Reference<UiFluentMediumFieldSize, UiVerticalFieldLayout>(ui, secondReference, second.Options, new(160, 10, 100, 120));
            }));
            check(actual.AsSpan().SequenceEqual(expected), $"independently paced Field labels/descriptions and clip changes remain exact across acquired images; vector = {vector}, frame = {frame}");
        }
        check(builds == expectedBuilds, "unchanged allocated Field layers retain their content, vector = " + vector);
    }

    private static void Compare<TSize, TOrientation>(string name, Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, bool vector) where TSize : IUiFieldSize where TOrientation : IUiFieldOrientation
    {
        var index = 0;
        foreach (var options in new UiFieldOptions[]
        {
            default,
            new() { Label = "Several label words", Required = true, Hint = "Hint text", ValidationMessage = "Check this value" },
            new() { Label = "Label", ValidationMessage = "", ValidationState = UiFieldValidationState.Warning, Disabled = true }
        })
        foreach (var size in new[] { new UiVector2(80, 120), new UiVector2(160, 16), new UiVector2(0, 0) })
        {
            var actualInput = Fluent.Input("Input", new UiState<string>("Value").Binding, 64, width : 220);
            using var field = index % 2 == 0
                ? new UiField<UiLabelFieldBehavior, UiFluentFieldPresentation<TSize, TOrientation>>(actualInput, new(220, 32), options)
                : new UiField<UiLabelFieldBehavior, UiFluentFieldPresentation<TSize, TOrientation>>(actualInput) { Width = 220, Options = options };
            using var box = Dux.SizedBox(field, size.X, size.Y);
            var expectedInput = Fluent.Input("Input", new UiState<string>("Value").Binding, 64, width : 220);
            expectedInput.Options = new() { Invalid = options.EffectiveValidationState == UiFieldValidationState.Error };
            using var expectedOwner = expectedInput;
            var key = $"field-allocated-{name}-{vector}-{index++}";
            var actual = capture(key, ui => Scene(ui, vector, key, () => box.Render(ui)));
            var expected = capture(key + "-reference", ui => Scene(ui, vector, key + "-reference", () =>
                Reference<TSize, TOrientation>(ui, expectedInput, options, new(20, 20, size.X, size.Y))));
            check(actual.AsSpan().SequenceEqual(expected), key + " matches independently allocated control/description rows, label wrapping and root clipping");
        }
    }

    private static void Reference<TSize, TOrientation>(UiImmediateContext ui, UiWidget child, UiFieldOptions options, UiRect root)
        where TSize : IUiFieldSize where TOrientation : IUiFieldOrientation
    {
        var context = new UiControlContext(ui);
        var width = TOrientation.Horizontal ? root.Width * .67f : root.Width;
        var natural = UiFluentFieldPresentation<TSize, TOrientation>.Measure(context, options, new(width, 32));
        var descriptions = options.ValidationMessage is null ? 0
            : 2 + MathF.Max(natural.ValidationText.Size.Y, options.EffectiveValidationState == UiFieldValidationState.None ? 0 : 16);
        if (options.Hint is not null)
            descriptions += 2 + natural.HintText.Size.Y;
        var height = MathF.Max(0, root.Height - natural.Control.Y - descriptions);
        var afterControl = natural.Control.Y + height;
        var validationY = afterControl + 2;
        var hintY = afterControl + (options.ValidationMessage is null ? 0
            : 2 + MathF.Max(natural.ValidationText.Size.Y, options.EffectiveValidationState == UiFieldValidationState.None ? 0 : 16)) + 2;
        var layout = natural with
        {
            Size = new(root.Width, root.Height),
            Control = natural.Control with { Height = height },
            Validation = natural.Validation with { Y = validationY },
            ValidationIcon = natural.ValidationIcon with { Y = validationY + 3 },
            Hint = natural.Hint with { Y = hintY }
        };
        using var clip = context.Clip(root);
        UiFluentFieldPresentation<TSize, TOrientation>.Paint(context, options, layout, new(root.X, root.Y));
        ui.SetCursorScreenPos(new(root.X + layout.Control.X, root.Y + layout.Control.Y));
        using var control = Dux.SizedBox(child, width, height);
        ui.BeginDisabled(options.Disabled);
        try
        {
            control.Render(ui);
        }
        finally
        {
            ui.EndDisabled();
        }
    }

    private static void Scene(UiImmediateContext ui, bool vector, string id, Action draw)
    {
        ui.EnableRootViewportContentLayout(contentPadding : 0);
        if (vector)
            ui.BeginLayer("field-allocation-vector-" + id, new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
        try
        {
            ui.SetCursorScreenPos(new(20, 20));
            draw();
        }
        finally
        {
            if (vector)
                ui.EndLayer();
        }
    }
}

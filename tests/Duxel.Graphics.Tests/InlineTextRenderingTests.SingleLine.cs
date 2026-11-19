using System.Security.Cryptography;
using Duxel.Core;

internal static partial class InlineTextRenderingTests
{
    private static void CompareSingleLineInput(string output, PresentationReuseTests.Fixture reference,
        PresentationReuseTests.Fixture candidate, List<TextAccessibilityPixelPair> pairs)
    {
        foreach (var text in new[] { "abc אבגד 123", "AV e\u0301 office" })
            foreach (var dark in new[] { false, true })
                foreach (var samples in new[] { 1, 4 })
                {
                    reference.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                    candidate.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                    reference.Renderer.SetMsaaSamples(samples);
                    candidate.Renderer.SetMsaaSamples(samples);
                    var state = new UiState<string>(text);
                    using var input = new UiInput<UiStandardTextInputBehavior, NativeInputPresentation>("Native input pixels", state.Binding, 256, 280);
                    void Begin(UiImmediateContext ui)
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(11, 33, 71), default, 0, 0);
                    }
                    void Candidate(UiImmediateContext ui)
                    {
                        Begin(ui);
                        ui.SetCursorScreenPos(new(20, 20));
                        input.Render(ui);
                    }
                    candidate.Capture(Candidate);
                    var id = NativeInputPresentation.Last.Id;
                    candidate.Context.State.ActiveId = candidate.Context.State.FocusedId = id;
                    candidate.Context.State.SetCursor(id, 7);
                    candidate.Context.State.SetSelection(id, new(2, 7));
                    var probes = DirectWriteLayoutOracle.GeometryProbes(text);
                    var range = probes.Ranges.Single(probe => probe.Start == 2 && probe.End == 7);
                    var caret = DirectWriteLayoutOracle.CaretPositions("Segoe UI", text, 14)[7];
                    var expected = reference.Capture(ui =>
                    {
                        Begin(ui);
                        var paint = new UiControlContext(ui);
                        using var clip = paint.Clip(new(20, 20, 280, 32));
                        foreach (var fragment in range.Fragments)
                            paint.Frame(new(20 + fragment.Left, 20, fragment.Width, 20), new(40, 100, 180), default, 0, 0);
                        PaintNativeParagraphReference(reference.Context, ui, "", text, UiInlineTextWeight.Regular, 280, false,
                            new(255, 255, 255), origin: new(20, 20), combineRasters: true);
                        paint.Frame(new(MathF.Round(20 + caret), 21, 1, 18), new(255, 170, 0), default, 0, 0);
                    });
                    for (var repeat = 0; repeat < 2; repeat++)
                    {
                        candidate.Context.State.ResetCaretBlink();
                        var actual = candidate.Capture(Candidate);
                        var name = $"native-input-{(text[0] == 'a' ? "bidi" : "combining")}-{dark}-{samples}-{repeat}";
                        var equal = expected.AsSpan().SequenceEqual(actual);
                        pairs.Add(new(name, Convert.ToHexString(SHA256.HashData(expected)), Convert.ToHexString(SHA256.HashData(actual)), equal));
                        if (!equal)
                        {
                            File.WriteAllBytes(Path.Combine(output, name + ".reference.rgba"), expected);
                            File.WriteAllBytes(Path.Combine(output, name + ".candidate.rgba"), actual);
                        }
                    }
                }
    }

    private readonly struct NativeInputPresentation : IUiInputPresentation
    {
        internal static UiInputEditResult Last;
        public static UiControlTypographyScope BeginText(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        public static UiInputLayout Measure(UiControlContext context, string label, float width) =>
            new(new(width, 32), new(0, 0, width, 32), new(0, 0, width, 20), default, 20);
        public static void Paint(UiControlContext context, string label, in UiInputLayout layout, in UiInputEditResult edit)
        {
            Last = edit;
            context.PaintEditableText(layout, edit, new(new(255, 255, 255), new(40, 100, 180), new(255, 170, 0))
            {
                Typography = true
            });
        }
    }
}

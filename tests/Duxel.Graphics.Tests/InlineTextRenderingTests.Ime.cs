using System.Security.Cryptography;
using Duxel.Core;

internal static partial class InlineTextRenderingTests
{
    private static void CompareSingleLineComposition(string output, PresentationReuseTests.Fixture reference,
        PresentationReuseTests.Fixture candidate, List<TextAccessibilityPixelPair> pairs)
    {
        for (var variant = 0; variant < SingleLineImeTests.Cases.Length; variant++)
            foreach (var dark in new[] { false, true })
                foreach (var samples in new[] { 1, 4 })
                {
                    var item = SingleLineImeTests.Cases[variant];
                    reference.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                    candidate.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                    reference.Renderer.SetMsaaSamples(samples);
                    candidate.Renderer.SetMsaaSamples(samples);
                    var ime = new SingleLineImeTests.Handler { Preedit = item.Preedit };
                    candidate.Context.SetImeHandler(ime);
                    var state = new UiState<string>(item.Source);
                    using var input = new UiInput<UiStandardTextInputBehavior, NativeInputPresentation>("IME input pixels", state.Binding, 256, item.Width);
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
                    candidate.Context.State.SetCursor(id, item.Start);
                    candidate.Context.State.SetSelection(id, new(item.Start, item.Start));
                    var probes = DirectWriteLayoutOracle.GeometryProbes(item.Display, item.Configure);
                    var range = probes.Ranges.Single(probe => probe.Start == item.Start && probe.End == item.Start + item.Preedit.Length);
                    var caret = DirectWriteLayoutOracle.TrailingCaretPosition(item.Display, item.Start + item.Preedit.Length, item.Configure);
                    var maximum = MathF.Max(0, probes.Ranges.Single(probe => probe.Start == 0 && probe.End == item.Display.Length)
                        .Fragments.Max(fragment => fragment.Left + fragment.Width) - item.Width);
                    var scroll = caret > item.Width ? MathF.Min(maximum, caret - item.Width + 4) : 0;
                    var expected = reference.Capture(ui =>
                    {
                        Begin(ui);
                        var paint = new UiControlContext(ui);
                        using var clip = paint.Clip(new(20, 20, item.Width, 32));
                        PaintNativeParagraphReference(reference.Context, ui, "", item.Display, UiInlineTextWeight.Regular, 280, false,
                            new(255, 255, 255), origin: new(20 - scroll, 20), combineRasters: true);
                        foreach (var fragment in range.Fragments)
                            paint.Frame(new(20 - scroll + fragment.Left, 39, fragment.Width, 1), new(255, 255, 255), default, 0, 0);
                        paint.Frame(new(MathF.Round(20 - scroll + caret), 21, 1, 18), new(255, 170, 0), default, 0, 0);
                    });
                    for (var repeat = 0; repeat < 2; repeat++)
                    {
                        candidate.Context.State.ResetCaretBlink();
                        var actual = candidate.Capture(Candidate);
                        var name = $"native-ime-{variant}-{dark}-{samples}-{repeat}";
                        var equal = expected.AsSpan().SequenceEqual(actual);
                        pairs.Add(new(name, Convert.ToHexString(SHA256.HashData(expected)), Convert.ToHexString(SHA256.HashData(actual)), equal));
                        if (!equal)
                        {
                            File.WriteAllBytes(Path.Combine(output, name + ".reference.rgba"), expected);
                            File.WriteAllBytes(Path.Combine(output, name + ".candidate.rgba"), actual);
                        }
                    }
                    ime.Pending = item.Preedit;
                    ime.Preedit = null;
                    var committedExpected = reference.Capture(ui =>
                    {
                        Begin(ui);
                        var paint = new UiControlContext(ui);
                        using var clip = paint.Clip(new(20, 20, item.Width, 32));
                        PaintNativeParagraphReference(reference.Context, ui, "", item.Display, UiInlineTextWeight.Regular, 280, false,
                            new(255, 255, 255), origin: new(20 - scroll, 20), combineRasters: true);
                        paint.Frame(new(MathF.Round(20 - scroll + caret), 21, 1, 18), new(255, 170, 0), default, 0, 0);
                    });
                    for (var repeat = 0; repeat < 2; repeat++)
                    {
                        candidate.Context.State.ResetCaretBlink();
                        var actual = candidate.Capture(Candidate);
                        var name = $"native-ime-commit-{variant}-{dark}-{samples}-{repeat}";
                        var equal = state.Value == item.Display && committedExpected.AsSpan().SequenceEqual(actual);
                        pairs.Add(new(name, Convert.ToHexString(SHA256.HashData(committedExpected)), Convert.ToHexString(SHA256.HashData(actual)), equal));
                        if (!equal)
                        {
                            File.WriteAllBytes(Path.Combine(output, name + ".reference.rgba"), committedExpected);
                            File.WriteAllBytes(Path.Combine(output, name + ".candidate.rgba"), actual);
                        }
                    }
                }
    }
}

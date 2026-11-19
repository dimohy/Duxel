using System.Security.Cryptography;
using Duxel.Core;

internal static partial class InlineTextRenderingTests
{
    private static void CompareSingleLineInsertion(string output, List<TextAccessibilityPixelPair> pairs)
    {
        using var reference = new PresentationReuseTests.Fixture(1, false);
        using var candidate = new PresentationReuseTests.Fixture(1, true);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        for (var variant = 0; variant < SingleLineInsertionTests.Cases.Length; variant++)
            foreach (var paste in new[] { false, true })
                foreach (var dark in new[] { false, true })
                    foreach (var samples in new[] { 1, 4 })
                    {
                        var item = SingleLineInsertionTests.Cases[variant];
                        reference.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                        candidate.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                        reference.Renderer.SetMsaaSamples(samples);
                        candidate.Renderer.SetMsaaSamples(samples);
                        candidate.Context.SetClipboard(new SingleLineInsertionTests.Clipboard(item.Insert));
                        var state = new UiState<string>(item.Source);
                        using var input = new UiInput<UiStandardTextInputBehavior, NativeInputPresentation>("Insertion pixels", state.Binding, item.Limit, item.Width);
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
                        candidate.Context.State.SetCursor(id, item.End);
                        candidate.Context.State.SetSelection(id, new(item.Start, item.End));
                        var caret = DirectWriteLayoutOracle.TrailingCaretPosition(item.Expected, item.Caret, item.Configure);
                        var probes = DirectWriteLayoutOracle.GeometryProbes(item.Expected, item.Configure);
                        var maximum = MathF.Max(0, probes.Ranges.Single(range => range.Start == 0 && range.End == item.Expected.Length)
                            .Fragments.Max(fragment => fragment.Left + fragment.Width) - item.Width);
                        var scroll = caret > item.Width ? MathF.Min(maximum, caret - item.Width + 4) : 0;
                        var expected = reference.Capture(ui =>
                        {
                            Begin(ui);
                            var paint = new UiControlContext(ui);
                            using var clip = paint.Clip(new(20, 20, item.Width, 32));
                            PaintNativeParagraphReference(reference.Context, ui, "", item.Expected, UiInlineTextWeight.Regular, 280, false,
                                new(255, 255, 255), origin: new(20 - scroll, 20), combineRasters: true);
                            paint.Frame(new(MathF.Round(20 - scroll + caret), 21, 1, 18), new(255, 170, 0), default, 0, 0);
                        });
                        for (var repeat = 0; repeat < 2; repeat++)
                        {
                            var events = repeat != 0 ? idle : paste ? idle with
                            {
                                KeyEvents = [new(UiKey.V, true, KeyModifiers.Ctrl)]
                            }
                                : idle with
                                {
                                    CharEvents = item.Insert.EnumerateRunes().Select(rune => new UiCharEvent((uint)rune.Value)).ToArray()
                                };
                            candidate.Context.State.ResetCaretBlink();
                            var actual = candidate.Capture(Candidate, events);
                            var name = $"native-insertion-{variant}-{paste}-{dark}-{samples}-{repeat}";
                            var equal = state.Value == item.Expected && expected.AsSpan().SequenceEqual(actual);
                            pairs.Add(new(name, Convert.ToHexString(SHA256.HashData(expected)), Convert.ToHexString(SHA256.HashData(actual)), equal));
                            if (!equal)
                            {
                                File.WriteAllBytes(Path.Combine(output, name + ".reference.rgba"), expected);
                                File.WriteAllBytes(Path.Combine(output, name + ".candidate.rgba"), actual);
                            }
                        }
                    }
    }
}

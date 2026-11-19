using Duxel.Core;

internal static class SpinNumberPolicyTests
{
    internal static void Run(Action<bool, string> check)
    {
        var integers = UiSpinNumberPolicy.Create(-2, 3);
        check(integers.Move(1, 1) == 2 && integers.Move(1, -1) == 0, "SpinButton increments and decrements by its declared step");
        check(integers.Move(3, 1) == 3 && integers.Move(-2, -1) == -2, "SpinButton stepping clamps both numeric bounds");
        check(!integers.CanMove(3, 1) && !integers.CanMove(-2, -1) && integers.CanMove(null, 1), "SpinButton bound buttons distinguish empty values from a numeric bound");
        var fractions = UiSpinNumberPolicy.Create(-1, 1, .1, .5);
        var number = 0d;
        for (var i = 0; i < 10; i++)
            number = fractions.Move(number, 1);
        check(number == 1 && fractions.Format(fractions.Move(.1, 1)) == "0.2", "SpinButton repeated decimal stepping does not publish binary floating artifacts");
        check(integers.Format(1.25) == "1.25", "SpinButton display preserves externally bound fractional values independently of step precision");
        check(fractions.Move(0, 1, true) == .5 && fractions.Move(0, -1, true) == -.5, "SpinButton page stepping has an independent declared amount");
        check(UiSpinNumberPolicy.Create(step: 1e-7).Precision == 7 && UiSpinNumberPolicy.Create(step: 1.25e-3).Precision == 5,
            "SpinButton infers decimal precision from ordinary and exponent step values");
        check(UiSpinNumberPolicy.Create(step: 1e6).Precision == 0, "SpinButton integer exponent steps do not invent fractional precision");
        check(UiSpinNumberPolicy.Create(10, 20).Move(null, 1) == 11 && UiSpinNumberPolicy.Create(10, 20).Move(null, -1) == 10,
            "SpinButton empty stepping starts from the declared minimum and clamps its direction");
        check(UiSpinNumberPolicy.Create().Move(null, 1) == 1 && UiSpinNumberPolicy.Create().Move(null, -1) == -1,
            "SpinButton empty unbounded stepping uses the explicit zero origin");
        check(fractions.TryCommit("3.25", out var clamped) && clamped == 1 && fractions.TryCommit("", out var empty) && empty is null,
            "SpinButton numeric commits clamp to bounds while empty text commits a nullable value");
        foreach (var text in new[] { "NaN", "Infinity", "1e999", "not a number", "12pt", "1,2" })
            check(!fractions.TryCommit(text, out _), "SpinButton rejects invalid default numeric text explicitly : " + text);
        check(fractions.TryCommit(" -2e-1 ", out var parsed) && parsed == -.2, "SpinButton default parser supports finite invariant exponent input");
        var precise = UiSpinNumberPolicy.Create(precision: 2);
        check(precise.Normalize(1.125) == 1.13 && precise.Normalize(-1.125) == -1.13,
            "SpinButton decimal midpoint commits use the documented away-from-zero rounding rule");
        check(UiSpinNumberPolicy.Create(step: double.MaxValue).Move(double.MaxValue, 1) == double.MaxValue,
            "SpinButton overflowing addition reaches the finite numeric endpoint without an invalid published value");
        check(UiSpinNumberPolicy.Create(0, 0).Move(null, 1) == 0 && !UiSpinNumberPolicy.Create(0, 0).CanMove(0, 1),
            "SpinButton a single-valued range resolves empty stepping and disables subsequent increments");
        static bool Throws<T>(Action action) where T : Exception
        {
            try
            {
                action();
                return false;
            }
            catch (T) { return true; }
        }
        check(Throws<ArgumentOutOfRangeException>(() => UiSpinNumberPolicy.Create(2, 1))
            && Throws<ArgumentOutOfRangeException>(() => UiSpinNumberPolicy.Create(step: 0))
            && Throws<ArgumentOutOfRangeException>(() => UiSpinNumberPolicy.Create(pageStep: double.NaN))
            && Throws<ArgumentOutOfRangeException>(() => UiSpinNumberPolicy.Create(precision: 16)),
            "SpinButton invalid ranges, step amounts and precision fail before state mutation");
        check(Throws<NotSupportedException>(() => UiSpinNumberPolicy.Create(step: 1e-16)),
            "SpinButton unsupported inferred precision fails explicitly instead of silently disabling stepping");
        check(Throws<ArgumentOutOfRangeException>(() => integers.ValidateValue(4))
            && Throws<ArgumentOutOfRangeException>(() => integers.ValidateValue(double.NaN)),
            "SpinButton rejects an invalid externally bound numeric value");
    }
}

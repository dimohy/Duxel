using Duxel.Core;

internal static class BackdropCompositeTests
{
    internal static void Run(Action<bool, string> check)
    {
        var bounds = new UiRect(0, 0, 16, 16);
        var builder = new UiDrawListBuilder(bounds);
        builder.AddBackdropComposite(new(3), new(4), bounds, .8f);
        builder.AddBackdropComposite(new(3), new(5), bounds, .8f);
        builder.AddImage(new(3), new(0, 0), new(16, 16), new(0, 0), new(1, 1), new(255, 255, 255));
        var lists = builder.Build();
        try
        {
            var commands = lists.Single().Commands;
            check(commands.Count == 3, "different white references and ordinary images cannot merge");
            check(commands[0].WhiteBackdropTextureId == new UiTextureId(4)
                && commands[1].WhiteBackdropTextureId == new UiTextureId(5)
                && !commands[2].IsBackdropComposite, "composite paint state ends before ordinary image submission");
            check(commands[0].Opacity == .8f && commands[2].Opacity == 1, "group opacity applies once without leaking");
            var before = UiDrawList.ComputeCommandTopologyStamp([commands[0]]);
            var after = UiDrawList.ComputeCommandTopologyStamp([commands[0] with { WhiteBackdropTextureId = new(5) }]);
            check(before != after, "white reference alone invalidates command topology");
        }
        finally { foreach (var list in lists) list.ReleasePooled(); }
        var rejected = false;
        try
        {
            builder.AddBackdropComposite(new(3), new(4), bounds, float.NaN);
        }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected, "nonfinite opacity fails before texture acquisition");
        rejected = false;
        try
        {
            using var invalid = new UiBackdropImage(new(new(1, 1, [255, 0, 0, 0])), new(new(1, 1, [0, 255, 255, 255])));
        }
        catch (ArgumentException) { rejected = true; }
        check(rejected, "negative affine transmittance is rejected");
        using var lcd = new UiBackdropImage(new(new(1, 1, [128, 0, 0, 0])), new(new(1, 1, [128, 127, 255, 255])));
        check(true, "zero alpha with nonzero RGB coverage is accepted for LCD reference data");
    }
}

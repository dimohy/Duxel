using System.Numerics;
using Duxel.Core;

internal static class AllocationRoutingRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, bool vector)
    {
        foreach (var (name, create) in AllocationRoutingTests.Leaves)
        {
            if (name == "spinner")
                continue;
            foreach (var table in new[] { true, false })
            foreach (var narrowClip in new[] { true, false })
            {
                using var actual = Dux.SizedBox(Dux.Padding(Dux.Padding(create(), new(3)), new(7)), 80, 64);
                using var empty = Dux.SizedBox(null, 80, 64);
                using var reference = Dux.SizedBox(create(), 60, 44);
                var key = $"routing-{name}-{table}-{narrowClip}-{vector}";
                void Draw(UiImmediateContext ui, bool compiled)
                {
                    if (table)
                    {
                        if (!ui.BeginTable("routing-pixel-table", 1))
                            throw new InvalidOperationException("Table fixture did not open.");
                        ui.TableSetupColumn("Column", 200, 0);
                        ui.TableNextRow();
                    }
                    else
                    {
                        ui.Columns(2);
                        ui.NextColumn();
                    }
                    var anchor = ui.GetCursorScreenPos();
                    var outer = new UiRect(anchor.X, anchor.Y, narrowClip ? 40 : 80, 64);
                    var inheritedClip = default(UiRect);
                    using (var clip = new UiControlContext(ui).Clip(outer))
                    {
                        inheritedClip = new UiControlContext(ui).ClipRect;
                        if (compiled)
                            actual.Render(ui);
                        else
                            empty.Render(ui);
                    }
                    var origin = ui.GetItemRectMin();
                    if (table)
                        ui.EndTable();
                    else
                        ui.Columns(1);
                    if (compiled)
                        return;
                    using var rootClip = new UiControlContext(ui).Clip(inheritedClip);
                    ui.SetCursorScreenPos(new(origin.X + 10, origin.Y + 10));
                    reference.Render(ui);
                }
                var actualPixels = capture(key, ui => Scene(ui, vector, key, () => Draw(ui, true)));
                var expectedPixels = capture(key + "-reference", ui => Scene(ui, vector, key + "-reference", () => Draw(ui, false)));
                check(actualPixels.AsSpan().SequenceEqual(expectedPixels),
                    key + " matches explicit world coordinates and ancestor clip after leaving the legacy table/columns");
            }
        }
    }

    private static void Scene(UiImmediateContext ui, bool vector, string id, Action draw)
    {
        ui.EnableRootViewportContentLayout(contentPadding : 0);
        if (vector)
            ui.BeginLayer("allocated-routing-vector-" + id, new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
        try
        {
            ui.SetCursorScreenPos(default);
            draw();
        }
        finally
        {
            if (vector)
                ui.EndLayer();
        }
    }
}

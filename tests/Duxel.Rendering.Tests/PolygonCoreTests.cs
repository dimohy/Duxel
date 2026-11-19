using Duxel.Core;
using Duxel.Core.Simulation;

internal static class PolygonCoreTests
{
    internal static void Run(Action<bool, string> check)
    {
        var featureCommands = UiPooledList<UiDrawCommand>.FromArray([new(default,default,0,3,0)]);
        var featureList = new UiDrawList(UiPooledList<UiDrawVertex>.FromArray([]),UiPooledList<uint>.FromArray([]),featureCommands)
            { CommandStateStamp = 1 };
        check(featureList.GetCommandFeatures() == UiDrawCommandFeatures.None,
            "ordinary retained commands expose no callback or GPU feature");
        featureCommands.AsWritableSpan()[0] = featureCommands[0] with { Callback = static (_,_) => {} };
        var revisedFeatures = featureList with { CommandStateStamp = 2 };
        check(revisedFeatures.GetCommandFeatures() == UiDrawCommandFeatures.Callbacks,
            "command feature cache invalidates when retained command state changes");
        var unknownFeatures = featureList with { CommandStateStamp = 0 };
        check(unknownFeatures.GetCommandFeatures() == UiDrawCommandFeatures.Callbacks,
            "unstamped caller-owned command metadata is inspected directly");
        featureCommands.AsWritableSpan()[0] = featureCommands[0] with { Callback = null };
        check(unknownFeatures.GetCommandFeatures() == UiDrawCommandFeatures.None,
            "unstamped command mutation cannot leave a stale callback requirement");
        featureList.ReleasePooled();
        var solver = new CircleCollisionSolver2D();
        var bodies = new TestBody[] { new() { Position = new(0,0), Direction = new(1,0), SizeScale = 1 },
            new() { Position = new(1,0), Direction = new(-1,0), SizeScale = 1 } };
        solver.Resolve(bodies.AsSpan(), 1, 3, new(1));
        check(bodies[0].Position.X == -.5f && bodies[1].Position.X == 1.5f
            && bodies[0].Direction.X == -1 && bodies[1].Direction.X == 1
            && bodies[0].HitResponse == 1 && bodies[1].HitResponse == 1,
            "shared collision solver separates overlapping bodies and exchanges normal directions");
        bodies[1].Position = bodies[0].Position;
        solver.Resolve(bodies.AsSpan(), 1, 3, new(1));
        check(float.IsFinite(bodies[0].Direction.X) && float.IsFinite(bodies[1].Direction.Y)
            && bodies[0].Position != bodies[1].Position,
            "shared collision solver resolves coincident centers with finite state");
        bodies[0].Position = new(-100,100); bodies[1].Position = new(-100,100);
        solver.Resolve(bodies.AsSpan(), 1, 3, new(1), new(0,0,20,20));
        check(bodies.All(b => b.Position.X >= 1 && b.Position.X <= 19 && b.Position.Y >= 1 && b.Position.Y <= 19),
            "shared collision solver keeps full bodies inside bounds after overlap correction");
        var grid = new SpatialGrid2D();
        var random = new Random(4251);
        var points = new UiVector2[500];
        var radii = new float[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            points[i] = new((float)random.NextDouble() * 200 - 100, (float)random.NextDouble() * 200 - 100);
            radii[i] = .5f + (float)random.NextDouble() * 7.9f;
        }
        grid.Rebuild(points, 16.8f);
        var next = grid.Next;
        var covered = new bool[points.Length, points.Length];
        var pairs = 0;
        var duplicates = 0;
        for (var i = 0; i < points.Length; i++)
        {
            var x = (int)MathF.Floor(points[i].X * grid.InverseCellSize);
            var y = (int)MathF.Floor(points[i].Y * grid.InverseCellSize);
            for (var cy = y - 1; cy <= y + 1; cy++)
            for (var cx = x - 1; cx <= x + 1; cx++)
            for (var j = grid.First(cx, cy); j >= 0; j = next[j])
            {
                if (j <= i) continue;
                if (covered[i,j]) duplicates++;
                covered[i,j] = true; pairs++;
            }
        }
        var missing = 0;
        for (var i = 0; i < points.Length; i++)
        for (var j = i + 1; j < points.Length; j++)
        {
            var dx = points[i].X - points[j].X; var dy = points[i].Y - points[j].Y;
            if (dx * dx + dy * dy < (radii[i] + radii[j]) * (radii[i] + radii[j]) && !covered[i,j]) missing++;
        }
        check(duplicates == 0, "spatial grid visits every candidate pair once");
        check(missing == 0 && pairs < points.Length * (points.Length - 1) / 2, "spatial grid covers all brute force overlaps with fewer candidates");
        grid.Rebuild([new(-1,-1), new(-1,-1)], 2);
        check(grid.First(-1,-1) == 1 && grid.Next[1] == 0 && grid.Next[0] == -1, "spatial grid rebuild preserves descending index order and clears previous cells");
        grid.Rebuild([], 2);
        check(grid.First(0,0) == -1 && grid.Next.Length == 0, "spatial grid empty rebuild removes all links");
        var limited = new SpatialGrid2D(4);
        var rejected = false;
        try { limited.Rebuild([new(0,0), new(100,100)], 1); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected, "spatial grid refuses excessive cell allocation");

        var clip = new UiRect(0,0,1000,1000);
        var mixed = new UiDrawListBuilder(clip);
        mixed.AddRectFilled(new(0,0,10,10), new(0xFFFFFFFF), new(2));
        mixed.AddCircleFilled(new(15,15), 5, new(0xFFFFFFFF), new(2));
        mixed.AddRectFilled(new(20,20,10,10), new(0x80FFFFFF), new(2));
        var mixedLists = mixed.Build();
        check(mixedLists.Count == 1 && mixedLists[0].Commands.Count == 1
            && mixedLists[0].RectFilledPrimitives!.Count == 3
            && mixedLists[0].RectFilledPrimitives![1].CornerRadius == -5,
            "interleaved circles and rectangles share one ordered analytic instance command");
        foreach (var list in mixedLists) { list.Vertices.Return(); list.Indices.Return(); list.Commands.Return(); list.RectFilledPrimitives?.Return(); list.CircleFilledPrimitives?.Return(); }
        mixedLists.Return();
        var reference = new UiDrawListBuilder(clip);
        var batch = new UiDrawListBuilder(clip);
        var shapes = new UiRegularPolygon[10000];
        for (var i = 0; i < shapes.Length; i++)
        {
            shapes[i] = new(new(i % 100 * 9, i / 100 * 9), 6, 3 + i % 10, i * .123f, new(0xFF123456));
            var shape = shapes[i];
            var vertices = new UiVector2[shape.Sides];
            var cos = MathF.Cos(shape.Rotation); var sin = MathF.Sin(shape.Rotation);
            var cs = MathF.Cos(MathF.Tau / shape.Sides); var ss = MathF.Sin(MathF.Tau / shape.Sides);
            for (var vertex = 0; vertex < vertices.Length; vertex++)
            {
                vertices[vertex] = new(shape.Center.X + cos * shape.Radius, shape.Center.Y + sin * shape.Radius);
                var nc = cos * cs - sin * ss; sin = sin * cs + cos * ss; cos = nc;
            }
            reference.AddConvexPolyFilled(vertices, shape.Color);
        }
        batch.AddRegularPolygons(shapes);
        var a = reference.Build(); var b = batch.Build();
        var av = new List<UiDrawVertex>(); var bv = new List<UiDrawVertex>();
        var at = new List<UiDrawVertex>(); var bt = new List<UiDrawVertex>();
        foreach (var list in a) { av.AddRange(list.Vertices.AsSpan().ToArray()); foreach(var index in list.Indices.AsSpan()) at.Add(list.Vertices[(int)index]); }
        foreach (var list in b) { bv.AddRange(list.Vertices.AsSpan().ToArray()); foreach(var index in list.Indices.AsSpan()) bt.Add(list.Vertices[(int)index]); }
        check(av.SequenceEqual(bv) && at.SequenceEqual(bt), "regular polygon batches preserve exact ordered vertices, colors and triangles across flush boundaries");
        foreach (var list in a) { list.Vertices.Return(); list.Indices.Return(); list.Commands.Return(); }
        foreach (var list in b) { list.Vertices.Return(); list.Indices.Return(); list.Commands.Return(); }
        a.Return(); b.Return();
    }
    private struct TestBody : ICircleCollisionBody2D
    {
        public UiVector2 Position { get; set; }
        public UiVector2 Direction { get; set; }
        public float SizeScale { get; set; }
        public double AngularSpeed { get; set; }
        public float HitResponse { get; set; }
    }
}

namespace Duxel.Core.Simulation;

/// <summary>CPU implementation of the GPU solver's snapshot-based circular response contract.</summary>
public sealed class ParticleJacobiSimulation
{
    private readonly UiGpuParticleSystem _system;
    private UiGpuParticle[] _current, _scratch;
    private UiVector2[] _positions;
    private UiGpuParticleSnapshot _snapshot;
    private readonly SpatialGrid2D _grid = new();
    public ReadOnlySpan<UiGpuParticle> Particles => _current;

    public ParticleJacobiSimulation(UiGpuParticleSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        _system = system;
        _snapshot = system.CaptureSnapshot();
        _current = _snapshot.Initial.ToArray();
        _scratch = new UiGpuParticle[_current.Length];
        _positions = new UiVector2[_current.Length];
    }

    public void Step(UiGpuParticleFrame frame)
    {
        frame.Validate();
        if (!ReferenceEquals(frame.System, _system)) throw new ArgumentException("Simulation settings belong to another system.", nameof(frame));
        if (!ReferenceEquals(_snapshot, frame.Snapshot))
        {
            var preserved = frame.Snapshot.GetPreservedPrefixCount(_snapshot);
            var current = frame.Snapshot.Initial.ToArray();
            _current.AsSpan(0, preserved).CopyTo(current);
            _current = current; _scratch = new UiGpuParticle[current.Length];
            _positions = new UiVector2[current.Length]; _snapshot = frame.Snapshot;
        }
        if (frame.DeltaSeconds == 0) return;
        var dt = frame.DeltaSeconds;
        for (var i = 0; i < _current.Length; i++)
        {
            ref var body = ref _current[i];
            body.HitResponse = MathF.Max(0, body.HitResponse - dt * 2.4f);
            var distance = frame.Speed * body.SpeedScale * dt;
            body.Position = new(body.Position.X + body.Direction.X * distance, body.Position.Y + body.Direction.Y * distance);
            body.AngularSpeed *= MathF.Max(0, 1 - dt * .7f);
            body.Rotation += body.AngularSpeed * dt;
            Constrain(ref body, frame);
        }
        for (var iteration = 0; iteration < frame.CollisionIterations; iteration++)
        {
            for (var i = 0; i < _positions.Length; i++) _positions[i] = _current[i].Position;
            _grid.Rebuild(_positions, frame.Radius * frame.Snapshot.MaximumSizeScale * 2);
            var next = _grid.Next;
            for (var i = 0; i < _current.Length; i++)
            {
                var a = _current[i];
                var cellX = (int)MathF.Floor(a.Position.X * _grid.InverseCellSize);
                var cellY = (int)MathF.Floor(a.Position.Y * _grid.InverseCellSize);
                float correctionX = 0, correctionY = 0, directionX = 0, directionY = 0, angular = 0;
                var touched = false;
                for (var y = cellY - 1; y <= cellY + 1; y++)
                for (var x = cellX - 1; x <= cellX + 1; x++)
                for (var j = _grid.First(x,y); j >= 0; j = next[j])
                {
                    if (j == i) continue;
                    ref readonly var b = ref _current[j];
                    var dx = b.Position.X - a.Position.X; var dy = b.Position.Y - a.Position.Y;
                    var distanceSquared = dx * dx + dy * dy;
                    var minimum = frame.Radius * (a.SizeScale + b.SizeScale);
                    if (distanceSquared >= minimum * minimum) continue;
                    var distance = MathF.Sqrt(distanceSquared);
                    float nx, ny;
                    if (distanceSquared <= 0)
                    {
                        var hash = unchecked((uint)Math.Min(i,j) * 1664525u + (uint)Math.Max(i,j) * 1013904223u);
                        var angle = (hash & 65535u) * (MathF.Tau / 65536);
                        var sign = i < j ? 1 : -1;
                        nx = MathF.Cos(angle) * sign; ny = MathF.Sin(angle) * sign;
                    }
                    else { nx = dx / distance; ny = dy / distance; }
                    var correction = (minimum - distance) * .5f;
                    correctionX -= nx * correction; correctionY -= ny * correction;
                    var relativeX = b.Direction.X - a.Direction.X; var relativeY = b.Direction.Y - a.Direction.Y;
                    var exchange = relativeX * nx + relativeY * ny;
                    var coordinateScale = MathF.Max(minimum, MathF.Max(MathF.Max(MathF.Abs(a.Position.X),MathF.Abs(a.Position.Y)),
                        MathF.Max(MathF.Abs(b.Position.X),MathF.Abs(b.Position.Y))));
                    var contactTolerance = coordinateScale * .000000476837158203125f;
                    // Compare the same penetration threshold without subtracting
                    // a rounded square root. CPU/GPU sqrt rounding must not turn
                    // an almost-separated pair into a finite second impulse.
                    var contactMinimum = minimum - contactTolerance;
                    if (exchange < 0 && contactMinimum > 0 && distanceSquared < contactMinimum * contactMinimum)
                    {
                        directionX += nx * exchange; directionY += ny * exchange;
                        // Tangential response vanishes with normal impulse strength, avoiding spin
                        // discontinuities when floating-point error changes a grazing contact's sign.
                        var angularLimit = -exchange * 1.15f;
                        var angularImpulse = Math.Clamp((relativeX * -ny + relativeY * nx) * 1.15f,
                            -angularLimit, angularLimit);
                        angular += i < j ? -angularImpulse : angularImpulse;
                    }
                    touched = true;
                }
                if (touched)
                {
                    a.Position = new(a.Position.X + correctionX, a.Position.Y + correctionY);
                    var vx = a.Direction.X + directionX; var vy = a.Direction.Y + directionY;
                    var lengthSquared = vx * vx + vy * vy;
                    var length = MathF.Sqrt(lengthSquared);
                    a.Direction = lengthSquared <= .000001f ? new(1,0) : new(vx / length,vy / length);
                    a.AngularSpeed = Math.Clamp(a.AngularSpeed + angular, -frame.MaximumAngularSpeed, frame.MaximumAngularSpeed);
                    a.HitResponse = 1;
                }
                Constrain(ref a, frame);
                _scratch[i] = a;
            }
            (_current, _scratch) = (_scratch, _current);
        }
    }
    private static void Constrain(ref UiGpuParticle body, UiGpuParticleFrame frame)
    {
        var bounds = frame.Bounds; var radius = frame.Radius * body.SizeScale;
        var minX = bounds.X + radius; var maxX = bounds.X + bounds.Width - radius;
        var minY = bounds.Y + radius; var maxY = bounds.Y + bounds.Height - radius;
        var p = body.Position; var d = body.Direction;
        if (p.X < minX) { p = p with { X = minX }; d = d with { X = MathF.Abs(d.X) }; }
        if (p.X > maxX) { p = p with { X = maxX }; d = d with { X = -MathF.Abs(d.X) }; }
        if (p.Y < minY) { p = p with { Y = minY }; d = d with { Y = MathF.Abs(d.Y) }; }
        if (p.Y > maxY) { p = p with { Y = maxY }; d = d with { Y = -MathF.Abs(d.Y) }; }
        body.Position = p; body.Direction = d;
    }
}

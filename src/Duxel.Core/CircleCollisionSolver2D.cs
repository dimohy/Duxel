namespace Duxel.Core.Simulation;

/// <summary>Mutable circular collision state. Appearance and rendering remain owned by the caller.</summary>
public interface ICircleCollisionBody2D
{
    UiVector2 Position { get; set; }
    UiVector2 Direction { get; set; }
    float SizeScale { get; }
    double AngularSpeed { get; set; }
    float HitResponse { get; set; }
}

/// <summary>Reusable ordered circular broad-phase and response solver with caller-owned contiguous bodies.</summary>
public sealed class CircleCollisionSolver2D
{
    private const float CollisionAngularImpulseScale = 1.15f;
    private readonly SpatialGrid2D _collisionGrid = new();
    private UiVector2[] _collisionPositions = [];
    private float[] _collisionRadii = [];
    private int[] _collisionTouchedStamp = [];
    private int _collisionStamp = 1;

    public void Resolve<TBody>(Span<TBody> polygons, float radiusScale, float maxAngularSpeed, Random random, UiRect? bounds = null)
        where TBody : struct, ICircleCollisionBody2D
    {
        if (!float.IsFinite(radiusScale) || radiusScale <= 0) throw new ArgumentOutOfRangeException(nameof(radiusScale));
        if (!float.IsFinite(maxAngularSpeed) || maxAngularSpeed < 0) throw new ArgumentOutOfRangeException(nameof(maxAngularSpeed));
        ArgumentNullException.ThrowIfNull(random);
        var count = polygons.Length;
        if (count <= 1)
        {
            if (count == 1 && bounds is { } singleBounds) Constrain(ref polygons[0], radiusScale * polygons[0].SizeScale, singleBounds);
            return;
        }

        if (_collisionPositions.Length < count)
        {
            _collisionPositions = new UiVector2[count];
        }

        if (_collisionRadii.Length < count)
        {
            _collisionRadii = new float[count];
        }

        if (_collisionTouchedStamp.Length < count)
        {
            _collisionTouchedStamp = new int[count];
        }

        _collisionStamp++;
        if (_collisionStamp == int.MaxValue)
        {
            Array.Clear(_collisionTouchedStamp, 0, _collisionTouchedStamp.Length);
            _collisionStamp = 1;
        }

        var maxRadius = 0f;
        for (var i = 0; i < count; i++)
        {
            ref var body = ref polygons[i];
            var radius = radiusScale * body.SizeScale;
            if (!float.IsFinite(radius) || radius <= 0) throw new ArgumentOutOfRangeException(nameof(polygons));
            _collisionRadii[i] = radius;
            maxRadius = MathF.Max(maxRadius, radius);
            _collisionPositions[i] = body.Position;
        }
        // A 3x3 search covers every initial overlap only when a cell spans the maximum diameter.
        _collisionGrid.Rebuild(_collisionPositions.AsSpan(0, count), maxRadius * 2f);
        var invCellSize = _collisionGrid.InverseCellSize;
        var collisionNext = _collisionGrid.Next;

        for (var i = 0; i < polygons.Length; i++)
        {
            ref var bodyA = ref polygons[i];
            var radiusA = _collisionRadii[i];
            var centerA = _collisionPositions[i];
            var cellAX = (int)MathF.Floor(centerA.X * invCellSize);
            var cellAY = (int)MathF.Floor(centerA.Y * invCellSize);

            for (var ny = cellAY - 1; ny <= cellAY + 1; ny++)
            {
                for (var nx = cellAX - 1; nx <= cellAX + 1; nx++)
                {
                    var head = _collisionGrid.First(nx, ny);
                    if (head < 0)
                    {
                        continue;
                    }

                    for (var j = head; j >= 0; j = collisionNext[j])
                    {
                        if (j <= i)
                        {
                            continue;
                        }

                        ref var bodyB = ref polygons[j];
                        var radiusB = _collisionRadii[j];
                        var minDistance = radiusA + radiusB;

                        var dx = bodyB.Position.X - bodyA.Position.X;
                        var dy = bodyB.Position.Y - bodyA.Position.Y;
                        var distanceSquared = dx * dx + dy * dy;
                        var minDistanceSquared = minDistance * minDistance;
                        if (distanceSquared >= minDistanceSquared)
                        {
                            continue;
                        }

                        float normalX;
                        float normalY;
                        float distance;

                        if (distanceSquared <= float.Epsilon)
                        {
                            var angle = (float)(random.NextDouble() * MathF.Tau);
                            normalX = MathF.Cos(angle);
                            normalY = MathF.Sin(angle);
                            distance = 0f;
                        }
                        else
                        {
                            distance = MathF.Sqrt(distanceSquared);
                            var inverseDistance = 1f / distance;
                            normalX = dx * inverseDistance;
                            normalY = dy * inverseDistance;
                        }

                        var overlap = minDistance - distance;
                        if (overlap > 0f)
                        {
                            var correctionX = normalX * (overlap * 0.5f);
                            var correctionY = normalY * (overlap * 0.5f);
                            bodyA.Position = new UiVector2(bodyA.Position.X - correctionX, bodyA.Position.Y - correctionY);
                            bodyB.Position = new UiVector2(bodyB.Position.X + correctionX, bodyB.Position.Y + correctionY);
                        }

                        var aAlong = bodyA.Direction.X * normalX + bodyA.Direction.Y * normalY;
                        var bAlong = bodyB.Direction.X * normalX + bodyB.Direction.Y * normalY;
                        var exchange = bAlong - aAlong;

                        var nextDirA = new UiVector2(bodyA.Direction.X + normalX * exchange, bodyA.Direction.Y + normalY * exchange);
                        var nextDirB = new UiVector2(bodyB.Direction.X - normalX * exchange, bodyB.Direction.Y - normalY * exchange);

                        var tangentX = -normalY;
                        var tangentY = normalX;
                        var relativeTangentVelocity =
                            (bodyB.Direction.X - bodyA.Direction.X) * tangentX
                            + (bodyB.Direction.Y - bodyA.Direction.Y) * tangentY;
                        var angularImpulse = relativeTangentVelocity * CollisionAngularImpulseScale;
                        

                        bodyA.Direction = nextDirA;
                        bodyB.Direction = nextDirB;
                        bodyA.AngularSpeed = Math.Clamp(bodyA.AngularSpeed - angularImpulse, -maxAngularSpeed, maxAngularSpeed);
                        bodyB.AngularSpeed = Math.Clamp(bodyB.AngularSpeed + angularImpulse, -maxAngularSpeed, maxAngularSpeed);
                        bodyA.HitResponse = 1f;
                        bodyB.HitResponse = 1f;
                        _collisionTouchedStamp[i] = _collisionStamp;
                        _collisionTouchedStamp[j] = _collisionStamp;
                    }
                }
            }
        }

        for (var i = 0; i < count; i++)
        {
            ref var body = ref polygons[i];
            if (_collisionTouchedStamp[i] == _collisionStamp) body.Direction = NormalizeDirection(body.Direction);
            if (bounds is { } area) Constrain(ref body, _collisionRadii[i], area);
        }
    }

    private static void Constrain<TBody>(ref TBody body, float radius, UiRect bounds) where TBody : struct, ICircleCollisionBody2D
    {
        if (bounds.Width < 2 * radius || bounds.Height < 2 * radius)
            throw new ArgumentOutOfRangeException(nameof(bounds), "Collision bounds must contain each body's full diameter.");
        var position = body.Position; var direction = body.Direction;
        var minX = bounds.X + radius; var maxX = bounds.X + bounds.Width - radius;
        var minY = bounds.Y + radius; var maxY = bounds.Y + bounds.Height - radius;
        if (position.X < minX) { position = position with { X = minX }; direction = direction with { X = MathF.Abs(direction.X) }; }
        if (position.X > maxX) { position = position with { X = maxX }; direction = direction with { X = -MathF.Abs(direction.X) }; }
        if (position.Y < minY) { position = position with { Y = minY }; direction = direction with { Y = MathF.Abs(direction.Y) }; }
        if (position.Y > maxY) { position = position with { Y = maxY }; direction = direction with { Y = -MathF.Abs(direction.Y) }; }
        body.Position = position; body.Direction = direction;
    }

    private static UiVector2 NormalizeDirection(UiVector2 direction)
    {
        var lengthSquared = direction.X * direction.X + direction.Y * direction.Y;
        if (lengthSquared <= 1e-6f)
        {
            return new UiVector2(1f, 0f);
        }

        var invLength = 1f / MathF.Sqrt(lengthSquared);
        return new UiVector2(direction.X * invLength, direction.Y * invLength);
    }

}

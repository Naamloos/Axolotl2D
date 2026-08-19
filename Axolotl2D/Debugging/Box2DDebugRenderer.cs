using Axolotl2D.Physics;
using Axolotl2D.Rendering;
using Box2D.NET;
using System.Numerics;
using static Box2D.NET.B2MathFunction;
using static Box2D.NET.B2Worlds;

namespace Axolotl2D.Debugging;

internal sealed class Box2DDebugRenderer(PrimitiveBatch primitives, Camera2D camera)
{
    private const float Depth = 99_000f;

    public void Draw(PhysicsWorld world, bool drawBounds)
    {
        var visible = VisibleBounds(world);
        var context = new Context(primitives, world);
        var debugDraw = new B2DebugDraw
        {
            DrawPolygonFcn = DrawPolygon,
            DrawSolidPolygonFcn = DrawSolidPolygon,
            DrawCircleFcn = DrawCircle,
            DrawSolidCircleFcn = DrawSolidCircle,
            DrawSolidCapsuleFcn = DrawSolidCapsule,
            drawLineFcn = DrawLine,
            DrawTransformFcn = DrawTransform,
            DrawPointFcn = DrawPoint,
            DrawStringFcn = DrawString,
            drawingBounds = visible,
            drawShapes = true,
            drawJoints = true,
            drawJointExtras = true,
            drawBounds = drawBounds,
            drawMass = true,
            drawContactPoints = true,
            drawContactNormals = true,
            context = context
        };
        b2World_Draw(world.WorldId, debugDraw);
    }

    private B2AABB VisibleBounds(PhysicsWorld world)
    {
        var viewport = camera.ViewportSize;
        Span<Vector2> points =
        [
            camera.ScreenToWorld(Vector2.Zero),
            camera.ScreenToWorld(new Vector2(viewport.X, 0f)),
            camera.ScreenToWorld(viewport),
            camera.ScreenToWorld(new Vector2(0f, viewport.Y))
        ];
        var minimum = points[0];
        var maximum = points[0];
        foreach (var point in points[1..])
        {
            minimum = Vector2.Min(minimum, point);
            maximum = Vector2.Max(maximum, point);
        }
        minimum = world.ToPhysics(minimum);
        maximum = world.ToPhysics(maximum);
        return new B2AABB(new B2Vec2(minimum.X, minimum.Y), new B2Vec2(maximum.X, maximum.Y));
    }

    private static void DrawPolygon(ReadOnlySpan<B2Vec2> vertices, int count, B2HexColor color, object value)
    {
        var context = (Context)value;
        for (var index = 0; index < count; index++)
            context.Line(vertices[index], vertices[(index + 1) % count], color);
    }

    private static void DrawSolidPolygon(in B2Transform transform, ReadOnlySpan<B2Vec2> vertices,
        int count, float radius, B2HexColor color, object value)
    {
        var context = (Context)value;
        for (var index = 0; index < count; index++)
        {
            var a = vertices[index];
            var b = vertices[(index + 1) % count];
            context.Line(b2TransformPoint(in transform, in a), b2TransformPoint(in transform, in b), color);
        }
    }

    private static void DrawCircle(in B2Vec2 center, float radius, B2HexColor color, object value) =>
        ((Context)value).Circle(center, radius, color);

    private static void DrawSolidCircle(in B2Transform transform, float radius, B2HexColor color, object value)
    {
        var context = (Context)value;
        context.Circle(transform.p, radius, color);
        var axis = new B2Vec2(transform.p.X + transform.q.c * radius, transform.p.Y + transform.q.s * radius);
        context.Line(transform.p, axis, color);
    }

    private static void DrawSolidCapsule(in B2Vec2 point1, in B2Vec2 point2, float radius,
        B2HexColor color, object value)
    {
        var context = (Context)value;
        context.Circle(point1, radius, color);
        context.Circle(point2, radius, color);
        var delta = new Vector2(point2.X - point1.X, point2.Y - point1.Y);
        if (delta.LengthSquared() <= float.Epsilon)
            return;
        var normal = Vector2.Normalize(new Vector2(-delta.Y, delta.X)) * radius;
        context.Line(new B2Vec2(point1.X + normal.X, point1.Y + normal.Y),
            new B2Vec2(point2.X + normal.X, point2.Y + normal.Y), color);
        context.Line(new B2Vec2(point1.X - normal.X, point1.Y - normal.Y),
            new B2Vec2(point2.X - normal.X, point2.Y - normal.Y), color);
    }

    private static void DrawLine(in B2Vec2 point1, in B2Vec2 point2, B2HexColor color, object value) =>
        ((Context)value).Line(point1, point2, color);

    private static void DrawTransform(in B2Transform transform, object value)
    {
        const float axisLength = 0.25f;
        var context = (Context)value;
        var x = new B2Vec2(transform.p.X + transform.q.c * axisLength, transform.p.Y + transform.q.s * axisLength);
        var y = new B2Vec2(transform.p.X - transform.q.s * axisLength, transform.p.Y + transform.q.c * axisLength);
        context.Line(transform.p, x, B2HexColor.b2_colorRed);
        context.Line(transform.p, y, B2HexColor.b2_colorGreen);
    }

    private static void DrawPoint(in B2Vec2 point, float size, B2HexColor color, object value)
    {
        var context = (Context)value;
        context.Primitives.FillCircle(context.World.ToWorld(point), MathF.Max(1f, size / 2f),
            ToColor(color), CoordinateSpace.World, Depth);
    }

    private static void DrawString(in B2Vec2 point, string text, B2HexColor color, object value) { }

    private static Color ToColor(B2HexColor color)
    {
        var value = (int)color;
        return new Color(
            ((value >> 16) & 255) / 255f,
            ((value >> 8) & 255) / 255f,
            (value & 255) / 255f,
            0.9f);
    }

    private sealed record Context(PrimitiveBatch Primitives, PhysicsWorld World)
    {
        public void Line(B2Vec2 start, B2Vec2 end, B2HexColor color) =>
            Primitives.DrawLine(World.ToWorld(start), World.ToWorld(end), ToColor(color),
                1.5f, CoordinateSpace.World, Depth);

        public void Circle(B2Vec2 center, float radius, B2HexColor color) =>
            Primitives.DrawCircle(World.ToWorld(center), radius * World.PixelsPerMeter, ToColor(color),
                1.5f, space: CoordinateSpace.World, depth: Depth);
    }
}

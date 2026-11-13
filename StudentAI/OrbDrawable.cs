using System;
using Microsoft.Maui.Graphics;

namespace StudentAI;

public enum OrbMode { Idle, Thinking, Speaking }

public sealed class OrbDrawable : IDrawable
{
    public float Time { get; set; }
    public OrbMode Mode { get; set; } = OrbMode.Idle;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float size = MathF.Min(dirtyRect.Width, dirtyRect.Height);
        if (size <= 0) return;
        float cx = dirtyRect.Center.X, cy = dirtyRect.Center.Y;
        float radius = size * 0.33f;
        float activity = Mode switch { OrbMode.Thinking => 0.085f, OrbMode.Speaking => 0.11f, _ => 0.04f };
        float speed = Mode switch { OrbMode.Thinking => 2.4f, OrbMode.Speaking => 3.8f, _ => 0.75f };
        float phase = Time * speed;
        radius *= Mode == OrbMode.Speaking ? 1f + 0.035f * MathF.Sin(Time * 11f) : 1f + 0.018f * MathF.Sin(Time * 1.8f);
        canvas.SaveState();
        for (int i = 14; i >= 1; i--)
        {
            canvas.FillColor = new Color(0.02f, 0.65f, 0.9f, 0.009f);
            canvas.FillCircle(cx, cy, radius * (1.03f + i * 0.023f));
        }
        DrawOuterRing(canvas, cx, cy, radius);
        for (int latitude = 1; latitude < 29; latitude++)
            DrawMeshLine(canvas, cx, cy, radius, MathF.PI * latitude / 29f, true, phase, activity);
        for (int longitude = 0; longitude < 52; longitude++)
            DrawMeshLine(canvas, cx, cy, radius, MathF.PI * 2f * longitude / 52f, false, phase, activity);
        for (int i = 0; i < 80; i++)
        {
            float angle = i * 2.399963f + Time * 0.045f;
            float distance = radius * (1.15f + 0.25f * (0.5f + 0.5f * MathF.Sin(i * 7.13f)));
            float x = cx + MathF.Cos(angle) * distance, y = cy + MathF.Sin(angle) * distance;
            float alpha = 0.12f + 0.4f * (0.5f + 0.5f * MathF.Sin(Time * 1.6f + i));
            canvas.FillColor = MeshColor(x, cx, radius, alpha);
            canvas.FillCircle(x, y, i % 6 == 0 ? 1.2f : 0.6f);
        }
        canvas.RestoreState();
    }
    void DrawOuterRing(ICanvas canvas, float cx, float cy, float radius)
    {
        float ringRadius = radius * 1.39f;
        canvas.StrokeColor = new Color(0.15f, 0.8f, 1f, 0.23f); canvas.StrokeSize = 0.7f;
        for (int section = 0; section < 4; section++)
        {
            float start = section * MathF.PI / 2f + Time * 0.035f;
            PointF previous = default;
            for (int step = 0; step <= 32; step++)
            {
                float angle = start + step / 32f * MathF.PI * 0.36f;
                var point = new PointF(cx + MathF.Cos(angle) * ringRadius, cy + MathF.Sin(angle) * ringRadius);
                if (step > 0) canvas.DrawLine(previous.X, previous.Y, point.X, point.Y);
                previous = point;
            }
        }
    }
    void DrawMeshLine(ICanvas canvas, float cx, float cy, float radius, float fixedAngle, bool latitude, float phase, float activity)
    {
        PointF previous = default;
        for (int step = 0; step <= 100; step++)
        {
            float fraction = step / 100f;
            float theta = latitude ? fixedAngle : MathF.PI * fraction;
            float phi = latitude ? MathF.PI * 2f * fraction : fixedAngle;
            PointF point = Project(theta, phi, cx, cy, radius, phase, activity, out float depth);
            if (step > 0)
            {
                float alpha = 0.13f + 0.7f * Math.Clamp((depth + 1f) * 0.5f, 0f, 1f);
                canvas.StrokeColor = MeshColor(point.X, cx, radius, alpha);
                canvas.StrokeSize = depth > 0f ? 0.85f : 0.45f;
                canvas.DrawLine(previous.X, previous.Y, point.X, point.Y);
            }
            previous = point;
        }
    }
    PointF Project(float theta, float phi, float cx, float cy, float radius, float phase, float activity, out float depth)
    {
        float deformation = 1f + activity * MathF.Sin(5f * theta + phase) * MathF.Sin(4f * phi - phase * 0.7f)
            + activity * 0.45f * MathF.Sin(7f * phi + 3f * theta + phase);
        float x = MathF.Sin(theta) * MathF.Cos(phi) * deformation;
        float y = MathF.Cos(theta) * deformation;
        float z = MathF.Sin(theta) * MathF.Sin(phi) * deformation;
        float rotation = Time * 0.13f;
        float rotatedX = x * MathF.Cos(rotation) + z * MathF.Sin(rotation);
        float rotatedZ = -x * MathF.Sin(rotation) + z * MathF.Cos(rotation);
        const float tilt = 0.3f;
        float tiltedY = y * MathF.Cos(tilt) - rotatedZ * MathF.Sin(tilt);
        depth = y * MathF.Sin(tilt) + rotatedZ * MathF.Cos(tilt);
        return new PointF(cx + rotatedX * radius, cy + tiltedY * radius);
    }
    static Color MeshColor(float x, float cx, float radius, float alpha)
    {
        float blend = Math.Clamp((x - cx + radius) / (2f * radius), 0f, 1f);
        return new Color(0.08f + 0.46f * blend, 0.58f + 0.38f * blend, 1f, alpha);
    }
}

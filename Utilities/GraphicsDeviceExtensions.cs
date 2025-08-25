using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace SpaceTrafficController.Utilities;

public static class GraphicsDeviceExtensions
{
    public static void DrawRing(this GraphicsDevice graphicsDevice, Vector2 center, float innerRadius, float outerRadius, int segments, Color color, BasicEffect effect)
    {
        VertexPositionColor[] vertices = new VertexPositionColor[segments * 6]; // 2 triangles per segment
        float angleStep = MathF.Tau / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * angleStep;
            float angle2 = (i + 1) * angleStep;

            Vector2 outer1 = center + new Vector2(MathF.Cos(angle1), MathF.Sin(angle1)) * outerRadius;
            Vector2 outer2 = center + new Vector2(MathF.Cos(angle2), MathF.Sin(angle2)) * outerRadius;
            Vector2 inner1 = center + new Vector2(MathF.Cos(angle1), MathF.Sin(angle1)) * innerRadius;
            Vector2 inner2 = center + new Vector2(MathF.Cos(angle2), MathF.Sin(angle2)) * innerRadius;

            int index = i * 6;

            // Triangle 1
            vertices[index] = new VertexPositionColor(new Vector3(inner1, 0), color);
            vertices[index + 1] = new VertexPositionColor(new Vector3(outer1, 0), color);
            vertices[index + 2] = new VertexPositionColor(new Vector3(outer2, 0), color);

            // Triangle 2
            vertices[index + 3] = new VertexPositionColor(new Vector3(inner1, 0), color);
            vertices[index + 4] = new VertexPositionColor(new Vector3(outer2, 0), color);
            vertices[index + 5] = new VertexPositionColor(new Vector3(inner2, 0), color);
        }

        // Apply the effect
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, segments * 2);
        }
    }
}

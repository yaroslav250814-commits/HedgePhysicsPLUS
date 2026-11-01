using UnityEngine;
using UnityEngine.UI;

// Apply before the outline/shadow so the lettering keeps its dark edge.
public sealed class AdventureTextGradient : BaseMeshEffect
{
    public Color Top = new Color32(255, 241, 100, 255);
    public Color Bottom = new Color32(255, 65, 0, 255);
    Gradient horizontalColors;

    public void SetPalette(Color top, Color bottom, Gradient horizontal = null)
    {
        Top = top; Bottom = bottom; horizontalColors = horizontal;
        if (graphic) graphic.SetVerticesDirty();
    }
    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive()) return;
        var vertex = new UIVertex();
        float left = float.MaxValue, right = float.MinValue;
        if (horizontalColors != null)
        {
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                left = Mathf.Min(left, vertex.position.x); right = Mathf.Max(right, vertex.position.x);
            }
        }
        for (int first = 0; first + 3 < vertices.currentVertCount; first += 4)
        {
            float low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < 4; i++) { vertices.PopulateUIVertex(ref vertex, first + i); low = Mathf.Min(low, vertex.position.y); high = Mathf.Max(high, vertex.position.y); }
            for (int i = 0; i < 4; i++)
            {
                vertices.PopulateUIVertex(ref vertex, first + i);
                float height = Mathf.InverseLerp(low, high, vertex.position.y);
                if (horizontalColors == null) vertex.color = Color.Lerp(Bottom, Top, height);
                else
                {
                    var color = horizontalColors.Evaluate(Mathf.InverseLerp(left, right, vertex.position.x));
                    vertex.color = Color.Lerp(color, Color.white, height * .38f);
                }
                vertices.SetUIVertex(vertex, first + i);
            }
        }
    }
}

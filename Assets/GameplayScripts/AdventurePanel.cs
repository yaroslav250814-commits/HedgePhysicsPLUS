using UnityEngine;
using UnityEngine.UI;

// Bevelled, slightly slanted panels keep feedback legible over bright particles.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class AdventurePanel : MaskableGraphic
{
    public Color Top = new Color32(43, 67, 156, 240);
    public Color Bottom = new Color32(16, 20, 68, 245);
    public Color Border = new Color32(146, 164, 221, 255);
    public float Slant = 8;
    public bool StraightFrame;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); var r = rectTransform.rect;
        if (StraightFrame)
        {
            var inside = new Rect(r.xMin + 3, r.yMin + 3, r.width - 6, r.height - 6);
            FrameBorder(mesh, r, inside);
            Frame(mesh, inside, Bottom, Top, 3);
            return;
        }
        Quad(mesh, r.xMin + 4, r.yMin - 5, r.xMax + 4, r.yMax - 5, new Color32(0, 3, 20, 210), new Color32(0, 3, 20, 210));
        Quad(mesh, r.xMin, r.yMin, r.xMax, r.yMax, Border, Border);
        Quad(mesh, r.xMin + 3, r.yMin + 3, r.xMax - 3, r.yMax - 3, Bottom, Top);
        Quad(mesh, r.xMin + 5, r.yMax - 6, r.xMax - 5, r.yMax - 4, new Color(1, 1, 1, .25f), new Color(1, 1, 1, .25f));
    }
    void FrameBorder(VertexHelper mesh, Rect outside, Rect inside)
    {
        var outer = Corners(outside, 5); var inner = Corners(inside, 3);
        int start = mesh.currentVertCount;
        for (int i = 0; i < 8; i++) { mesh.AddVert(outer[i], Border, Vector2.zero); mesh.AddVert(inner[i], Border, Vector2.zero); }
        for (int i = 0; i < 8; i++)
        {
            int a = start + i * 2, b = start + ((i + 1) % 8) * 2;
            mesh.AddTriangle(a, b, a + 1); mesh.AddTriangle(b, b + 1, a + 1);
        }
    }
    static Vector2[] Corners(Rect r, float radius) => new[] {
        new Vector2(r.xMin + radius, r.yMin), new Vector2(r.xMin, r.yMin + radius),
        new Vector2(r.xMin, r.yMax - radius), new Vector2(r.xMin + radius, r.yMax),
        new Vector2(r.xMax - radius, r.yMax), new Vector2(r.xMax, r.yMax - radius),
        new Vector2(r.xMax, r.yMin + radius), new Vector2(r.xMax - radius, r.yMin) };
    static void Frame(VertexHelper mesh, Rect r, Color low, Color high, float radius)
    {
        int start = mesh.currentVertCount;
        Vector2[] points = Corners(r, radius);
        mesh.AddVert(r.center, Color.Lerp(low, high, .5f), Vector2.zero);
        foreach (var point in points) mesh.AddVert(point, Color.Lerp(low, high, Mathf.InverseLerp(r.yMin, r.yMax, point.y)), Vector2.zero);
        for (int i = 0; i < points.Length; i++) mesh.AddTriangle(start, start + i + 1, start + (i + 1) % points.Length + 1);
    }
    void Quad(VertexHelper mesh, float left, float bottom, float right, float top, Color low, Color high)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(new Vector3(left, bottom), low, Vector2.zero);
        mesh.AddVert(new Vector3(left + Slant, top), high, Vector2.up);
        mesh.AddVert(new Vector3(right, top), high, Vector2.one);
        mesh.AddVert(new Vector3(right - Slant, bottom), low, Vector2.right);
        mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
    }
}

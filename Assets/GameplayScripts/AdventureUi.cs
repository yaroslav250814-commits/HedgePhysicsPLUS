using UnityEngine;
using UnityEngine.UI;

public static class AdventureUi
{
    public static readonly Color Gold = new Color32(255, 201, 45, 255);
    public static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    public static AdventurePanel Panel(string name, Transform parent, Vector2 size, Vector2 position)
    { var panel = Rect(name, parent, size, position).gameObject.AddComponent<AdventurePanel>(); panel.raycastTarget = false; return panel; }
    public static Text Label(string name, Transform parent, Font font, string value, int size, Color color, Vector2 position, Vector2 dimensions)
    {
        var rect = Rect(name, parent, dimensions, position);
        var text = rect.gameObject.AddComponent<Text>(); text.font = font; text.fontSize = size;
        text.text = value; text.color = color; text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var shadow = rect.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color32(0, 3, 22, 255); shadow.effectDistance = new Vector2(3, -4);
        var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = new Color32(0, 3, 22, 255); outline.effectDistance = new Vector2(1.3f, -1.3f);
        return text;
    }
}

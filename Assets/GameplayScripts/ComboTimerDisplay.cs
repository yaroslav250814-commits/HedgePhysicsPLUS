using UnityEngine;
using UnityEngine.UI;

public sealed class ComboTimerDisplay : MonoBehaviour
{
    public Text Counter { get; private set; }
    public Image TimeBar { get; private set; }
    TechniqueChain chain;
    int displayedHits = -1;

    public static ComboTimerDisplay Create(LevelRunState owner, TechniqueChain chain)
    {
        if (!owner || !owner.ScoreLabel) return null;
        var root = TopLeft("Combo timer", owner.ScoreLabel.canvas.rootCanvas.transform, new Vector2(248, 24), new Vector2(22, -162));
        var display = root.gameObject.AddComponent<ComboTimerDisplay>(); display.chain = chain;
        var group = root.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
        var label = TopLeft("Combo count", root, new Vector2(128, 24), Vector2.zero);
        display.Counter = label.gameObject.AddComponent<Text>();
        display.Counter.font = owner.ScoreLabel.font; display.Counter.fontSize = 18;
        display.Counter.alignment = TextAnchor.MiddleLeft; display.Counter.horizontalOverflow = HorizontalWrapMode.Overflow;
        display.Counter.raycastTarget = false;
        var outline = label.gameObject.AddComponent<Outline>(); outline.effectColor = Color.black; outline.effectDistance = new Vector2(1, -1);
        var background = TopLeft("Time background", root, new Vector2(116, 7), new Vector2(130, -9));
        var image = background.gameObject.AddComponent<Image>(); image.color = new Color32(0, 0, 0, 210); image.raycastTarget = false;
        var fill = TopLeft("Time remaining", background, new Vector2(112, 3), new Vector2(2, -2));
        display.TimeBar = fill.gameObject.AddComponent<Image>(); display.TimeBar.color = AdventureUi.Gold;
        display.TimeBar.raycastTarget = false;
        display.Refresh(); return display;
    }

    static RectTransform TopLeft(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = AdventureUi.Rect(name, parent, size, position);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); return rect;
    }

    void LateUpdate() { Refresh(); }

    void Refresh()
    {
        if (!chain || chain.Hits == 0 || !chain.ShowTimer) { Hide(); return; }
        if (displayedHits != chain.Hits) { Counter.text = "COMBO " + chain.Hits; displayedHits = chain.Hits; }
        TimeBar.rectTransform.sizeDelta = new Vector2(112 * chain.RemainingFraction, 3);
        TimeBar.color = chain.RemainingFraction <= .25f ? new Color32(255, 82, 45, 255) : AdventureUi.Gold;
    }

    public void Hide() { gameObject.SetActive(false); }
}

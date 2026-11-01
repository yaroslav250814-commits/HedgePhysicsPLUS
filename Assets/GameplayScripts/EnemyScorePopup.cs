using UnityEngine;
using UnityEngine.UI;

public sealed class EnemyScorePopup : MonoBehaviour
{
    public Text Caption { get; private set; }
    public int DisplayedPoints { get; private set; }
    public bool IsLive { get; private set; }
    LevelRunState owner;
    RectTransform rect;
    CanvasGroup visibility;
    float age;

    static EnemyScorePopup Find(LevelRunState run)
    {
        if (!run || !run.ScoreLabel) return null;
        foreach (var popup in run.ScoreLabel.canvas.rootCanvas.GetComponentsInChildren<EnemyScorePopup>())
            if (popup.owner == run) return popup;
        return null;
    }

    public static void Show(LevelRunState run, int points, int hits, bool live)
    {
        if (!run || !run.ScoreLabel || hits < 2) return;
        var popup = Find(run);
        if (!popup) popup = Create(run);
        popup.age = 0; popup.IsLive = live; popup.DisplayedPoints = live && hits >= 2 ? TechniqueChain.BonusFor(hits) : points;
        popup.Caption.text = TechniqueChain.NameFor(hits);
        TechniquePraisePalette.Apply(popup.Caption.GetComponent<AdventureTextGradient>(), popup.Caption.text);
    }

    static EnemyScorePopup Create(LevelRunState run)
    {
        var canvas = run.ScoreLabel.canvas.rootCanvas;
        var rect = AdventureUi.Rect("Adventure score feedback", canvas.transform, new Vector2(270, 38), Vector2.zero);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(22, -120);
        var popup = rect.gameObject.AddComponent<EnemyScorePopup>();
        popup.rect = rect; popup.owner = run;
        popup.visibility = rect.gameObject.AddComponent<CanvasGroup>();
        popup.visibility.blocksRaycasts = false; popup.visibility.interactable = false;
        var font = run.ScoreLabel.font;
        var label = AdventureUi.Rect("Technique", rect, new Vector2(260, 38), Vector2.zero);
        label.anchorMin = label.anchorMax = label.pivot = new Vector2(0, 1);
        popup.Caption = label.gameObject.AddComponent<Text>(); popup.Caption.font = font;
        popup.Caption.fontSize = 24; popup.Caption.fontStyle = FontStyle.Italic;
        popup.Caption.alignment = TextAnchor.MiddleLeft; popup.Caption.raycastTarget = false;
        popup.Caption.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.gameObject.AddComponent<AdventureTextGradient>();
        var shadow = label.gameObject.AddComponent<Shadow>(); shadow.effectColor = Color.black; shadow.effectDistance = new Vector2(1.5f, -2);
        var outline = label.gameObject.AddComponent<Outline>(); outline.effectColor = Color.black; outline.effectDistance = new Vector2(.8f, -.8f);
        label.localScale = new Vector3(1.2f, 1, 1);
        return popup;
    }

    public static void EndChain(LevelRunState run) { var popup = Find(run); if (popup) popup.IsLive = false; }
    public static void Hide(LevelRunState run) { var popup = Find(run); if (popup) { popup.gameObject.SetActive(false); Destroy(popup.gameObject); } }

    void Update()
    {
        if (Time.deltaTime <= 0) return;
        age += Time.deltaTime;
        if (age > 1.3f) { Destroy(gameObject); return; }
        float slide = 1 - Mathf.Pow(1 - Mathf.Clamp01(age / .18f), 3);
        rect.anchoredPosition = new Vector2(Mathf.Lerp(10, 22, slide), -120);
        rect.localScale = Vector3.one * Mathf.Lerp(1.13f, 1, slide);
        visibility.alpha = Mathf.Clamp01((1.3f - age) / .2f);
    }
}

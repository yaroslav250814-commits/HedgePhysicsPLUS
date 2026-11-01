using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class AdventurePresentationSetup
{
    public static void Install()
    {
        const string path = "Assets/StageCompleteScreen.unity";
        Directory.CreateDirectory("Migration/AdventurePresentation/Before");
        string backup = "Migration/AdventurePresentation/Before/StageCompleteScreen.unity";
        if (!File.Exists(backup)) File.Copy(path, backup);
        EditorSceneManager.OpenScene(path);
        ConfigureResults(); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        File.WriteAllText("Migration/AdventurePresentation/setup.txt", "SA2-style technique feedback and stage results installed; no rank UI.");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    public static void ConfigureResults()
    {
        var controller = Object.FindFirstObjectByType<StageConpleteControl>();
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas.name == "ClassicResults" || canvas.name == "AdventureResults") Object.DestroyImmediate(canvas.gameObject);
            else canvas.enabled = false;
        }
        foreach (var animator in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None)) animator.enabled = false;
        if (!controller) controller = new GameObject("Stage results controller").AddComponent<StageConpleteControl>();
        controller.enabled = true;
        var root = new GameObject("AdventureResults", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.layer = 5; root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Design/kimberley bl.ttf");
        var backdrop = AdventureUi.Rect("Stage clear view", root.transform, Vector2.zero, Vector2.zero);
        backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one; backdrop.sizeDelta = Vector2.zero;
        controller.Backdrop = backdrop.gameObject.AddComponent<RawImage>();
        controller.Backdrop.raycastTarget = false; controller.Backdrop.color = Color.white;
        var shade = AdventureUi.Rect("Screen shade", root.transform, Vector2.zero, Vector2.zero);
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.sizeDelta = Vector2.zero;
        var image = shade.gameObject.AddComponent<Image>(); image.color = new Color32(0, 0, 0, 72); image.raycastTarget = false;
        var panel = AdventureUi.Rect("Mission cleared presentation", root.transform, new Vector2(760, 580), Vector2.zero);
        controller.Panel = panel;
        controller.StageTitle = AdventureUi.Label("Mission cleared", panel, font, "1st MISSION CLEARED!", 31, Color.white, new Vector2(0, 210), new Vector2(570, 50));
        controller.StageTitle.alignment = TextAnchor.MiddleCenter;
        controller.StageTitle.rectTransform.localScale = new Vector3(1.25f, 1, 1);
        controller.StageTitle.GetComponent<Shadow>().effectDistance = new Vector2(1, -1.5f);
        controller.StageSubtitle = null;
        var stats = ResultPanel("Stage statistics", panel, new Vector2(560, 140), new Vector2(0, 111));
        controller.StatisticsGroup = stats.gameObject.AddComponent<CanvasGroup>();
        controller.ScoreValue = Row(stats.transform, font, "SCORE", 43, false, out _);
        controller.ClearTimeValue = Row(stats.transform, font, "TIME", 0, false, out _);
        controller.RingCountValue = Row(stats.transform, font, "RINGS", -43, false, out _);
        var bank = AdventureUi.Rect("Total rings strip", panel, new Vector2(410, 30), new Vector2(75, 20));
        bank.gameObject.AddComponent<Image>().color = new Color32(197, 125, 28, 145);
        controller.BankGroup = bank.gameObject.AddComponent<CanvasGroup>();
        var bankCaption = AdventureUi.Label("TOTAL RINGS caption", bank, font, "TOTAL RINGS", 20, Color.white, new Vector2(-60, 0), new Vector2(270, 30));
        bankCaption.rectTransform.localScale = new Vector3(1.18f, 1, 1);
        controller.TotalRingsValue = AdventureUi.Label("Total rings value", bank, font, "0", 23, Color.white, new Vector2(155, 0), new Vector2(86, 30));
        controller.TotalRingsValue.alignment = TextAnchor.MiddleRight;
        var tally = ResultPanel("Score tally", panel, new Vector2(560, 110), new Vector2(0, -75));
        controller.TallyGroup = tally.gameObject.AddComponent<CanvasGroup>();
        controller.TimeValue = Row(tally.transform, font, "TIME BONUS", 25, true, out var bonusCaption);
        controller.BonusCaption = bonusCaption;
        controller.TotalValue = Row(tally.transform, font, "TOTAL SCORE", -25, true, out _);
        controller.RingsValue = null;
        var prompt = AdventureUi.Label("Continue", panel, font, "ENTER / A  -  SKIP COUNT", 15, Color.white, new Vector2(0, -195), new Vector2(410, 30));
        prompt.alignment = TextAnchor.MiddleCenter; prompt.raycastTarget = true;
        controller.Prompt = prompt; controller.ContinueButton = prompt.gameObject.AddComponent<Button>(); controller.ContinueButton.targetGraphic = prompt;
        if (!Object.FindFirstObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        EditorUtility.SetDirty(controller);
    }
    static AdventurePanel ResultPanel(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var panel = AdventureUi.Panel(name, parent, size, position); panel.StraightFrame = true; panel.Slant = 0;
        panel.Top = new Color32(41, 0, 200, 172); panel.Bottom = new Color32(24, 4, 144, 170);
        panel.Border = new Color32(191, 207, 255, 255); return panel;
    }
    static Text Row(Transform parent, Font font, string caption, float y, bool orange, out Text label)
    {
        label = AdventureUi.Label(caption + " caption", parent, font, caption, 26, Color.white, new Vector2(-245, y), new Vector2(270, 40));
        label.rectTransform.pivot = new Vector2(0, .5f); label.rectTransform.localScale = new Vector3(1.2f, 1, 1);
        label.GetComponent<Shadow>().effectDistance = new Vector2(1, -1);
        if (orange)
        {
            // Keep gradient ahead of the outline effects in component order.
            var gradient = label.gameObject.AddComponent<AdventureTextGradient>();
            UnityEditorInternal.ComponentUtility.MoveComponentUp(gradient); UnityEditorInternal.ComponentUtility.MoveComponentUp(gradient);
        }
        var value = AdventureUi.Label(caption + " value", parent, font, "0", 29, Color.white, new Vector2(245, y), new Vector2(235, 40));
        value.rectTransform.pivot = new Vector2(1, .5f); value.rectTransform.localScale = new Vector3(1.2f, 1, 1);
        value.GetComponent<Shadow>().effectDistance = new Vector2(1, -1);
        value.alignment = TextAnchor.MiddleRight; return value;
    }
}

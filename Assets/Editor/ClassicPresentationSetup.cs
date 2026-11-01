using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class ClassicPresentationSetup
{
    static Font Face => AssetDatabase.LoadAssetAtPath<Font>("Assets/Design/kimberley bl.ttf");
    static readonly Color Gold = new Color32(255, 212, 59, 255);

    public static void Install()
    {
        Directory.CreateDirectory("Migration/ClassicPresentation/Before");
        var paths = AssetDatabase.FindAssets("t:Prefab", new[]{"Assets"}).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".prefab") && !p.StartsWith("Assets/Editor/")).ToArray();
        foreach (string path in paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = ApplyFont(root);
                if (path == "Assets/Player/Player_Sonic.prefab" || path == "Assets/Prototyping/ProtoPrefabs/PlayerObjects.prefab")
                {
                    root.GetComponentInChildren<PlayerBinput>(true).SteeringResponsiveness = .45f;
                    root.GetComponentInChildren<Action00_Regular>(true).skinRotationSpeed = 12;
                    root.GetComponentInChildren<Action01_Jump>(true).skinRotationSpeed = 10;
                    root.GetComponentInChildren<Action02_Homing>(true).skinRotationSpeed = 35;
                    changed = true;
                }
                if (path == "Assets/Prototyping/ProtoPrefabs/PlayerObjects.prefab")
                {
                    var interaction = root.GetComponentInChildren<Objects_Interaction>(true);
                    ConfigureHud(root.GetComponentInChildren<PauseCotrol>(true).GetComponent<Canvas>(),
                        interaction.GetComponent<LevelRunState>(), interaction);
                    changed = true;
                }
                if (changed)
                {
                    Backup(path);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            Backup(entry.path);
            var scene = EditorSceneManager.OpenScene(entry.path);
            if (entry.path == "Assets/StageCompleteScreen.unity") ConfigureResults();
            foreach (var root in scene.GetRootGameObjects()) ApplyFont(root);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("Migration/ClassicPresentation/setup.txt", "Classic HUD, enemy popups, time/ring tally, Kimberley font and slower steering installed.");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void Backup(string path)
    {
        string target = "Migration/ClassicPresentation/Before/" + path;
        if (File.Exists(target)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(path, target);
    }

    static bool ApplyFont(GameObject root)
    {
        bool changed = false;
        foreach (var text in root.GetComponentsInChildren<Text>(true))
            if (text.font != Face || text.fontStyle != FontStyle.Normal)
            { text.font = Face; text.fontStyle = FontStyle.Normal; EditorUtility.SetDirty(text); changed = true; }
        foreach (var text in root.GetComponentsInChildren<TextMesh>(true))
            if (text.font != Face)
            { text.font = Face; text.GetComponent<Renderer>().sharedMaterial = Face.material; EditorUtility.SetDirty(text); changed = true; }
        return changed;
    }

    public static void ConfigureHud(Canvas canvas, LevelRunState run, Objects_Interaction interaction)
    {
        var group = canvas.transform.Find("ScoreAndTime") as RectTransform;
        if (!group) group = Rect("ScoreAndTime", canvas.transform, new Vector2(260, 96));
        group.anchorMin = group.anchorMax = group.pivot = new Vector2(0, 1);
        group.anchoredPosition = new Vector2(22, -18); group.sizeDelta = new Vector2(270, 96);
        run.ScoreLabel = HudLabel(group, "Score", 112, 0, "0", Color.white);
        run.TimeLabel = HudLabel(group, "Time", 112, -30, "0:00", Color.white);
        HudLabel(group, "ScoreCaption", 0, 0, "SCORE", Gold);
        HudLabel(group, "TimeCaption", 0, -30, "TIME", Gold);
        HudLabel(group, "RingsCaption", 0, -60, "RINGS", Gold);
        var rings = interaction.RingsCounter;
        var oldParent = rings.transform.parent;
        rings.transform.SetParent(group, false); rings.name = "RingsValue";
        StyleHud(rings, 112, -60); rings.text = "0";
        if (oldParent && oldParent != group && oldParent != canvas.transform) oldParent.gameObject.SetActive(false);
        EditorUtility.SetDirty(run); EditorUtility.SetDirty(interaction);
    }

    static Text HudLabel(RectTransform group, string name, float x, float y, string value, Color color)
    {
        var existing = group.Find(name);
        var text = existing ? existing.GetComponent<Text>() : Label(name, group, value, 24, color);
        text.text = value; StyleHud(text, x, y); text.color = color;
        return text;
    }
    static void StyleHud(Text text, float x, float y)
    {
        text.font = Face; text.fontStyle = FontStyle.Normal; text.fontSize = 24;
        text.color = Color.white; text.alignment = TextAnchor.MiddleLeft;
        text.supportRichText = false; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(155, 30);
        var shadow = text.GetComponent<Shadow>(); if (!shadow) shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = Color.black; shadow.effectDistance = new Vector2(2, -2);
    }

    static void ConfigureResults()
    {
        AdventurePresentationSetup.ConfigureResults();
    }

    static Text Row(RectTransform panel, string caption, float y)
    {
        var label = Label(caption + " caption", panel, caption, 28, Gold);
        Place(label, -120, y, 340, 40); label.alignment = TextAnchor.MiddleLeft;
        var value = Label(caption + " value", panel, "0", 28, Color.white);
        Place(value, 190, y, 180, 40); value.alignment = TextAnchor.MiddleRight;
        return value;
    }
    static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false); rect.sizeDelta = size; return rect;
    }
    static Text Label(string name, Transform parent, string value, int size, Color color)
    {
        var rect = Rect(name, parent, new Vector2(400, 50));
        var text = rect.gameObject.AddComponent<Text>(); text.font = Face; text.fontSize = size;
        text.text = value; text.color = color; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        var shadow = rect.gameObject.AddComponent<Shadow>(); shadow.effectColor = Color.black; shadow.effectDistance = new Vector2(2, -2);
        return text;
    }
    static void Place(Text text, float x, float y, float width, float height)
    { text.rectTransform.anchoredPosition = new Vector2(x, y); text.rectTransform.sizeDelta = new Vector2(width, height); }
}

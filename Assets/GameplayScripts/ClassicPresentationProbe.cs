#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ClassicPresentationProbe : MonoBehaviour
{
    readonly List<string> report = new List<string>();
    int failures;
    float started;
    Keyboard keyboard;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (!File.Exists("Migration/classic-test.request")) return;
        File.Delete("Migration/classic-test.request");
        var go = new GameObject("Classic presentation regression");
        DontDestroyOnLoad(go); go.AddComponent<ClassicPresentationProbe>();
    }
    void OnEnable() { Application.logMessageReceived += Log; }
    void OnDisable() { Application.logMessageReceived -= Log; }
    void Log(string message, string stack, LogType type)
    { if (type == LogType.Exception || type == LogType.Error) Check(false, message + "\n" + stack); }
    void Update() { if (started > 0 && Time.realtimeSinceStartup - started > 65) { Check(false, "Timeout"); Finish(); } }
    IEnumerator Start()
    {
        started = Time.realtimeSinceStartup; Application.runInBackground = true; Time.timeScale = 1;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        yield return new WaitForSeconds(.8f);
        var run = LevelRunState.Current; var player = run.GetComponent<PlayerBhysics>();
        var input = run.GetComponent<PlayerBinput>(); var interaction = run.GetComponent<Objects_Interaction>();
        var progress = run.GetComponent<LevelProgressControl>();
        Check(run.Score == 0 && Objects_Interaction.RingAmmount == 0, "Fresh campaign starts at zero");
        Check(run.ScoreLabel.font.name.ToLowerInvariant().Contains("kimberley"), "HUD uses project Kimberley font");
        Check(run.ScoreLabel.transform.parent == interaction.RingsCounter.transform.parent &&
            run.ScoreLabel.rectTransform.anchoredPosition.y > run.TimeLabel.rectTransform.anchoredPosition.y &&
            run.TimeLabel.rectTransform.anchoredPosition.y > interaction.RingsCounter.rectTransform.anchoredPosition.y,
            "HUD order is SCORE, TIME, RINGS in one left column");
        Check(((RectTransform)run.ScoreLabel.transform.parent).anchorMin == new Vector2(0, 1), "HUD is anchored at top left");
        Check(LevelRunState.FormatClassicTime(61.9f) == "1:01", "Classic timer displays minutes and seconds");

        // Drive the real input component with a virtual keyboard, without touching Windows input.
        player.enabled = false; player.Body.linearVelocity = Vector3.zero; player.Body.isKinematic = true;
        run.GetComponent<CameraControl>().Cam.enabled = false;
        keyboard = InputSystem.AddDevice<Keyboard>();
        input.SteeringResponsiveness = 1;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); yield return new WaitForSeconds(.65f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D)); yield return new WaitForSeconds(.1f);
        float fastError = Vector3.Angle(player.MoveInput, player.RawInput);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSeconds(.4f);
        input.SteeringResponsiveness = .45f;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); yield return new WaitForSeconds(.65f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D)); yield return new WaitForSeconds(.1f);
        float slowError = Vector3.Angle(player.MoveInput, player.RawInput);
        Check(slowError > fastError + 5, "Real 90-degree steering response is slower: remaining angle " + fastError.ToString("F1") + " -> " + slowError.ToString("F1"));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); input.enabled = false;
        Check(run.GetComponent<Action00_Regular>().skinRotationSpeed == 12, "Model turn smoothing is reduced from 35 to 12");

        float[] times = { 0, 29.99f, 30, 44.99f, 45, 60, 90, 120, 180, 240, 299.99f, 300, 600 };
        int[] bonuses = { 50000, 50000, 10000, 10000, 5000, 4000, 3000, 2000, 1000, 500, 500, 0, 0 };
        Check(times.Select((t, i) => ClassicScore.TimeBonus(t) == bonuses[i]).All(v => v), "Time bonuses match all Sonic 1 boundaries");
        Check(ClassicScore.RingBonus(9) == 900 && ClassicScore.RingBonus(0) == 0, "Retained rings give 100 points each at results");
        var ring = GameObject.FindGameObjectsWithTag("Ring").First(r => r.GetComponent<Collider>());
        interaction.OnTriggerEnter(ring.GetComponent<Collider>());
        Check(Objects_Interaction.RingAmmount == 1 && run.Score == 0, "Ring pickup does not add immediate score");
        var giver = Instantiate(run.GetComponent<Monitors_Interactions>().RingGiver).GetComponent<RingGiverControl>();
        giver.Duration = 0; giver.Rings = 10;
        yield return new WaitForSeconds(.12f);
        Check(Objects_Interaction.RingAmmount == 11 && run.Score == 0, "Ring monitor does not add immediate score");

        player.Grounded = true; run.GetComponent<ActionManager>().ChangeAction(0);
        var fixture = new GameObject("Enemy popup fixture"); fixture.transform.position = player.transform.position + player.transform.forward * 3;
        var enemy = fixture.AddComponent<EnemyHealth>(); enemy.Explosion = new GameObject("Hidden test explosion"); enemy.Explosion.SetActive(false);
        enemy.DealDamage(1); enemy.DealDamage(1);
        yield return null;
        var popup = FindFirstObjectByType<EnemyScorePopup>();
        Check(run.Score == 100 && !popup, "Single enemy adds score without an invented ENEMY card");
        Capture(run.ScoreLabel.canvas.rootCanvas, "hud.png");
        yield return new WaitForSeconds(1.8f);
        Check(!FindFirstObjectByType<EnemyScorePopup>(), "Enemy popup expires");

        Objects_Interaction.RingAmmount = 9; progress.LevelToGoNext = 4;
        progress.NextLevelNameLeft = "Altitude"; progress.NextLevelNameRight = "Limit";
        var goal = GameObject.FindGameObjectsWithTag("GoalRing").First(g => g.GetComponent<Collider>());
        progress.OnTriggerEnter(goal.GetComponent<Collider>());
        var result = ClassicScore.Result;
        Check(run.Completed && result != null && result.Rings == 9 && result.Score == 100 && result.Total == 51000,
            "Goal snapshots score + 50000 time bonus + 900 ring bonus");
        Objects_Interaction.RingAmmount = 99; run.CompleteStage();
        Check(ClassicScore.Result == result && result.Rings == 9, "Repeated goal calls cannot duplicate or change the result");
        for (int i = 0; i < 300 && SceneManager.GetActiveScene().name != "StageCompleteScreen"; i++) yield return new WaitForSeconds(.02f);
        Check(SceneManager.GetActiveScene().name == "StageCompleteScreen", "Real goal transition opens results scene");
        yield return new WaitForSeconds(2.3f);
        var tally = FindFirstObjectByType<StageConpleteControl>();
        Check(tally && tally.ClearTimeValue.text.Contains(":") && tally.RingCountValue.text == "9 / " + result.MapRings, "Results show clear time and rings collected / total");
        Check(tally && tally.TotalRingsValue.text == "9", "TOTAL RINGS strip counts the banked rings");
        Check(tally && tally.Backdrop.texture, "Results retain a rendered view of the completed level");
        Check(!FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t => t.text.ToUpperInvariant().Contains("RANK")), "Results contain no rank");
        Check(tally && tally.DisplayedTotal > 100 && tally.DisplayedTotal < result.Total, "Bonuses visibly count into total over time");
        Check(FindObjectsByType<Text>(FindObjectsSortMode.None).All(t => t.font && t.font.name.ToLowerInvariant().Contains("kimberley")), "All result text uses project font");
        Capture(tally.ScoreValue.canvas.rootCanvas, "results-counting.png");
        tally.ContinueButton.onClick.Invoke();
        Check(tally.Finished && tally.DisplayedTotal == result.Total && ClassicScore.Total == result.Total, "Click finishes tally and commits total");
        tally.Confirm();
        Check(SceneManager.GetActiveScene().name == "StageCompleteScreen", "The skip press cannot also dismiss results");
        ClassicScore.Commit(result); Check(ClassicScore.Total == result.Total, "Bonus commit is idempotent");
        Check(ClassicScore.TotalRings == 9, "Repeated commit cannot bank the rings twice");
        Capture(tally.ScoreValue.canvas.rootCanvas, "results-total.png");
        yield return new WaitForSeconds(.4f); tally.ContinueButton.onClick.Invoke();
        for (int i = 0; i < 500 && (!LevelRunState.Current || LevelRunState.Current.gameObject.scene.buildIndex != 4); i++) yield return new WaitForSeconds(.02f);
        Check(LevelRunState.Current && LevelRunState.Current.gameObject.scene.buildIndex == 4, "Continue loads the configured next stage");
        Check(LevelRunState.Current && LevelRunState.Current.Score == result.Total && Objects_Interaction.RingAmmount == 0, "Next stage retains total score and starts with zero rings");
        Finish();
    }
    void Check(bool ok, string message) { report.Add((ok ? "PASS " : "FAIL ") + message); if (!ok) failures++; }
    void Finish()
    {
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        Directory.CreateDirectory("Migration/ClassicPresentation"); report.Add("Failures=" + failures);
        File.WriteAllLines("Migration/ClassicPresentation/tests.txt", report); EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
    static void Capture(Canvas canvas, string file)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var camera = Camera.main ? Camera.main : FindFirstObjectByType<Camera>();
        var mode = canvas.renderMode; var world = canvas.worldCamera; float distance = canvas.planeDistance;
        var target = RenderTexture.GetTemporary(1280, 720, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .5f;
        camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
        File.WriteAllBytes("Migration/ClassicPresentation/" + file, image.EncodeToPNG());
        camera.targetTexture = previous; RenderTexture.active = active; canvas.renderMode = mode; canvas.worldCamera = world; canvas.planeDistance = distance;
        RenderTexture.ReleaseTemporary(target); Destroy(image);
    }
}
#endif

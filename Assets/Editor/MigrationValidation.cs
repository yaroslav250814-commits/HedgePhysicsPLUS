using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MigrationValidation
{
    static readonly List<string> lines = new List<string>();
    static int phase, frame, errors;
    static double started;
    static float phaseFixedTime;
    static Quaternion idleThigh;
    static Keyboard keyboard;
    static Gamepad pad;
    static Vector3 position;
    static PlayerBhysics player;
    static InputValidationProbe probe;
    static MigrationValidation() {
        if (SessionState.GetBool("HedgeSmoke", false)) EditorApplication.delayCall += Resume;
    }
    public static void Smoke() {
        Directory.CreateDirectory("Migration/SonicModel");
        File.WriteAllText("Migration/SonicModel/play-pose-samples.txt","");
        SessionState.SetBool("HedgeSmoke", true);
        EditorSceneManager.OpenScene("Assets/[Stage00_Test].unity");
        EditorApplication.EnterPlaymode();
    }
    static void Resume() {
        started = EditorApplication.timeSinceStartup;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
    }
    static void OnLog(string message, string stack, LogType type) {
        if (stack.Contains("UnityEditor.Search.SearchDatabase")) { lines.Add("EDITOR SEARCH DIAGNOSTIC: " + message); return; }
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) {
            errors++; if (errors < 30) lines.Add(type + ": " + message + "\n" + stack);
        }
    }
    static void Check(bool condition, string message) { lines.Add((condition ? "PASS " : "FAIL ") + message); if (!condition) errors++; }
    static void Tick() {
        if (!EditorApplication.isPlaying) return;
        try {
            frame++;
            if (EditorApplication.timeSinceStartup - started > 90) throw new Exception("Smoke test timed out");
            switch (phase) {
                case 0:
                    if (frame < 60) return;
                    player = Object.FindFirstObjectByType<PlayerBhysics>();
                    if(player && !player.Grounded){phaseFixedTime=Time.fixedTime;return;}
                    if(player && Time.fixedTime-phaseFixedTime<.3f)return;
                    Check(player != null, "Test stage has player");
                    Check(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset, "URP is active");
                    Check(player.GetComponent<Action00_Regular>().CharacterAnimator.avatar.isValid, "Sonic avatar is valid");
                    Check(player.GetComponentsInChildren<SkinnedMeshRenderer>(true).All(s=>s.sharedMesh && s.sharedMesh.bindposes.Length==s.bones.Length && s.bones.All(b=>b)), "Sonic mesh bind poses and bones are complete");
                    SonicModelDiagnostics.CaptureGameplay("play-idle.png");
                    idleThigh=player.GetComponent<Action00_Regular>().CharacterAnimator.transform.Find("SkinOffset/SonicModel_5/metarig/Center/hips/hips_001/thigh_L").localRotation;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    probe = new GameObject("Input validation probe").AddComponent<InputValidationProbe>();
                    var pause = Object.FindFirstObjectByType<PauseCotrol>();
                    lines.Add("Pause component=" + (pause ? pause.name + " enabled=" + pause.enabled : "missing"));
                    keyboard = InputSystem.AddDevice<Keyboard>(); pad = InputSystem.AddDevice<Gamepad>();
                    position = player.transform.position;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    phaseFixedTime=Time.fixedTime;
                    phase++; frame=0; break;
                case 1:
                    if (frame < 90 || Time.fixedTime-phaseFixedTime<1f) return;
                    lines.Add("Move=" + probe.Move + " samples=" + probe.Samples);
                    Check(probe.Move.y > .9f, "Keyboard Move action");
                    Check(Vector3.ProjectOnPlane(player.transform.position-position,Vector3.up).magnitude > 1, "Player moves horizontally under input");
                    Check(Quaternion.Angle(idleThigh,player.GetComponent<Action00_Regular>().CharacterAnimator.transform.Find("SkinOffset/SonicModel_5/metarig/Center/hips/hips_001/thigh_L").localRotation)>1f, "Sonic skeleton changes pose from idle to running");
                    SonicModelDiagnostics.CaptureGameplay("play-run.png");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.QueueStateEvent(pad, new GamepadState {leftStick = new Vector2(.75f, 0)}.WithButton(GamepadButton.South));
                    phaseFixedTime=Time.fixedTime;
                    phase++; frame=0; break;
                case 2:
                    if (frame < 5 || Time.fixedTime-phaseFixedTime<.15f) return;
                    Check(probe.Jump, "Gamepad Jump action");
                    Check(probe.Move.x > .5f, "Gamepad Move action");
                    SonicModelDiagnostics.CaptureGameplay("play-jump.png");
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                    phase++; frame=0; break;
                case 3:
                    if (frame < 5) return;
                    Check(Time.timeScale == 0, "Pause opens");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    phase++; frame=0; break;
                case 4:
                    if (frame < 5) return;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                    phase++; frame=0; break;
                case 5:
                    if (frame < 5) return;
                    Check(Time.timeScale == 1, "Pause resumes");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Capture("test-stage.png");
                    SceneManager.LoadScene("[Stage01_AltitudeLimit]");
                    phase++; frame=0; break;
                case 6:
                    if (frame < 120) return;
                    if(!Object.FindFirstObjectByType<PlayerBhysics>().Grounded){phaseFixedTime=Time.fixedTime;return;}
                    if(Time.fixedTime-phaseFixedTime<.3f)return;
                    Check(Object.FindFirstObjectByType<PlayerBhysics>() != null, "Altitude Limit has player");
                    Capture("altitude-limit.png");
                    SonicModelDiagnostics.CaptureGameplay("play-altitude-idle.png");
                    SceneManager.LoadScene("LogoScreen");
                    phase++; frame=0; break;
                case 7:
                    if (frame < 90) return;
                    Capture("title.png");
                    SceneController.LoadStageLoading(4);
                    phase++; frame=0; break;
                case 8:
                    if (frame < 180 || SceneManager.GetSceneByName("LoadingScreen").isLoaded) return;
                    if(!Object.FindFirstObjectByType<PlayerBhysics>() || !Object.FindFirstObjectByType<PlayerBhysics>().Grounded)return;
                    Check(SceneManager.GetActiveScene().name == "[Stage01_AltitudeLimit]", "Loading screen activates requested stage");
                    Check(Object.FindFirstObjectByType<PlayerBhysics>() != null, "Player survives stage loading");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
                    phaseFixedTime=Time.fixedTime;
                    phase++; frame=0; break;
                case 9:
                    if(frame<60 || Time.fixedTime-phaseFixedTime<.5f)return;
                    var spin=Object.FindFirstObjectByType<PlayerBhysics>().GetComponent<Action03_SpinDash>();
                    Check(spin.enabled && spin.SpinDashBall.enabled, "Spin dash displays the restored ball mesh");
                    Check(spin.BallAnimator.avatar.isValid, "Spin dash avatar is valid");
                    SonicModelDiagnostics.CaptureGameplay("play-spindash.png");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    SceneManager.LoadScene("LogoScreen");
                    phase++;frame=0;break;
                case 10:
                    if(frame<90)return;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));
                    phase++;frame=0;break;
                case 11:
                    if(frame<5)return;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                    phase++;frame=0;break;
                case 12:
                    if(SceneManager.GetActiveScene().name!="StageSelect")return;
                    Check(true,"Enter confirms the title screen");
                    Finish(); break;
            }
        } catch (Exception e) { errors++; lines.Add(e.ToString()); Finish(); }
    }
    static void Capture(string name) {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        var camera = Camera.main;
        if (!camera) return;
        Directory.CreateDirectory("Migration/Screenshots");
        var rt = RenderTexture.GetTemporary(1280,720,24);
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
        var texture = new Texture2D(1280,720,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply();
        File.WriteAllBytes("Migration/Screenshots/" + name, texture.EncodeToPNG());
        camera.targetTexture = previous; RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(texture);
    }
    static void Finish() {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        SessionState.SetBool("HedgeSmoke", false);
        Directory.CreateDirectory("Migration");
        lines.Add("Errors: " + errors);
        File.WriteAllLines("Migration/playmode-report.txt",lines);
        Debug.Log("HEDGE_SMOKE_FINISHED errors=" + errors);
        EditorApplication.Exit(errors == 0 ? 0 : 1);
    }
    public static void Audit() {
        int missing = 0, unsupported=0, brokenMeshes=0, missingCollision=0;
        foreach (var guid in AssetDatabase.FindAssets("t:Scene",new[]{"Assets"})) {
            var path=AssetDatabase.GUIDToAssetPath(guid);
            var scene=EditorSceneManager.OpenScene(path);
            foreach (var root in scene.GetRootGameObjects()) foreach (var t in root.GetComponentsInChildren<Transform>(true)) {
                int count=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (count > 0) lines.Add("Missing scripts: " + path + " / " + t.name + " : " + count);
                missing+=count;
                var mesh = t.GetComponent<MeshFilter>();
                var skin = t.GetComponent<SkinnedMeshRenderer>();
                if ((mesh && !mesh.sharedMesh) || (skin && !skin.sharedMesh)) { brokenMeshes++; lines.Add("Missing mesh: " + path + " / " + t.name); }
                if(mesh && mesh.sharedMesh) {
                    var model=AssetDatabase.GetAssetPath(mesh.sharedMesh);
                    if(model=="Assets/Prototyping/TestWorldMod.fbx" || model=="Assets/Stage/Models/Level.fbx") {
                        var collider=t.GetComponent<MeshCollider>();
                        if(!collider || !collider.sharedMesh || !collider.enabled || collider.isTrigger) { missingCollision++; lines.Add("Missing solid environment collider: " + path + " / " + t.name); }
                    }
                }
                if (PrefabUtility.GetPrefabInstanceStatus(t.gameObject) == PrefabInstanceStatus.MissingAsset) { missing++; lines.Add("Missing prefab: " + path + " / " + t.name); }
            }
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets"})) {
            var path=AssetDatabase.GUIDToAssetPath(guid); if (!path.EndsWith(".mat")) continue;
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat.shader == null || ShaderUtil.ShaderHasError(mat.shader)) { unsupported++; lines.Add("Shader error: " + path); }
        }
        lines.Add("Missing scripts/prefabs=" + missing + "; shader errors=" + unsupported + "; missing meshes=" + brokenMeshes + "; missing solid level colliders="+missingCollision);
        Directory.CreateDirectory("Migration"); File.WriteAllLines("Migration/audit-report.txt", lines);
        Debug.Log("HEDGE_AUDIT missing="+missing+" shaderErrors="+unsupported);
        if(Application.isBatchMode) EditorApplication.Exit(missing+unsupported+brokenMeshes+missingCollision == 0 ? 0 : 1);
    }
    public static void DumpModels() {
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[]{"Assets"})) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var objects = new List<Object>(AssetDatabase.LoadAllAssetsAtPath(path));
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model) foreach(var t in model.GetComponentsInChildren<Transform>(true)) { objects.Add(t.gameObject); objects.AddRange(t.GetComponents<Component>()); }
            foreach(var o in objects.Where(o => o).Distinct()) {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string g, out long id)) lines.Add(g + "\t" + id + "\t" + o.GetType().Name + "\t" + o.name + "\t" + path);
            }
        }
        File.WriteAllLines("Migration/model-identifiers.tsv", lines);
        EditorApplication.Exit(0);
    }
}

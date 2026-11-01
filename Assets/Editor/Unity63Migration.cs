using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class Unity63Migration
{
    const string Settings = "Assets/Settings";
    static readonly List<string> report = new List<string>();
    static VolumeProfile profile;
    static readonly string[] retired = { "UnityStandardAssets.CinematicEffects.", "UnityStandardAssets.ImageEffects.",
        "AtmosphericScattering", "DeferredDecalRenderer", "CustomLightRenderer" };

    [MenuItem("HedgePhysics/Upgrade project to Unity 6.3")]
    public static void Run()
    {
        try {
            Directory.CreateDirectory(Settings);
            AssetDatabase.Refresh();
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorBuildSettings.scenes = new[]{"LogoScreen", "StageSelect", "LoadingScreen", "[Stage00_Test]", "[Stage01_AltitudeLimit]", "StageCompleteScreen"}
                .Select(name => new EditorBuildSettingsScene("Assets/" + name + ".unity", true)).ToArray();
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            var ps = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            ps.FindProperty("activeInputHandler").intValue = 1;
            ps.ApplyModifiedPropertiesWithoutUndo();
            ConfigureRendering();
            ExtractModelMaterials();
            UpgradeMaterials();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[]{"Assets"})) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                UpgradeRoot(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[]{"Assets"})) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects()) UpgradeRoot(root);
                // A single global profile per scene replaces the old camera image effects.
                if (!Object.FindAnyObjectByType<Volume>()) {
                    var volume = new GameObject("Environment Volume (URP)").AddComponent<Volume>();
                    volume.isGlobal = true;
                    volume.sharedProfile = profile;
                }
                var sky = RenderSettings.skybox;
                if (sky && !sky.shader.name.StartsWith("Skybox/")) {
                    var material = AssetDatabase.LoadAssetAtPath<Material>(Settings + "/Sky.mat");
                    if (!material) { material = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(material, Settings + "/Sky.mat"); }
                    RenderSettings.skybox = material;
                }
                EditorSceneManager.SaveScene(scene);
                report.Add("Scene upgraded: " + path);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && new[]{".unity", ".prefab", ".mat", ".asset", ".controller", ".anim"}.Contains(Path.GetExtension(p))));
            Directory.CreateDirectory("Migration");
            File.WriteAllLines("Migration/conversion-report.txt", report);
            Debug.Log("HEDGE_MIGRATION_OK: " + report.Count + " entries");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        } catch (Exception e) { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }

    static void ConfigureRendering()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Settings + "/HedgeRenderer.asset");
        if (!renderer) {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.renderingMode = RenderingMode.ForwardPlus;
            AssetDatabase.CreateAsset(renderer, Settings + "/HedgeRenderer.asset");
        }
        if (!renderer.rendererFeatures.OfType<DecalRendererFeature>().Any()) {
            var decals = ScriptableObject.CreateInstance<DecalRendererFeature>();
            decals.name = "URP Decals";
            renderer.rendererFeatures.Add(decals);
            AssetDatabase.AddObjectToAsset(decals, renderer);
            renderer.SetDirty();
        }
        if (!renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().Any()) {
            var occlusion = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            occlusion.name = "URP Ambient Occlusion";
            renderer.rendererFeatures.Add(occlusion);
            AssetDatabase.AddObjectToAsset(occlusion, renderer);
            renderer.SetDirty();
        }
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Settings + "/HedgeURP.asset");
        if (!pipeline) { pipeline = UniversalRenderPipelineAsset.Create(renderer); AssetDatabase.CreateAsset(pipeline, Settings + "/HedgeURP.asset"); }
        pipeline.supportsHDR = true;
        pipeline.supportsCameraDepthTexture = true;
        pipeline.supportsCameraOpaqueTexture = true;
        pipeline.msaaSampleCount = 4;
        pipeline.shadowDistance = 150;
        GraphicsSettings.defaultRenderPipeline = pipeline;
        int quality = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i); QualitySettings.renderPipeline = pipeline; }
        QualitySettings.SetQualityLevel(quality);
        typeof(UniversalRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings")
            .GetMethod("Ensure", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[]{true});
        // Unity 6.3 uses Render Graph by default; no compatibility define is enabled.
        profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Settings + "/Environment.asset");
        if (!profile) {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, Settings + "/Environment.asset");
            var bloom = profile.Add<Bloom>(true); bloom.intensity.value = .35f; bloom.threshold.value = 1;
            var tone = profile.Add<Tonemapping>(true); tone.mode.value = TonemappingMode.ACES;
            var vignette = profile.Add<Vignette>(true); vignette.intensity.value = .15f;
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
        }
    }

    static void UpgradeMaterials()
    {
        var upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(typeof(UniversalRenderPipelineAsset));
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[]{"Assets"})) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var mat in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>()) {
                if (Path.GetExtension(path) != ".mat") continue; // Model materials are remapped below.
                string old = mat.shader ? mat.shader.name : "Missing";
                if (mat.name == "HomingTarget" && old.StartsWith("Universal Render Pipeline/")) { mat.shader = Shader.Find("HedgePhysics/HomingTarget"); EditorUtility.SetDirty(mat); continue; }
                if (old.StartsWith("HedgePhysics/") || old.StartsWith("Universal Render Pipeline/") || old.StartsWith("Shader Graphs/") || old.StartsWith("UI/") || old.StartsWith("Skybox/") || old == "Custom Shaders/Dissolving") continue;
                if (old.StartsWith("Decal/")) {
                    var texture = mat.GetTexture("_MainTex");
                    var normal = mat.GetTexture("_BumpMap");
                    mat.shader = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Decal.mat").shader;
                    mat.SetTexture("Base_Map", texture); mat.SetTexture("Normal_Map", normal);
                    EditorUtility.SetDirty(mat); report.Add("Decal material: " + path); continue;
                }
                string message = "";
                if (!MaterialUpgrader.Upgrade(mat, upgraders, MaterialUpgrader.UpgradeFlags.None, ref message) || mat.shader.name == old) {
                    var texture = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    var scale = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
                    var offset = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;
                    var color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                    bool particle = old.IndexOf("Particle", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool alpha = particle || old.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) >= 0;
                    mat.shader = Shader.Find(particle ? "Universal Render Pipeline/Particles/Unlit" : old.Contains("Unlit") ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
                    mat.SetTexture("_BaseMap", texture); mat.SetTextureScale("_BaseMap", scale); mat.SetTextureOffset("_BaseMap", offset); mat.SetColor("_BaseColor", color);
                    if (alpha) { mat.SetFloat("_Surface", 1); mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); mat.SetFloat("_ZWrite", 0); mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = 3000; }
                    if (old.Contains("Additive")) { mat.SetFloat("_Blend", 2); mat.SetFloat("_DstBlend", (float)BlendMode.One); }
                    if (old.Contains("Cutout") || old.Contains("AlphaTest")) { mat.SetFloat("_AlphaClip", 1); mat.EnableKeyword("_ALPHATEST_ON"); mat.renderQueue = 2450; }
                }
                EditorUtility.SetDirty(mat);
                report.Add("Material: " + path + " : " + old + " -> " + mat.shader.name);
            }
        }
    }

    static void ExtractModelMaterials()
    {
        Directory.CreateDirectory("Assets/Materials/Models");
        AssetDatabase.Refresh();
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[]{"Assets"})) {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
            bool changed = false;
            foreach (var mat in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>()) {
                string target = "Assets/Materials/Models/" + guid + "_" + mat.name.Replace('/', '_').Replace(':', '_') + ".mat";
                var external = AssetDatabase.LoadAssetAtPath<Material>(target);
                if (!external) { external = new Material(mat); AssetDatabase.CreateAsset(external, target); }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(mat), external);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }
    }

    static void UpgradeRoot(GameObject root)
    {
        foreach (var module in root.GetComponentsInChildren<StandaloneInputModule>(true)) {
            var go = module.gameObject; Object.DestroyImmediate(module, true);
            if (!go.GetComponent<InputSystemUIInputModule>()) go.AddComponent<InputSystemUIInputModule>();
        }
        foreach (var module in root.GetComponentsInChildren<InputSystemUIInputModule>(true)) {
            var ui = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Resources/HedgeUI.inputactions");
            var serialized = new SerializedObject(module);
            serialized.FindProperty("m_ActionsAsset").objectReferenceValue = ui;
            foreach (var pair in new[]{("m_PointAction", "Point"), ("m_MoveAction", "Navigate"), ("m_SubmitAction", "Submit"), ("m_CancelAction", "Cancel"), ("m_LeftClickAction", "Click"), ("m_RightClickAction", "RightClick"), ("m_MiddleClickAction", "MiddleClick"), ("m_ScrollWheelAction", "ScrollWheel"), ("m_TrackedDevicePositionAction", "TrackedDevicePosition"), ("m_TrackedDeviceOrientationAction", "TrackedDeviceOrientation")}) {
                var reference = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/HedgeUI.inputactions").OfType<InputActionReference>().First(r => r.action.name == pair.Item2 && r.action.actionMap.name == "UI");
                serialized.FindProperty(pair.Item1).objectReferenceValue = reference;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (var camera in root.GetComponentsInChildren<Camera>(true)) {
            var data = camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }
        foreach (var quality in root.GetComponentsInChildren<Qualitysettings>(true)) quality.environmentProfile = profile;
        foreach (var projector in root.GetComponentsInChildren<Projector>(true)) Object.DestroyImmediate(projector, true);
        foreach (var light in root.GetComponentsInChildren<Light>(true)) if (light.type == LightType.Directional) light.shadows = LightShadows.Soft;
        foreach (var decal in root.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c && c.GetType().Name == "Decal")) {
            var projector = decal.GetComponent<DecalProjector>() ?? decal.gameObject.AddComponent<DecalProjector>();
            projector.material = new SerializedObject(decal).FindProperty("m_Material").objectReferenceValue as Material;
            projector.size = Vector3.one;
            Object.DestroyImmediate(decal, true);
        }
        foreach (var light in root.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c && c.GetType().Name == "CustomLight")) {
            var data = new SerializedObject(light);
            var actual = light.GetComponent<Light>() ?? light.gameObject.AddComponent<Light>();
            actual.type = LightType.Point; actual.color = data.FindProperty("m_Color").colorValue; actual.intensity = data.FindProperty("m_Intensity").floatValue; actual.range = data.FindProperty("m_Range").floatValue;
            report.Add("Custom light replaced: " + light.name);
            Object.DestroyImmediate(light, true);
        }
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true)) {
            if (component == null) continue;
            string name = component.GetType().FullName;
            if (retired.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))) Object.DestroyImmediate(component, true);
        }
        foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) {
            body.maxLinearVelocity = 500;
            if (body.GetComponent<PlayerBhysics>()) { body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; }
        }
        // Built-in default materials and FBX sub-assets cannot be modified in place.
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) {
            var mats = r.sharedMaterials;
            for (int i=0; i<mats.Length; i++) {
                var m=mats[i]; if (!m || m.shader.name.StartsWith("HedgePhysics/") || m.shader.name.StartsWith("Universal Render Pipeline/") || m.shader.name.StartsWith("Shader Graphs/") || m.shader.name.StartsWith("Skybox/") || m.shader.name == "Custom Shaders/Dissolving") continue;
                if (m.shader.name.StartsWith("UI/")) continue;
                string path = Settings + "/Imported_" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m)) + "_" + m.name.Replace('/', '_') + ".mat";
                var replacement = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!replacement) {
                    replacement = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    if (m.HasProperty("_MainTex")) replacement.SetTexture("_BaseMap", m.GetTexture("_MainTex"));
                    if (m.HasProperty("_Color")) replacement.SetColor("_BaseColor", m.GetColor("_Color"));
                    AssetDatabase.CreateAsset(replacement, path);
                }
                mats[i]=replacement;
            }
            r.sharedMaterials=mats;
        }
    }

    public static void Build()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Debug.Log("BUILD_SCENES: " + string.Join(", ", scenes));
        var result = BuildPipeline.BuildPlayer(scenes, "Builds/Windows/HedgePhysics.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);
        Directory.CreateDirectory("Migration");
        File.WriteAllLines("Migration/build-report.txt", new[]{"Unity: " + Application.unityVersion, "Result: " + result.summary.result,
            "Errors: " + result.summary.totalErrors, "Warnings: " + result.summary.totalWarnings,
            "Size: " + result.summary.totalSize + " bytes", "Output: Builds/Windows/HedgePhysics.exe", "Scenes: " + string.Join(", ", scenes)});
        Debug.Log("HEDGE_BUILD: " + result.summary.result + " errors=" + result.summary.totalErrors);
        if(Application.isBatchMode) EditorApplication.Exit(result.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}

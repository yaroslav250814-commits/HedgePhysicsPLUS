using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GameplayDiagnostics
{
    const string Request = "Migration/gameplay-request.txt";
    static GameplayDiagnostics() { EditorApplication.update += Update; File.WriteAllText("Migration/diagnostic-version.txt", "collision-fix-v2"); }
    public static void BatchStage00() { BatchScene("Assets/[Stage00_Test].unity"); }
    public static void BatchAltitude() { BatchScene("Assets/[Stage01_AltitudeLimit].unity"); }
    static void BatchScene(string path) {
        EditorSceneManager.OpenScene(path);
        File.WriteAllText("Migration/ground-test.request", "run");
        EditorApplication.EnterPlaymode();
    }
    static void Update()
    {
        if (EditorApplication.isCompiling || !File.Exists(Request)) return;
        string command = File.ReadAllText(Request).Trim();
        File.WriteAllText("Migration/gameplay-request-consumed.txt", command);
        File.Delete(Request);
        try {
            if(command.StartsWith("run") && EditorApplication.isPlayingOrWillChangePlaymode) {
                EditorApplication.isPlaying=false; File.WriteAllText(Request,command); return;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (command == "inspect") Inspect();
            else if (command == "fix") FixColliders();
            else if (command == "run") { Inspect(); File.WriteAllText("Migration/ground-test.request", "run"); EditorApplication.EnterPlaymode(); }
            else if (command == "run-test") {
                if(SceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                EditorSceneManager.OpenScene("Assets/[Stage00_Test].unity");
                Inspect(); File.WriteAllText("Migration/ground-test.request", "run"); EditorApplication.EnterPlaymode();
            }
            else if (command == "build") Unity63Migration.Build();
            else if (command == "audit") MigrationValidation.Audit();
            else if (command == "run-altitude") {
                if(SceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                EditorSceneManager.OpenScene("Assets/[Stage01_AltitudeLimit].unity");
                Inspect(); File.WriteAllText("Migration/ground-test.request", "run"); EditorApplication.EnterPlaymode();
            }
            else if (command == "play") EditorApplication.EnterPlaymode();
            else if (command == "stop") EditorApplication.ExitPlaymode();
        } catch (Exception e) { File.WriteAllText("Migration/gameplay-diagnostic-error.txt", e.ToString()); Debug.LogException(e); }
    }
    static void FixColliders()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before fixing scene assets");
        string original=SceneManager.GetActiveScene().path;
        Directory.CreateDirectory("Migration/BeforeCollisionFix");
        foreach(var path in new[]{"Assets/[Stage00_Test].unity","Assets/[Stage01_AltitudeLimit].unity"}) {
            string backup="Migration/BeforeCollisionFix/"+Path.GetFileName(path);
            if(!File.Exists(backup)) File.Copy(path,backup);
        }
        // Preserve the user's current scene edits before opening another scene.
        if(SceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        var report=new List<string>();
        foreach(var path in new[]{"Assets/[Stage00_Test].unity","Assets/[Stage01_AltitudeLimit].unity"}) {
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            int added=0,repaired=0;
            foreach(var root in scene.GetRootGameObjects()) foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)) {
                if(!filter.sharedMesh) continue;
                string model=AssetDatabase.GetAssetPath(filter.sharedMesh);
                bool environment=model=="Assets/Prototyping/TestWorldMod.fbx" || model=="Assets/Stage/Models/Level.fbx";
                var collider=filter.GetComponent<MeshCollider>();
                if(!collider && environment) { collider=filter.gameObject.AddComponent<MeshCollider>(); added++; }
                else if(collider && !collider.sharedMesh) repaired++;
                else continue;
                collider.sharedMesh=filter.sharedMesh;
                collider.cookingOptions=MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices | MeshColliderCookingOptions.UseFastMidphase;
                EditorUtility.SetDirty(collider);
            }
            EditorSceneManager.SaveScene(scene);
            report.Add(path+": added="+added+", repaired="+repaired);
        }
        if(!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
        AssetDatabase.SaveAssets();
        File.WriteAllLines("Migration/collision-fix-report.txt",report);
        Inspect();
    }
    public static void Inspect()
    {
        var lines = new List<string>();
        Physics.SyncTransforms();
        var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        lines.Add("Scene=" + SceneManager.GetActiveScene().path + " play=" + EditorApplication.isPlaying);
        lines.Add("Colliders=" + colliders.Length);
        foreach (var group in colliders.GroupBy(c => c.GetType().Name)) lines.Add(group.Key + "=" + group.Count());
        foreach (var c in colliders.OfType<MeshCollider>()) {
            var mf = c.GetComponent<MeshFilter>();
            lines.Add("MESH " + PathOf(c.transform) + " enabled=" + c.enabled + " active=" + c.gameObject.activeInHierarchy + " trigger=" + c.isTrigger + " mesh=" + (c.sharedMesh ? c.sharedMesh.name + "/" + c.sharedMesh.vertexCount : "MISSING") + " renderMesh=" + (mf && mf.sharedMesh ? mf.sharedMesh.name : "none") + " layer=" + c.gameObject.layer);
            if(c.gameObject.activeInHierarchy) lines.Add("BOUNDS " + c.bounds + " render=" + (c.GetComponent<Renderer>() ? c.GetComponent<Renderer>().bounds.ToString() : "none") + " scale=" + c.transform.lossyScale);
        }
        foreach (var p in UnityEngine.Object.FindObjectsByType<PlayerBhysics>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            lines.Add("PLAYER " + p.transform.position + " layer=" + p.gameObject.layer);
            foreach (var c in p.GetComponentsInChildren<Collider>(true)) lines.Add("PLAYER COLLIDER " + c.name + " active=" + c.gameObject.activeInHierarchy + " enabled=" + c.enabled + " trigger=" + c.isTrigger + " bounds=" + c.bounds);
            var hits = Physics.RaycastAll(p.transform.position + Vector3.up, Vector3.down, 10000, ~0, QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance);
            foreach(var h in hits.Take(10)) lines.Add("GROUND HIT " + PathOf(h.collider.transform) + " point=" + h.point + " distance=" + h.distance + " ignored=" + Physics.GetIgnoreLayerCollision(p.gameObject.layer,h.collider.gameObject.layer));
            bool old = Physics.queriesHitBackfaces; Physics.queriesHitBackfaces = true;
            foreach(var h in Physics.RaycastAll(p.transform.position + Vector3.up, Vector3.down, 10000, ~0, QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance).Take(10)) lines.Add("BACKFACE HIT " + PathOf(h.collider.transform) + " point=" + h.point + " normal=" + h.normal);
            Physics.queriesHitBackfaces = old;
        }
        File.WriteAllLines("Migration/collision-diagnostic.txt", lines);
    }
    static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
}

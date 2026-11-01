using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AdventureChainTests
{
    public static void Run()
    {
        Test("Assets/[Stage00_Test].unity");
    }
    public static void Altitude() { Test("Assets/[Stage01_AltitudeLimit].unity"); }
    static void Test(string scene)
    {
        Directory.CreateDirectory("Migration/AdventurePresentation");
        File.WriteAllText("Migration/adventure-chain-test.request", "run");
        EditorSceneManager.OpenScene(scene);
        EditorApplication.EnterPlaymode();
    }
    public static void Install()
    {
        foreach(string path in new[]{"Assets/Player/Player_Sonic.prefab","Assets/Prototyping/ProtoPrefabs/PlayerObjects.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try { Attach(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach(string path in new[]{"Assets/[Stage00_Test].unity","Assets/[Stage01_AltitudeLimit].unity"})
        {
            var scene=EditorSceneManager.OpenScene(path);foreach(var root in scene.GetRootGameObjects())Attach(root);EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
    static void Attach(GameObject root)
    {
        foreach(var player in root.GetComponentsInChildren<LevelRunState>(true))
        {
            var chain=player.GetComponent<TechniqueChain>();if(!chain)chain=player.gameObject.AddComponent<TechniqueChain>();
            EditorUtility.SetDirty(chain);
        }
    }
}

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DropDashTools
{
    public static void Install()
    {
        Directory.CreateDirectory("Migration/DropDash");
        foreach(string path in new[]{"Assets/Player/Player_Sonic.prefab","Assets/Prototyping/ProtoPrefabs/PlayerObjects.prefab"})
        {
            Backup(path);var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var manager in root.GetComponentsInChildren<ActionManager>(true))Apply(manager);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(string path in new[]{"Assets/[Stage00_Test].unity","Assets/[Stage01_AltitudeLimit].unity"})
        {
            Backup(path);var scene=EditorSceneManager.OpenScene(path);
            foreach(var root in scene.GetRootGameObjects())foreach(var manager in root.GetComponentsInChildren<ActionManager>(true))Apply(manager);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
    static void Apply(ActionManager manager)
    {
        manager.Action06=manager.GetComponent<Action06_DropDash>();
        if(!manager.Action06)manager.Action06=manager.gameObject.AddComponent<Action06_DropDash>();
        manager.Action06.enabled=false;EditorUtility.SetDirty(manager);EditorUtility.SetDirty(manager.Action06);
    }
    static void Backup(string path)
    {
        string destination="Migration/DropDash/Before/"+path;
        if(File.Exists(destination))return;
        Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(path,destination);
    }
    public static void Stage00()=>Test("Assets/[Stage00_Test].unity");
    public static void Altitude()=>Test("Assets/[Stage01_AltitudeLimit].unity");
    static void Test(string scene)
    {
        Directory.CreateDirectory("Migration/DropDash");File.WriteAllText("Migration/drop-dash.request","run");
        EditorSceneManager.OpenScene(scene);EditorApplication.EnterPlaymode();
    }
}

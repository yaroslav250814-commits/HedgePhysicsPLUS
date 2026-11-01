using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FallTuning
{
    public static void Restore()
    {
        foreach(string path in new[]{"Assets/Player/Player_Sonic.prefab","Assets/Prototyping/ProtoPrefabs/PlayerObjects.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var player in root.GetComponentsInChildren<PlayerBhysics>(true))
                {
                    player.MaxFallingSpeed=path=="Assets/Player/Player_Sonic.prefab"?140:120;
                    player.DescentGravityMultiplier=1;
                    EditorUtility.SetDirty(player);
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(string path in new[]{"Assets/[Stage00_Test].unity","Assets/[Stage01_AltitudeLimit].unity"})
        {
            var scene=EditorSceneManager.OpenScene(path);
            foreach(var root in scene.GetRootGameObjects())foreach(var player in root.GetComponentsInChildren<PlayerBhysics>(true))
            {
                player.MaxFallingSpeed=120;player.DescentGravityMultiplier=1;EditorUtility.SetDirty(player);
            }
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("Migration/FallTuning/restored-settings.txt","Original fall acceleration restored (descent multiplier 1). Gameplay scenes and PlayerObjects limit: 120. Player_Sonic base prefab limit: 140.\n");
        EditorApplication.Exit(0);
    }
    public static void Install()
    {
        foreach(string path in new[]{"Assets/Player/Player_Sonic.prefab","Assets/Prototyping/ProtoPrefabs/PlayerObjects.prefab"})
        {
            Backup(path);var root=PrefabUtility.LoadPrefabContents(path);
            try{foreach(var player in root.GetComponentsInChildren<PlayerBhysics>(true))Apply(player);PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(string path in new[]{"Assets/[Stage00_Test].unity","Assets/[Stage01_AltitudeLimit].unity"})
        {
            Backup(path);var scene=EditorSceneManager.OpenScene(path);
            foreach(var root in scene.GetRootGameObjects())foreach(var player in root.GetComponentsInChildren<PlayerBhysics>(true))Apply(player);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
    static void Apply(PlayerBhysics player){player.MaxFallingSpeed=45;player.DescentGravityMultiplier=.6f;EditorUtility.SetDirty(player);}
    static void Backup(string path){string destination="Migration/FallTuning/Before/"+path;if(File.Exists(destination))return;Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(path,destination);}
    public static void Stage00()=>Test("Assets/[Stage00_Test].unity");
    public static void Altitude()=>Test("Assets/[Stage01_AltitudeLimit].unity");
    static void Test(string scene){Directory.CreateDirectory("Migration/FallTuning");File.WriteAllText("Migration/fall-tuning.request","run");EditorSceneManager.OpenScene(scene);EditorApplication.EnterPlaymode();}
}

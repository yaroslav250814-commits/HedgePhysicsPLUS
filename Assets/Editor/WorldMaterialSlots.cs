using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WorldMaterialSlots
{
    static readonly List<string> report=new List<string>();
    public static void Fix()
    {
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);if(!path.EndsWith(".prefab")||path.StartsWith("Assets/Editor/"))continue;
            var root=PrefabUtility.LoadPrefabContents(path);
            try{if(Trim(root))PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(string path in new[]{"Assets/[Stage00_Test].unity","Assets/[Stage01_AltitudeLimit].unity"})
        {
            var scene=EditorSceneManager.OpenScene(path);bool changed=false;
            foreach(var root in scene.GetRootGameObjects())changed|=Trim(root);
            if(changed)EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();File.WriteAllLines("Migration/WorldModels/material-slots.txt",report);
        EditorApplication.Exit(0);
    }
    static bool Trim(GameObject root)
    {
        bool changed=false;
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var filter=renderer.GetComponent<MeshFilter>();
            var mesh=renderer is SkinnedMeshRenderer skin?skin.sharedMesh:(filter?filter.sharedMesh:null);
            if(!mesh||!AssetDatabase.GetAssetPath(mesh).StartsWith("Assets/WorldModelRig/")||renderer.sharedMaterials.Length<=mesh.subMeshCount)continue;
            report.Add(renderer.name+" "+renderer.sharedMaterials.Length+" -> "+mesh.subMeshCount);
            renderer.sharedMaterials=renderer.sharedMaterials.Take(mesh.subMeshCount).ToArray();EditorUtility.SetDirty(renderer);changed=true;
        }
        return changed;
    }
}

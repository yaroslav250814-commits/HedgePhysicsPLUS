using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class WorldModelAudit
{
    public const string Reference = "Assets/Editor/WorldModelReference/";
    public static readonly List<string> Lines = new List<string>();
    public static string Key(string name) => new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    public static void Inspect()
    {
        Directory.CreateDirectory("Migration/WorldModels");
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[]{Reference.TrimEnd('/')}))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid); var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.useFileScale = false; importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; importer.SaveAndReimport();
            var modernPath = "Assets/" + path.Substring(Reference.Length);
            var old = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
            foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(modernPath).OfType<Mesh>())
            {
                var match = old.Where(m=>Key(m.name)==Key(mesh.name)).ToArray();
                Lines.Add("ASSET " + modernPath + " mesh=" + mesh.name + " vertices=" + mesh.vertexCount + " bounds=" + mesh.bounds.size + " bones="+mesh.bindposes.Length + " sub="+mesh.subMeshCount);
                foreach (var legacy in match) Lines.Add(" OLD vertices="+legacy.vertexCount+" bounds="+legacy.bounds.size+" bones="+legacy.bindposes.Length+" sub="+legacy.subMeshCount);
            }
        }
        foreach (var scenePath in new[]{"Assets/[Stage00_Test].unity", "Assets/[Stage01_AltitudeLimit].unity"})
        {
            var scene = EditorSceneManager.OpenScene(scenePath);
            foreach (var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if (!skin.sharedMesh) continue;
                var baked = new Mesh(); skin.BakeMesh(baked);
                Lines.Add("SKIN " + scene.name + " " + AnimationUtility.CalculateTransformPath(skin.transform,null) + " asset="+AssetDatabase.GetAssetPath(skin.sharedMesh)+" mesh="+skin.sharedMesh.name+" boneCount="+skin.bones.Length+" restError="+RestError(skin).ToString("F4")+" meshBounds="+skin.sharedMesh.bounds.size+" bakedBounds="+baked.bounds.size);
                Object.DestroyImmediate(baked);
            }
            Lines.Add("ENEMIES " + scene.name + " placed="+Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length+" spawners="+Object.FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length+" eternal="+Object.FindObjectsByType<EnemySpawnerEternal>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
        }
        File.WriteAllLines("Migration/WorldModels/audit-before.txt",Lines);
        EditorApplication.Exit(0);
    }
    public static float RestError(SkinnedMeshRenderer skin)
    {
        var poses = skin.sharedMesh.bindposes; float error = 0;
        for(int i=0;i<Mathf.Min(poses.Length,skin.bones.Length);i++)
        {
            if(!skin.bones[i]){error+=10000;continue;}
            var m = skin.transform.worldToLocalMatrix * skin.bones[i].localToWorldMatrix * poses[i];
            for(int r=0;r<3;r++)for(int c=0;c<4;c++)error+=Mathf.Abs(m[r,c] - (r==c?1:0));
        }
        return error / Mathf.Max(1,poses.Length);
    }
    public static void PropDetails()
    {
        foreach(string model in new[]{"Prototyping/PrototypingObjects/Spring","Prototyping/PrototypeEnemies/Motobug_Generations","Prototyping/PrototypeEnemies/GunBettleModel"})
        {
            var reference=AssetDatabase.LoadAssetAtPath<GameObject>(Reference+model+".fbx");
            Lines.Add("REFERENCE "+model);
            foreach(var t in reference.GetComponentsInChildren<Transform>(true))Lines.Add(AnimationUtility.CalculateTransformPath(t,reference.transform)+" p="+t.localPosition+" r="+t.localEulerAngles+" s="+t.localScale);
            string prefab=model.EndsWith("Spring")?"Assets/Prototyping/PrototypingObjects/Spring.prefab":model.EndsWith("GunBettleModel")?"Assets/Prototyping/PrototypeEnemies/[Enemy] - Gun Bettle.prefab":"Assets/Prototyping/PrototypeEnemies/[Enemy] - Motobug.prefab";
            var root=PrefabUtility.LoadPrefabContents(prefab);
            try{
                Lines.Add("ACTUAL "+model);
                foreach(var t in root.GetComponentsInChildren<Transform>(true))if(!t.GetComponentInParent<ParticleSystem>())Lines.Add(AnimationUtility.CalculateTransformPath(t,root.transform)+" p="+t.localPosition+" r="+t.localEulerAngles+" s="+t.localScale);
                foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(!(r is ParticleSystemRenderer))Lines.Add("RENDERER "+r.name+" "+r.GetType().Name+" bounds="+r.bounds);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        File.WriteAllLines("Migration/WorldModels/prop-details.txt",Lines);EditorApplication.Exit(0);
    }
}

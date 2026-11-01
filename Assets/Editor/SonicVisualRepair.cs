using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static class SonicVisualRepair
{
    const string Folder="Assets/Player/SonicRig";
    const string Prefab="Assets/Player/Player_Sonic.prefab";
    static readonly List<string> report=new List<string>();
    public static void Run()
    {
        Directory.CreateDirectory("Migration/SonicModel/Before");
        foreach(string path in new[]{Prefab,"Assets/[Stage00_Test].unity","Assets/[Stage01_AltitudeLimit].unity"}) {
            string backup="Migration/SonicModel/Before/"+Path.GetFileName(path);
            if(!File.Exists(backup))File.Copy(path,backup);
        }
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Player","SonicRig");
        foreach(string path in new[]{"Assets/Editor/SonicReference/OriginalSonicModel5.fbx","Assets/Editor/SonicReference/Ball.fbx"}) {
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.useFileScale=false;importer.animationType=ModelImporterAnimationType.Generic;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.SaveAndReimport();
        }
        var prefab=PrefabUtility.LoadPrefabContents(Prefab);
        try { Apply(prefab,true); PrefabUtility.SaveAsPrefabAsset(prefab,Prefab); }
        finally {PrefabUtility.UnloadPrefabContents(prefab);}
        foreach(string scene in new[]{"Assets/[Stage00_Test].unity","Assets/[Stage01_AltitudeLimit].unity"}) {
            var opened=EditorSceneManager.OpenScene(scene);
            foreach(var player in Object.FindObjectsByType<PlayerBhysics>(FindObjectsInactive.Include,FindObjectsSortMode.None))Apply(player.gameObject,false);
            EditorSceneManager.SaveScene(opened);
        }
        AssetDatabase.SaveAssets();File.WriteAllLines("Migration/SonicModel/repair.txt",report);
        EditorApplication.Exit(0);
    }
    static string Relative(Transform t,Transform root) => AnimationUtility.CalculateTransformPath(t,root);
    static void Apply(GameObject player,bool create)
    {
        var visual=player.GetComponentsInChildren<Transform>(true).First(t=>t.name=="SonicModel_5");
        var main=player.GetComponentsInChildren<Animator>(true).First(a=>a.transform.name=="SonicSkin");
        var ball=player.GetComponentsInChildren<Animator>(true).First(a=>a.transform.name=="Ball");
        ReplaceMeshes(visual,"Assets/Editor/SonicReference/OriginalSonicModel5.fbx","Sonic",create);
        ReplaceMeshes(ball.transform,"Assets/Editor/SonicReference/Ball.fbx","Ball",create);
        ConfigureAnimator(main,visual,"Assets/Prototyping/ProtoCharacter/Sonic_Animator.controller","Sonic",create);
        ConfigureAnimator(ball,ball.transform,"Assets/Prototyping/SonicModel/Ball.controller","Ball",create);
    }
    static void ReplaceMeshes(Transform target,string sourcePath,string prefix,bool create)
    {
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        foreach(var source in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
            string path=Relative(source.transform,model.transform);
            var renderer=target.Find(path)?.GetComponent<SkinnedMeshRenderer>();
            if(!renderer)throw new Exception("Missing renderer "+path);
            string meshPath=Folder+"/"+prefix+"_"+source.name+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(create) {
                if(!mesh){mesh=Object.Instantiate(source.sharedMesh);mesh.name=prefix+"_"+source.name;AssetDatabase.CreateAsset(mesh,meshPath);}
                else EditorUtility.CopySerialized(source.sharedMesh,mesh);
            }
            if(!mesh)throw new Exception("Missing converted mesh "+meshPath);
            renderer.sharedMesh=mesh;
            renderer.bones=source.bones.Select(b=>target.Find(Relative(b,model.transform))).ToArray();
            renderer.rootBone=target.Find(Relative(source.rootBone,model.transform));
            if(renderer.bones.Any(b=>!b))throw new Exception("Unresolved bone "+path);
            renderer.localBounds=source.localBounds;
            EditorUtility.SetDirty(renderer);
            report.Add("MESH "+path+" vertices="+mesh.vertexCount+" bones="+renderer.bones.Length);
        }
    }
    static void ConfigureAnimator(Animator animator,Transform clipRoot,string sourceController,string prefix,bool create)
    {
        string avatarPath=Folder+"/"+prefix+"Avatar.asset";
        var avatar=AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
        if(create && !avatar) {
            avatar=AvatarBuilder.BuildGenericAvatar(animator.gameObject,"");avatar.name=prefix+"Avatar";
            if(!avatar.isValid)throw new Exception("Invalid generated avatar");
            AssetDatabase.CreateAsset(avatar,avatarPath);
        }
        string controllerPath=Folder+"/"+prefix+"Animations.overrideController";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
        if(create) {
            var original=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(sourceController);
            if(!controller){controller=new AnimatorOverrideController(original);AssetDatabase.CreateAsset(controller,controllerPath);}
            var replacements=new List<KeyValuePair<AnimationClip,AnimationClip>>();
            string rootPath=Relative(clipRoot,animator.transform);
            foreach(var source in original.animationClips.Distinct()) {
                string clipPath=Folder+"/"+prefix+"_"+source.name.Replace('/','_')+".anim";
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if(!clip){clip=Object.Instantiate(source);AssetDatabase.CreateAsset(clip,clipPath);}
                else EditorUtility.CopySerialized(source,clip);
                clip.ClearCurves();
                var bindings=new List<EditorCurveBinding>();var curves=new List<AnimationCurve>();int skipped=0;
                foreach(var binding in AnimationUtility.GetCurveBindings(source)) {
                    var updated=binding;
                    if(binding.type!=typeof(Animator)) {
                        if(binding.path!="" && !clipRoot.Find(binding.path)){skipped++;continue;}
                        updated.path=rootPath=="" ? binding.path : rootPath+(binding.path=="" ? "" : "/"+binding.path);
                    }
                    bindings.Add(updated);curves.Add(AnimationUtility.GetEditorCurve(source,binding));
                }
                AnimationUtility.SetEditorCurves(clip,bindings.ToArray(),curves.ToArray());
                foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(source)) {
                    if(binding.path!="" && !clipRoot.Find(binding.path))continue;
                    var updated=binding;updated.path=rootPath=="" ? binding.path : rootPath+(binding.path=="" ? "" : "/"+binding.path);
                    AnimationUtility.SetObjectReferenceCurve(clip,updated,AnimationUtility.GetObjectReferenceCurve(source,binding));
                }
                EditorUtility.SetDirty(clip);replacements.Add(new KeyValuePair<AnimationClip,AnimationClip>(source,clip));
                report.Add("CLIP "+source.name+" curves="+bindings.Count+" unused source curves omitted="+skipped);
            }
            controller.ApplyOverrides(replacements);EditorUtility.SetDirty(controller);
        }
        if(!avatar || !controller)throw new Exception("Missing converted rig assets");
        animator.avatar=avatar;animator.runtimeAnimatorController=controller;
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        EditorUtility.SetDirty(animator);
    }
}

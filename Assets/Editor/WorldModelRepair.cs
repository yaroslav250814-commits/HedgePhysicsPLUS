using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class WorldModelRepair
{
    const string Folder="Assets/WorldModelRig";
    static readonly Dictionary<string,SkinnedMeshRenderer> sources=new Dictionary<string,SkinnedMeshRenderer>();
    static readonly Dictionary<string,Mesh> originalMeshes=new Dictionary<string,Mesh>();
    static readonly List<string> report=new List<string>();
    static readonly HashSet<string> prepared=new HashSet<string>();
    static string Key(string s)=>WorldModelAudit.Key(s);
    public static void Install()
    {
        Directory.CreateDirectory("Migration/WorldModels/Before");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets","WorldModelRig");
        foreach(var file in Directory.GetFiles(WorldModelAudit.Reference,"*.fbx",SearchOption.AllDirectories))
        {
            string path=file.Replace('\\','/');var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            string modern="Assets/"+path.Substring(WorldModelAudit.Reference.Length);
            foreach(var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())originalMeshes[modern+"|"+Key(mesh.name)]=mesh;
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                sources[modern+"|"+Key(skin.sharedMesh.name)]=skin;
        }
        if(!Directory.Exists("Migration/WorldModels/before"))WorldModelGallery.Capture("before");
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if(!path.EndsWith(".prefab")||path.StartsWith("Assets/Editor/"))continue;
            var root=PrefabUtility.LoadPrefabContents(path);
            try{if(Apply(root)){Backup(path);PrefabUtility.SaveAsPrefabAsset(root,path);}}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(string path in new[]{"Assets/[Stage00_Test].unity","Assets/[Stage01_AltitudeLimit].unity"})
        {
            var scene=EditorSceneManager.OpenScene(path);bool changed=false;
            foreach(var root in scene.GetRootGameObjects())changed|=Apply(root);
            if(changed){Backup(path);EditorSceneManager.SaveScene(scene);}
        }
        AssetDatabase.SaveAssets(); WorldModelGallery.Capture("after");
        File.WriteAllLines("Migration/WorldModels/repair.txt",report);EditorApplication.Exit(0);
    }
    static void Backup(string path)
    {
        string destination="Migration/WorldModels/Before/"+path;
        if(File.Exists(destination))return;Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(path,destination);
    }
    public static Transform Find(Transform root,string path)
    {
        if(string.IsNullOrEmpty(path))return root;
        var t=root;
        foreach(string segment in path.Split('/'))
        {
            var matches=t.Cast<Transform>().Where(c=>Key(c.name)==Key(segment)).ToArray();
            if(matches.Length!=1)return null;t=matches[0];
        }
        return t;
    }
    static bool Apply(GameObject root)
    {
        bool changed=false;var animators=new Dictionary<Animator,string>();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var replacement=CompatibleStatic(filter.sharedMesh);
            if(!replacement)continue;
            report.Add("STATIC "+filter.name+" "+filter.sharedMesh.subMeshCount+" -> "+replacement.subMeshCount+" material sections");
            filter.sharedMesh=replacement;EditorUtility.SetDirty(filter);changed=true;
        }
        foreach(var collider in root.GetComponentsInChildren<MeshCollider>(true))
        {
            var replacement=CompatibleStatic(collider.sharedMesh);if(!replacement)continue;
            collider.sharedMesh=null;collider.sharedMesh=replacement;EditorUtility.SetDirty(collider);changed=true;
        }
        foreach(var target in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if(!target.sharedMesh)continue;
            string modelPath=AssetDatabase.GetAssetPath(target.sharedMesh);
            if(!sources.TryGetValue(modelPath+"|"+Key(target.sharedMesh.name),out var source))continue;
            var oldRoot=source.transform.root;
            string relative=AnimationUtility.CalculateTransformPath(source.transform,oldRoot);
            Transform actualRoot=null;
            for(var ancestor=target.transform;ancestor;ancestor=ancestor.parent)
                if(Find(ancestor,relative)==target.transform){actualRoot=ancestor;break;}
            if(!actualRoot)throw new Exception("Cannot resolve rig root: "+target.name+" "+relative);
            var bones=source.bones.Select(b=>Find(actualRoot,AnimationUtility.CalculateTransformPath(b,oldRoot))).ToArray();
            if(bones.Any(b=>!b))throw new Exception("Missing compatible bones: "+modelPath+" "+target.name);
            string prefix=Path.GetFileNameWithoutExtension(modelPath);
            string meshPath=Folder+"/"+prefix+"_"+source.name+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(!mesh){mesh=Object.Instantiate(source.sharedMesh);AssetDatabase.CreateAsset(mesh,meshPath);}
            else EditorUtility.CopySerialized(source.sharedMesh,mesh);
            float before=WorldModelAudit.RestError(target);
            target.sharedMesh=mesh;target.bones=bones;
            target.rootBone=source.rootBone?Find(actualRoot,AnimationUtility.CalculateTransformPath(source.rootBone,oldRoot)):null;
            target.localBounds=source.localBounds;EditorUtility.SetDirty(target);changed=true;
            report.Add("SKIN "+AnimationUtility.CalculateTransformPath(target.transform,null)+" source="+modelPath+" bones="+bones.Length+" restError="+before.ToString("F3")+" -> "+WorldModelAudit.RestError(target).ToString("F3"));
            var animator=target.GetComponentInParent<Animator>(true);
            if(animator)animators[animator]=prefix;
        }
        foreach(var pair in animators)ConfigureAnimator(pair.Key,pair.Value);
        return changed;
    }
    static Mesh CompatibleStatic(Mesh current)
    {
        if(!current)return null;string path=AssetDatabase.GetAssetPath(current);
        if(!originalMeshes.TryGetValue(path+"|"+Key(current.name),out var original)||original.vertexCount==0)return null;
        string meshPath=Folder+"/"+Path.GetFileNameWithoutExtension(path)+"_"+original.name+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(!mesh){mesh=Object.Instantiate(original);AssetDatabase.CreateAsset(mesh,meshPath);}
        return mesh;
    }
    static void ConfigureAnimator(Animator animator,string prefix)
    {
        var original=animator.runtimeAnimatorController;if(!original)return;
        string controllerPath=Folder+"/"+prefix+"Animations.overrideController";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
        string avatarPath=Folder+"/"+prefix+"Avatar.asset";
        var avatar=AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
        if(!avatar)
        {
            avatar=AvatarBuilder.BuildGenericAvatar(animator.gameObject,"");avatar.name=prefix+"Avatar";
            if(!avatar.isValid)throw new Exception("Invalid world prop avatar: "+prefix);
            AssetDatabase.CreateAsset(avatar,avatarPath);
        }
        if(prepared.Add(prefix))
        {
            if(!controller){controller=new AnimatorOverrideController(original);AssetDatabase.CreateAsset(controller,controllerPath);}
            var replacements=new List<KeyValuePair<AnimationClip,AnimationClip>>();
            foreach(var source in original.animationClips.Distinct())
            {
                string clipPath=Folder+"/"+prefix+"_"+source.name.Replace('/','_')+".anim";
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if(!clip){clip=Object.Instantiate(source);AssetDatabase.CreateAsset(clip,clipPath);}else EditorUtility.CopySerialized(source,clip);
                clip.ClearCurves();int missing=0;
                foreach(var binding in AnimationUtility.GetCurveBindings(source))
                {
                    var updated=binding;
                    if(binding.type!=typeof(Animator))
                    {
                        var target=Find(animator.transform,binding.path);
                        if(!target){missing++;continue;}updated.path=AnimationUtility.CalculateTransformPath(target,animator.transform);
                    }
                    AnimationUtility.SetEditorCurve(clip,updated,AnimationUtility.GetEditorCurve(source,binding));
                }
                foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
                {
                    var target=Find(animator.transform,binding.path);if(!target){missing++;continue;}
                    var updated=binding;updated.path=AnimationUtility.CalculateTransformPath(target,animator.transform);
                    AnimationUtility.SetObjectReferenceCurve(clip,updated,AnimationUtility.GetObjectReferenceCurve(source,binding));
                }
                if(missing>0)throw new Exception("Unresolved animation paths: "+source.name+" count="+missing);
                EditorUtility.SetDirty(clip);replacements.Add(new KeyValuePair<AnimationClip,AnimationClip>(source,clip));
                report.Add("CLIP "+prefix+" "+source.name+" bindings="+AnimationUtility.GetCurveBindings(clip).Length);
            }
            controller.ApplyOverrides(replacements);EditorUtility.SetDirty(controller);
        }
        animator.avatar=avatar;animator.runtimeAnimatorController=controller;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;EditorUtility.SetDirty(animator);
    }
}

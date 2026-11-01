using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

public static class SonicModelDiagnostics
{
    const string Folder="Migration/SonicModel";
    static readonly List<string> report=new List<string>();
    public static void CaptureGameplay(string name)
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
        var player=Object.FindFirstObjectByType<PlayerBhysics>();
        var animator=player.GetComponent<Action00_Regular>().CharacterAnimator;
        var camera=new GameObject("Visual verification camera").AddComponent<Camera>();
        camera.enabled=false;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.2f);
        Front=true;Capture(camera,animator.gameObject,name);
        string clips=string.Join(",",animator.GetCurrentAnimatorClipInfo(0).Select(c=>c.clip.name+":"+c.weight));
        var bone=animator.transform.Find("SkinOffset/SonicModel_5/metarig/Center/hips/hips_001/thigh_L");
        File.AppendAllText(Folder+"/play-pose-samples.txt",name+" grounded="+player.Grounded+" speed="+player.Body.linearVelocity.magnitude+" thigh="+bone.localEulerAngles+" clips="+clips+Environment.NewLine);
        Object.DestroyImmediate(camera.gameObject);
    }
    public static void IsolatePose()
    {
        Directory.CreateDirectory(Folder);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player_Sonic.prefab"));
        foreach(var c in player.GetComponentsInChildren<Camera>(true))c.gameObject.SetActive(false);
        foreach(var b in player.GetComponentsInChildren<Behaviour>(true))b.enabled=false;
        var visual=player.GetComponentsInChildren<Transform>(true).First(t=>t.name=="SonicModel_5");
        var native=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/SonicReference/OriginalSonicModel5.fbx"));
        foreach(var skin in native.GetComponentsInChildren<SkinnedMeshRenderer>())skin.sharedMaterials=visual.Find(skin.name).GetComponent<SkinnedMeshRenderer>().sharedMaterials;
        var camera=new GameObject("Camera").AddComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.2f);
        var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(30,-25,0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;Front=true;
        var original=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Prototyping/SonicModel/Sonic_Idle[i].anim");
        foreach(var root in new[]{visual,native.transform}) {
            bool prefab=root==visual;player.SetActive(prefab);native.SetActive(!prefab);
            original.SampleAnimation(root.gameObject,.25f);Capture(camera,root.gameObject,prefab ? "isolate-prefab.png":"isolate-native.png");
            report.Add("ROOT "+root.name+" pos="+root.localPosition+" scale="+root.lossyScale);
            foreach(string path in new[]{"metarig","metarig/Center","metarig/Center/hips","metarig/Center/hips/spine/chest/head","Cube_000","Cube_002","Cube_004"}) {
                var t=root.Find(path);report.Add(path+" position="+t.localPosition+" rotation="+t.localEulerAngles+" localScale="+t.localScale+" worldScale="+t.lossyScale);
            }
        }
        File.WriteAllLines(Folder+"/pose-isolation.txt",report);EditorApplication.Exit(0);
    }
    public static void RenderFixed()
    {
        Directory.CreateDirectory(Folder);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player_Sonic.prefab"));
        foreach(var c in player.GetComponentsInChildren<Camera>(true)) c.gameObject.SetActive(false);
        foreach(var b in player.GetComponentsInChildren<Behaviour>(true)) if(!(b is Animator)) b.enabled=false;
        var visual=player.GetComponentsInChildren<Transform>(true).First(t=>t.name=="SonicModel_5");
        var animator=player.GetComponentsInChildren<Animator>(true).First(a=>a.transform.name=="SonicSkin");
        var cam=new GameObject("Model camera").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.15f,.2f);cam.orthographic=true;
        var light=new GameObject("Model light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(30,-25,0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
        var graph=PlayableGraph.Create("Fixed animation validation");
        var output=AnimationPlayableOutput.Create(graph,"Sonic",animator);
        Front=true;
        foreach(var clip in animator.runtimeAnimatorController.animationClips.Distinct()) {
            int missing=AnimationUtility.GetCurveBindings(clip).Count(b=>b.path!="" && !animator.transform.Find(b.path));
            if(missing!=0)throw new Exception("Missing fixed clip bindings: "+clip.name+" "+missing);
            var playable=AnimationClipPlayable.Create(graph,clip);playable.SetTime(Mathf.Min(.3f,clip.length/2));output.SetSourcePlayable(playable);graph.Play();graph.Evaluate(0);
            var bone=visual.Find("metarig/Center/hips/hips_001/thigh_L");
            report.Add("CLIP "+clip.name+" length="+clip.length+" missing="+missing+" thigh="+bone.localEulerAngles);
            Capture(cam,visual.gameObject,"fixed-"+clip.name.Split('[')[0]+".png");playable.Destroy();
        }
        graph.Destroy();File.WriteAllLines(Folder+"/fixed-animation-report.txt",report);EditorApplication.Exit(0);
    }
    static string CompatiblePath(Transform t,Transform root) => Path(t,root).Replace('.','_');
    public static void PreviewReference()
    {
        Directory.CreateDirectory(Folder);
        string path="Assets/Editor/SonicReference/OriginalSonicModel5.fbx";
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        report.Add("Reference fileScale="+importer.fileScale);
        importer.useFileScale=false;importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.SaveAndReimport();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player_Sonic.prefab"));
        foreach(var c in player.GetComponentsInChildren<Camera>(true)) c.gameObject.SetActive(false);
        foreach(var b in player.GetComponentsInChildren<Behaviour>(true)) if(!(b is Animator)) b.enabled=false;
        var visual=player.GetComponentsInChildren<Transform>(true).First(t=>t.name=="SonicModel_5");
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        foreach(var original in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
            var target=visual.Find(Path(original.transform,model.transform))?.GetComponent<SkinnedMeshRenderer>();
            if(!target)throw new Exception("Missing skin "+original.name);
            target.sharedMesh=original.sharedMesh;
            target.bones=original.bones.Select(b=>visual.Find(Path(b,model.transform))).ToArray();
            target.rootBone=visual.Find(Path(original.rootBone,model.transform));
            target.localBounds=original.localBounds;
            report.Add("REPLACE "+target.name+" nullBones="+target.bones.Count(b=>!b));
        }
        var cam=new GameObject("Model camera").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.15f,.2f);cam.orthographic=true;
        var light=new GameObject("Model light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(30,-25,0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
        Capture(cam,visual.gameObject,"reference-prefab-rest.png");
        var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Prototyping/SonicModel/Sonic_Idle[i].anim");
        idle.SampleAnimation(visual.gameObject,.5f);Capture(cam,visual.gameObject,"reference-prefab-idle.png");
        cam.transform.position=Vector3.zero;
        var animator=player.GetComponentInChildren<Animator>();
        animator.avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().First();
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        var before=visual.GetComponentsInChildren<Transform>().Select(t=>t.localRotation).ToArray();
        var graph=PlayableGraph.Create("Sonic reference animation");
        var playable=AnimationClipPlayable.Create(graph,idle);playable.SetTime(.5);
        var output=AnimationPlayableOutput.Create(graph,"Sonic",animator);output.SetSourcePlayable(playable);graph.Play();graph.Evaluate(0);
        var after=visual.GetComponentsInChildren<Transform>();
        report.Add("Animator avatar valid="+animator.avatar.isValid+" bones changed="+after.Where((t,i)=>Quaternion.Angle(t.localRotation,before[i])>.1f).Count());
        Front=true;Capture(cam,visual.gameObject,"reference-animator-idle.png");
        foreach(string clipName in new[]{"Running_Low[i4]","Sonic_Up[i]","Sonic_Spinning[i]"}) {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Prototyping/SonicModel/"+clipName+".anim");
            playable.Destroy();playable=AnimationClipPlayable.Create(graph,clip);playable.SetTime(Mathf.Min(.2f,clip.length/2));output.SetSourcePlayable(playable);graph.Evaluate(0);
            Capture(cam,visual.gameObject,"reference-animator-"+clipName.Split('[')[0]+".png");
        }
        graph.Destroy();
        File.WriteAllLines(Folder+"/reference-preview.txt",report);EditorApplication.Exit(0);
    }
    public static void InspectReference()
    {
        Directory.CreateDirectory(Folder);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player_Sonic.prefab");
        var instance=prefab.GetComponentsInChildren<Transform>(true).First(t=>t.name=="SonicModel_5");
        foreach(string asset in new[]{"Assets/Prototyping/SonicModel/SonicModel_5.fbx","Assets/Editor/SonicReference/OriginalSonicModel5.fbx"}) {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(asset);
            if(!model) {report.Add("IMPORT FAILED "+asset);continue;}
            report.Add("MODEL "+asset);
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                var target=instance.Find(CompatiblePath(skin.transform,model.transform))?.GetComponent<SkinnedMeshRenderer>();
                int differences=target ? Enumerable.Range(0,Math.Min(target.bones.Length,skin.bones.Length)).Count(i=>target.bones[i].name!=skin.bones[i].name.Replace('.','_')) : -1;
                report.Add("BONE ORDER "+skin.name+" count="+skin.bones.Length+" mismatches="+differences+" meshPos="+skin.transform.localPosition+" rot="+skin.transform.localEulerAngles+" scale="+skin.transform.localScale);
            }
            foreach(var source in model.GetComponentsInChildren<Transform>(true)) {
                string path=CompatiblePath(source,model.transform); if(path=="") continue;
                var target=instance.Find(path);if(!target){report.Add("MISSING "+path);continue;}
                float a=Quaternion.Angle(source.localRotation,target.localRotation),p=Vector3.Distance(source.localPosition,target.localPosition);
                if(a>0.2f || p>0.002f)report.Add("DIFFERENCE "+path+" angle="+a+" position="+p);
            }
        }
        File.WriteAllLines(Folder+"/reference-inspection.txt",report);EditorApplication.Exit(0);
    }
    public static void Inspect()
    {
        Directory.CreateDirectory(Folder);
        EditorSceneManager.OpenScene("Assets/[Stage00_Test].unity");
        var player=Object.FindFirstObjectByType<PlayerBhysics>();
        foreach(var animator in player.GetComponentsInChildren<Animator>(true)) {
            report.Add("ANIMATOR "+Path(animator.transform,player.transform)+" active="+animator.gameObject.activeInHierarchy+" controller="+AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)+" avatar="+AssetDatabase.GetAssetPath(animator.avatar));
            if(!animator.runtimeAnimatorController) continue;
            foreach(var clip in animator.runtimeAnimatorController.animationClips.Distinct()) {
                var paths=AnimationUtility.GetCurveBindings(clip).Select(b=>b.path).Distinct().ToArray();
                var missing=paths.Where(p=>p!="" && !animator.transform.Find(p)).ToArray();
                report.Add("CLIP "+clip.name+" "+AssetDatabase.GetAssetPath(clip)+" paths="+paths.Length+" missing="+missing.Length+" "+string.Join(",",missing.Take(8)));
            }
        }
        foreach(var skin in player.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
            var baked=new Mesh(); skin.BakeMesh(baked);
            report.Add("SKIN "+Path(skin.transform,player.transform)+" active="+skin.gameObject.activeInHierarchy+" mesh="+AssetDatabase.GetAssetPath(skin.sharedMesh)+" vertices="+skin.sharedMesh.vertexCount+" bounds="+skin.sharedMesh.bounds+" baked="+baked.bounds+" bones="+skin.bones.Length+" null="+skin.bones.Count(b=>!b));
            Object.DestroyImmediate(baked);
        }
        var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prototyping/SonicModel/SonicModel_5.fbx");
        var instance=player.GetComponentsInChildren<Transform>(true).First(t=>t.name=="SonicModel_5");
        foreach(var source in model.GetComponentsInChildren<Transform>(true)) {
            string path=Path(source,model.transform);
            var target=path=="" ? instance : instance.Find(path);
            if(!target) {report.Add("MODEL PATH MISSING "+path);continue;}
            float angle=Quaternion.Angle(source.localRotation,target.localRotation);
            float pos=Vector3.Distance(source.localPosition,target.localPosition);
            float scale=Vector3.Distance(source.localScale,target.localScale);
            if(angle>0.2f || pos>0.002f || scale>0.002f) report.Add("TRANSFORM "+path+" angle="+angle+" pos="+pos+" scale="+scale+" modelPos="+source.localPosition+" prefabPos="+target.localPosition+" modelRot="+source.localEulerAngles+" prefabRot="+target.localEulerAngles+" modelScale="+source.localScale+" prefabScale="+target.localScale);
        }
        File.WriteAllLines(Folder+"/inspection.txt",report);
        if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null) RenderComparison();
        EditorApplication.Exit(0);
    }
    static string Path(Transform t,Transform root) { return t==root ? "" : (t.parent==root ? t.name : Path(t.parent,root)+"/"+t.name); }
    static void RenderComparison()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player_Sonic.prefab");
        var player=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        foreach(var c in player.GetComponentsInChildren<Camera>(true)) c.gameObject.SetActive(false);
        foreach(var b in player.GetComponentsInChildren<Behaviour>(true)) if(!(b is Animator)) b.enabled=false;
        var visual=player.GetComponentsInChildren<Transform>(true).First(t=>t.name=="SonicModel_5").gameObject;
        var native=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prototyping/SonicModel/SonicModel_5.fbx"));
        player.transform.position=Vector3.zero; player.transform.rotation=Quaternion.identity;
        native.transform.position=Vector3.zero;
        var cam=new GameObject("Model camera").AddComponent<Camera>();
        cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.12f,.15f,.2f); cam.orthographic=true;
        var light=new GameObject("Model light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(30,-25,0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
        var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Prototyping/SonicModel/Sonic_Idle[i].anim");
        native.SetActive(false); Capture(cam,visual,"prefab-rest.png");
        idle.SampleAnimation(visual,0.5f);Capture(cam,visual,"prefab-idle.png");
        player.SetActive(false);native.SetActive(true);Capture(cam,native,"model-rest.png");
        idle.SampleAnimation(native,0.5f);Capture(cam,native,"model-idle.png");
    }
    static bool Front;
    static void Capture(Camera camera,GameObject root,string name)
    {
        var originalMeshRenderers=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
        foreach(var r in originalMeshRenderers) r.enabled=false;
        var renderers=root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.enabled).ToArray();
        var temporary=new List<GameObject>();var bakedMeshes=new List<Mesh>();
        Bounds bounds=new Bounds();bool first=true;
        // Bake each pose explicitly: editor Camera.Render can otherwise reuse the
        // GPU skinning result from an earlier sample within this same editor frame.
        foreach(var skin in renderers) {
            var mesh=new Mesh();skin.BakeMesh(mesh,true);bakedMeshes.Add(mesh);
            var go=new GameObject("Pose snapshot");temporary.Add(go);
            go.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);go.transform.localScale=skin.transform.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=skin.sharedMaterials;
            if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);
            skin.enabled=false;
        }
        camera.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x)*1.2f;
        camera.transform.position=bounds.center+new Vector3(3,1,Front ? 6 : -6).normalized*Mathf.Max(10,bounds.size.magnitude*2);
        camera.transform.LookAt(bounds.center);
        var target=RenderTexture.GetTemporary(800,800,24);var previous=RenderTexture.active;
        camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        var pixels=new Texture2D(800,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,800,800),0,0);pixels.Apply();
        File.WriteAllBytes(Folder+"/"+name,pixels.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(pixels);
        foreach(var go in temporary)Object.DestroyImmediate(go);
        foreach(var mesh in bakedMeshes)Object.DestroyImmediate(mesh);
        foreach(var skin in renderers)skin.enabled=true;
        foreach(var renderer in originalMeshRenderers)renderer.enabled=true;
    }
}

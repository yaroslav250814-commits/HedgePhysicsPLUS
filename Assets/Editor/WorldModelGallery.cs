using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class WorldModelGallery
{
    public static void Capture(string phase)
    {
        if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
        Directory.CreateDirectory("Migration/WorldModels/"+phase);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var camera=new GameObject("Prop review camera").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.12f,.18f);
        var light=new GameObject("Prop review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;
        light.transform.rotation=Quaternion.Euler(35,-30,0);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
        string[] paths={"Prototyping/PrototypingObjects/Spring.prefab","Prototyping/PrototypeEnemies/[Enemy] - Motobug.prefab","Prototyping/PrototypeEnemies/[Enemy] - Gun Bettle.prefab","Prototyping/PrototypingObjects/CheckPoint.prefab","Prototyping/PrototypingObjects/DashRing.prefab","Prototyping/PrototypingObjects/GoalRing.prefab","Prototyping/PrototypingObjects/Speed Pad.prefab","Prototyping/PrototypingObjects/MovingPlatform.prefab"};
        foreach(string relative in paths)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/"+relative);if(!prefab)continue;
            var root=Object.Instantiate(prefab);foreach(var b in root.GetComponentsInChildren<Behaviour>(true))b.enabled=false;
            foreach(var hidden in root.GetComponentsInChildren<DeactivateOnStart>(true))hidden.gameObject.SetActive(false);
            foreach(var particle in root.GetComponentsInChildren<ParticleSystem>(true))particle.gameObject.SetActive(false);
            bool first=true;Bounds bounds=default;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if(!renderer.enabled||renderer is ParticleSystemRenderer)continue;
                Bounds world=renderer.bounds;
                if(renderer is SkinnedMeshRenderer skin){var mesh=new Mesh();skin.BakeMesh(mesh);var verts=mesh.vertices;if(verts.Length>0){world=new Bounds(skin.transform.TransformPoint(verts[0]),Vector3.zero);foreach(var v in verts)world.Encapsulate(skin.transform.TransformPoint(v));}Object.DestroyImmediate(mesh);}
                if(first){bounds=world;first=false;}else bounds.Encapsulate(world);
            }
            float size=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);if(size<.01f)size=1;
            camera.transform.position=bounds.center+new Vector3(.85f,.55f,1.3f)*size;
            camera.transform.LookAt(bounds.center);camera.orthographicSize=size*.7f;camera.nearClipPlane=.001f;camera.farClipPlane=size*20+10;
            var rt=RenderTexture.GetTemporary(640,640,24);var active=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(640,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();
            File.WriteAllBytes("Migration/WorldModels/"+phase+"/"+Path.GetFileNameWithoutExtension(relative)+".png",image.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(root);
        }
    }
}

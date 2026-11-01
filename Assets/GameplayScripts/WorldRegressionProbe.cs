#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WorldRegressionProbe:MonoBehaviour
{
    readonly List<string> report=new List<string>();int failures;float started;
    LevelRunState run;PlayerBhysics player;HurtControl hurt;LevelProgressControl level;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if(!File.Exists("Migration/world-regression.request"))return;File.Delete("Migration/world-regression.request");
        FindFirstObjectByType<LevelRunState>().gameObject.AddComponent<WorldRegressionProbe>();
    }
    void OnEnable(){Application.logMessageReceived+=Log;}
    void OnDisable(){Application.logMessageReceived-=Log;}
    void Log(string m,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)Check(false,m+"\n"+stack);}
    void Update(){if(started>0&&Time.realtimeSinceStartup-started>65){Check(false,"Timeout");Finish();}}
    IEnumerator Start()
    {
        started=Time.realtimeSinceStartup;Application.runInBackground=true;Time.timeScale=1;
        run=GetComponent<LevelRunState>();player=GetComponent<PlayerBhysics>();hurt=GetComponent<HurtControl>();level=GetComponent<LevelProgressControl>();
        GetComponent<PlayerBinput>().enabled=false;
        yield return new WaitForSeconds(.8f);
        player.enabled=false;player.Body.useGravity=false;player.Body.linearVelocity=Vector3.zero;hurt.IsHurt=true;
        Vector3 safeArea=player.Body.position+Vector3.up*12;
        Check(!hurt.isDead,"Player remains alive after normal scene startup");
        GetComponent<Objects_Interaction>().enabled=false;
        foreach(var collider in player.GetComponentsInChildren<Collider>())collider.enabled=false;
        Check(run.Enemies.SpawnPointCount>=90,"All ordinary and eternal spawn points are registered");
        var spring=FindFirstObjectByType<Spring_Proprieties>();var springAnimator=spring.GetComponent<Animator>();
        Check(spring.GetComponentsInChildren<SkinnedMeshRenderer>().All(s=>AssetDatabase.GetAssetPath(s.sharedMesh).StartsWith("Assets/WorldModelRig/")&&s.bones.Length==s.sharedMesh.bindposes.Length&&s.bones.All(b=>b)),"Scene springs use compatible native meshes and bones");
        Check(springAnimator.avatar&&springAnimator.avatar.isValid,"Spring avatar is valid");
        var springSkin=spring.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name=="Cube");
        var before=Bake(springSkin);springAnimator.SetTrigger("Hit");yield return new WaitForSeconds(.15f);var after=Bake(springSkin);
        Check(Difference(before,after)>.0001f,"Spring compresses and rebounds using its real animation");
        Capture(spring.gameObject,"spring-play.png");

        var once=FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include,FindObjectsSortMode.None).OrderBy(s=>Vector3.Distance(s.transform.position,transform.position)).First();
        // Ordinary spawn points belong to the original disabled test area. Activate one registered point in isolation.
        once.transform.SetParent(null,true);once.transform.position=safeArea;once.gameObject.SetActive(true);
        MoveNear(once.transform,once.Distance);yield return new WaitForSeconds(.4f);
        Check(once&&once.HasSpawned&&once.SpawnedEnemy,"Ordinary spawn point produces an enemy and stays in the scene");
        if(!once.SpawnedEnemy){Finish();yield break;}
        var first=once.SpawnedEnemy;int originalId=first.GetInstanceID();
        Check(first.scene==gameObject.scene,"Spawned enemy belongs to the gameplay scene");
        foreach(var skin in first.GetComponentsInChildren<SkinnedMeshRenderer>())Check(AssetDatabase.GetAssetPath(skin.sharedMesh).StartsWith("Assets/WorldModelRig/")&&skin.bones.All(b=>b),"Spawned enemy uses repaired geometry: "+skin.name);
        Freeze(first);Capture(first,"enemy-play.png");
        var health=first.GetComponent<EnemyHealth>();int score=run.Score;health.DealDamage(health.MaxHealth);yield return null;
        Check(!once.SpawnedEnemy&&run.Score==score+100,"Defeating an ordinary enemy removes it and awards score");
        yield return DieAndRespawn();yield return new WaitForSeconds(.35f);
        Check(once&&once.SpawnedEnemy&&once.SpawnedEnemy.GetInstanceID()!=originalId,"Ordinary enemy returns after player death");
        int secondId=once.SpawnedEnemy?once.SpawnedEnemy.GetInstanceID():0;
        yield return DieAndRespawn();yield return new WaitForSeconds(.35f);
        Check(once.SpawnedEnemy&&once.SpawnedEnemy.GetInstanceID()!=secondId,"A living enemy also resets on the next death without duplicates");

        var eternal=FindObjectsByType<EnemySpawnerEternal>(FindObjectsInactive.Include,FindObjectsSortMode.None).OrderBy(s=>Vector3.Distance(s.transform.position,transform.position)).First();
        eternal.transform.SetParent(null,true);eternal.transform.position=safeArea+Vector3.right*8;eternal.gameObject.SetActive(true);
        eternal.RespawnTime=0;MoveNear(eternal.transform,eternal.Distance);yield return new WaitForSeconds(.4f);
        Check(eternal.SpawnedEnemy,"Eternal spawn point produces an enemy");
        if(!eternal.SpawnedEnemy){Finish();yield break;}
        var live=eternal.SpawnedEnemy;int liveId=live.GetInstanceID();eternal.RespawnTime=100;
        Freeze(live);var animator=live.GetComponentInChildren<Animator>();
        Check(animator&&animator.avatar&&animator.avatar.isValid,"Enemy animation avatar is valid");
        var skinToTest=live.GetComponentInChildren<SkinnedMeshRenderer>();var pose=Bake(skinToTest);
        if(animator&&animator.parameters.Any(p=>p.name=="GroundSpeed"))animator.SetFloat("GroundSpeed",4);
        yield return new WaitForSeconds(.3f);
        Check(Difference(pose,Bake(skinToTest))>.0001f,"Enemy skeleton changes pose during animation");Capture(live,"enemy-animated.png");
        live.GetComponent<EnemyHealth>().DealDamage(live.GetComponent<EnemyHealth>().MaxHealth);yield return new WaitForSeconds(.1f);
        Check(!eternal.SpawnedEnemy,"Eternal enemy remains absent during its normal respawn delay");
        yield return DieAndRespawn();yield return new WaitForSeconds(.35f);
        Check(eternal.SpawnedEnemy&&eternal.SpawnedEnemy.GetInstanceID()!=liveId,"Player death restores eternal enemy immediately despite its old delay");
        Check(Objects_Interaction.RingAmmount==0,"Enemy restoration keeps death ring reset intact");
        Check(FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).All(e=>e.gameObject.scene==gameObject.scene),"All restored enemies remain owned by the correct scene");
        Finish();
    }
    void MoveNear(Transform spawn,float distance)
    {
        Vector3 point=spawn.position+Vector3.up*Mathf.Min(2,Mathf.Max(.1f,distance*.3f));
        player.Body.position=point;player.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
        var checkpoint=new GameObject("Regression checkpoint");checkpoint.transform.position=point;checkpoint.transform.rotation=player.transform.rotation;level.SetCheckPoint(checkpoint.transform);
    }
    IEnumerator DieAndRespawn()
    {
        hurt.IsHurt=false;Objects_Interaction.RingAmmount=3;hurt.isDead=true;
        Check(Objects_Interaction.RingAmmount==0,"Death immediately clears rings");
        for(int i=0;i<300&&hurt.isDead;i++)yield return new WaitForFixedUpdate();
        Check(!hurt.isDead,"Player completes death and returns to checkpoint");hurt.IsHurt=true;
    }
    static void Freeze(GameObject enemy)
    {
        foreach(var b in enemy.GetComponents<MonoBehaviour>())if(b is MotobugControl||b is GunBettleControl||b is EnemyBhysics)b.enabled=false;
        var body=enemy.GetComponent<Rigidbody>();if(body){body.linearVelocity=Vector3.zero;body.isKinematic=true;}
    }
    static Vector3[] Bake(SkinnedMeshRenderer skin){var mesh=new Mesh();skin.BakeMesh(mesh);var result=mesh.vertices;Destroy(mesh);return result;}
    static float Difference(Vector3[] a,Vector3[] b){if(a.Length!=b.Length)return 1;float value=0;for(int i=0;i<a.Length;i++)value=Mathf.Max(value,Vector3.Distance(a[i],b[i]));return value;}
    void Check(bool ok,string message){report.Add((ok?"PASS ":"FAIL ")+message);if(!ok)failures++;}
    void Finish(){Directory.CreateDirectory("Migration/WorldModels");report.Add("Failures="+failures);File.WriteAllLines("Migration/WorldModels/"+gameObject.scene.name+"-test.txt",report);EditorApplication.Exit(failures==0?0:1);}
    void Capture(GameObject root,string file)
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
        var transforms=root.GetComponentsInChildren<Transform>(true);var layers=transforms.Select(t=>t.gameObject.layer).ToArray();foreach(var t in transforms)t.gameObject.layer=30;
        bool first=true;Bounds bounds=default;
        foreach(var r in root.GetComponentsInChildren<Renderer>())if(r.enabled&&!(r is ParticleSystemRenderer)){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
        var camera=new GameObject("Runtime prop review").AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.12f,.18f);
        float size=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);camera.orthographicSize=size*.7f;camera.transform.position=bounds.center+new Vector3(.85f,.55f,1.3f)*size;camera.transform.LookAt(bounds.center);camera.nearClipPlane=.001f;camera.farClipPlane=size*20+10;
        var target=RenderTexture.GetTemporary(640,640,24);var active=RenderTexture.active;camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        var image=new Texture2D(640,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();
        File.WriteAllBytes("Migration/WorldModels/"+gameObject.scene.name+"-"+file,image.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Destroy(image);Destroy(camera.gameObject);
        for(int i=0;i<transforms.Length;i++)transforms[i].gameObject.layer=layers[i];
    }
}
#endif

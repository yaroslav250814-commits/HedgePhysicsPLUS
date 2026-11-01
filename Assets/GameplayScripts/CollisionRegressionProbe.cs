#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class CollisionRegressionProbe : MonoBehaviour
{
    PlayerBhysics player;
    Vector3 start;
    int steps, grounded, contacts;
    float minY;
    Rigidbody fastDrop, fastWall;
    float floorY, wallMinDistance=float.PositiveInfinity;
    Vector3 wallPoint,wallNormal;
    bool floorFound;
    string wallName="not found";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (!File.Exists("Migration/ground-test.request")) return;
        File.Delete("Migration/ground-test.request");
        var p = FindFirstObjectByType<PlayerBhysics>();
        if (p) p.gameObject.AddComponent<CollisionRegressionProbe>();
    }
    void Start() {
        player=GetComponent<PlayerBhysics>(); start=transform.position; minY=start.y;
        Application.runInBackground=true; Time.timeScale=1; EditorApplication.isPaused=false;
        var pause=FindFirstObjectByType<PauseCotrol>();
        if(pause) { pause.Pause.SetActive(false); pause.enabled=false; }
        File.WriteAllText("Migration/ground-test-started.txt",UnityEngine.SceneManagement.SceneManager.GetActiveScene().name+" "+System.DateTime.UtcNow.ToString("O"));
        Physics.SyncTransforms();
        int mask=~(1<<gameObject.layer);
        floorFound=Physics.Raycast(start+Vector3.up,Vector3.down,out var floor,1000,mask,QueryTriggerInteraction.Ignore);
        if(floorFound) {
            floorY=floor.point.y;
            fastDrop=Sphere("Fast drop test",floor.point+Vector3.up*10,Vector3.down*120);
            for(int i=0;i<16;i++) {
                var direction=Quaternion.Euler(0,i*22.5f,0)*Vector3.forward;
                foreach(var wall in Physics.RaycastAll(floor.point+Vector3.up*1.5f,direction,200,mask,QueryTriggerInteraction.Ignore)) {
                    if(wall.collider.attachedRigidbody || !(wall.collider is MeshCollider) || Mathf.Abs(wall.normal.y)>=.2f) continue;
                    wallPoint=wall.point; wallNormal=wall.normal; wallName=wall.collider.name;
                    fastWall=Sphere("Fast wall test",wallPoint+wallNormal*5,-wallNormal*120); break;
                }
                if(fastWall) break;
            }
        }
    }
    static Rigidbody Sphere(string name,Vector3 position,Vector3 velocity) {
        var go=new GameObject(name); go.transform.position=position; go.layer=2;
        var collider=go.AddComponent<SphereCollider>(); collider.radius=.25f;
        var character=FindFirstObjectByType<PlayerBhysics>();
        if(character) foreach(var other in character.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(collider,other);
        var body=go.AddComponent<Rigidbody>(); body.useGravity=false;
        body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        body.maxLinearVelocity=200; body.linearVelocity=velocity;
        return body;
    }
    void OnCollisionStay(Collision collision) { contacts++; }
    void FixedUpdate()
    {
        steps++; minY=Mathf.Min(minY,transform.position.y);
        if(player.Grounded) grounded++;
        if(fastWall) wallMinDistance=Mathf.Min(wallMinDistance,Vector3.Dot(fastWall.position-wallPoint,wallNormal));
        if(steps<240) return;
        bool supported=Physics.Raycast(transform.position+Vector3.up*.1f,Vector3.down,1.2f,~(1<<gameObject.layer),QueryTriggerInteraction.Ignore);
        var lines=new List<string>{"Scene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,"Start="+start,"End="+transform.position,"Minimum Y="+minY,"Grounded steps="+grounded,"Contact frames="+contacts,
            (floorFound && grounded>120 && contacts>60 && supported ? "PASS" : "FAIL")+" Player lands on real level and stays on ground",
            (fastDrop && fastDrop.position.y>=floorY+.15f ? "PASS" : "FAIL")+" Continuous collision at 120 m/s downward",
            "Wall="+wallName+" normal="+wallNormal+" minimum signed distance="+wallMinDistance,
            fastWall ? (wallMinDistance>=.1f ? "PASS" : "FAIL")+" Continuous collision at 120 m/s into real level wall" : "SKIP No vertical static mesh wall within 200m of spawn; wall test runs in Altitude Limit",
            (Cursor.visible && Cursor.lockState==CursorLockMode.None ? "PASS" : "FAIL")+" Desktop cursor stays visible and unlocked"};
        File.WriteAllLines("Migration/ground-playtest.txt",lines);
        if(Application.isBatchMode) EditorApplication.Exit(lines.Exists(line=>line.StartsWith("FAIL")) ? 1 : 0);
        else EditorApplication.isPlaying=false;
    }
}
#endif

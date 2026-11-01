#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public sealed class HomingSafetyProbe:MonoBehaviour
{
    readonly List<string> report=new List<string>();int failures;float started;
    PlayerBhysics player;ActionManager actions;Action02_Homing attack;HomingAttackControl homing;
    Vector3 origin;Keyboard keyboard;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if(!File.Exists("Migration/homing-safety.request"))return;
        File.Delete("Migration/homing-safety.request");FindFirstObjectByType<PlayerBhysics>().gameObject.AddComponent<HomingSafetyProbe>();
    }
    void OnEnable(){Application.logMessageReceived+=Log;}
    void OnDisable(){Application.logMessageReceived-=Log;}
    void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)Check(false,message+"\n"+stack);}
    void Update(){if(started>0&&Time.realtimeSinceStartup-started>45){Check(false,"Timeout");Finish();}}
    IEnumerator Start()
    {
        started=Time.realtimeSinceStartup;Application.runInBackground=true;Time.timeScale=1;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        player=GetComponent<PlayerBhysics>();actions=GetComponent<ActionManager>();attack=actions.Action02;homing=actions.Action02Control;
        GetComponent<PlayerBinput>().enabled=false;
        yield return new WaitForSeconds(.8f);
        origin=transform.position+Vector3.up*8;player.enabled=false;player.Body.useGravity=false;
        foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
        ResetPlayer();

        var target=Target("Live target");Begin(target);
        Check(actions.Action==2&&attack.Target==target.transform,"A valid target starts a real homing attack");
        yield return new WaitForFixedUpdate();
        Check(player.Body.linearVelocity.magnitude>1,"Normal homing moves toward its locked target");
        player.isRolling=true;Destroy(target);yield return null;yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
        Check(Recovered(),"Destroying the locked target exits homing and restores regular action");
        Check(!player.isRolling&&player.Body.linearVelocity.magnitude<=player.MaxSpeed+.01f,"Target loss clears stuck rolling state and excessive attack speed");

        ResetPlayer();target=Target("Disabled target");Begin(target);target.SetActive(false);
        Check(!homing.IsValidTarget(target),"Disabled target is invalid immediately");
        yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
        Check(Recovered(),"Disabling a target in flight returns control without death");Destroy(target);

        ResetPlayer();target=Target("Selection race");HomingAttackControl.TargetObject=target;
        Check(homing.HasTarget,"Live selected target is available");
        target.SetActive(false);Check(!homing.HasTarget,"HasTarget does not remain true for an inactive object");
        actions.ChangeAction(2);attack.IsAirDash=false;attack.InitialEvents();
        Check(Recovered(),"A target lost between selection and InitialEvents cancels safely");Destroy(target);

        ResetPlayer();HomingAttackControl.TargetObject=null;actions.ChangeAction(2);attack.IsAirDash=false;attack.InitialEvents();
        Check(Recovered(),"Starting with no target cannot leave action 2 enabled");

        ResetPlayer();target=Target("Timeout target");float limit=attack.HomingTimerLimit;attack.HomingTimerLimit=1;Begin(target);
        for(int i=0;i<4;i++)yield return new WaitForFixedUpdate();
        Check(Recovered(),"The normal homing timeout still returns to regular control");attack.HomingTimerLimit=limit;Destroy(target);

        ResetPlayer();HomingAttackControl.TargetObject=null;player.RawInput=Vector3.forward;actions.ChangeAction(2);attack.IsAirDash=true;attack.InitialEvents();
        for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
        Check(Recovered()&&player.Body.linearVelocity.magnitude>1,"Intentional air dash works without a homing target");
        Check(!attack.AirDashAvailable,"The first air dash spends the airborne dash allowance");
        homing.enabled=false;homing.HomingAvailable=true;HomingAttackControl.TargetObject=null;player.Body.linearVelocity=Vector3.zero;
        keyboard=InputSystem.AddDevice<Keyboard>();
        actions.ChangeAction(1);actions.Action01.Counter=.2f;
        for(int i=0;i<3;i++)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return new WaitForSeconds(.12f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.12f);
        }
        Check(actions.Action==1&&player.Body.linearVelocity.sqrMagnitude<.01f&&!attack.AirDashAvailable,"Repeated jump presses cannot produce additional dashes in jump action");
        actions.ChangeAction(0);
        for(int i=0;i<3;i++)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return new WaitForSeconds(.12f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.12f);
        }
        Check(Recovered()&&player.Body.linearVelocity.sqrMagnitude<.01f&&!attack.AirDashAvailable,"Repeated presses cannot bypass the limit through regular airborne action");
        actions.ChangeAction(2);attack.IsAirDash=true;attack.InitialEvents();
        Check(Recovered()&&!attack.AirDashAvailable,"Direct air-dash entry also enforces the limit");
        GetComponent<Objects_Interaction>().updateTargets=true;yield return new WaitForSeconds(.12f);
        Check(!attack.AirDashAvailable,"Refreshing homing targets does not replenish an air dash");
        target=Target("Homing after used dash");Begin(target);
        Check(actions.Action==2&&!attack.IsAirDash&&!attack.AirDashAvailable,"A targeted homing attack remains available after spending the free dash");
        Destroy(target);yield return null;for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
        Check(Recovered()&&!attack.AirDashAvailable,"Losing a homing target cannot replenish a spent air dash");
        InputSystem.RemoveDevice(keyboard);keyboard=null;homing.enabled=true;

        // Exercise the real ground detection and landing transition, without hand-resetting the allowance.
        player.Body.position=origin;player.Body.linearVelocity=Vector3.zero;player.MoveInput=Vector3.zero;player.RawInput=Vector3.zero;
        bool sphere=player.UseSphereToGetNormal;player.UseSphereToGetNormal=false;player.WasOnAir=true;player.Grounded=false;
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Air dash landing fixture";floor.transform.position=origin-Vector3.up*2.5f;floor.transform.localScale=new Vector3(20,1,20);floor.GetComponent<Renderer>().enabled=false;
        Physics.SyncTransforms();player.enabled=true;
        for(int i=0;i<150&&!player.Grounded;i++)yield return new WaitForFixedUpdate();
        Check(player.Grounded&&attack.AirDashAvailable,"Actual ground detection replenishes the air dash after landing");
        player.enabled=false;player.UseSphereToGetNormal=sphere;Destroy(floor);player.Grounded=false;player.RawInput=Vector3.forward;player.Body.linearVelocity=Vector3.zero;
        actions.ChangeAction(2);attack.IsAirDash=true;attack.InitialEvents();for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
        Check(Recovered()&&!attack.AirDashAvailable&&player.Body.linearVelocity.magnitude>1,"A new airborne period permits one new dash");
        var deathTarget=Target("Death during homing");Begin(deathTarget);
        GetComponent<HurtControl>().isDead=true;
        for(int i=0;i<300&&GetComponent<HurtControl>().isDead;i++)yield return new WaitForFixedUpdate();
        Check(!GetComponent<HurtControl>().isDead&&attack.AirDashAvailable&&attack.HomingAvailable,"Death during an attack restores dash readiness before another landing");Destroy(deathTarget);
        GetComponent<PlayerBinput>().enabled=false;

        ResetPlayer();var monitor=FindObjectsByType<MonitorData>(FindObjectsSortMode.None).First(m=>m.Type==MonitorType.Ring);
        Begin(monitor.gameObject);monitor.DestroyMonitor();
        for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
        Check(Recovered(),"A ring monitor hidden during flight cannot trap the player");

        ResetPlayer();var spawner=FindObjectsByType<EnemySpawnerEternal>(FindObjectsInactive.Include,FindObjectsSortMode.None).First();
        spawner.transform.SetParent(null,true);spawner.transform.position=origin+Vector3.forward*3;spawner.Distance=30;spawner.RespawnTime=0;spawner.gameObject.SetActive(true);
        yield return new WaitForSeconds(.2f);
        Check(spawner.SpawnedEnemy,"Enemy fixture uses a registered gameplay spawner");
        var enemy=spawner.SpawnedEnemy.GetComponent<EnemyHealth>();var enemyTarget=enemy.GetComponentsInChildren<Transform>().First(t=>t.CompareTag("HomingTarget"));
        Begin(enemyTarget.gameObject);enemy.DealDamage(enemy.MaxHealth);
        Check(enemy.IsDefeated&&!homing.IsValidTarget(enemyTarget.gameObject),"Defeated enemy stops being targetable before deferred destruction");
        for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
        Check(Recovered(),"Enemy defeat during attack does not require player death to recover");

        ResetPlayer();GetComponent<LevelRunState>().Enemies.Restore();yield return new WaitForSeconds(.2f);
        enemyTarget=spawner.SpawnedEnemy.GetComponentsInChildren<Transform>().First(t=>t.CompareTag("HomingTarget"));Begin(enemyTarget.gameObject);
        GetComponent<LevelRunState>().Enemies.Restore();
        for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
        Check(Recovered(),"Recreating enemies while a target is locked cannot leave a stale attack");

        ResetPlayer();var other=SceneManager.CreateScene("Homing foreign scene");target=Target("Foreign target");SceneManager.MoveGameObjectToScene(target,other);Begin(target);
        Check(Recovered(),"Targets from another scene are rejected");yield return SceneManager.UnloadSceneAsync(other);

        ResetPlayer();target=Target("Jump recovery target");Begin(target);Destroy(target);yield return null;
        for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
        player.Grounded=true;player.GroundNormal=Vector3.up;player.Body.linearVelocity=Vector3.zero;
        keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));
        for(int i=0;i<4;i++)yield return new WaitForFixedUpdate();
        Check(actions.Action==1&&player.Body.linearVelocity.y>0,"Jump input works again after target loss without dying");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        Check(!GetComponent<HurtControl>().isDead,"All target-loss cases recover with the player alive");
        Finish();
    }
    GameObject Target(string name){var target=new GameObject(name);target.tag="HomingTarget";target.transform.position=origin+Vector3.forward*4;return target;}
    void Begin(GameObject target){HomingAttackControl.TargetObject=target;actions.ChangeAction(2);attack.IsAirDash=false;attack.InitialEvents();}
    void ResetPlayer(){actions.ChangeAction(0);attack.ReplenishAirDash();player.Body.position=origin;player.Body.linearVelocity=Vector3.zero;player.Grounded=false;player.RawInput=Vector3.zero;player.PreviousRawInput=Vector3.zero;player.isRolling=false;}
    bool Recovered()=>actions.Action==0&&actions.Action00.enabled&&!attack.enabled&&!attack.Target;
    void Check(bool ok,string message){report.Add((ok?"PASS ":"FAIL ")+message);if(!ok)failures++;}
    void Finish(){if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);Directory.CreateDirectory("Migration/HomingSafety");report.Add("Failures="+failures);File.WriteAllLines("Migration/HomingSafety/"+gameObject.scene.name+"-test.txt",report);EditorApplication.Exit(failures==0?0:1);}
}
#endif

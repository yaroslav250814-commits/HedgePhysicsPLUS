#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class DropDashProbe:MonoBehaviour
{
    readonly List<string> report=new List<string>();int failures;float started;
    PlayerBhysics player;ActionManager actions;Action06_DropDash drop;Action02_Homing attack;
    HomingAttackControl homing;Keyboard keyboard;Gamepad gamepad;Vector3 origin;
    Collider[] colliders;Objects_Interaction objects;GameObject explosion;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if(!File.Exists("Migration/drop-dash.request"))return;File.Delete("Migration/drop-dash.request");
        FindFirstObjectByType<PlayerBhysics>().gameObject.AddComponent<DropDashProbe>();
    }
    void OnEnable(){Application.logMessageReceived+=Log;}
    void OnDisable(){Application.logMessageReceived-=Log;}
    void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)Check(false,message+"\n"+stack);}
    void Update(){if(started>0&&Time.realtimeSinceStartup-started>65){Check(false,"Timeout");Finish();}}
    IEnumerator Start()
    {
        started=Time.realtimeSinceStartup;Application.runInBackground=true;Time.timeScale=1;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        player=GetComponent<PlayerBhysics>();actions=GetComponent<ActionManager>();drop=actions.Action06;attack=actions.Action02;homing=actions.Action02Control;objects=GetComponent<Objects_Interaction>();
        GetComponent<PlayerBinput>().enabled=false;yield return new WaitForSeconds(.8f);
        origin=player.Body.position+Vector3.up*500;player.Body.interpolation=RigidbodyInterpolation.None;
        player.Body.useGravity=false;player.enabled=false;homing.enabled=false;
        colliders=GetComponentsInChildren<Collider>();foreach(var collider in colliders)collider.enabled=false;
        explosion=FindObjectsByType<EnemySpawnerEternal>(FindObjectsInactive.Include,FindObjectsSortMode.None).First().Enemy.GetComponent<EnemyHealth>().Explosion;
        keyboard=InputSystem.AddDevice<Keyboard>();gamepad=InputSystem.AddDevice<Gamepad>();
        Check(drop&&Mathf.Approximately(drop.LaunchSpeed,actions.Action03.MaximunCharge),"Drop Dash uses the existing maximum Spin Dash power");
        Check(player.MaxFallingSpeed==120&&player.DescentGravityMultiplier==1,"Restored fall settings are preserved");

        Reset();player.Grounded=true;player.GroundNormal=Vector3.up;
        yield return Keys(Key.LeftCtrl);yield return new WaitForSeconds(actions.Action03.MaximunCharge/actions.Action03.SpinDashChargingSpeed*Time.fixedDeltaTime+.1f);
        Check(actions.Action==3,"Grounded Spin Dash still charges on its usual button");yield return Keys();
        Check(actions.Action==0&&Mathf.Abs(player.Body.linearVelocity.magnitude-drop.LaunchSpeed)<.1f,"A fully charged ordinary Spin Dash matches the Drop Dash launch power");
        Reset();yield return Keys(Key.LeftCtrl);yield return Keys();yield return Keys(Key.Space);
        Check(actions.Action==6&&!drop.Charged&&attack.AirDashAvailable,"An uncharged Drop Dash cannot produce an instant boosted attack");yield return Keys();

        Reset();actions.ChangeAction(1);actions.Action01.Counter=actions.Action01.JumpDuration;
        yield return Keys(Key.LeftCtrl);yield return new WaitForSeconds(drop.ChargeDuration+.06f);
        Check(actions.Action==6&&drop.Charged,"Keyboard Spin Dash in jump action charges Drop Dash");
        Check(actions.Action03.SpinDashBall.enabled&&actions.Action00.CharacterAnimator.GetInteger("Action")==1,"Drop Dash displays Sonic as a spinning ball");
        yield return Keys();player.enabled=true;player.UseSphereToGetNormal=false;
        yield return new WaitForSeconds(drop.BoostInputWindow+.12f);
        Check(actions.Action==6&&drop.Released&&player.isRolling&&player.Body.linearVelocity.y<0,"Releasing without another input falls naturally while rolling");
        Check(Vector3.ProjectOnPlane(player.Body.linearVelocity,Vector3.up).magnitude<1,"Releasing does not automatically launch an air dash");
        yield return Keys(Key.Space);
        Check(actions.Action==6&&attack.AirDashAvailable,"A late jump does not trigger the short release combo");yield return Keys();
        player.Body.linearVelocity=Vector3.zero;var floor=Floor(player.Body.position,Quaternion.identity);
        for(int i=0;i<180&&actions.Action==6;i++)yield return new WaitForFixedUpdate();
        Check(player.Grounded&&actions.Action==0&&player.Body.linearVelocity.magnitude<drop.LaunchSpeed*.5f,"Landing after releasing does not deliver a charged ground launch");
        player.enabled=false;Destroy(floor);yield return null;

        yield return Charge();floor=Floor(origin,Quaternion.Euler(20,0,0));player.enabled=true;
        for(int i=0;i<180&&actions.Action==6;i++)yield return new WaitForFixedUpdate();
        Check(player.Grounded&&actions.Action==0&&player.isRolling,"A held Drop Dash launches on actual slope contact");
        Check(Mathf.Abs(player.Body.linearVelocity.magnitude-drop.LaunchSpeed)<.1f,"Landing launch equals maximum Spin Dash speed");
        Check(Mathf.Abs(Vector3.Dot(player.Body.linearVelocity,player.GroundNormal))<.1f,"Landing launch follows the ground tangent");
        yield return new WaitForSeconds(.04f);Check(actions.Action!=3,"Continuing to hold Spin Dash does not brake the landing launch into a new charge");
        player.enabled=false;Destroy(floor);yield return Keys();

        yield return Charge();yield return Keys();yield return Keys(Key.Space);yield return new WaitForFixedUpdate();
        Check(actions.Action==0&&!attack.AirDashAvailable,"The boosted free dash spends the airborne dash allowance");
        Check(Mathf.Abs(player.Body.linearVelocity.magnitude-drop.LaunchSpeed)<.1f&&Vector3.Dot(player.Body.linearVelocity,Vector3.forward)>0,"The boosted free dash is faster and has a facing fallback with zero movement input");
        yield return Charge(false);yield return Keys();yield return Keys(Key.Space);
        Check(actions.Action==6&&!attack.AirDashAvailable&&player.Body.linearVelocity.sqrMagnitude<.01f,"Recharging Drop Dash cannot grant infinite free air dashes");

        // A target remains available after spending the free dash. Use the real enemy hit handler.
        yield return Charge(false);var enemy=Enemy(origin+Vector3.forward*100,out var target,out var hit);
        HomingAttackControl.TargetObject=target;yield return Keys();yield return Keys(Key.Space);
        Check(attack.IsBoostedHoming&&attack.Target==target.transform,"The release combo retains target-lock homing even after a used free dash");
        Check(player.Body.linearVelocity.magnitude>attack.HomingAttackSpeed&&Mathf.Abs(player.Body.linearVelocity.magnitude-drop.LaunchSpeed)<.1f,"Drop Dash homing is faster than normal homing");
        target.transform.position+=Vector3.right*35;yield return new WaitForFixedUpdate();
        Check(Vector3.Dot(player.Body.linearVelocity.normalized,(target.transform.position-player.Body.position).normalized)>.99f,"Boosted homing continues steering toward a moving target");
        int score=GetComponent<LevelRunState>().Score;objects.OnTriggerEnter(hit);
        Check(enemy.IsDefeated&&GetComponent<LevelRunState>().Score==score+GetComponent<LevelRunState>().EnemyPoints,"Boosted homing still destroys enemies and awards normal points");
        Check(actions.Action==1&&Mathf.Abs(player.Body.linearVelocity.y-attack.BoostReboundSpeed)<.01f,"Holding jump at a successful boosted hit gives the upward burst");
        Check(attack.AirDashAvailable&&attack.HomingAvailable&&homing.HomingAvailable,"The upward rebound restores all airborne attack availability");
        Check(!drop.enabled&&!attack.IsBoostedHoming,"The rebound clears the completed charge and boosted attack state");
        objects.OnTriggerEnter(hit);Check(GetComponent<LevelRunState>().Score==score+GetComponent<LevelRunState>().EnemyPoints,"A defeated target cannot award a second hit");yield return Keys();

        SpendDash();yield return Charge(false);enemy=Enemy(origin+Vector3.forward*100,out target,out hit);
        HomingAttackControl.TargetObject=target;yield return Keys();yield return Keys(Key.Space);yield return Keys();objects.OnTriggerEnter(hit);
        Check(enemy.IsDefeated&&!attack.AirDashAvailable&&Mathf.Abs(player.Body.linearVelocity.y-objects.BouncingPower)<.01f,"Releasing jump before the hit keeps the normal bounce and does not reset spent abilities");
        yield return null;

        SpendDash();yield return Charge(false);target=Target(origin+Vector3.forward*100);HomingAttackControl.TargetObject=target;
        yield return Keys();yield return Keys(Key.Space);Destroy(target);yield return null;yield return new WaitForFixedUpdate();
        Check(actions.Action==0&&!attack.AirDashAvailable&&player.Body.linearVelocity.y<attack.BoostReboundSpeed-.1f,"Losing the boosted target does not grant a rebound or ability reset");

        SpendDash();yield return Keys();enemy=Enemy(origin+Vector3.forward*100,out target,out hit);HomingAttackControl.TargetObject=target;
        actions.ChangeAction(2);attack.IsAirDash=false;attack.InitialEvents();yield return Keys(Key.Space);objects.OnTriggerEnter(hit);
        Check(!attack.AirDashAvailable&&Mathf.Abs(player.Body.linearVelocity.y-objects.BouncingPower)<.01f,"Ordinary homing does not gain the Drop Dash rebound");yield return null;

        SpendDash();yield return Charge(false);
        var monitor=FindObjectsByType<MonitorData>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(m=>m.Type==MonitorType.Ring&&m.gameObject.activeInHierarchy);
        monitor.transform.position=origin+Vector3.forward*100;HomingAttackControl.TargetObject=monitor.gameObject;
        yield return Keys();yield return Keys(Key.Space);GetComponent<Monitors_Interactions>().OnTriggerEnter(monitor.GetComponent<Collider>());
        Check(!monitor.gameObject.activeSelf&&actions.Action==1&&attack.AirDashAvailable&&Mathf.Abs(player.Body.linearVelocity.y-attack.BoostReboundSpeed)<.01f,"A successful boosted homing hit on a ring monitor also rebounds and replenishes abilities");yield return Keys();

        // Exercise a real trigger crossing at boosted speed, rather than only invoking its handler.
        yield return Charge();enemy=Enemy(origin+Vector3.forward*12,out target,out hit);HomingAttackControl.TargetObject=target;
        foreach(var collider in colliders)collider.enabled=true;Physics.SyncTransforms();
        score=GetComponent<LevelRunState>().Score;yield return Keys();yield return Keys(Key.Space);
        for(int i=0;i<35&&enemy&&!enemy.IsDefeated;i++)yield return new WaitForFixedUpdate();
        Check(!enemy||enemy.IsDefeated,"Boosted homing reaches a real enemy trigger without tunnelling through it");
        Check(GetComponent<LevelRunState>().Score==score+GetComponent<LevelRunState>().EnemyPoints&&attack.AirDashAvailable&&player.Body.linearVelocity.y>0,"A real collision awards the kill, rebounds upward and restores the air dash");
        foreach(var collider in colliders)collider.enabled=false;yield return Keys();

        Reset();InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.East));yield return new WaitForSeconds(drop.ChargeDuration+.08f);
        Check(actions.Action==6&&drop.Charged,"Gamepad Spin Dash also charges Drop Dash");
        InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return new WaitForSeconds(.03f);
        InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.South));yield return new WaitForSeconds(.05f);
        Check(!attack.AirDashAvailable&&player.Body.linearVelocity.magnitude>attack.AirDashSpeed,"Gamepad release then jump activates the boosted air dash");
        InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return null;

        yield return Charge();Time.timeScale=0;yield return new WaitForSecondsRealtime(.25f);
        Check(actions.Action==6&&drop.Charged&&!drop.Released,"Pausing preserves the Drop Dash charge");Time.timeScale=1;
        actions.ChangeAction(4);Check(!drop.enabled&&!actions.Action03.SpinDashBall.enabled,"Damage cancels the charge and restores the normal renderer");
        yield return Charge();GetComponent<HurtControl>().isDead=true;yield return new WaitForFixedUpdate();
        Check(!drop.enabled&&actions.Action==4,"Death cancels Drop Dash");
        Finish();
    }
    IEnumerator Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));yield return null;yield return new WaitForSeconds(.035f);}
    IEnumerator Charge(bool replenish=true){yield return Keys();Reset(replenish);yield return Keys(Key.LeftCtrl);yield return new WaitForSeconds(drop.ChargeDuration+.06f);}
    void Reset(bool replenish=true)
    {
        player.enabled=false;actions.ChangeAction(0);if(replenish)attack.ReplenishAirDash();homing.HomingAvailable=true;HomingAttackControl.TargetObject=null;
        transform.SetPositionAndRotation(origin,Quaternion.identity);Physics.SyncTransforms();player.Body.position=origin;player.Body.linearVelocity=Vector3.zero;
        player.Grounded=false;player.isRolling=false;player.RawInput=player.PreviousRawInput=player.MoveInput=Vector3.zero;actions.Action00.CharacterAnimator.transform.rotation=Quaternion.identity;
    }
    void SpendDash(){Reset();actions.ChangeAction(2);attack.IsAirDash=true;attack.InitialEvents();actions.ChangeAction(0);}
    GameObject Floor(Vector3 position,Quaternion rotation){var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Drop Dash floor";floor.transform.SetPositionAndRotation(position-Vector3.up*2.5f,rotation);floor.transform.localScale=new Vector3(100,1,100);floor.GetComponent<Renderer>().enabled=false;Physics.SyncTransforms();return floor;}
    GameObject Target(Vector3 position){var target=new GameObject("Drop Dash target");target.tag="HomingTarget";target.transform.position=position;return target;}
    EnemyHealth Enemy(Vector3 position,out GameObject target,out Collider hit)
    {
        var root=new GameObject("Drop Dash enemy");root.transform.position=position;var enemy=root.AddComponent<EnemyHealth>();enemy.Explosion=explosion;
        target=Target(position);target.transform.SetParent(root.transform,true);target.tag="Enemy";hit=target.AddComponent<BoxCollider>();((BoxCollider)hit).size=Vector3.one*2;hit.isTrigger=true;
        return enemy;
    }
    void Check(bool ok,string message){report.Add((ok?"PASS ":"FAIL ")+message);if(!ok)failures++;}
    void Finish(){Time.timeScale=1;if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(gamepad!=null&&gamepad.added)InputSystem.RemoveDevice(gamepad);Directory.CreateDirectory("Migration/DropDash");report.Add("Failures="+failures);File.WriteAllLines("Migration/DropDash/"+gameObject.scene.name+"-test.txt",report);EditorApplication.Exit(failures==0?0:1);}
}
#endif

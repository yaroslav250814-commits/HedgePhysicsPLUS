#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class CustomizationProbe : MonoBehaviour
{
    readonly List<string> report=new List<string>();
    readonly Dictionary<FieldInfo,bool> initialFeatures=new Dictionary<FieldInfo,bool>();
    int failures;float started;Vector3 origin;Keyboard keyboard;
    PlayerBhysics player;ActionManager actions;Action06_DropDash drop;Action02_Homing attack;HomingAttackControl homing;Objects_Interaction objects;GameObject explosion;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if(!File.Exists("Migration/customization.request"))return;File.Delete("Migration/customization.request");
        FindFirstObjectByType<PlayerBhysics>().gameObject.AddComponent<CustomizationProbe>();
    }
    void OnEnable(){Application.logMessageReceived+=Log;}
    void OnDisable(){Application.logMessageReceived-=Log;RestoreFeatures();}
    void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)Check(false,message+"\n"+stack);}
    void Update(){if(started>0&&Time.realtimeSinceStartup-started>75){Check(false,"Timeout");Finish();}}
    IEnumerator Start()
    {
        started=Time.realtimeSinceStartup;Application.runInBackground=true;Time.timeScale=1;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        foreach(var field in typeof(PlayerAbilityFeatures).GetFields(BindingFlags.Public|BindingFlags.Static)){initialFeatures[field]=(bool)field.GetValue(null);field.SetValue(null,true);}
        player=GetComponent<PlayerBhysics>();actions=GetComponent<ActionManager>();drop=actions.Action06;attack=actions.Action02;homing=actions.Action02Control;objects=GetComponent<Objects_Interaction>();
        GetComponent<PlayerBinput>().enabled=false;yield return new WaitForSeconds(.8f);
        origin=player.Body.position+Vector3.up*500;player.Body.interpolation=RigidbodyInterpolation.None;
        player.Body.useGravity=false;player.enabled=false;homing.enabled=false;
        foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
        explosion=FindObjectsByType<EnemySpawnerEternal>(FindObjectsInactive.Include,FindObjectsSortMode.None).First().Enemy.GetComponent<EnemyHealth>().Explosion;
        keyboard=InputSystem.AddDevice<Keyboard>();
        Check(Mathf.Approximately(drop.MaximumCharge,180)&&Mathf.Approximately(drop.ChargeDuration,.35f),"Default maximum power and full-charge time are preserved");

        Reset();PlayerAbilityFeatures.DropDash=false;yield return Keys(Key.LeftCtrl);
        Check(actions.Action==0,"Code switch disables airborne Drop Dash input");
        actions.ChangeAction(6);drop.InitialEvents();Check(actions.Action==0&&!drop.enabled,"Direct Drop Dash entry also respects the code switch");PlayerAbilityFeatures.DropDash=true;yield return Keys();
        Reset();player.Grounded=true;player.GroundNormal=Vector3.up;PlayerAbilityFeatures.SpinDash=false;yield return Keys(Key.LeftCtrl);
        Check(actions.Action==0,"Ground Spin Dash can be disabled independently");
        actions.ChangeAction(3);actions.Action03.InitialEvents();Check(actions.Action==0,"Direct Spin Dash entry respects the switch even before its first charge");
        PlayerAbilityFeatures.SpinDash=true;yield return Keys();
        Reset();player.Body.linearVelocity=Vector3.forward*30;PlayerAbilityFeatures.ManualRolling=false;yield return Keys(Key.LeftShift);player.SendMessage("InputChecks");
        Check(!player.isRolling,"The code switch disables manual roll input");PlayerAbilityFeatures.ManualRolling=true;player.SendMessage("InputChecks");
        Check(player.isRolling,"Manual rolling works again when its code switch is enabled");yield return Keys();
        Reset();player.Grounded=true;PlayerAbilityFeatures.Jump=false;yield return Keys(Key.Space);
        Check(actions.Action==0&&player.Body.linearVelocity.y==0,"Ground jump can be disabled independently");PlayerAbilityFeatures.Jump=true;yield return Keys();

        Reset();PlayerAbilityFeatures.AirDash=false;yield return Keys(Key.Space);
        Check(actions.Action==0&&!attack.AirDashAvailable,"Normal free air dash input respects its code switch");
        yield return Keys();var target=Target();HomingAttackControl.TargetObject=target;yield return Keys(Key.Space);
        Check(actions.Action==2&&attack.Target==target.transform,"Disabling free air dash preserves ordinary targeted homing");Destroy(target);PlayerAbilityFeatures.AirDash=true;yield return Keys();
        Reset();PlayerAbilityFeatures.HomingAttack=false;target=Target();HomingAttackControl.TargetObject=target;yield return Keys(Key.Space);
        Check(!homing.HasTarget&&actions.Action==0&&!attack.AirDashAvailable,"Disabling homing preserves the ordinary untargeted dash");
        actions.ChangeAction(2);attack.IsAirDash=false;attack.InitialEvents();Check(actions.Action==0,"Direct homing entry cannot bypass the code switch");
        Destroy(target);PlayerAbilityFeatures.HomingAttack=true;yield return Keys();

        yield return Charge();PlayerAbilityFeatures.DropDashAirDash=false;yield return Keys();yield return Keys(Key.Space);
        Check(actions.Action==6&&drop.Released&&attack.AirDashAvailable,"Release-combo air dash can be disabled without cancelling Drop Dash rolling");
        PlayerAbilityFeatures.DropDashAirDash=true;
        yield return Charge();target=Target();HomingAttackControl.TargetObject=target;PlayerAbilityFeatures.DropDashHomingAttack=false;yield return Keys();yield return Keys(Key.Space);
        Check(actions.Action==6&&drop.Released,"Release-combo homing can be disabled separately");
        yield return Keys();Reset();HomingAttackControl.TargetObject=target;yield return Keys(Key.Space);
        Check(actions.Action==2&&!attack.IsBoostedHoming,"Disabling boosted homing leaves ordinary homing available");Destroy(target);PlayerAbilityFeatures.DropDashHomingAttack=true;yield return Keys();

        SpendDash();yield return Charge(false);var enemy=Enemy(out target,out var hit);HomingAttackControl.TargetObject=target;
        yield return Keys();yield return Keys(Key.Space);PlayerAbilityFeatures.BoostedHomingRebound=false;objects.OnTriggerEnter(hit);
        Check(enemy.IsDefeated&&actions.Action==0&&!attack.AirDashAvailable&&Mathf.Approximately(player.Body.linearVelocity.y,objects.BouncingPower),"Disabling rebound preserves the kill and ordinary bounce without restoring spent abilities");
        PlayerAbilityFeatures.BoostedHomingRebound=true;yield return Keys();
        SpendDash();yield return Charge(false);enemy=Enemy(out target,out hit);HomingAttackControl.TargetObject=target;
        yield return Keys();yield return Keys(Key.Space);PlayerAbilityFeatures.ReboundRestoresAirAbilities=false;objects.OnTriggerEnter(hit);
        Check(enemy.IsDefeated&&actions.Action==1&&Mathf.Approximately(player.Body.linearVelocity.y,attack.BoostReboundSpeed)&&!attack.AirDashAvailable,"Ability reset can be disabled while retaining the upward rebound");
        PlayerAbilityFeatures.ReboundRestoresAirAbilities=true;yield return Keys();

        // Charge grows linearly in physics time, and partial power actually reaches the launch.
        Reset();drop.ChargeSpeed=120;drop.MaximumCharge=180;drop.MinimumCharge=20;yield return Keys(Key.LeftCtrl);
        float first=drop.Charge;
        for(int i=0;i<20;i++)yield return new WaitForFixedUpdate();
        float partial=drop.Charge;
        Check(partial>first&&Mathf.Abs(partial-first-120*Time.fixedDeltaTime*20)<3,"Drop Dash charge accumulates at the configured strength per second");
        Check(drop.HasCharge&&!drop.Charged,"A partial charge becomes usable without waiting for maximum power");
        player.Grounded=true;player.GroundNormal=Vector3.up;float expected=Mathf.Min(drop.MaximumCharge,partial+drop.ChargeSpeed*Time.fixedDeltaTime);
        yield return new WaitForFixedUpdate();
        Check(actions.Action==0&&Mathf.Abs(player.Body.linearVelocity.magnitude-expected)<.1f,"Partial charge produces a proportionally weaker real ground launch");
        yield return Keys();Reset();yield return Keys(Key.LeftCtrl);for(int i=0;i<100;i++)yield return new WaitForFixedUpdate();
        Check(drop.Charged&&Mathf.Approximately(drop.Charge,180),"Long charging stops exactly at the configured maximum");

        yield return Keys();Reset();drop.MaximumCharge=300;drop.ChargeSpeed=1000;yield return Keys(Key.LeftCtrl);yield return new WaitForSeconds(.4f);
        player.Grounded=true;player.GroundNormal=Vector3.up;yield return new WaitForFixedUpdate();
        Check(Mathf.Abs(player.Body.linearVelocity.magnitude-300)<.1f,"Changing maximum power from C# changes the actual launch independently of Spin Dash");
        yield return Keys();Reset();drop.AirDashSpeedMultiplier=.5f;yield return Keys(Key.LeftCtrl);yield return new WaitForSeconds(.4f);yield return Keys();yield return Keys(Key.Space);
        Check(Mathf.Abs(player.Body.linearVelocity.magnitude-150)<.1f,"The air combo has an independent serialized speed multiplier");
        yield return Keys();Reset();drop.HomingSpeedMultiplier=.75f;target=Target();HomingAttackControl.TargetObject=target;
        yield return Keys(Key.LeftCtrl);yield return new WaitForSeconds(.4f);yield return Keys();yield return Keys(Key.Space);
        Check(attack.IsBoostedHoming&&Mathf.Abs(player.Body.linearVelocity.magnitude-225)<.1f,"The targeted combo has an independent serialized speed multiplier");Destroy(target);yield return Keys();

        Reset();attack.AirDashesPerFlight=2;attack.ReplenishAirDash();yield return Keys(Key.Space);
        Check(attack.RemainingAirDashes==1,"Air dash count is configurable beyond the original single use");
        yield return Keys();Reset(false);yield return Keys(Key.Space);Check(attack.RemainingAirDashes==0,"The second configured air dash spends the last use");
        yield return Keys();Reset(false);yield return Keys(Key.Space);Check(actions.Action==0&&player.Body.linearVelocity.sqrMagnitude<.01f,"A third dash remains blocked with a two-dash limit");
        yield return Keys();Reset();attack.AirDashesPerFlight=0;yield return Keys(Key.Space);Check(actions.Action==0&&!attack.AirDashAvailable,"A zero dash count disables free dashes without altering homing");yield return Keys();

        // Unity must persist private serialized tuning, not just expose writable C# properties.
        Reset();drop.ChargeSpeed=270;drop.MaximumCharge=210;drop.BoostInputWindow=.4f;drop.LandingSpeedMultiplier=1.2f;
        string json=JsonUtility.ToJson(drop);drop.ChargeSpeed=1;drop.MaximumCharge=1;drop.BoostInputWindow=0;drop.LandingSpeedMultiplier=1;
        JsonUtility.FromJsonOverwrite(json,drop);
        Check(drop.ChargeSpeed==270&&drop.MaximumCharge==210&&drop.BoostInputWindow==.4f&&drop.LandingSpeedMultiplier==1.2f,"Unity serialization restores private charge, maximum, timing and multiplier settings");
        drop.ChargeSpeed=0;drop.MaximumCharge=-1;drop.MinimumCharge=100;
        Check(drop.ChargeSpeed>0&&drop.MaximumCharge>0&&drop.MinimumCharge<=drop.MaximumCharge,"Invalid charge settings are clamped to usable bounds");
        player.MoveDecell=0;player.MaxSpeed=0;Check(player.MoveDecell>0&&player.MaxSpeed>0,"Physics settings prevent zero divisors when configured from code");
        Finish();
    }
    IEnumerator Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));yield return null;yield return new WaitForSeconds(.035f);}
    IEnumerator Charge(bool replenish=true){yield return Keys();Reset(replenish);yield return Keys(Key.LeftCtrl);yield return new WaitForSeconds(drop.ChargeDuration+.06f);}
    void Reset(bool replenish=true)
    {
        actions.ChangeAction(0);if(replenish)attack.ReplenishAirDash();homing.HomingAvailable=true;HomingAttackControl.TargetObject=null;
        transform.SetPositionAndRotation(origin,Quaternion.identity);Physics.SyncTransforms();player.Body.position=origin;player.Body.linearVelocity=Vector3.zero;
        player.Grounded=false;player.isRolling=false;player.RawInput=player.PreviousRawInput=player.MoveInput=Vector3.zero;actions.Action00.CharacterAnimator.transform.rotation=Quaternion.identity;
    }
    void SpendDash(){Reset();actions.ChangeAction(2);attack.IsAirDash=true;attack.InitialEvents();actions.ChangeAction(0);}
    GameObject Target(){var target=new GameObject("Configuration target");target.transform.position=origin+Vector3.forward*200;return target;}
    EnemyHealth Enemy(out GameObject target,out Collider hit)
    {
        var root=new GameObject("Configuration enemy");root.transform.position=origin+Vector3.forward*200;var enemy=root.AddComponent<EnemyHealth>();enemy.Explosion=explosion;
        target=Target();target.transform.SetParent(root.transform,true);target.tag="Enemy";hit=target.AddComponent<BoxCollider>();hit.isTrigger=true;return enemy;
    }
    void RestoreFeatures(){foreach(var pair in initialFeatures)pair.Key.SetValue(null,pair.Value);}
    void Check(bool ok,string message){report.Add((ok?"PASS ":"FAIL ")+message);if(!ok)failures++;}
    void Finish(){RestoreFeatures();if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);Directory.CreateDirectory("Migration/Customization");report.Add("Failures="+failures);File.WriteAllLines("Migration/Customization/"+gameObject.scene.name+"-test.txt",report);EditorApplication.Exit(failures==0?0:1);}
}
#endif

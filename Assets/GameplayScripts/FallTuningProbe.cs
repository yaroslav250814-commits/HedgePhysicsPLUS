#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class FallTuningProbe:MonoBehaviour
{
    readonly List<string> lines=new List<string>();int failures;float started;
    PlayerBhysics player;ActionManager actions;Keyboard keyboard;Vector3 origin;
    float baselineDistance,tunedDistance,baselinePeak,tunedPeak;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if(!File.Exists("Migration/fall-tuning.request"))return;File.Delete("Migration/fall-tuning.request");
        FindFirstObjectByType<PlayerBhysics>().gameObject.AddComponent<FallTuningProbe>();
    }
    void OnEnable(){Application.logMessageReceived+=Log;}
    void OnDisable(){Application.logMessageReceived-=Log;}
    void Log(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error)Check(false,message+"\n"+stack);}
    void Update(){if(started>0&&Time.realtimeSinceStartup-started>40){Check(false,"Timeout");Finish();}}
    IEnumerator Start()
    {
        started=Time.realtimeSinceStartup;Application.runInBackground=true;Time.timeScale=1;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        player=GetComponent<PlayerBhysics>();actions=GetComponent<ActionManager>();
        GetComponent<PlayerBinput>().enabled=false;
        yield return new WaitForSeconds(.8f);
        Check(player.MaxFallingSpeed==45&&Mathf.Approximately(player.DescentGravityMultiplier,.6f),"Gameplay scene uses the slower fall settings");
        Check(player.Gravity==new Vector3(0,-1.5f,0),"Original upward gravity remains configured");
        foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
        // Teleport between independent trials without rendering an interpolated previous pose.
        player.Body.interpolation=RigidbodyInterpolation.None;
        origin=player.Body.position+Vector3.up*500;
        keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        actions.ChangeAction(0);actions.Action00.enabled=false;
        yield return Drop(120,1, true);
        yield return Drop(45,.6f, false);
        Check(tunedDistance<baselineDistance*.8f,"Measured fall over one second is slower: "+baselineDistance.ToString("F2")+" -> "+tunedDistance.ToString("F2"));

        bool stopAirMovement=player.StopAirMovementIfNoInput;player.StopAirMovementIfNoInput=false;
        ResetBody(new Vector3(20,-200,5));player.MaxFallingSpeed=45;
        yield return new WaitForFixedUpdate();
        Check(Mathf.Abs(player.Body.linearVelocity.y+45)<.01f,"Downward velocity is capped at 45");
        Check(Mathf.Abs(player.Body.linearVelocity.x-20)<.01f&&Mathf.Abs(player.Body.linearVelocity.z-5)<.01f,"The fall cap preserves horizontal momentum");
        player.StopAirMovementIfNoInput=stopAirMovement;
        ResetBody(new Vector3(0,60,0));yield return new WaitForFixedUpdate();
        Check(player.Body.linearVelocity.y>45,"The fall cap does not clamp upward jump or spring velocity");
        Check(Mathf.Abs(player.Body.linearVelocity.y-58.5f)<.05f,"Upward acceleration uses the original gravity");
        ResetBody(new Vector3(0,-200,0));player.MaxFallingSpeed=-45;yield return new WaitForFixedUpdate();
        Check(Mathf.Abs(player.Body.linearVelocity.y+45)<.01f,"Legacy negative fall-limit values are interpreted safely");

        yield return Jump(120,1,true);
        yield return Jump(45,.6f,false);
        Check(baselinePeak>1&&Mathf.Abs(baselinePeak-tunedPeak)<.1f,"Held-jump peak is unchanged: "+baselinePeak.ToString("F2")+" -> "+tunedPeak.ToString("F2"));
        player.MaxFallingSpeed=45;player.DescentGravityMultiplier=.6f;
        Finish();
    }
    IEnumerator Drop(float limit,float multiplier,bool baseline)
    {
        player.MaxFallingSpeed=limit;player.DescentGravityMultiplier=multiplier;ResetBody(Vector3.zero);
        int steps=Mathf.RoundToInt(1/Time.fixedDeltaTime);
        for(int i=0;i<steps;i++)yield return new WaitForFixedUpdate();
        float distance=origin.y-player.Body.position.y;
        if(baseline)baselineDistance=distance;else tunedDistance=distance;
        Check(!player.Grounded&&player.Body.linearVelocity.y<0,"Freefall trial stays airborne");
        for(int i=0;i<steps*2;i++)yield return new WaitForFixedUpdate();
        Check(Mathf.Abs(player.Body.linearVelocity.y+limit)<.1f,(baseline?"Baseline":"Tuned")+" terminal fall velocity is respected");
    }
    IEnumerator Jump(float limit,float multiplier,bool baseline)
    {
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return null;
        yield return new WaitForFixedUpdate();
        Check(HedgeInput.GetButton("A"),"Virtual jump input is held before the jump starts");
        player.MaxFallingSpeed=limit;player.DescentGravityMultiplier=multiplier;ResetBody(Vector3.zero);
        player.GroundNormal=Vector3.up;
        actions.Action01.InitialEvents();actions.ChangeAction(1);
        float highest=origin.y;bool rising=false;
        for(int i=0;i<150;i++)
        {
            yield return new WaitForFixedUpdate();highest=Mathf.Max(highest,player.Body.position.y);
            if(player.Body.linearVelocity.y>0)rising=true;
            if(rising&&player.Body.linearVelocity.y<=0)break;
        }
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());actions.ChangeAction(0);actions.Action00.enabled=false;
        if(baseline)baselinePeak=highest-origin.y;else tunedPeak=highest-origin.y;
    }
    void ResetBody(Vector3 velocity)
    {
        transform.SetPositionAndRotation(origin,Quaternion.identity);Physics.SyncTransforms();
        player.Body.position=origin;player.Body.rotation=Quaternion.identity;player.Body.useGravity=false;player.Body.linearVelocity=velocity;
        player.Grounded=false;player.MoveInput=player.RawInput=player.PreviousRawInput=Vector3.zero;player.isRolling=false;
    }
    void Check(bool ok,string message){lines.Add((ok?"PASS ":"FAIL ")+message);if(!ok)failures++;}
    void Finish(){if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);Directory.CreateDirectory("Migration/FallTuning");lines.Add("Failures="+failures);File.WriteAllLines("Migration/FallTuning/"+gameObject.scene.name+"-test.txt",lines);EditorApplication.Exit(failures==0?0:1);}
}
#endif

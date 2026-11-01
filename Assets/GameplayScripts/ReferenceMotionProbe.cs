#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[DefaultExecutionOrder(-10000)]
public class ReferenceMotionProbe : MonoBehaviour
{
    PlayerBhysics player;
    Rigidbody body;
    Vector3 settled, movingEnd;
    int step, groundSteps;
    List<string> report = new List<string>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install() {
        if(!File.Exists("ReferenceResults/motion.request")) return;
        File.Delete("ReferenceResults/motion.request");
#if UNITY_5_6
        PlayerBhysics target=UnityEngine.Object.FindObjectOfType<PlayerBhysics>();
#else
        PlayerBhysics target=UnityEngine.Object.FindFirstObjectByType<PlayerBhysics>();
#endif
        if(target) target.gameObject.AddComponent<ReferenceMotionProbe>();
    }
    void Start() {
        player=GetComponent<PlayerBhysics>(); body=GetComponent<Rigidbody>();
        PlayerBinput input=GetComponent<PlayerBinput>(); if(input) input.enabled=false;
        Application.runInBackground=true; Time.timeScale=1; EditorApplication.isPaused=false;
#if UNITY_5_6
        PauseCotrol pause=UnityEngine.Object.FindObjectOfType<PauseCotrol>();
#else
        PauseCotrol pause=UnityEngine.Object.FindFirstObjectByType<PauseCotrol>();
#endif
        if(pause) { pause.Pause.SetActive(false); pause.enabled=false; }
        report.Add("Unity="+Application.unityVersion);
        report.Add("Fixed timestep="+Time.fixedDeltaTime.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
        report.Add("Start="+transform.position);
        report.Add("MaxSpeed="+player.MaxSpeed+" Gravity="+player.Gravity);
    }
    Vector3 Velocity() {
#if UNITY_5_6
        return body.velocity;
#else
        return body.linearVelocity;
#endif
    }
    void FixedUpdate() {
        step++;
        if(player.Grounded) groundSteps++;
        Vector3 move=step>180 && step<=360 ? Vector3.forward : Vector3.zero;
        player.MoveInput=move; player.RawInput=move;
        if(step==180) { settled=transform.position; report.Add("Settled="+settled+" Grounded="+player.Grounded+" GroundedSteps="+groundSteps); }
        if(step==360) { movingEnd=transform.position; report.Add("MoveEnd="+movingEnd+" Distance="+Vector3.Distance(settled,movingEnd)+" Speed="+Velocity().magnitude); }
        if(step<480) return;
        report.Add("Stopped="+transform.position+" Speed="+Velocity().magnitude+" Grounded="+player.Grounded);
        report.Add("End="+transform.position+" TotalGroundedSteps="+groundSteps);
        File.WriteAllLines("ReferenceResults/motion-"+Application.unityVersion+".txt",report.ToArray());
#if UNITY_5_6
        EditorApplication.isPlaying=false;
#else
        if(Application.isBatchMode) EditorApplication.Exit(groundSteps>0 ? 0 : 1);
        else EditorApplication.isPlaying=false;
#endif
    }
    void LateUpdate() { Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
    void OnDisable() { Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
}
#endif

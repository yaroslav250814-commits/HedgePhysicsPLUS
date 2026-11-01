#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LevelFeatureRegressionProbe:MonoBehaviour
{
    readonly List<string> lines=new List<string>();
    int failures;PlayerBhysics player;LevelRunState run;HurtControl hurt;Objects_Interaction interaction;
    List<GameObject> pickups;float watchdog;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install(){
        if(!File.Exists("Migration/level-features-test.request"))return;
        File.Delete("Migration/level-features-test.request");
        FindFirstObjectByType<PlayerBhysics>().gameObject.AddComponent<LevelFeatureRegressionProbe>();
    }
    void Update(){if(Time.realtimeSinceStartup-watchdog>45 && watchdog>0){Check(false,"Timed out");Finish();}}
    IEnumerator Start()
    {
        watchdog=Time.realtimeSinceStartup;Application.runInBackground=true;EditorApplication.isPaused=false;Time.timeScale=1;
        player=GetComponent<PlayerBhysics>();run=GetComponent<LevelRunState>();hurt=GetComponent<HurtControl>();interaction=GetComponent<Objects_Interaction>();
        GetComponent<PlayerBinput>().enabled=false;
        yield return new WaitForSeconds(.9f);
        player.enabled=false;player.Body.useGravity=false;player.Body.linearVelocity=Vector3.zero;
        yield return new WaitForSeconds(.3f);
        Check(run.CheckpointSeconds==0 && run.ElapsedSeconds>1,"Without a checkpoint the saved time remains zero");
        hurt.isDead=true;yield return WaitForRespawn();
        Check(run.ElapsedSeconds<.1f && run.TimeLabel.text=="0:00","Death before any checkpoint restarts the timer at zero");
        GetComponent<PlayerBinput>().enabled=false;
        pickups=GameObject.FindGameObjectsWithTag("Ring").Where(r=>r.GetComponent<Collider>()).Take(3).ToList();
        Check(run && run.ScoreLabel && run.TimeLabel,"HUD and level state are connected");
        Check(run.TrackedRingCount>=pickups.Count && pickups.Count>0,"Initial map rings are tracked");
        Check(Objects_Interaction.RingAmmount==0 && run.Score==0,"New level starts with zero rings and score");
        foreach(var ring in pickups)interaction.OnTriggerEnter(ring.GetComponent<Collider>());
        Check(Objects_Interaction.RingAmmount==pickups.Count && run.Score==0,"Collecting map rings adds rings without immediate score");
        Check(pickups.All(r=>r && !r.activeSelf),"Collected rings are preserved for respawn");
        interaction.OnTriggerEnter(pickups[0].GetComponent<Collider>());
        Check(Objects_Interaction.RingAmmount==pickups.Count,"Duplicate trigger does not collect a ring twice");
        float before=run.ElapsedSeconds;
        var pause=FindFirstObjectByType<PauseCotrol>();pause.ShowPauseMenu();
        yield return new WaitForSecondsRealtime(.2f);
        Check(Mathf.Approximately(before,run.ElapsedSeconds),"Timer stops while paused");pause.Resume();
        yield return new WaitForSeconds(.15f);
        Check(run.ElapsedSeconds>before,"Timer resumes with gameplay");
        Check(LevelRunState.FormatTime(61.23f)=="01:01.23","Timer formats minutes, seconds and centiseconds");
        var enemy=FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).FirstOrDefault();
        GameObject fixture=null;
        if(!enemy){fixture=new GameObject("Regression enemy");enemy=fixture.AddComponent<EnemyHealth>();var fx=new GameObject("Regression explosion");fx.SetActive(false);enemy.Explosion=fx;}
        int score=run.Score;enemy.DealDamage(enemy.MaxHealth);enemy.DealDamage(enemy.MaxHealth);
        Check(run.Score==score+100,"Enemy defeat awards 100 points exactly once");
        CaptureHud();
        var checkpoint=FindFirstObjectByType<CheckPointData>();
        float checkpointTime=run.ElapsedSeconds;
        if(checkpoint)GetComponent<LevelProgressControl>().OnTriggerEnter(checkpoint.GetComponent<Collider>());
        Check(checkpoint && checkpoint.IsOn && Mathf.Approximately(run.CheckpointSeconds,checkpointTime),"Touching a checkpoint saves its exact elapsed time");
        yield return new WaitForSeconds(.25f);
        if(checkpoint)GetComponent<LevelProgressControl>().OnTriggerEnter(checkpoint.GetComponent<Collider>());
        Check(Mathf.Approximately(run.CheckpointSeconds,checkpointTime),"An already active checkpoint does not overwrite its saved time");
        var expectedPosition=GetComponent<LevelProgressControl>().ResumePosition;
        var monitor=FindObjectsByType<MonitorData>(FindObjectsSortMode.None).FirstOrDefault(m=>m.Type==MonitorType.Ring && m.GetComponent<Collider>());
        if(monitor){GetComponent<Monitors_Interactions>().OnTriggerEnter(monitor.GetComponent<Collider>());Check(!monitor.gameObject.activeSelf,"Ring monitor is retained for respawn");}
        var pit=FindObjectsByType<Collider>(FindObjectsSortMode.None).First(c=>c.CompareTag("Pit") && c.enabled);
        var dropped=Instantiate(hurt.MovingRing,transform.position+Vector3.up*2,Quaternion.identity);
        var giver=Instantiate(GetComponent<Monitors_Interactions>().RingGiver,transform.position,Quaternion.identity).GetComponent<RingGiverControl>();giver.Duration=100;
        score=run.Score;before=run.ElapsedSeconds;
        // Enter the actual pit trigger with the physical character collider.
        var solid=GetComponentsInChildren<Collider>().First(c=>c.enabled && !c.isTrigger);
        player.Body.position=pit.bounds.center-(solid.bounds.center-player.Body.position);Physics.SyncTransforms();
        for(int i=0;i<20 && !hurt.isDead;i++)yield return new WaitForFixedUpdate();
        Check(hurt.isDead,"Pit contact enters the shared death path");
        Check(Objects_Interaction.RingAmmount==0,"Pit death immediately clears rings");
        float deathTime=run.ElapsedSeconds;
        interaction.OnTriggerEnter(pickups[0].GetComponent<Collider>());
        yield return new WaitForSecondsRealtime(.12f);
        Check(Mathf.Approximately(deathTime,run.ElapsedSeconds),"Death animation does not advance the timer");
        Check(Objects_Interaction.RingAmmount==0 && !dropped && !giver,"Death blocks pickups and clears transient ring rewards");
        yield return WaitForRespawn();
        Check(Mathf.Abs(run.ElapsedSeconds-checkpointTime)<.1f && run.TimeLabel.text==LevelRunState.FormatClassicTime(checkpointTime),"Pit death restores the saved checkpoint time and HUD");
        Check(pickups.All(r=>r && r.activeSelf),"Map rings return after pit death");
        if(monitor)Check(monitor.gameObject.activeSelf,"Ring monitor returns after death");
        Check(Objects_Interaction.RingAmmount==0 && run.Score==score,"Respawn keeps zero rings and preserves score");
        Check(Vector3.Distance(player.Body.position,expectedPosition)<.5f && player.Body.linearVelocity.sqrMagnitude<.1f,"Respawn returns to checkpoint without falling velocity");
        interaction.OnTriggerEnter(pickups[0].GetComponent<Collider>());
        Check(Objects_Interaction.RingAmmount==1,"A restored map ring can be collected again");
        hurt.isDead=true;Check(Objects_Interaction.RingAmmount==0,"Other death sources also clear rings");
        yield return WaitForRespawn();
        Check(Mathf.Abs(run.ElapsedSeconds-checkpointTime)<.1f,"Repeated death returns to the same saved checkpoint time");
        Check(pickups.All(r=>r.activeSelf),"Repeated deaths restore the map again");
        yield return new WaitForSeconds(.25f);
        var nextCheckpoint=Instantiate(checkpoint.gameObject).GetComponent<CheckPointData>();
        var nextCollider=nextCheckpoint.GetComponent<Collider>();nextCollider.enabled=false;
        checkpointTime=run.ElapsedSeconds;
        GetComponent<LevelProgressControl>().OnTriggerEnter(nextCollider);
        Check(nextCheckpoint.IsOn && Mathf.Approximately(run.CheckpointSeconds,checkpointTime),"A later checkpoint replaces the previously saved time");
        yield return new WaitForSeconds(.2f);
        interaction.OnTriggerEnter(pickups[0].GetComponent<Collider>());
        Objects_Interaction.RingAmmount=0;hurt.IsHurt=false;GetComponent<ActionManager>().ChangeAction(0);interaction.DamagePlayer();
        Check(hurt.isDead,"Fatal damage uses the same death path");
        yield return WaitForRespawn();
        Check(Mathf.Abs(run.ElapsedSeconds-checkpointTime)<.1f,"Fatal damage restores the most recent checkpoint time");
        Check(Objects_Interaction.RingAmmount==0 && pickups.All(r=>r.activeSelf),"Fatal damage restores map rings and leaves zero rings");
        before=run.ElapsedSeconds;run.CompleteStage();yield return new WaitForSeconds(.2f);
        Check(Mathf.Approximately(before,run.ElapsedSeconds),"Timer stops at the goal");
        Finish();
    }
    IEnumerator WaitForRespawn(){for(int i=0;i<300 && hurt.isDead;i++)yield return new WaitForFixedUpdate();Check(!hurt.isDead,"Death animation finishes and player respawns");}
    void Check(bool ok,string description){lines.Add((ok?"PASS ":"FAIL ")+description);if(!ok)failures++;}
    void Finish(){
        Directory.CreateDirectory("Migration/LevelFeatures");lines.Add("Failures="+failures);
        File.WriteAllLines("Migration/LevelFeatures/"+SceneManager.GetActiveScene().name+"-test.txt",lines);
        EditorApplication.Exit(failures==0?0:1);
    }
    void CaptureHud(){
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
        Directory.CreateDirectory("Migration/LevelFeatures");
        var camera=Camera.main;var canvas=run.ScoreLabel.canvas.rootCanvas;
        var mode=canvas.renderMode;var world=canvas.worldCamera;float distance=canvas.planeDistance;
        var target=RenderTexture.GetTemporary(1280,720,24);var previous=camera.targetTexture;var active=RenderTexture.active;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.5f;
        camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
        File.WriteAllBytes("Migration/LevelFeatures/"+SceneManager.GetActiveScene().name+"-hud.png",image.EncodeToPNG());
        camera.targetTexture=previous;RenderTexture.active=active;canvas.renderMode=mode;canvas.worldCamera=world;canvas.planeDistance=distance;
        RenderTexture.ReleaseTemporary(target);Destroy(image);
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class PauseMenuRegressionProbe:MonoBehaviour
{
    readonly List<string> report=new List<string>();
    PauseCotrol pause;Keyboard keyboard;Mouse mouse;Gamepad gamepad;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install(){
        if(!File.Exists("Migration/pause-menu-test.request"))return;
        File.Delete("Migration/pause-menu-test.request");
        FindFirstObjectByType<PauseCotrol>().gameObject.AddComponent<PauseMenuRegressionProbe>();
    }
    IEnumerator Start(){
        pause=GetComponent<PauseCotrol>();Application.runInBackground=true;EditorApplication.isPaused=false;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();gamepad=InputSystem.AddDevice<Gamepad>();
        yield return Frames(8);
        var resume=pause.Pause.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Resume");
        var options=pause.Pause.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Options");
        pause.Pause.SetActive(false);pause.OptionsMenu.SetActive(false);Time.timeScale=1;
        foreach(int mode in new[]{0,1,2}){
            bool submit=mode!=0;string source=mode==0?"Mouse":mode==1?"Enter":"Gamepad";
            pause.PauseToggle();yield return new WaitForSecondsRealtime(.5f);
            report.Add("BEFORE Continue "+State());
            yield return submit ? Submit(resume,mode==2):Click(resume);
            report.Add((Time.timeScale==1 && !pause.Pause.activeSelf ? "PASS ":"FAIL ")+source+" Continue "+State());
            pause.Pause.SetActive(false);pause.OptionsMenu.SetActive(false);Time.timeScale=1;
            pause.PauseToggle();yield return new WaitForSecondsRealtime(.5f);
            yield return submit ? Submit(options,mode==2):Click(options);
            yield return new WaitForSecondsRealtime(.5f);
            report.Add((Time.timeScale==0 && pause.OptionsMenu.activeInHierarchy ? "PASS ":"FAIL ")+source+" Options "+State());
            if(pause.OptionsMenu.activeInHierarchy){
                var back=pause.OptionsMenu.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Resume");
                yield return submit ? Submit(back,mode==2):Click(back);
                report.Add((Time.timeScale==0 && pause.Pause.activeInHierarchy && !pause.OptionsMenu.activeSelf ? "PASS ":"FAIL ")+source+" Options back "+State());
            }
            pause.Pause.SetActive(false);pause.OptionsMenu.SetActive(false);Time.timeScale=1;
        }
        InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(gamepad);
        File.WriteAllLines("Migration/pause-menu-playtest.txt",report);
        if(Application.isBatchMode)EditorApplication.Exit(report.Any(l=>l.StartsWith("FAIL "))?1:0);
        else EditorApplication.isPlaying=false;
    }
    string State()=>"scale="+Time.timeScale+" pause="+pause.Pause.activeInHierarchy+" options="+pause.OptionsMenu.activeInHierarchy;
    IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
    IEnumerator Submit(Button button,bool useGamepad){
        EventSystem.current.SetSelectedGameObject(button.gameObject);yield return Frames(2);
        if(useGamepad)InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.South));
        else InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));
        yield return Frames(2);
        if(useGamepad)InputSystem.QueueStateEvent(gamepad,new GamepadState());
        else InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        yield return Frames(3);
    }
    IEnumerator Click(Button button){
        Canvas.ForceUpdateCanvases();
        var rect=(RectTransform)button.transform;var canvas=button.GetComponentInParent<Canvas>();
        Vector2 point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        report.Add("RAYCAST "+button.name+" "+string.Join(",",hits.Take(4).Select(h=>h.gameObject.name)));
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Frames(2);
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));yield return Frames(2);
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Frames(3);
    }
}
#endif

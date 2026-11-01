using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor.Events;
using UnityEngine.Events;

[InitializeOnLoad]
public static class PauseMenuTools
{
    const string Request="Migration/pause-menu-request.txt";
    static PauseMenuTools(){EditorApplication.update+=Update;}
    public static void BatchTest(){
        EditorSceneManager.OpenScene("Assets/[Stage01_AltitudeLimit].unity");
        File.WriteAllText("Migration/pause-menu-test.request","run");EditorApplication.EnterPlaymode();
    }
    public static void FixLinks(){
        const string path="Assets/Prototyping/ProtoPrefabs/PlayerObjects.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try{
            var pause=root.GetComponentInChildren<PauseCotrol>(true);
            var buttons=pause.Pause.GetComponentsInChildren<Button>(true);
            var resume=buttons.Single(b=>b.name=="Resume");
            Set(resume,pause.Resume);Set(buttons.Single(b=>b.name=="Options"),pause.ShowOptions);
            Set(pause.OptionsMenu.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Resume"),pause.ShowPauseMenu);
            pause.PauseMainButton=resume;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
        if(Application.isBatchMode)EditorApplication.Exit(0);
    }
    static void Set(Button button,UnityAction action){
        while(button.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(button.onClick,0);
        UnityEventTools.AddPersistentListener(button.onClick,action);EditorUtility.SetDirty(button);
    }
    static void Update()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request))return;
        string command=File.ReadAllText(Request).Trim();File.Delete(Request);
        try {
            if(command=="inspect")Inspect();
            else if(command=="test"){
                if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;File.WriteAllText(Request,"test");return;}
                File.WriteAllText("Migration/pause-menu-test.request","run");EditorApplication.EnterPlaymode();
            }
            else if(command=="build")Unity63Migration.Build();
        }catch(Exception error){File.WriteAllText("Migration/pause-menu-error.txt",error.ToString());Debug.LogException(error);}
    }
    static string PathOf(Transform t)=>t.parent ? PathOf(t.parent)+"/"+t.name:t.name;
    static void Inspect()
    {
        var lines=new List<string>();
        lines.Add("Editor playing="+EditorApplication.isPlaying+" paused="+EditorApplication.isPaused);
        for(int i=0;i<SceneManager.sceneCount;i++){var s=SceneManager.GetSceneAt(i);lines.Add("SCENE "+s.path+" dirty="+s.isDirty);}
        var root=PrefabUtility.LoadPrefabContents("Assets/Prototyping/ProtoPrefabs/PlayerObjects.prefab");
        try {
            var pause=root.GetComponentInChildren<PauseCotrol>(true);
            lines.Add("PAUSE="+PathOf(pause.Pause.transform)+" OPTIONS="+PathOf(pause.OptionsMenu.transform));
            foreach(var button in root.GetComponentsInChildren<Button>(true)){
                lines.Add("BUTTON "+PathOf(button.transform)+" enabled="+button.enabled+" interactable="+button.interactable);
                for(int i=0;i<button.onClick.GetPersistentEventCount();i++){
                    var target=button.onClick.GetPersistentTarget(i);string method=button.onClick.GetPersistentMethodName(i);
                    lines.Add("  CALL target="+(target ? target.name+"/"+target.GetType().FullName:"MISSING")+" method="+method+" state="+button.onClick.GetPersistentListenerState(i));
                }
            }
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        File.WriteAllLines("Migration/pause-menu-inspection.txt",lines);
    }
}

using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class LevelFeatureSetup
{
    public static void Install()
    {
        Directory.CreateDirectory("Migration/LevelFeatures/Before");
        const string playerPath="Assets/Player/Player_Sonic.prefab";
        const string objectsPath="Assets/Prototyping/ProtoPrefabs/PlayerObjects.prefab";
        foreach(string path in new[]{playerPath,objectsPath}){
            string backup="Migration/LevelFeatures/Before/"+Path.GetFileName(path);
            if(!File.Exists(backup))File.Copy(path,backup);
        }
        var root=PrefabUtility.LoadPrefabContents(playerPath);
        try{
            var player=root.GetComponentInChildren<PlayerBhysics>(true);
            if(!player.GetComponent<LevelRunState>())player.gameObject.AddComponent<LevelRunState>();
            PrefabUtility.SaveAsPrefabAsset(root,playerPath);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        root=PrefabUtility.LoadPrefabContents(objectsPath);
        try{
            var interaction=root.GetComponentInChildren<Objects_Interaction>(true);
            var run=interaction.GetComponent<LevelRunState>();
            if(!run)run=interaction.gameObject.AddComponent<LevelRunState>();
            var canvas=root.GetComponentInChildren<PauseCotrol>(true).GetComponent<Canvas>();
            ClassicPresentationSetup.ConfigureHud(canvas,run,interaction);
            EditorUtility.SetDirty(run);
            PrefabUtility.SaveAsPrefabAsset(root,objectsPath);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
        File.WriteAllText("Migration/LevelFeatures/setup.txt","Score and time HUD installed in PlayerObjects; run state installed in Player_Sonic.");
        if(Application.isBatchMode)EditorApplication.Exit(0);
    }
    static Text Label(RectTransform group,string name,Text source,float y)
    {
        var existing=group.Find(name);
        var text=existing ? existing.GetComponent<Text>() : null;
        if(!text){
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text),typeof(Shadow));
            go.layer=5;go.transform.SetParent(group,false);text=go.GetComponent<Text>();
        }
        text.font=source.font;text.fontSize=22;text.fontStyle=FontStyle.Bold;
        text.alignment=TextAnchor.MiddleRight;text.supportRichText=true;text.raycastTarget=false;
        text.color=Color.white;text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;
        var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(1,1);
        rect.anchoredPosition=new Vector2(0,y);rect.sizeDelta=new Vector2(330,30);
        var shadow=text.GetComponent<Shadow>();shadow.effectColor=Color.black;shadow.effectDistance=new Vector2(1.5f,-1.5f);
        return text;
    }
    public static void TestStage00(){Test("Assets/[Stage00_Test].unity");}
    public static void TestAltitude(){Test("Assets/[Stage01_AltitudeLimit].unity");}
    static void Test(string scene){
        EditorSceneManager.OpenScene(scene);
        File.WriteAllText("Migration/level-features-test.request","run");EditorApplication.EnterPlaymode();
    }
}

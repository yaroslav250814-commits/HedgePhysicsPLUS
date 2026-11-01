using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CustomizationTools
{
    static readonly string[] Paths={"Assets/Player/Player_Sonic.prefab","Assets/Prototyping/ProtoPrefabs/PlayerObjects.prefab","Assets/[Stage00_Test].unity","Assets/[Stage01_AltitudeLimit].unity"};
    static readonly Dictionary<Type,string[]> Members=new Dictionary<Type,string[]>
    {
        {typeof(PlayerBhysics),new[]{"MoveAccell","MoveDecell","AirDecell","TopSpeed","MaxSpeed","MaxFallingSpeed","DescentGravityMultiplier","AirControlAmmount","AirSkiddingForce","StopAirMovementIfNoInput","Gravity","GroundStickingDistance","GroundStickingPower","SlopePower","UphillMultiplier","DownhillMultiplier","LandingConversionFactor","RollingLandingBoost","RollingStartSpeed","RollingTurningDecreace"}},
        {typeof(PlayerBinput),new[]{"SteeringResponsiveness","UtopiaTurning","UtopiaIntensity","UtopiaInitialInputLerpSpeed"}},
        {typeof(Action01_Jump),new[]{"JumpDuration","SlopedJumpDuration","JumpSpeed","JumpSlopeConversion","StopYSpeedOnRelease","skinRotationSpeed"}},
        {typeof(Action02_Homing),new[]{"isAdditive","HomingAttackSpeed","AirDashSpeed","HomingTimerLimit","FacingAmmount","BoostReboundSpeed","skinRotationSpeed"}},
        {typeof(Action03_SpinDash),new[]{"SpinDashChargingSpeed","MinimunCharge","MaximunCharge","SpinDashStillForce","ReleaseShakeAmmount","BallAnimationSpeedMultiplier","SpinDashChargedEffectAmm"}},
        {typeof(Action06_DropDash),new[]{"ChargeDuration","BoostInputWindow"}},
        {typeof(HomingAttackControl),new[]{"TargetSearchDistance"}}
    };
    [Serializable] public sealed class Entry {public string key,value;}
    [Serializable] public sealed class Snapshot {public List<Entry> entries=new List<Entry>();}
    public static void CaptureBefore()
    {
        Directory.CreateDirectory("Migration/Customization");
        foreach(string path in Paths)Backup(path);
        foreach(string path in Directory.GetFiles("Assets/GameplayScripts","*.cs"))Backup(path.Replace('\\','/'));
        string destination="Migration/Customization/before.json";
        if(File.Exists(destination))throw new InvalidOperationException("Original settings snapshot already exists; it must not be overwritten.");
        File.WriteAllText(destination,JsonUtility.ToJson(Capture(),true));EditorApplication.Exit(0);
    }
    static void Backup(string path){string dest="Migration/Customization/Before/"+path;if(File.Exists(dest))return;Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest));File.Copy(path,dest);}
    static Snapshot Capture()
    {
        var snapshot=new Snapshot();
        foreach(string path in Paths)
        {
            if(path.EndsWith(".prefab"))
            {
                var root=PrefabUtility.LoadPrefabContents(path);try{Read(root,path,snapshot);}finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            else{var scene=EditorSceneManager.OpenScene(path);foreach(var root in scene.GetRootGameObjects())Read(root,path,snapshot);}
        }
        return snapshot;
    }
    static void Read(GameObject root,string path,Snapshot snapshot)
    {
        foreach(var pair in Members)foreach(var component in root.GetComponentsInChildren(pair.Key,true))foreach(string member in pair.Value)
        {
            var field=pair.Key.GetField(member);var property=pair.Key.GetProperty(member);
            object value=field!=null?field.GetValue(component):property.GetValue(component);
            string formatted=value is IFormattable f?f.ToString(null,System.Globalization.CultureInfo.InvariantCulture):value.ToString();
            snapshot.entries.Add(new Entry{key=path+"/"+pair.Key.Name+"/"+member,value=formatted});
        }
    }
    public static void VerifyAndSave()
    {
        var before=JsonUtility.FromJson<Snapshot>(File.ReadAllText("Migration/Customization/before.json"));var after=Capture();
        var lines=new List<string>();int failures=0;
        if(before.entries.Count!=after.entries.Count)throw new InvalidOperationException("Component count changed during settings migration");
        for(int i=0;i<before.entries.Count;i++)
        {
            var a=before.entries[i];var b=after.entries[i];bool equal=a.value==b.value;
            if(float.TryParse(a.value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float av)&&float.TryParse(b.value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float bv))equal=Mathf.Abs(av-bv)<=.00001f;
            bool ok=a.key==b.key&&equal;
            lines.Add((ok?"PASS ":"FAIL ")+a.key+" = "+a.value+(ok?"":" -> "+b.value));if(!ok)failures++;
        }
        if(failures==0)
        {
            string migration="Migration/Customization/charge-settings-migrated.txt";
            if(!File.Exists(migration))
            {
                foreach(string path in Paths)
                {
                    if(path.EndsWith(".prefab"))
                    {
                        var root=PrefabUtility.LoadPrefabContents(path);try{MigrateCharge(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
                    }
                    else{var scene=EditorSceneManager.OpenScene(path);foreach(var root in scene.GetRootGameObjects())MigrateCharge(root);EditorSceneManager.SaveScene(scene);}
                }
                File.WriteAllText(migration,"Each Drop Dash retains its previous full-charge duration and maximum Spin Dash launch strength; charge speed and maximum are now independent serialized settings.\n");
            }
            AssetDatabase.ForceReserializeAssets(Paths);AssetDatabase.SaveAssets();
        }
        lines.Add("Failures="+failures);File.WriteAllLines("Migration/Customization/serialization-report.txt",lines);EditorApplication.Exit(failures==0?0:1);
    }
    static void MigrateCharge(GameObject root)
    {
        foreach(var drop in root.GetComponentsInChildren<Action06_DropDash>(true))
        {
            var spin=drop.GetComponent<Action03_SpinDash>();
            float duration=new SerializedObject(drop).FindProperty("legacyChargeDuration").floatValue;
            if(duration<=0)duration=drop.ChargeDuration;
            drop.MaximumCharge=spin.MaximunCharge;drop.MinimumCharge=spin.MinimunCharge;
            drop.ChargeSpeed=drop.MaximumCharge/Mathf.Max(.01f,duration);EditorUtility.SetDirty(drop);
        }
    }
    public static void Stage00()=>Test("Assets/[Stage00_Test].unity");
    public static void Altitude()=>Test("Assets/[Stage01_AltitudeLimit].unity");
    static void Test(string scene){File.WriteAllText("Migration/customization.request","run");EditorSceneManager.OpenScene(scene);EditorApplication.EnterPlaymode();}
}

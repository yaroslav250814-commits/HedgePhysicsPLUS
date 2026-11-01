using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class OriginalComparison
{
    static OriginalComparison() { EditorApplication.update+=Update; }
    static void Update() {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists("ReferenceResults/run.request")) return;
        File.Delete("ReferenceResults/run.request");
        try { Run(); }
        catch(Exception e) { File.WriteAllText("ReferenceResults/error.txt",e.ToString()); }
    }
    public static void Run() {
        Directory.CreateDirectory("ReferenceResults");
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Comparison must start in Edit Mode");
        EditorSceneManager.OpenScene("Assets/[Stage00_Test].unity");
        File.WriteAllText("ReferenceResults/motion.request","run");
        EditorApplication.isPlaying=true;
    }
}

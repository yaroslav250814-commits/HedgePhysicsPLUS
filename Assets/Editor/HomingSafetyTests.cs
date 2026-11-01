using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class HomingSafetyTests
{
    public static void Stage00()=>Run("Assets/[Stage00_Test].unity");
    public static void Altitude()=>Run("Assets/[Stage01_AltitudeLimit].unity");
    static void Run(string scene)
    {
        Directory.CreateDirectory("Migration/HomingSafety");
        File.WriteAllText("Migration/homing-safety.request","run");
        EditorSceneManager.OpenScene(scene);EditorApplication.EnterPlaymode();
    }
}

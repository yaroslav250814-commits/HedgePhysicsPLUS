using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ClassicPresentationTests
{
    public static void Run()
    {
        Directory.CreateDirectory("Migration/ClassicPresentation");
        File.WriteAllText("Migration/classic-test.request", "run");
        EditorSceneManager.OpenScene("Assets/[Stage00_Test].unity");
        EditorApplication.EnterPlaymode();
    }
}

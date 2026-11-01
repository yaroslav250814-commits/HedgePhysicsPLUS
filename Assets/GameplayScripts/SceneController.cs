using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    public static int LevelToLoad = 3;
    public static int LastLoadedLevel = -1;

    public static void LoadStageLoading(int stage)
    {
        ClassicScore.Reset();
        ContinueStageLoading(stage);
    }

    public static void ContinueStageLoading(int stage)
    {
        LevelToLoad = stage;
        Time.timeScale = 1;
        SceneManager.LoadSceneAsync("LoadingScreen");
    }

    public static void LoadGameplay() => LoadStage(LevelToLoad);

    public static void LoadStage(int stage)
    {
        Time.timeScale = 1;
        LastLoadedLevel = stage;
        SceneManager.LoadSceneAsync(stage);
    }
}

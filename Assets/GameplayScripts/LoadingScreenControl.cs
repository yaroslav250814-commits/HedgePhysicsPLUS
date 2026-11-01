using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoadingScreenControl : MonoBehaviour
{
    public Animator Anim;
    public int LoadingStart;
    public Camera Cam;
    public static bool StageLoaded;
    public static string StageName1;
    public static string StageName2;
    public Text StageName1txt;
    public Text StageName2txt;

    IEnumerator Start()
    {
        Time.timeScale = 1;
        StageName1txt.text = StageName1;
        StageName2txt.text = StageName2;
        yield return new WaitForSecondsRealtime(LoadingStart * Time.fixedDeltaTime);
        int index = SceneController.LevelToLoad;
        if (index < 3 || index > 4) index = 3;
        var loader = SceneManager.LoadSceneAsync(index, LoadSceneMode.Additive);
        yield return loader;
        SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex(index));
        SceneController.LastLoadedLevel = index;
        StageLoaded = false;
        if (Cam) Destroy(Cam.gameObject);
        if (Anim) Anim.SetInteger("Action", 1);
        yield return new WaitForSecondsRealtime(1);
        SceneManager.UnloadSceneAsync(gameObject.scene);
    }
}

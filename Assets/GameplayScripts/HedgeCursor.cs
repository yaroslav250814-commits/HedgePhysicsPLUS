using UnityEngine;

// This project uses an unlocked desktop pointer; camera input still reads mouse delta.
[DefaultExecutionOrder(10000)]
public sealed class HedgeCursor : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        var owner=new GameObject("Desktop Cursor");
        DontDestroyOnLoad(owner);
        owner.AddComponent<HedgeCursor>();
        Restore();
    }
    public static void Restore() { Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
    void LateUpdate() => Restore();
    void OnApplicationFocus(bool focus) => Restore();
    void OnApplicationPause(bool paused) => Restore();
    void OnDisable() => Restore();
    void OnApplicationQuit() => Restore();
}

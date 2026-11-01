#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Optional diagnostics for a foreground development player; never enabled by default.
public sealed class HedgeDevelopmentDiagnostics : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-hedge-diagnostics") < 0) return;
        var owner = new GameObject("Development input diagnostics");
        DontDestroyOnLoad(owner);
        owner.AddComponent<HedgeDevelopmentDiagnostics>();
        Application.runInBackground = true;
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-hedge-stage00") >= 0)
            SceneManager.LoadSceneAsync("[Stage00_Test]");
    }
    float next;
    void OnEnable() => InputSystem.onEvent += Observe;
    void OnDisable() => InputSystem.onEvent -= Observe;
    void Observe(InputEventPtr evt, InputDevice device)
    {
        if (!Application.isFocused || !(device is Keyboard keyboard) ||
            !(evt.IsA<StateEvent>() || evt.IsA<DeltaStateEvent>())) return;
        if (keyboard.enterKey.ReadValueFromEvent(evt) > 0 || keyboard.spaceKey.ReadValueFromEvent(evt) > 0)
            Debug.Log("Hedge diagnostic: foreground Enter/Space input event");
    }
    void Update()
    {
        bool start=HedgeInput.GetButtonDown("Start"), jump=HedgeInput.GetButtonDown("A");
        if (start || jump) Debug.Log("Hedge diagnostic: Start="+start+" Jump="+jump);
        if (Time.unscaledTime < next) return;
        next=Time.unscaledTime+2;
        var title=FindFirstObjectByType<TitleScreenControl>();
        var player=FindFirstObjectByType<PlayerBhysics>();
        Debug.Log("Hedge diagnostic: scene="+SceneManager.GetActiveScene().name+
            " focus="+Application.isFocused+" scale="+Time.timeScale+
            " keyboard="+(Keyboard.current != null)+" title="+(title && title.isActiveAndEnabled)+
            (player ? " player="+player.transform.position+" grounded="+player.Grounded : ""));
    }
}
#endif

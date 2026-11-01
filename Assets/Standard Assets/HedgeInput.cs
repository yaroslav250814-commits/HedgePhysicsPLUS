using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>Shared Input System actions for gameplay, camera and UI-independent shortcuts.</summary>
public static class HedgeInput
{
    static InputActionAsset asset;
    static InputAction move, look, mouse, jump, spin, roll, pause, reset, confirm;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        if (asset != null) { asset.Disable(); UnityEngine.Object.Destroy(asset); }
        asset = null;
    }

    static void EnsureActions()
    {
        if (asset != null) return;
        asset = Resources.Load<InputActionAsset>("HedgeControls");
        if (asset == null) throw new InvalidOperationException("Missing Resources/HedgeControls.inputactions");
        asset = UnityEngine.Object.Instantiate(asset);
        move = asset.FindAction("Gameplay/Move", true);
        look = asset.FindAction("Gameplay/Look", true);
        mouse = asset.FindAction("Gameplay/MouseLook", true);
        jump = asset.FindAction("Gameplay/Jump", true);
        spin = asset.FindAction("Gameplay/SpinDash", true);
        roll = asset.FindAction("Gameplay/Roll", true);
        pause = asset.FindAction("Gameplay/Pause", true);
        reset = asset.FindAction("Gameplay/Reset", true);
        confirm = asset.FindAction("Gameplay/Confirm", true);
        asset.Enable();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize() => EnsureActions();

    static InputAction Button(string name)
    {
        EnsureActions();
        switch (name) {
            case "A": case "Jump": case "Fire1": return jump;
            case "Submit": return confirm;
            case "B": case "Cancel": case "Fire2": return spin;
            case "R1": case "Fire3": return roll;
            case "Start": return pause;
            case "ResetObject": return reset;
            default: throw new ArgumentException("Unmapped input action: " + name);
        }
    }
    public static bool GetButton(string name) => Button(name).IsPressed();
    public static bool GetButtonDown(string name) => Button(name).WasPressedThisFrame();
    public static bool GetButtonUp(string name) => Button(name).WasReleasedThisFrame();
    public static float GetAxisRaw(string name) => GetAxis(name);
    public static float GetAxis(string name)
    {
        EnsureActions();
        switch (name) {
            case "Horizontal": return move.ReadValue<Vector2>().x;
            case "Vertical": return move.ReadValue<Vector2>().y;
            case "Horizontal_right": return look.ReadValue<Vector2>().x;
            case "Vertical_right": return look.ReadValue<Vector2>().y;
            // Original camera sensitivity was calibrated in legacy mouse-axis units.
            case "Mouse X": return mouse.ReadValue<Vector2>().x * 0.05f;
            case "Mouse Y": return mouse.ReadValue<Vector2>().y * 0.05f;
            default: throw new ArgumentException("Unmapped input axis: " + name);
        }
    }
    static ButtonControl MouseButton(int button) => Mouse.current == null ? null :
        button == 0 ? Mouse.current.leftButton : button == 1 ? Mouse.current.rightButton : Mouse.current.middleButton;
    public static bool GetMouseButton(int button) => MouseButton(button)?.isPressed ?? false;
    public static bool GetMouseButtonDown(int button) => MouseButton(button)?.wasPressedThisFrame ?? false;
    public static bool GetMouseButtonUp(int button) => MouseButton(button)?.wasReleasedThisFrame ?? false;
    public static bool GetKeyUp(KeyCode key) => key == KeyCode.Escape && (Keyboard.current?.escapeKey.wasReleasedThisFrame ?? false);
    public static Vector3 mousePosition => Mouse.current == null ? Vector3.zero : (Vector3)Mouse.current.position.ReadValue();
    public static Vector3 acceleration => Accelerometer.current?.acceleration.ReadValue() ?? Vector3.zero;
}

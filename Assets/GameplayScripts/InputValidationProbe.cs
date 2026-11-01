#if UNITY_EDITOR
using UnityEngine;

// Samples actions inside the player loop, where Input System uses the game state buffer.
public sealed class InputValidationProbe : MonoBehaviour
{
    public Vector2 Move;
    public bool Jump;
    public bool SawPause;
    public int Samples;
    void Update() {
        Move = new Vector2(HedgeInput.GetAxis("Horizontal"), HedgeInput.GetAxis("Vertical"));
        Jump = HedgeInput.GetButton("A");
        SawPause |= Time.timeScale == 0;
        Samples++;
    }
}
#endif

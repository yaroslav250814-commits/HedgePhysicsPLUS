using UnityEngine;
using System.Collections;

public class SonicEffectsControl : MonoBehaviour
{
    public PlayerBhysics Player;
    public ParticleSystem RunningDust;
    public ParticleSystem SpinDashDust;
    public float RunningDustThreshold;

    void FixedUpdate()
    {
        if (Player.Body.linearVelocity.sqrMagnitude > RunningDustThreshold && Player.Grounded)
        {
            RunningDust.Emit(Random.Range(0, 20));
        }
    }

    public void DoSpindashDust(int amm, float speed)
    {
        var main = SpinDashDust.main; main.startSpeed = speed;
        SpinDashDust.Emit(amm);
    }
}
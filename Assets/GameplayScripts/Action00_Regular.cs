using UnityEngine;
using System.Collections;

public class Action00_Regular : MonoBehaviour
{
    public Animator CharacterAnimator;
    PlayerBhysics Player;
    ActionManager Actions;
    public SonicSoundsControl sounds;

    public float skinRotationSpeed;
    Quaternion CharRot;

    public float SkiddingStartPoint;
    public float SkiddingIntensity;

    bool hasSked;

    void Awake()
    {
        Player = GetComponent<PlayerBhysics>();
        Actions = GetComponent<ActionManager>();
    }

    void FixedUpdate()
    {
        //Jump

        if (Actions.TryStartJump()) return;

        //Skidding

        if (Player.b_normalSpeed < -SkiddingStartPoint && Player.Grounded)
        {
            Player.isRolling = false;
            Player.AddVelocity(Player.Body.linearVelocity.normalized * SkiddingIntensity);
            if (!hasSked && Player.Grounded)
            {
                sounds.SkiddingSound();
                hasSked = true;
            }
            if (Player.SpeedMagnitude < 4)
            {
                Player.b_normalSpeed = 0;
                hasSked = false;
            }
        }
        else
        {
            hasSked = false;
        }


        //Set Homing attack to true
        if (Player.Grounded)
        {
            Actions.Action02.HomingAvailable = true;
        }
    }

    void Update()
    {
        if (Actions.TryStartDropDash()) return;
        //Set Animator Parameters
        if (Player.Grounded)
        {
            CharacterAnimator.SetInteger("Action", 0);
        }
        CharacterAnimator.SetFloat("YSpeed", Player.Body.linearVelocity.y);
        CharacterAnimator.SetFloat("GroundSpeed", Player.Body.linearVelocity.magnitude);
        CharacterAnimator.SetBool("Grounded", Player.Grounded);
        CharacterAnimator.SetFloat("NormalSpeed", Player.b_normalSpeed + SkiddingStartPoint);

        //Do Spindash
        if (Actions.TryStartSpinDash()) return;

        //Check if rolling
        if (Player.Grounded && Player.isRolling)
        {
            CharacterAnimator.SetInteger("Action", 1);
        }
        CharacterAnimator.SetBool("isRolling", Player.isRolling);

        //Play Rolling Sound
        if (HedgeInput.GetButtonDown("R1") && Player.Grounded)
        {
            sounds.SpinningSound();
        }

        //Set Character Animations and position
        CharacterAnimator.transform.parent = null;

        //Set Skin Rotation
        if (Player.Grounded)
        {
            Vector3 newForward = Player.Body.linearVelocity -
                                 transform.up * Vector3.Dot(Player.Body.linearVelocity, transform.up);

            if (newForward.magnitude < 0.1f)
            {
                newForward = CharacterAnimator.transform.forward;
            }

            CharRot = Quaternion.LookRotation(newForward, transform.up);
            CharacterAnimator.transform.rotation = Quaternion.Lerp(CharacterAnimator.transform.rotation, CharRot,
                Time.deltaTime * skinRotationSpeed);

            // CharRot = Quaternion.LookRotation( Player.Body.linearVelocity, transform.up);
            // CharacterAnimator.transform.rotation = Quaternion.Lerp(CharacterAnimator.transform.rotation, CharRot, Time.deltaTime * skinRotationSpeed);
        }
        else
        {
            Vector3 VelocityMod = new Vector3(Player.Body.linearVelocity.x, 0, Player.Body.linearVelocity.z);
            Quaternion CharRot = VelocityMod.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(VelocityMod, -Player.Gravity.normalized) : CharacterAnimator.transform.rotation;
            CharacterAnimator.transform.rotation = Quaternion.Lerp(CharacterAnimator.transform.rotation, CharRot,
                Time.deltaTime * skinRotationSpeed);
        }

        Actions.TryStartAirAttack();
    }
}

using UnityEngine;
using UnityEngine.Serialization;
using System.Collections;

public class Action01_Jump : MonoBehaviour
{
    public Animator CharacterAnimator;
    PlayerBhysics Player;
    ActionManager Actions;
    public SonicSoundsControl sounds;

    [FormerlySerializedAs("skinRotationSpeed"), SerializeField, Tooltip("Visual facing response during a jump.")] [Min(0)] private float _skinRotationSpeed;
    public float skinRotationSpeed { get => Mathf.Max(0, _skinRotationSpeed); set => _skinRotationSpeed = Mathf.Max(0, value); }

    public Vector3 InitialNormal { get; set; }
    public float Counter { get; set; }
    [FormerlySerializedAs("JumpDuration"), SerializeField, Tooltip("Maximum time during which holding jump adds lift, in seconds.")] [Min(.001f)] private float _JumpDuration;
    public float JumpDuration { get => Mathf.Max(.001f, _JumpDuration); set => _JumpDuration = Mathf.Max(.001f, value); }
    [FormerlySerializedAs("SlopedJumpDuration"), SerializeField, Tooltip("Time during which jump lift follows the original surface normal.")] [Min(0)] private float _SlopedJumpDuration;
    public float SlopedJumpDuration { get => Mathf.Max(0, _SlopedJumpDuration); set => _SlopedJumpDuration = Mathf.Max(0, value); }
    [FormerlySerializedAs("JumpSpeed"), SerializeField, Tooltip("Velocity added on each charged jump physics step.")] [Min(0)] private float _JumpSpeed;
    public float JumpSpeed { get => Mathf.Max(0, _JumpSpeed); set => _JumpSpeed = Mathf.Max(0, value); }
    [FormerlySerializedAs("JumpSlopeConversion"), SerializeField, Tooltip("Upward slope velocity converted to additional jump lift.")] [Min(0)] private float _JumpSlopeConversion;
    public float JumpSlopeConversion { get => Mathf.Max(0, _JumpSlopeConversion); set => _JumpSlopeConversion = Mathf.Max(0, value); }
    [FormerlySerializedAs("StopYSpeedOnRelease"), SerializeField, Tooltip("Upward velocity removed per step after releasing jump.")] [Min(0)] private float _StopYSpeedOnRelease;
    public float StopYSpeedOnRelease { get => Mathf.Max(0, _StopYSpeedOnRelease); set => _StopYSpeedOnRelease = Mathf.Max(0, value); }
    public float RollingLandingBoost;

    float jumpSlopeSpeed;
    [Header("Input timing")]
    [SerializeField, Min(0), Tooltip("Minimum time after jumping before another press can start an air attack.")]
    private float airAttackInputDelay = .08f;
    [SerializeField, Min(0), Tooltip("Distance to leave the ground when starting a jump.")]
    private float groundExitOffset = .3f;
    public float AirAttackInputDelay { get => Mathf.Max(0, airAttackInputDelay); set => airAttackInputDelay = Mathf.Max(0, value); }
    public float GroundExitOffset { get => Mathf.Max(0, groundExitOffset); set => groundExitOffset = Mathf.Max(0, value); }

    void Awake()
    {
        Player = GetComponent<PlayerBhysics>();
        Actions = GetComponent<ActionManager>();
    }

    public void InitialEvents()
    {
        //Set Initial Variables
        Counter = 0;
        jumpSlopeSpeed = 0;
        InitialNormal = Player.GroundNormal;

        //SnapOutOfGround to make sure you do jump
        transform.position += InitialNormal * GroundExitOffset;

        //Jump higher depending on the speed and the slope you're in
        if (Player.Body.linearVelocity.y > 0 && Player.GroundNormal.y > 0)
        {
            jumpSlopeSpeed = Player.Body.linearVelocity.y * JumpSlopeConversion;
        }
        //Sound
        sounds.JumpSound();
    }

    void Update()
    {
        if (Actions.TryStartDropDash()) return;
        //Set Animator Parameters
        CharacterAnimator.SetInteger("Action", 1);
        CharacterAnimator.SetFloat("YSpeed", Player.Body.linearVelocity.y);
        CharacterAnimator.SetFloat("GroundSpeed", Player.Body.linearVelocity.magnitude);
        CharacterAnimator.SetBool("Grounded", Player.Grounded);
        CharacterAnimator.SetBool("isRolling", false);

        //Set Animation Angle
        Vector3 VelocityMod = new Vector3(Player.Body.linearVelocity.x, 0, Player.Body.linearVelocity.z);
        Quaternion CharRot = VelocityMod.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(VelocityMod, transform.up) : CharacterAnimator.transform.rotation;
        CharacterAnimator.transform.rotation = Quaternion.Lerp(CharacterAnimator.transform.rotation, CharRot,
            Time.deltaTime * skinRotationSpeed);

        Actions.Action02.HomingAvailable = true;

        if (Counter > AirAttackInputDelay) Actions.TryStartAirAttack();
    }

    void FixedUpdate()
    {
        //Jump action
        Counter += Time.deltaTime;

        if (!HedgeInput.GetButton("A") && Counter < JumpDuration)
        {
            Counter = JumpDuration;
        }

        //Keep Colliders Rotation to avoid collision Issues
        if (Counter < 0.2f)
        {
            //transform.rotation = Quaternion.FromToRotation(transform.up, InitialNormal) * transform.rotation;
        }

        //Add Jump Speed
        if (Counter < JumpDuration)
        {
            Player.isRolling = false;
            if (Counter < SlopedJumpDuration)
            {
                Player.AddVelocity(InitialNormal * (JumpSpeed));
            }
            else
            {
                Player.AddVelocity(new Vector3(0, 1, 0) * (JumpSpeed));
            }
            //Extra speed
            Player.AddVelocity(new Vector3(0, 1, 0) * (jumpSlopeSpeed));
        }

        //Cancell Jump
        if (Player.Body.linearVelocity.y > 0 && !HedgeInput.GetButton("A"))
        {
            Vector3 Velocity = new Vector3(Player.Body.linearVelocity.x, Player.Body.linearVelocity.y,
                Player.Body.linearVelocity.z);
            Velocity.y = Velocity.y - StopYSpeedOnRelease;
            Player.Body.linearVelocity = Velocity;
        }


        //End Action
        if (Player.Grounded && Counter > SlopedJumpDuration)
        {
            Actions.ChangeAction(0);
        }
    }
}

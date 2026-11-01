using UnityEngine;
using UnityEngine.Serialization;
using System.Collections;

public class Action02_Homing : MonoBehaviour
{
    public ActionManager Action;
    public Animator CharacterAnimator;
    PlayerBhysics Player;

    [FormerlySerializedAs("isAdditive"), SerializeField, Tooltip("Preserve existing horizontal speed when it exceeds attack speed.")] private bool _isAdditive;
    public bool isAdditive { get => _isAdditive; set => _isAdditive = value; }
    [FormerlySerializedAs("HomingAttackSpeed"), SerializeField, Tooltip("Normal targeted homing speed.")] [Min(0)] private float _HomingAttackSpeed;
    public float HomingAttackSpeed { get => Mathf.Max(0, _HomingAttackSpeed); set => _HomingAttackSpeed = Mathf.Max(0, value); }
    [FormerlySerializedAs("AirDashSpeed"), SerializeField, Tooltip("Normal untargeted air dash speed.")] [Min(0)] private float _AirDashSpeed;
    public float AirDashSpeed { get => Mathf.Max(0, _AirDashSpeed); set => _AirDashSpeed = Mathf.Max(0, value); }
    [FormerlySerializedAs("HomingTimerLimit"), SerializeField, Tooltip("Maximum homing duration in physics steps, for compatibility.")] [Min(0)] private float _HomingTimerLimit;
    public float HomingTimerLimit { get => Mathf.Max(0, _HomingTimerLimit); set => _HomingTimerLimit = Mathf.Max(0, value); }
    [FormerlySerializedAs("FacingAmmount"), SerializeField, Tooltip("Minimum direction dot product for accepting a homing target.")] private float _FacingAmmount;
    public float FacingAmmount { get => Mathf.Clamp(_FacingAmmount, -1f,1f); set => _FacingAmmount = Mathf.Clamp(value, -1f,1f); }
    float Timer;
    float Speed;
    float Aspeed;
    Vector3 direction;

    public Transform Target { get; set; }
    [FormerlySerializedAs("skinRotationSpeed"), SerializeField, Tooltip("Visual facing response during an air attack.")] [Min(0)] private float _skinRotationSpeed;
    public float skinRotationSpeed { get => Mathf.Max(0, _skinRotationSpeed); set => _skinRotationSpeed = Mathf.Max(0, value); }
    public bool HomingAvailable { get; set; }
    public bool IsAirDash { get; set; }
    [Header("Airborne limits")]
    [SerializeField, Min(0), Tooltip("Free air dashes available between landings or ability resets. Zero disables free dashes.")]
    private int airDashesPerFlight = 1;
    [SerializeField, Range(0, 1), Tooltip("Fraction of horizontal momentum kept during a boosted upward rebound.")]
    private float reboundHorizontalRetention;
    [SerializeField, Tooltip("Require jump to be held at the boosted homing impact to rebound upward.")]
    private bool requireJumpForRebound = true;
    public int AirDashesPerFlight { get => Mathf.Max(0, airDashesPerFlight); set => airDashesPerFlight = Mathf.Max(0, value); }
    public float ReboundHorizontalRetention { get => Mathf.Clamp01(reboundHorizontalRetention); set => reboundHorizontalRetention = Mathf.Clamp01(value); }
    public bool RequireJumpForRebound { get => requireJumpForRebound; set => requireJumpForRebound = value; }
    public int RemainingAirDashes => Mathf.Max(0, AirDashesPerFlight - airDashesUsed);
    public bool AirDashAvailable => PlayerAbilityFeatures.AirDash && RemainingAirDashes > 0;
    int airDashesUsed;
    bool boostedAirDirection;
    Vector3 boostDirection;
    bool boostedAttack;
    [FormerlySerializedAs("BoostReboundSpeed"), SerializeField, Tooltip("Upward speed after a successful boosted homing rebound.")] [Min(0)] private float _BoostReboundSpeed = 65;
    public float BoostReboundSpeed { get => Mathf.Max(0, _BoostReboundSpeed); set => _BoostReboundSpeed = Mathf.Max(0, value); }
    public bool IsBoostedHoming => boostedAttack && !IsAirDash && Action.Action == 2;

    public void ReplenishAirDash() { airDashesUsed = 0; HomingAvailable = true; }

    public bool TryStartBoostedAttack(float boostSpeed, Vector3 airDirection)
    {
        return TryStartBoostedAttack(boostSpeed, boostSpeed, airDirection);
    }

    public bool TryStartBoostedAttack(float homingBoostSpeed, float airDashBoostSpeed, Vector3 airDirection)
    {
        if (!PlayerAbilityFeatures.DropDash || !Action.Action02Control.HomingAvailable) return false;
        bool hasTarget = Action.Action02Control.HasTarget;
        if (hasTarget ? !PlayerAbilityFeatures.DropDashHomingAttack : !PlayerAbilityFeatures.DropDashAirDash) return false;
        if (!hasTarget && !AirDashAvailable) return false;
        Action.ChangeAction(2);
        IsAirDash = !hasTarget;
        InitialEvents();
        if (Action.Action != 2) return false;
        Speed = Mathf.Max(Speed, homingBoostSpeed);
        Aspeed = Mathf.Max(Aspeed, airDashBoostSpeed);
        boostedAttack = true;
        boostedAirDirection = IsAirDash;
        boostDirection = airDirection.normalized;
        return true;
    }

    public bool ShouldReboundFromHit(Transform hitRoot)
    {
        return PlayerAbilityFeatures.BoostedHomingRebound && IsBoostedHoming && Target &&
            (Target == hitRoot || Target.IsChildOf(hitRoot)) && (!RequireJumpForRebound || HedgeInput.GetButton("A"));
    }

    public void ReboundAfterHit()
    {
        if (!PlayerAbilityFeatures.BoostedHomingRebound || !IsBoostedHoming) return;
        Vector3 up = Player.Gravity.sqrMagnitude > .0001f ? -Player.Gravity.normalized : Vector3.up;
        Vector3 horizontal = Vector3.ProjectOnPlane(Player.Body.linearVelocity, up) * ReboundHorizontalRetention;
        Action.ChangeAction(1);
        Action.Action01.InitialNormal = up;
        Action.Action01.Counter = Action.Action01.JumpDuration;
        Player.Grounded = false;
        Player.isRolling = false;
        Player.Body.linearVelocity = horizontal + up * BoostReboundSpeed;
        if (PlayerAbilityFeatures.ReboundRestoresAirAbilities)
        {
            ReplenishAirDash();
            Action.Action02Control.ReplenishAfterRebound();
        }
        Player.sounds.JumpSound();
    }

    void Awake()
    {
        HomingAvailable = true;
        Player = GetComponent<PlayerBhysics>();
    }

    public void InitialEvents()
    {
        if (IsAirDash ? !PlayerAbilityFeatures.AirDash : !PlayerAbilityFeatures.HomingAttack)
        {
            EndAttack(false);
            return;
        }
        Target = null;
        if (!IsAirDash)
        {
            var selected = HomingAttackControl.TargetObject;
            if (!Action.Action02Control.IsValidTarget(selected))
            {
                EndAttack(true);
                return;
            }
            Target = selected.transform;
        }

        Timer = 0;
        HomingAvailable = false;

        if (isAdditive)
        {
            // Apply Max Speed Limit
            float XZmag = new Vector3(Player.Body.linearVelocity.x, 0, Player.Body.linearVelocity.z).magnitude;

            if (XZmag < HomingAttackSpeed)
            {
                Speed = HomingAttackSpeed;
            }
            else
            {
                Speed = XZmag;
            }

            if (XZmag < AirDashSpeed)
            {
                Aspeed = AirDashSpeed;
            }
            else
            {
                Aspeed = XZmag;
            }
        }
        else
        {
            Aspeed = AirDashSpeed;
            Speed = HomingAttackSpeed;
        }

        //Check if not facing Object
        if (!IsAirDash)
        {
            Vector3 toTarget = Vector3.ProjectOnPlane(Target.position - transform.position, transform.up);
            Vector3 movement = Vector3.ProjectOnPlane(transform.TransformDirection(Player.PreviousRawInput), transform.up);
            if (toTarget.sqrMagnitude > .0001f && movement.sqrMagnitude > .0001f &&
                Vector3.Dot(movement.normalized, toTarget.normalized) < FacingAmmount)
            {
                IsAirDash = true;
            }
        }
        if (IsAirDash)
        {
            if (!AirDashAvailable)
            {
                EndAttack(false);
                return;
            }
            airDashesUsed++;
            Target = null;
        }
    }

    void Update()
    {
        //Set Animator Parameters
        CharacterAnimator.SetInteger("Action", 1);
        CharacterAnimator.SetFloat("YSpeed", Player.Body.linearVelocity.y);
        CharacterAnimator.SetFloat("GroundSpeed", Player.Body.linearVelocity.magnitude);
        CharacterAnimator.SetBool("Grounded", Player.Grounded);

        //Set Animation Angle
        Vector3 VelocityMod = new Vector3(Player.Body.linearVelocity.x, 0, Player.Body.linearVelocity.z);
        Quaternion CharRot = VelocityMod.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(VelocityMod, transform.up) : CharacterAnimator.transform.rotation;
        CharacterAnimator.transform.rotation = Quaternion.Lerp(CharacterAnimator.transform.rotation, CharRot,
            Time.deltaTime * skinRotationSpeed);
    }

    void FixedUpdate()
    {
        if (Action.Action != 2) return;
        if ((IsAirDash ? !PlayerAbilityFeatures.AirDash : !PlayerAbilityFeatures.HomingAttack) ||
            (boostedAttack && (!PlayerAbilityFeatures.DropDash ||
                (IsAirDash ? !PlayerAbilityFeatures.DropDashAirDash : !PlayerAbilityFeatures.DropDashHomingAttack))))
        {
            EndAttack(false);
            return;
        }
        if (!IsAirDash && (!Target || !Action.Action02Control.IsValidTarget(Target.gameObject)))
        {
            EndAttack(true);
            return;
        }
        Timer += 1;

        CharacterAnimator.SetInteger("Action", 1);

        if (IsAirDash)
        {
            if (boostedAirDirection)
            {
                Player.Body.linearVelocity = boostDirection * Aspeed;
            }
            else if (Player.RawInput != Vector3.zero)
            {
                Player.Body.linearVelocity = transform.TransformDirection(Player.RawInput) * Aspeed;
            }
            else
            {
                Player.Body.linearVelocity = transform.TransformDirection(Player.PreviousRawInput) * Aspeed;
            }
            Timer = HomingTimerLimit + 10;
        }
        else
        {
            direction = Target.position - transform.position;
            // A boosted attack must reach the target rather than stepping through its trigger.
            float stepSpeed = boostedAttack ? Mathf.Min(Speed, direction.magnitude / Time.fixedDeltaTime) : Speed;
            Player.Body.linearVelocity = direction.normalized * stepSpeed;
        }

        //End homing attck if on air for too long
        if (Timer > HomingTimerLimit)
        {
            EndAttack(false);
        }
    }

    void EndAttack(bool lostTarget)
    {
        if (lostTarget)
        {
            Player.isRolling = false;
            CharacterAnimator.SetBool("isRolling", false);
            if (Player.Body) Player.Body.linearVelocity = Vector3.ClampMagnitude(Player.Body.linearVelocity, Player.MaxSpeed);
        }
        Target = null;
        boostedAirDirection = false;
        HomingAvailable = true;
        Action.ChangeAction(0);
    }

    public void ResetHomingVariables()
    {
        Timer = 0;
        Target = null;
        boostedAttack = false;
        boostedAirDirection = false;
        //IsAirDash = false;
    }
}

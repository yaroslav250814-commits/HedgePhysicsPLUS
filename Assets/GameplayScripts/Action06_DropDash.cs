using UnityEngine;
using UnityEngine.Serialization;

// Runs after the movement controller so rolling and the landing impulse survive its physics step.
[DefaultExecutionOrder(100)]
public sealed class Action06_DropDash : MonoBehaviour
{
    [Header("Charge")]
    [SerializeField, Min(.01f), Tooltip("Charge gained per second while holding Spin Dash in the air.")]
    private float chargeSpeed = 180f / .35f;
    [SerializeField, Min(.01f), Tooltip("Maximum stored charge and base release speed. Independent of the ground Spin Dash.")]
    private float maximumCharge = 180;
    [SerializeField, Min(0), Tooltip("Minimum stored charge required to launch or use a release combo.")]
    private float minimumCharge = 40;
    [FormerlySerializedAs("ChargeDuration"), SerializeField, HideInInspector]
    private float legacyChargeDuration;
    [Header("Release combo")]
    [FormerlySerializedAs("BoostInputWindow"), SerializeField, Min(0), Tooltip("Seconds after release during which a fresh jump press can use a boosted attack.")]
    private float boostInputWindow = .2f;
    [SerializeField, Min(0), Tooltip("Ground launch speed per unit of stored charge.")]
    private float landingSpeedMultiplier = 1;
    [SerializeField, Min(0), Tooltip("Untargeted release-combo speed per unit of stored charge, with normal air dash speed as its floor.")]
    private float airDashSpeedMultiplier = 1;
    [SerializeField, Min(0), Tooltip("Targeted release-combo speed per unit of stored charge, with normal homing speed as its floor.")]
    private float homingSpeedMultiplier = 1;
    [SerializeField, Tooltip("Keep the ground launch queued after releasing the button in midair.")]
    private bool launchAfterRelease;
    [Header("Presentation")]
    [SerializeField, Min(0), Tooltip("Number of dust particles emitted by a ground launch.")]
    private int landingDustCount = 8;

    public float ChargeSpeed { get => Mathf.Max(.01f, chargeSpeed); set => chargeSpeed = Mathf.Max(.01f, value); }
    public float MaximumCharge { get => Mathf.Max(.01f, maximumCharge); set => maximumCharge = Mathf.Max(.01f, value); }
    public float MinimumCharge { get => Mathf.Clamp(minimumCharge, 0, MaximumCharge); set => minimumCharge = Mathf.Max(0, value); }
    public float BoostInputWindow { get => Mathf.Max(0, boostInputWindow); set => boostInputWindow = Mathf.Max(0, value); }
    public float LandingSpeedMultiplier { get => Mathf.Max(0, landingSpeedMultiplier); set => landingSpeedMultiplier = Mathf.Max(0, value); }
    public float AirDashSpeedMultiplier { get => Mathf.Max(0, airDashSpeedMultiplier); set => airDashSpeedMultiplier = Mathf.Max(0, value); }
    public float HomingSpeedMultiplier { get => Mathf.Max(0, homingSpeedMultiplier); set => homingSpeedMultiplier = Mathf.Max(0, value); }
    public bool LaunchAfterRelease { get => launchAfterRelease; set => launchAfterRelease = value; }
    public int LandingDustCount { get => Mathf.Max(0, landingDustCount); set => landingDustCount = Mathf.Max(0, value); }
    // Compatibility API: charging now accumulates strength rather than waiting for a binary timer.
    public float ChargeDuration { get => MaximumCharge / ChargeSpeed; set => ChargeSpeed = MaximumCharge / Mathf.Max(.01f, value); }
    public float Charge => Mathf.Clamp(charge, 0, MaximumCharge);
    public bool HasCharge => Charge > 0 && Charge >= MinimumCharge;
    public bool Charged => Charge >= MaximumCharge;
    public bool Released { get; private set; }
    public float LaunchSpeed => MaximumCharge * LandingSpeedMultiplier;
    public float CurrentLaunchSpeed => Charge * LandingSpeedMultiplier;

    PlayerBhysics player;
    ActionManager actions;
    float charge, releaseTime;
    bool chargingSound;

    void Awake() { player = GetComponent<PlayerBhysics>(); actions = GetComponent<ActionManager>(); }

    public void InitialEvents()
    {
        if (!PlayerAbilityFeatures.DropDash) { actions.ChangeAction(0); return; }
        charge = releaseTime = 0;
        Released = false;
        player.isRolling = true;
        player.sounds.SpinDashSound();
        chargingSound = true;
    }

    void Update()
    {
        if (Time.timeScale <= 0 || actions.Action04Control.isDead) return;
        ReadRelease();
        if (!PlayerAbilityFeatures.DropDash) { actions.ChangeAction(0); return; }
        if (Released && HasCharge && releaseTime <= BoostInputWindow && !player.Grounded && HedgeInput.GetButtonDown("A"))
        {
            float homingSpeed = Charge * HomingSpeedMultiplier;
            float airSpeed = Charge * AirDashSpeedMultiplier;
            Vector3 direction = LaunchDirection(Up);
            if (actions.Action02.TryStartBoostedAttack(homingSpeed, airSpeed, direction))
            {
                if (actions.Action02.IsAirDash) player.sounds.AirDashSound();
                else player.sounds.HomingAttackSound();
                return;
            }
        }
    }

    void FixedUpdate()
    {
        if (actions.Action04Control.isDead) return;
        ReadRelease();
        if (!Released) charge = Mathf.Min(MaximumCharge, Charge + ChargeSpeed * Time.fixedDeltaTime);
        else releaseTime += Time.fixedDeltaTime;
        player.isRolling = true;
        if (!player.Grounded) return;

        bool launch = HasCharge && (!Released || LaunchAfterRelease);
        Vector3 direction = LaunchDirection(player.GroundNormal);
        float speed = CurrentLaunchSpeed;
        actions.ChangeAction(0);
        player.isRolling = true;
        if (launch)
        {
            player.Body.linearVelocity = direction * speed;
            player.sounds.SpinDashReleaseSound();
            actions.Action03.effects.DoSpindashDust(LandingDustCount, actions.Action03.SpinDashChargedEffectAmm * speed);
            HedgeCamera.Shakeforce = actions.Action03.ReleaseShakeAmmount * speed / 100;
        }
    }

    void LateUpdate()
    {
        var animator = actions.Action00.CharacterAnimator;
        animator.SetInteger("Action", 1);
        animator.SetBool("isRolling", true);
        animator.SetBool("Grounded", player.Grounded);
        animator.SetFloat("YSpeed", player.Body.linearVelocity.y);
        animator.SetFloat("GroundSpeed", player.Body.linearVelocity.magnitude);
        actions.Action03.ShowBall(Mathf.Max(MinimumCharge, Charge));
    }

    Vector3 Up => player.Gravity.sqrMagnitude > .0001f ? -player.Gravity.normalized : Vector3.up;

    Vector3 LaunchDirection(Vector3 normal)
    {
        if (normal.sqrMagnitude < .0001f) normal = Up;
        Vector3 direction = player.RawInput.sqrMagnitude > .01f
            ? transform.TransformDirection(player.RawInput) : Vector3.ProjectOnPlane(player.Body.linearVelocity, Up);
        direction = Vector3.ProjectOnPlane(direction, normal);
        if (direction.sqrMagnitude < .0001f) direction = Vector3.ProjectOnPlane(actions.Action00.CharacterAnimator.transform.forward, normal);
        if (direction.sqrMagnitude < .0001f) direction = Vector3.Cross(normal, Vector3.right);
        if (direction.sqrMagnitude < .0001f) direction = Vector3.Cross(normal, Vector3.forward);
        return direction.normalized;
    }

    void ReadRelease()
    {
        if (Released || HedgeInput.GetButton("B")) return;
        Released = true;
        releaseTime = 0;
        StopChargeSound();
    }

    void StopChargeSound()
    {
        if (chargingSound && player.sounds.Source2.clip == player.sounds.SpinDash) player.sounds.Source2.Stop();
        chargingSound = false;
    }

    void OnDisable()
    {
        StopChargeSound();
        charge = releaseTime = 0;
        Released = false;
        if (player) player.isRolling = false;
    }
}
